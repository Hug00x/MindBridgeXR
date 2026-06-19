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

        if (!isPlayer)
            return;

        if (SceneTransitionManager.Instance == null)
        {
            return;
        }

        if (SceneTransitionManager.Instance.IsTransitioning)
        {
            return;
        }

        canTrigger = false;

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
