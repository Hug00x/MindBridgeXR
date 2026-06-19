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

    private string pendingSpawnID;
    private string pendingTransitionMessage;
    private float pendingMessageHoldSeconds;
    private bool isTransitioning = false;
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

    public void TransitionToScene(string sceneName, string spawnID)
    {
        TransitionToScene(sceneName, spawnID, null, defaultMessageHoldSeconds);
    }

    public void TransitionToScene(string sceneName, string spawnID, string transitionMessage)
    {
        TransitionToScene(sceneName, spawnID, transitionMessage, defaultMessageHoldSeconds);
    }

    public void TransitionToScene(string sceneName, string spawnID, string transitionMessage, float messageHoldSeconds)
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

        if (!string.IsNullOrWhiteSpace(pendingTransitionMessage) && pendingMessageHoldSeconds > 0f)
            yield return new WaitForSeconds(pendingMessageHoldSeconds);

        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        // Dá tempo ao XR e à nova cena para assentarem
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f);

        MovePlayerToSpawnPoint();

        // Espera mais um bocadinho para garantir estabilização
        yield return null;
        yield return null;

        yield return StartCoroutine(ResetXRState());

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

    private void MovePlayerToSpawnPoint()
    {
        SceneSpawnPoint[] spawnPoints = FindObjectsOfType<SceneSpawnPoint>(true);

        foreach (SceneSpawnPoint spawnPoint in spawnPoints)
        {
            bool matchesSpawnID = spawnPoint.spawnID == pendingSpawnID;
            bool matchesObjectName = spawnPoint.gameObject.name == pendingSpawnID;

            if (!matchesSpawnID && !matchesObjectName)
                continue;

            GameObject xrRigObject = GameObject.FindGameObjectWithTag("Player");
            if (xrRigObject == null)
            {
                return;
            }

            XROrigin xrOrigin = xrRigObject.GetComponent<XROrigin>();
            CharacterController cc = xrRigObject.GetComponent<CharacterController>();

            if (xrOrigin == null)
            {
                return;
            }

            if (cc != null)
                cc.enabled = false;

            // 1) move a câmara para o ponto desejado
            xrOrigin.MoveCameraToWorldLocation(spawnPoint.transform.position);

            // 2) alinha a rotação horizontal com o spawn
            Camera cam = xrOrigin.Camera;
            if (cam != null)
            {
                float deltaY = spawnPoint.transform.eulerAngles.y - cam.transform.eulerAngles.y;
                xrRigObject.transform.Rotate(0f, deltaY, 0f);
            }
            else
            {
                xrRigObject.transform.rotation = spawnPoint.transform.rotation;
            }

            Physics.SyncTransforms();

            if (cc != null)
            {
                cc.enabled = true;
                cc.Move(Vector3.zero);
            }

            return;
        }

    }

    private IEnumerator ResetXRState()
    {
        GameObject xrRig = FindObjectByName(xrRigName);
        GameObject locomotion = FindObjectByName(locomotionName);
        GameObject leftController = FindObjectByName(leftControllerName);
        GameObject rightController = FindObjectByName(rightControllerName);

        if (locomotion != null)
            locomotion.SetActive(false);

        if (leftController != null)
            leftController.SetActive(false);

        if (rightController != null)
            rightController.SetActive(false);

        yield return null;
        yield return null;

        if (locomotion != null)
            locomotion.SetActive(true);

        if (leftController != null)
            leftController.SetActive(true);

        if (rightController != null)
            rightController.SetActive(true);

        Physics.SyncTransforms();

    }

    private GameObject FindObjectByName(string objectName)
    {
        GameObject[] allObjects = FindObjectsOfType<GameObject>(true);

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
        {
            yield break;
        }

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
