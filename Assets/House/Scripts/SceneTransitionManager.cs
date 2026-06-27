using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

/*
 * Gere transições seguras entre cenas em XR.
 * O gestor aplica fade, apresenta mensagens, carrega a cena de destino e corrige
 * a posição/orientação da câmara para alinhar o jogador com o SceneSpawnPoint.
 */
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;

    // Referências visuais usadas para ocultar o carregamento e mostrar mensagens.
    [Header("Fade")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private TMP_Text fadeMessageText;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float defaultMessageHoldSeconds = 2f;

    // Nomes usados para encontrar partes do rig que precisam de suspensão temporária.
    [Header("XR Object Names")]
    [SerializeField] private string xrRigName = "XR Origin (XR Rig)";
    [SerializeField] private string locomotionName = "Locomotion";
    [SerializeField] private string leftControllerName = "Left Controller";
    [SerializeField] private string rightControllerName = "Right Controller";

    // Estabilização aplicada depois de uma transição de cena.
    [Header("Spawn XR")]
    [SerializeField, Min(1)]
    [Tooltip("Número de frames durante os quais a pose é novamente corrigida enquanto o ecrã está preto.")]
    private int spawnStabilizationFrames = 3;

    private string pendingSpawnID;
    private string pendingTransitionMessage;
    private float pendingMessageHoldSeconds;
    private bool isTransitioning;
    private GameObject suspendedLocomotion;
    private bool suspendedLocomotionWasActive;

    public bool IsTransitioning => isTransitioning;

    // Singleton persistente para que o fade e a lógica de transição sobrevivam a cenas.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        HideFadeMessage();
    }

    // Garante que a locomoção não fica suspensa se o gestor for destruído.
    private void OnDestroy()
    {
        if (Instance != this)
            return;

        RestoreSuspendedLocomotion();
        Instance = null;
    }

    // Sobrecargas públicas para pedir transições com ou sem mensagem intermédia.
    public void TransitionToScene(string sceneName, string spawnID)
    {
        TransitionToScene(sceneName, spawnID, null, defaultMessageHoldSeconds);
    }

    public void TransitionToScene(string sceneName, string spawnID, string transitionMessage)
    {
        TransitionToScene(sceneName, spawnID, transitionMessage, defaultMessageHoldSeconds);
    }

    public void TransitionToScene(
        string sceneName,
        string spawnID,
        string transitionMessage,
        float messageHoldSeconds)
    {
        if (isTransitioning)
            return;

        pendingSpawnID = spawnID;
        pendingTransitionMessage = transitionMessage;
        pendingMessageHoldSeconds = Mathf.Max(0f, messageHoldSeconds);
        StartCoroutine(TransitionRoutine(sceneName));
    }

    // Mostra a mensagem final sem carregar outra cena.
    public void ShowFinalMessage(string message)
    {
        ShowFinalMessage(message, defaultMessageHoldSeconds);
    }

    public void ShowFinalMessage(string message, float messageHoldSeconds)
    {
        if (isTransitioning)
            return;

        StartCoroutine(FinalMessageRoutine(message, messageHoldSeconds));
    }

    // Sequência completa: fade out, mensagem opcional, carregamento, spawn e fade in.
    private IEnumerator TransitionRoutine(string sceneName)
    {
        isTransitioning = true;

        yield return StartCoroutine(Fade(0f, 1f));

        ShowFadeMessage(pendingTransitionMessage);

        if (!string.IsNullOrWhiteSpace(pendingTransitionMessage) &&
            pendingMessageHoldSeconds > 0f)
        {
            yield return new WaitForSeconds(pendingMessageHoldSeconds);
        }

        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        // Espera alguns frames para que a cena e o tracking XR atualizem referências.
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(MovePlayerToSpawnPoint());

        // Reativa input/locomoção antes de revelar a cena ao utilizador.
        RestoreSuspendedLocomotion();
        yield return null;

        HideFadeMessage();
        yield return StartCoroutine(Fade(1f, 0f));

        pendingTransitionMessage = null;
        pendingMessageHoldSeconds = 0f;
        isTransitioning = false;
    }

    // Mantém o ecrã escuro com uma mensagem de encerramento da experiência.
    private IEnumerator FinalMessageRoutine(string message, float messageHoldSeconds)
    {
        isTransitioning = true;

        yield return StartCoroutine(Fade(0f, 1f));
        ShowFadeMessage(message);

        if (messageHoldSeconds > 0f)
            yield return new WaitForSeconds(messageHoldSeconds);

        isTransitioning = false;
    }

    // Coloca a câmara exatamente no spawn e estabiliza objetos XR durante a operação.
    private IEnumerator MovePlayerToSpawnPoint()
    {
        SceneSpawnPoint spawnPoint = FindSpawnPoint(pendingSpawnID);
        if (spawnPoint == null)
        {
            yield break;
        }

        XROrigin xrOrigin = FindPlayerXROrigin();
        if (xrOrigin == null)
        {
            yield break;
        }

        GameObject xrOriginObject = xrOrigin.Origin != null
            ? xrOrigin.Origin
            : xrOrigin.gameObject;

        CharacterController characterController =
            xrOriginObject.GetComponent<CharacterController>();

        Transform searchRoot = xrOriginObject.transform;
        GameObject locomotion = FindDescendantByName(searchRoot, locomotionName);
        GameObject leftController = FindDescendantByName(searchRoot, leftControllerName);
        GameObject rightController = FindDescendantByName(searchRoot, rightControllerName);

        SuspendLocomotion(locomotion);

        bool characterControllerWasEnabled =
            characterController != null && characterController.enabled;
        bool leftControllerWasActive =
            leftController != null && leftController.activeSelf;
        bool rightControllerWasActive =
            rightController != null && rightController.activeSelf;

        if (characterControllerWasEnabled)
            characterController.enabled = false;

        if (leftControllerWasActive)
            leftController.SetActive(false);

        if (rightControllerWasActive)
            rightController.SetActive(false);

        // Deixa terminar transformações pendentes antes da correção de pose.
        yield return null;
        yield return null;

        if (!ApplySpawnPose(xrOrigin, spawnPoint))
        {
            RestoreXRObjects(
                characterController,
                characterControllerWasEnabled,
                leftController,
                leftControllerWasActive,
                rightController,
                rightControllerWasActive);
            RestoreSuspendedLocomotion();
            yield break;
        }

        int stabilizationFrames = Mathf.Max(1, spawnStabilizationFrames);
        for (int frame = 0; frame < stabilizationFrames; frame++)
        {
            yield return null;
            ApplySpawnPose(xrOrigin, spawnPoint);
        }

        // Faz uma correção final depois da atualização de tracking do frame.
        yield return new WaitForEndOfFrame();
        ApplySpawnPose(xrOrigin, spawnPoint);

        RestoreXRObjects(
            characterController,
            characterControllerWasEnabled,
            leftController,
            leftControllerWasActive,
            rightController,
            rightControllerWasActive);
    }

    // Procura primeiro por spawnID e usa o nome do GameObject como fallback.
    private SceneSpawnPoint FindSpawnPoint(string spawnID)
    {
        if (string.IsNullOrWhiteSpace(spawnID))
            return null;

        SceneSpawnPoint[] spawnPoints = FindObjectsByType<SceneSpawnPoint>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        SceneSpawnPoint idMatch = null;
        SceneSpawnPoint nameMatch = null;

        foreach (SceneSpawnPoint spawnPoint in spawnPoints)
        {
            if (spawnPoint.spawnID == spawnID)
            {
                if (idMatch != null)
                {
                    continue;
                }

                idMatch = spawnPoint;
            }

            if (nameMatch == null && spawnPoint.gameObject.name == spawnID)
                nameMatch = spawnPoint;
        }

        if (idMatch != null)
            return idMatch;

        return nameMatch;
    }

    // Encontra o XROrigin ativo através da tag Player, nome configurado ou procura geral.
    private XROrigin FindPlayerXROrigin()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            XROrigin playerOrigin = player.GetComponent<XROrigin>();
            if (playerOrigin == null)
                playerOrigin = player.GetComponentInParent<XROrigin>();
            if (playerOrigin == null)
                playerOrigin = player.GetComponentInChildren<XROrigin>(true);

            if (playerOrigin != null)
                return playerOrigin;
        }

        GameObject namedRig = FindObjectByName(xrRigName);
        if (namedRig != null)
        {
            XROrigin namedOrigin = namedRig.GetComponent<XROrigin>();
            if (namedOrigin != null)
                return namedOrigin;
        }

        XROrigin[] allOrigins = FindObjectsByType<XROrigin>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (XROrigin origin in allOrigins)
        {
            if (origin.isActiveAndEnabled)
                return origin;
        }

        return null;
    }

    // Alinha o yaw da câmara com o spawn e move a câmara para a posição mundial exata.
    private bool ApplySpawnPose(XROrigin xrOrigin, SceneSpawnPoint spawnPoint)
    {
        Camera camera = xrOrigin.Camera;
        GameObject originObject = xrOrigin.Origin;
        if (camera == null || originObject == null)
            return false;

        Vector3 targetForward =
            Vector3.ProjectOnPlane(spawnPoint.transform.forward, Vector3.up);
        Vector3 cameraForward =
            Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);

        if (targetForward.sqrMagnitude > 0.000001f)
        {
            targetForward.Normalize();

            if (cameraForward.sqrMagnitude > 0.000001f)
            {
                cameraForward.Normalize();
                float yawCorrection =
                    Vector3.SignedAngle(cameraForward, targetForward, Vector3.up);
                xrOrigin.RotateAroundCameraUsingOriginUp(yawCorrection);
            }
            else
            {
                Vector3 originForward =
                    Vector3.ProjectOnPlane(originObject.transform.forward, Vector3.up);
                if (originForward.sqrMagnitude > 0.000001f)
                {
                    float yawCorrection =
                        Vector3.SignedAngle(originForward, targetForward, Vector3.up);
                    xrOrigin.RotateAroundCameraUsingOriginUp(yawCorrection);
                }
            }
        }

        if (!xrOrigin.MoveCameraToWorldLocation(spawnPoint.transform.position))
            return false;

        // Corrige qualquer erro residual que fique depois da transformação do XROrigin.
        Vector3 residualPositionError =
            spawnPoint.transform.position - camera.transform.position;
        originObject.transform.position += residualPositionError;

        Physics.SyncTransforms();
        return true;
    }

    // Desliga temporariamente a locomoção para impedir movimento durante a correção de pose.
    private void SuspendLocomotion(GameObject locomotion)
    {
        RestoreSuspendedLocomotion();

        suspendedLocomotion = locomotion;
        suspendedLocomotionWasActive =
            suspendedLocomotion != null && suspendedLocomotion.activeSelf;

        if (suspendedLocomotionWasActive)
            suspendedLocomotion.SetActive(false);
    }

    // Repõe a locomoção no estado em que estava antes da transição.
    private void RestoreSuspendedLocomotion()
    {
        if (suspendedLocomotion != null && suspendedLocomotionWasActive)
            suspendedLocomotion.SetActive(true);

        suspendedLocomotion = null;
        suspendedLocomotionWasActive = false;
    }

    // Repõe CharacterController e controladores XR depois do reposicionamento.
    private void RestoreXRObjects(
        CharacterController characterController,
        bool characterControllerWasEnabled,
        GameObject leftController,
        bool leftControllerWasActive,
        GameObject rightController,
        bool rightControllerWasActive)
    {
        if (characterController != null && characterControllerWasEnabled)
            characterController.enabled = true;

        if (leftController != null && leftControllerWasActive)
            leftController.SetActive(true);

        if (rightController != null && rightControllerWasActive)
            rightController.SetActive(true);

        Physics.SyncTransforms();
    }

    // Procura objetos filhos por nome mesmo que estejam inativos.
    private GameObject FindDescendantByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            if (descendant.name == objectName)
                return descendant.gameObject;
        }

        return null;
    }

    // Procura um GameObject global por nome, incluindo objetos inativos.
    private GameObject FindObjectByName(string objectName)
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
        {
            if (obj.name == objectName)
                return obj;
        }

        return null;
    }

    // Interpola a opacidade da imagem de fade.
    private IEnumerator Fade(float startAlpha, float endAlpha)
    {
        if (fadeImage == null)
            yield break;

        float elapsed = 0f;
        Color color = fadeImage.color;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            color.a = Mathf.Lerp(startAlpha, endAlpha, t);
            fadeImage.color = color;
            yield return null;
        }

        color.a = endAlpha;
        fadeImage.color = color;
    }

    // Atualiza e ativa o texto mostrado durante fades ou mensagem final.
    private void ShowFadeMessage(string message)
    {
        if (fadeMessageText == null)
            return;

        bool hasMessage = !string.IsNullOrWhiteSpace(message);
        fadeMessageText.text = hasMessage ? message : string.Empty;
        fadeMessageText.gameObject.SetActive(hasMessage);
    }

    // Limpa o texto para que não reapareça numa transição posterior.
    private void HideFadeMessage()
    {
        if (fadeMessageText == null)
            return;

        fadeMessageText.text = string.Empty;
        fadeMessageText.gameObject.SetActive(false);
    }
}
