using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/*
 * Controla a lista de alimentos que o jogador recolhe no exterior.
 * A primeira interação com a folha desbloqueia a etapa de entregas e pode
 * esconder o indicador visual que apontava para a lista.
 */
[RequireComponent(typeof(XRGrabInteractable))]
public class FoodListPickup : MonoBehaviour
{
    // Indicador opcional que guia o jogador até à lista antes de a apanhar.
    [SerializeField] private GameObject arrowIndicator;
    [SerializeField] private bool hideArrowOnPickup = true;

    // Ajuste da pose usada quando a folha é agarrada pela mão XR.
    [Header("Orientação ao pegar")]
    [Tooltip("Define a rotação local usada quando a folha fica presa à mão.")]
    [SerializeField] private Vector3 grabAttachEulerAngles = new Vector3(180f, 0f, 180f);

    // Estado público consultado pela fase exterior.
    public bool HasBeenPickedUp { get; private set; }

    // Evento emitido apenas na primeira recolha da lista.
    public event Action PickedUp;

    private XRGrabInteractable grabInteractable;

    // Prepara a interação XR e cria uma pose de agarrar se ela não existir.
    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        EnsureGrabAttachTransform();
    }

    // Subscreve o evento de seleção da lista.
    private void OnEnable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
    }

    // Remove a subscrição quando o objeto sai de cena ou é desativado.
    private void OnDisable()
    {
        if (grabInteractable != null)
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
    }

    // Repõe o estado da lista no início da fase exterior.
    public void ResetListPickup(bool showArrow)
    {
        HasBeenPickedUp = false;
        SetArrowVisible(showArrow);
    }

    // Controla o indicador visual associado à lista.
    public void SetArrowVisible(bool visible)
    {
        if (arrowIndicator != null)
            arrowIndicator.SetActive(visible);
    }

    // Valida a primeira recolha e notifica o controlador da fase.
    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (HasBeenPickedUp)
            return;

        HasBeenPickedUp = true;

        if (hideArrowOnPickup)
            SetArrowVisible(false);

        PickedUp?.Invoke();
    }

    // Cria um attach transform dedicado para a folha ficar orientada na mão.
    private void EnsureGrabAttachTransform()
    {
        if (grabInteractable == null || grabInteractable.attachTransform != null)
            return;

        GameObject attachObject = new GameObject("FoodListGrabAttach");
        Transform attachTransform = attachObject.transform;
        attachTransform.SetParent(transform, false);
        attachTransform.localPosition = Vector3.zero;
        attachTransform.localRotation = Quaternion.Euler(grabAttachEulerAngles);

        grabInteractable.attachTransform = attachTransform;
    }
}
