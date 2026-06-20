using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;

    [Header("Fade")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private TMP_Text fadeMessageText;
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float defaultMessageHoldSeconds = 2f;

    [Header("XR Object Names")]
    [SerializeField] private string xrRigName = "XR Origin (XR Rig)";
    [SerializeField] private string locomotionName = "Locomotion";
    [SerializeField] private string leftControllerName = "Left Controller";
    [SerializeField] private string rightControllerName = "Right Controller";

    [Header("Spawn XR")]
    [SerializeField, Min(1)]
    [Tooltip("Número de frames durante os quais a pose é novamente corrigida enquanto o ecrã está preto.")]
    private int spawnStabilizationFrames = 3;

    [SerializeField, Min(0.0001f)]
    [Tooltip("Erro máximo aceite entre a câmara e o ponto de spawn, em metros.")]
    private float spawnPositionTolerance = 0.005f;

    [SerializeField, Min(0.01f)]
    [Tooltip("Erro máximo aceite na orientação horizontal da câmara, em graus.")]
    private float spawnYawTolerance = 0.25f;

    private string pendingSpawnID;
    private string pendingTransitionMessage;
    private float pendingMessageHoldSeconds;
    private bool isTransitioning;
    private GameObject suspendedLocomotion;
    private bool suspendedLocomotionWasActive;

    public bool IsTransitioning => isTransitioning;

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

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        RestoreSuspendedLocomotion();
        Instance = null;
    }

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

        // Dá tempo à nova cena e ao tracking XR para estabilizarem.
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f);

        yield return StartCoroutine(MovePlayerToSpawnPoint());

        // Reativa a locomoção ainda com o ecrã totalmente preto, para que os
        // providers e ações de input estejam prontos antes de revelar a cena.
        RestoreSuspendedLocomotion();
        yield return null;

        HideFadeMessage();
        yield return StartCoroutine(Fade(1f, 0f));

        pendingTransitionMessage = null;
        pendingMessageHoldSeconds = 0f;
        isTransitioning = false;
    }

    private IEnumerator FinalMessageRoutine(string message, float messageHoldSeconds)
    {
        isTransitioning = true;

        yield return StartCoroutine(Fade(0f, 1f));
        ShowFadeMessage(message);

        if (messageHoldSeconds > 0f)
            yield return new WaitForSeconds(messageHoldSeconds);

        isTransitioning = false;
    }

    private IEnumerator MovePlayerToSpawnPoint()
    {
        SceneSpawnPoint spawnPoint = FindSpawnPoint(pendingSpawnID);
        if (spawnPoint == null)
        {
            Debug.LogError(
                $"Não foi encontrado nenhum SceneSpawnPoint com o ID ou nome '{pendingSpawnID}' " +
                $"na cena '{SceneManager.GetActiveScene().name}'.",
                this);
            yield break;
        }

        XROrigin xrOrigin = FindPlayerXROrigin();
        if (xrOrigin == null)
        {
            Debug.LogError("Não foi encontrado um XROrigin ativo para posicionar o jogador.", this);
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

        // Permite que transformações já enfileiradas pelo sistema de locomoção terminem.
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

        // Última correção depois de o tracking XR atualizar a pose deste frame.
        yield return new WaitForEndOfFrame();
        ApplySpawnPose(xrOrigin, spawnPoint);

        RestoreXRObjects(
            characterController,
            characterControllerWasEnabled,
            leftController,
            leftControllerWasActive,
            rightController,
            rightControllerWasActive);

        ValidateSpawnResult(xrOrigin, spawnPoint);
        ValidateHeadClearance(xrOriginObject, spawnPoint);
    }

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
                    Debug.LogError(
                        $"Existem vários SceneSpawnPoint com o spawnID '{spawnID}'. " +
                        $"Será usado '{idMatch.name}', mas os IDs devem ser únicos.",
                        this);
                    continue;
                }

                idMatch = spawnPoint;
            }

            if (nameMatch == null && spawnPoint.gameObject.name == spawnID)
                nameMatch = spawnPoint;
        }

        if (idMatch != null)
            return idMatch;

        if (nameMatch != null)
        {
            Debug.LogWarning(
                $"O spawn '{spawnID}' foi encontrado pelo nome do GameObject. " +
                "É mais robusto preencher o mesmo valor no campo Spawn ID.",
                nameMatch);
        }

        return nameMatch;
    }

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

        // Elimina qualquer erro numérico residual deixado pela transformação do XROrigin.
        Vector3 residualPositionError =
            spawnPoint.transform.position - camera.transform.position;
        originObject.transform.position += residualPositionError;

        Physics.SyncTransforms();
        return true;
    }

    private void ValidateSpawnResult(XROrigin xrOrigin, SceneSpawnPoint spawnPoint)
    {
        Camera camera = xrOrigin.Camera;
        if (camera == null)
            return;

        float positionError =
            Vector3.Distance(camera.transform.position, spawnPoint.transform.position);

        Vector3 targetForward =
            Vector3.ProjectOnPlane(spawnPoint.transform.forward, Vector3.up);
        Vector3 cameraForward =
            Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);

        float yawError = 0f;
        if (targetForward.sqrMagnitude > 0.000001f &&
            cameraForward.sqrMagnitude > 0.000001f)
        {
            yawError = Mathf.Abs(
                Vector3.SignedAngle(cameraForward, targetForward, Vector3.up));
        }

        if (positionError > Mathf.Max(0.0001f, spawnPositionTolerance) ||
            yawError > Mathf.Max(0.01f, spawnYawTolerance))
        {
            Debug.LogWarning(
                $"O spawn '{spawnPoint.spawnID}' terminou com erro de posição " +
                $"{positionError:F4} m e erro horizontal {yawError:F2}°. " +
                "Verifica colisões e componentes que movam o XR Origin.",
                spawnPoint);
        }
    }

    private void ValidateHeadClearance(GameObject xrOriginObject, SceneSpawnPoint spawnPoint)
    {
        float radius = spawnPoint.HeadClearanceRadius;
        if (radius <= 0f)
            return;

        Collider[] overlaps = Physics.OverlapSphere(
            spawnPoint.transform.position,
            radius,
            ~0,
            QueryTriggerInteraction.Ignore);

        foreach (Collider overlap in overlaps)
        {
            if (overlap == null ||
                overlap.transform == xrOriginObject.transform ||
                overlap.transform.IsChildOf(xrOriginObject.transform))
            {
                continue;
            }

            Debug.LogWarning(
                $"O spawn '{spawnPoint.spawnID}' tem o collider '{overlap.name}' a menos de " +
                $"{radius:F2} m da posição da cabeça. Move o spawn para uma zona livre.",
                spawnPoint);
            return;
        }
    }

    private void SuspendLocomotion(GameObject locomotion)
    {
        RestoreSuspendedLocomotion();

        suspendedLocomotion = locomotion;
        suspendedLocomotionWasActive =
            suspendedLocomotion != null && suspendedLocomotion.activeSelf;

        if (suspendedLocomotionWasActive)
            suspendedLocomotion.SetActive(false);
    }

    private void RestoreSuspendedLocomotion()
    {
        if (suspendedLocomotion != null && suspendedLocomotionWasActive)
            suspendedLocomotion.SetActive(true);

        suspendedLocomotion = null;
        suspendedLocomotionWasActive = false;
    }

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

    private void ShowFadeMessage(string message)
    {
        if (fadeMessageText == null)
            return;

        bool hasMessage = !string.IsNullOrWhiteSpace(message);
        fadeMessageText.text = hasMessage ? message : string.Empty;
        fadeMessageText.gameObject.SetActive(hasMessage);
    }

    private void HideFadeMessage()
    {
        if (fadeMessageText == null)
            return;

        fadeMessageText.text = string.Empty;
        fadeMessageText.gameObject.SetActive(false);
    }
}
