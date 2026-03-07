using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class HeavyTwoHandFurniture : MonoBehaviour
{
    [Header("Regras")]
    [Tooltip("Com 1 mão, bloqueia altura (Y) para só arrastar no chão.")]
    public bool oneHandDragOnly = true;

    [Tooltip("Com 2 mãos, permite levantar (desbloqueia Y).")]
    public bool allowLiftWithTwoHands = true;

    [Header("Física (sensação de peso)")]
    [Tooltip("Drag extra quando está agarrado com 1 mão.")]
    public float oneHandExtraDrag = 3f;

    [Tooltip("Angular drag extra quando está agarrado com 1 mão.")]
    public float oneHandExtraAngularDrag = 8f;

    [Tooltip("Drag extra quando está agarrado com 2 mãos.")]
    public float twoHandExtraDrag = 1.5f;

    [Tooltip("Angular drag extra quando está agarrado com 2 mãos.")]
    public float twoHandExtraAngularDrag = 6f;

    [Header("Estabilidade")]
    [Tooltip("Baixa o centro de massa para reduzir tombos (valores negativos em Y).")]
    public Vector3 centerOfMassOffset = new Vector3(0f, -0.2f, 0f);

    [Tooltip("Trava inclinação (X/Z) para a cadeira não tombar. Recomendo ligado.")]
    public bool lockTiltRotation = true;

    Rigidbody rb;
    UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable grab;

    float baseDrag;
    float baseAngularDrag;
    Vector3 baseCenterOfMass;
    RigidbodyConstraints baseConstraints;

    readonly HashSet<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor> selectingInteractors = new();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        // Guardar valores originais
        baseDrag = rb.linearDamping;
        baseAngularDrag = rb.angularDamping;
        baseCenterOfMass = rb.centerOfMass;
        baseConstraints = rb.constraints;

        // Garantir 2 mãos
        grab.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple;

        // Centro de massa mais baixo (estável)
        rb.centerOfMass = baseCenterOfMass + centerOfMassOffset;

        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);

        ApplyMode();
    }

    void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.selectExited.RemoveListener(OnSelectExited);
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        selectingInteractors.Add(args.interactorObject);
        ApplyMode();
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        selectingInteractors.Remove(args.interactorObject);
        ApplyMode();
    }

    void ApplyMode()
    {
        int hands = selectingInteractors.Count;

        // Reset para base sempre que muda
        rb.linearDamping = baseDrag;
        rb.angularDamping = baseAngularDrag;
        rb.constraints = baseConstraints;

        if (hands <= 0)
            return;

        bool isTwoHands = hands >= 2;

        if (!isTwoHands)
        {
            // 1 mão
            rb.linearDamping = baseDrag + oneHandExtraDrag;
            rb.angularDamping = baseAngularDrag + oneHandExtraAngularDrag;

            if (oneHandDragOnly)
            {
                // Só arrasta (não levanta)
                rb.constraints |= RigidbodyConstraints.FreezePositionY;
            }

            if (lockTiltRotation)
            {
                rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }
        else
        {
            // 2 mãos
            rb.linearDamping = baseDrag + twoHandExtraDrag;
            rb.angularDamping = baseAngularDrag + twoHandExtraAngularDrag;

            if (!allowLiftWithTwoHands)
            {
                // Mesmo com 2 mãos continua no chão
                rb.constraints |= RigidbodyConstraints.FreezePositionY;
            }

            if (lockTiltRotation)
            {
                rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }
    }
}