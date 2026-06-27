using UnityEngine;

/*
 * Representa uma divisão navegável da casa.
 * A zona informa o TaskManager quando o jogador entra e expõe objetos visuais
 * para destacar destinos ou marcar divisões já visitadas.
 */
public class RoomZone : MonoBehaviour
{
    // Identificador lógico usado no catálogo global e nas métricas.
    [Header("Identificação")]
    public string roomID;

    // Elementos opcionais ativados pelo TaskManager conforme a fase.
    [Header("Visual")]
    public GameObject highlightObject;
    public GameObject visitedMarkerObject;

    // Controla a pista visual da divisão alvo na navegação guiada.
    public void SetHighlight(bool state)
    {
        if (highlightObject != null)
            highlightObject.SetActive(state);
    }

    // Controla a marca de divisão já explorada na fase tutorial.
    public void SetVisitedMark(bool state)
    {
        if (visitedMarkerObject != null)
            visitedMarkerObject.SetActive(state);
    }

    // Notifica entrada apenas quando o collider pertence ao jogador.
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (TaskManager.Instance != null)
            TaskManager.Instance.PlayerEnteredRoom(this);
    }
}
