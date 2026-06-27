using UnityEngine;

/*
 * Faz a ponte entre eventos do XRSimpleInteractable e a lógica da MemoryCard3D.
 * Mantém dependências específicas do XR fora da carta e aplica debounce para evitar
 * múltiplas seleções geradas por um único gesto.
 */
[RequireComponent(typeof(MemoryCard3D))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class XRMemoryCardSelectBridge : MonoBehaviour
{
    // Referências locais aos componentes que comunicam entre si.
    private MemoryCard3D card;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;
    private float lastTriggerTime;
    [SerializeField] private float triggerDebounceSeconds = 0.12f;

    // Cache dos componentes obrigatórios.
    private void Awake()
    {
        card = GetComponent<MemoryCard3D>();
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
    }

    // Liga tanto ativação explícita como seleção fallback.
    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.activated.AddListener(OnActivated);
            interactable.selectEntered.AddListener(OnSelectEnteredFallback);
        }
    }

    // Remove listeners quando a carta é desativada.
    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.activated.RemoveListener(OnActivated);
            interactable.selectEntered.RemoveListener(OnSelectEnteredFallback);
        }
    }

    // Evento principal quando o utilizador ativa a carta.
    private void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs args)
    {
        TryNotifySelection();
    }

    // Fallback para configurações em que selecionar já deve contar como escolha.
    private void OnSelectEnteredFallback(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        TryNotifySelection();
    }

    // Aplica debounce e encaminha a seleção para a carta.
    private void TryNotifySelection()
    {
        if (card == null)
            return;

        if (Time.time - lastTriggerTime < triggerDebounceSeconds)
            return;

        lastTriggerTime = Time.time;
        card.NotifySelected();
    }

}
