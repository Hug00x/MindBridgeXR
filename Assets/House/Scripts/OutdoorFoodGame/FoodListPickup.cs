using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRGrabInteractable))]
public class FoodListPickup : MonoBehaviour
{
    [SerializeField] private GameObject arrowIndicator;
    [SerializeField] private bool hideArrowOnPickup = true;

    public bool HasBeenPickedUp { get; private set; }

    public event Action PickedUp;

    private XRGrabInteractable grabInteractable;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
    }

    public void ResetListPickup(bool showArrow)
    {
        HasBeenPickedUp = false;
        SetArrowVisible(showArrow);
    }

    public void SetArrowVisible(bool visible)
    {
        if (arrowIndicator != null)
            arrowIndicator.SetActive(visible);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (HasBeenPickedUp)
            return;

        HasBeenPickedUp = true;

        if (hideArrowOnPickup)
            SetArrowVisible(false);

        PickedUp?.Invoke();
    }
}
