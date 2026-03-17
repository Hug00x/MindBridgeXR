using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance;

    [Header("Fade")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 1f;

    [Header("XR Object Names")]
    [SerializeField] private string xrRigName = "XR Origin (XR Rig)";
    [SerializeField] private string locomotionName = "Locomotion";
    [SerializeField] private string leftControllerName = "Left Controller";
    [SerializeField] private string rightControllerName = "Right Controller";

    private string pendingSpawnID;
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
    }

    public void TransitionToScene(string sceneName, string spawnID)
    {
        if (isTransitioning)
            return;

        pendingSpawnID = spawnID;
        StartCoroutine(TransitionRoutine(sceneName));
    }

    private IEnumerator TransitionRoutine(string sceneName)
    {
        isTransitioning = true;

        Debug.Log("Fade out a começar.");
        yield return StartCoroutine(Fade(0f, 1f));

        Debug.Log("A carregar cena: " + sceneName);
        yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);

        Debug.Log("Cena carregada: " + SceneManager.GetActiveScene().name);

        // Dá tempo ao XR e à nova cena para assentarem
        yield return null;
        yield return null;
        yield return new WaitForSeconds(0.1f);

        MovePlayerToSpawnPoint();

        // Espera mais um bocadinho para garantir estabilização
        yield return null;
        yield return null;

        yield return StartCoroutine(ResetXRState());

        Debug.Log("Fade in a começar.");
        yield return StartCoroutine(Fade(1f, 0f));

        isTransitioning = false;
    }

    private void MovePlayerToSpawnPoint()
    {
        SceneSpawnPoint[] spawnPoints = FindObjectsOfType<SceneSpawnPoint>(true);
        Debug.Log("Spawn points encontrados: " + spawnPoints.Length);

        foreach (SceneSpawnPoint spawnPoint in spawnPoints)
        {
            Debug.Log("Spawn encontrado com ID: " + spawnPoint.spawnID);

            if (spawnPoint.spawnID != pendingSpawnID)
                continue;

            GameObject xrRigObject = GameObject.FindGameObjectWithTag("Player");
            if (xrRigObject == null)
            {
                Debug.LogWarning("Jogador com tag Player não encontrado.");
                return;
            }

            XROrigin xrOrigin = xrRigObject.GetComponent<XROrigin>();
            CharacterController cc = xrRigObject.GetComponent<CharacterController>();

            if (xrOrigin == null)
            {
                Debug.LogWarning("XROrigin não encontrado no XR Rig.");
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

            Debug.Log("XR Rig reposicionado para spawn: " + pendingSpawnID);
            return;
        }

        Debug.LogWarning("Não foi encontrado nenhum SceneSpawnPoint com spawnID = " + pendingSpawnID);
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

        Debug.Log("Estado XR resetado.");
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
            Debug.LogWarning("FadeImage não atribuída no SceneTransitionManager.");
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
}