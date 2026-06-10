using UnityEngine;

[RequireComponent(typeof(MemoryCard3D))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable))]
public class XRMemoryCardSelectBridge : MonoBehaviour
{
    private MemoryCard3D card;
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable;
    private float lastTriggerTime;
    [SerializeField] private float triggerDebounceSeconds = 0.12f;

    private void Awake()
    {
        card = GetComponent<MemoryCard3D>();
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.activated.AddListener(OnActivated);
            interactable.selectEntered.AddListener(OnSelectEnteredFallback);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.activated.RemoveListener(OnActivated);
            interactable.selectEntered.RemoveListener(OnSelectEnteredFallback);
        }
    }

    private void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs args)
    {
        TryNotifySelection();
    }

    private void OnSelectEnteredFallback(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        TryNotifySelection();
    }

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
