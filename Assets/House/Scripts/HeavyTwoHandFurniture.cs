using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/*
 * Ajusta a física de móveis pesados quando são agarrados em XR.
 * Com uma mão, o objeto pode ser limitado a arrastar no chão; com duas mãos,
 * pode receber menos resistência e, opcionalmente, ser levantado.
 */
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable))]
public class HeavyTwoHandFurniture : MonoBehaviour
{
    // Regras principais que definem como o móvel reage a uma ou duas mãos.
    [Header("Regras")]
    [Tooltip("Com 1 mão, bloqueia altura (Y) para só arrastar no chão.")]
    public bool oneHandDragOnly = true;

    [Tooltip("Com 2 mãos, permite levantar (desbloqueia Y).")]
    public bool allowLiftWithTwoHands = true;

    // Resistência extra aplicada para transmitir sensação de peso.
    [Header("Física (sensação de peso)")]
    [Tooltip("Drag extra quando está agarrado com 1 mão.")]
    public float oneHandExtraDrag = 3f;

    [Tooltip("Angular drag extra quando está agarrado com 1 mão.")]
    public float oneHandExtraAngularDrag = 8f;

    [Tooltip("Drag extra quando está agarrado com 2 mãos.")]
    public float twoHandExtraDrag = 1.5f;

    [Tooltip("Angular drag extra quando está agarrado com 2 mãos.")]
    public float twoHandExtraAngularDrag = 6f;

    // Estabilização para reduzir tombos e rotações desconfortáveis.
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

    // Guarda o estado físico original e força o modo de seleção múltipla.
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();

        baseDrag = rb.linearDamping;
        baseAngularDrag = rb.angularDamping;
        baseCenterOfMass = rb.centerOfMass;
        baseConstraints = rb.constraints;

        grab.selectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple;

        rb.centerOfMass = baseCenterOfMass + centerOfMassOffset;

        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);

        ApplyMode();
    }

    // Remove listeners para evitar chamadas a objetos destruídos.
    void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnSelectEntered);
            grab.selectExited.RemoveListener(OnSelectExited);
        }
    }

    // Mantém o conjunto de mãos/controladores que seguram o móvel.
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

    // Recalcula as restrições sempre que muda o número de mãos.
    void ApplyMode()
    {
        int hands = selectingInteractors.Count;

        rb.linearDamping = baseDrag;
        rb.angularDamping = baseAngularDrag;
        rb.constraints = baseConstraints;

        if (hands <= 0)
            return;

        bool isTwoHands = hands >= 2;

        if (!isTwoHands)
        {
            rb.linearDamping = baseDrag + oneHandExtraDrag;
            rb.angularDamping = baseAngularDrag + oneHandExtraAngularDrag;

            if (oneHandDragOnly)
            {
                rb.constraints |= RigidbodyConstraints.FreezePositionY;
            }

            if (lockTiltRotation)
            {
                rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }
        else
        {
            rb.linearDamping = baseDrag + twoHandExtraDrag;
            rb.angularDamping = baseAngularDrag + twoHandExtraAngularDrag;

            if (!allowLiftWithTwoHands)
            {
                rb.constraints |= RigidbodyConstraints.FreezePositionY;
            }

            if (lockTiltRotation)
            {
                rb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }
    }
}
