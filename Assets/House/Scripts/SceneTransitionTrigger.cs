using UnityEngine;

/*
 * Gatilho espacial que inicia a passagem para outra cena.
 * Ao detetar o jogador, solicita ao SceneTransitionManager que carregue a cena
 * configurada e posicione o XR rig no spawn de destino.
 */
public class SceneTransitionTrigger : MonoBehaviour
{
    // Destino lógico configurado na passagem entre espaços.
    [Header("Destino")]
    public string sceneToLoad;
    public string destinationSpawnID;

    // Critérios simples para reconhecer o jogador mesmo quando o collider é filho do rig.
    [Header("Player")]
    public string playerTag = "Player";

    private bool canTrigger = true;

    // Inicia uma transição uma única vez enquanto o jogador permanece no trigger.
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

    // Reativa o gatilho depois de o jogador sair da zona.
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
