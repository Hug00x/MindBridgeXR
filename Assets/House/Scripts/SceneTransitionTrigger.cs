using UnityEngine;

public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("Destino")]
    public string sceneToLoad;
    public string destinationSpawnID;

    [Header("Player")]
    public string playerTag = "Player";

    private bool canTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (!canTrigger)
            return;

        bool isPlayer =
            other.CompareTag(playerTag) ||
            other.GetComponentInParent<CharacterController>() != null ||
            (other.transform.root != null && other.transform.root.CompareTag(playerTag));

        Debug.Log("Trigger tocado por: " + other.name + " | Tag: " + other.tag + " | isPlayer=" + isPlayer);

        if (!isPlayer)
            return;

        if (SceneTransitionManager.Instance == null)
        {
            Debug.LogWarning("SceneTransitionManager não encontrado.");
            return;
        }

        if (SceneTransitionManager.Instance.IsTransitioning)
        {
            Debug.Log("Transição ignorada porque já está outra em curso.");
            return;
        }

        canTrigger = false;

        Debug.Log("A carregar cena: " + sceneToLoad + " | spawnID: " + destinationSpawnID);
        SceneTransitionManager.Instance.TransitionToScene(sceneToLoad, destinationSpawnID);
    }

    private void OnTriggerExit(Collider other)
    {
        bool isPlayer =
            other.CompareTag(playerTag) ||
            other.GetComponentInParent<CharacterController>() != null ||
            (other.transform.root != null && other.transform.root.CompareTag(playerTag));

        if (isPlayer)
        {
            canTrigger = true;
        }
    }
}