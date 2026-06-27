using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/*
 * Representa um alimento físico que o jogador pode agarrar no exterior.
 * Guarda a posição inicial, comunica eventos de interação ao sistema de
 * métricas e permite devolver o objeto ao sítio original quando a entrega falha.
 */
public class FoodCollectible : MonoBehaviour
{
    // Dados de identificação usados pela lógica de entrega e pelos relatórios.
    [Header("Food")]
    [SerializeField] private FoodType foodType;
    [SerializeField] private string displayName;
    [Tooltip("ID usado nas métricas. Se ficar vazio, será usado o nome do GameObject.")]
    [SerializeField] private string metricsID;

    // Configuração do comportamento depois de uma entrega ou rejeição.
    [Header("Return")]
    [SerializeField] private float returnToStartDuration = 0.35f;
    [SerializeField] private bool deactivateWhenDelivered = true;

    // Propriedades públicas usadas pelo controlador da fase e pela zona de entrega.
    public FoodType FoodType => foodType;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? foodType.ToString() : displayName;
    public string MetricsId => string.IsNullOrWhiteSpace(metricsID) ? gameObject.name : metricsID;
    public bool IsHeld { get; private set; }
    public bool IsDelivered { get; private set; }

    // Eventos emitidos quando o XR Interaction Toolkit agarra ou larga o alimento.
    public event Action<FoodCollectible> Grabbed;
    public event Action<FoodCollectible> Released;

    // Referências e estado necessários para restaurar o alimento.
    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Transform startParent;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Coroutine returnRoutine;
    private bool returnRoutineChangedKinematic;
    private bool returnRoutinePreviousKinematic;
    private bool hasCachedStartPose;

    // Inicializa referências locais e memoriza a posição de origem.
    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        CacheStartPose();
    }

    // Liga os eventos de seleção do XR Interaction Toolkit.
    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }
    }

    // Remove subscrições para evitar chamadas sobre objetos desativados.
    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    // Memoriza a posição, rotação e hierarquia inicial do alimento.
    public void CacheStartPose()
    {
        startParent = transform.parent;
        startPosition = transform.position;
        startRotation = transform.rotation;
        hasCachedStartPose = true;
    }

    // Confirma uma entrega correta e esconde o alimento se a fase assim o pedir.
    public void MarkDelivered()
    {
        IsDelivered = true;
        IsHeld = false;

        if (returnRoutine != null)
            StopReturnRoutine();

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (deactivateWhenDelivered)
            gameObject.SetActive(false);
    }

    // Repõe completamente o alimento para reutilizar a fase desde o início.
    public void ResetToStart()
    {
        if (!hasCachedStartPose)
            CacheStartPose();

        IsDelivered = false;
        IsHeld = false;

        if (returnRoutine != null)
            StopReturnRoutine();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        transform.SetParent(startParent, true);
        transform.SetPositionAndRotation(startPosition, startRotation);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    // Inicia uma animação curta de retorno após rejeição na zona de entrega.
    public void ReturnToStart()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (returnRoutine != null)
            StopReturnRoutine();

        returnRoutine = StartCoroutine(ReturnToStartRoutine());
    }

    // Marca o alimento como agarrado e regista a ação nas métricas.
    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        IsHeld = true;

        if (returnRoutine != null)
            StopReturnRoutine();

        MetricsManager.Instance?.RecordFoodGrab(this);
        Grabbed?.Invoke(this);
    }

    // Marca o alimento como largado e informa se a largada resultou em entrega.
    private void OnSelectExited(SelectExitEventArgs args)
    {
        IsHeld = false;
        Released?.Invoke(this);
        MetricsManager.Instance?.RecordFoodReleased(this, IsDelivered);
    }

    // Interpola suavemente o objeto até à pose inicial sem interferência física.
    private IEnumerator ReturnToStartRoutine()
    {
        Vector3 fromPosition = transform.position;
        Quaternion fromRotation = transform.rotation;

        if (rb != null)
        {
            returnRoutinePreviousKinematic = rb.isKinematic;
            returnRoutineChangedKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        float duration = Mathf.Max(0f, returnToStartDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (IsHeld)
            {
                RestoreReturnRigidbodyState();
                returnRoutine = null;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
            transform.position = Vector3.Lerp(fromPosition, startPosition, t);
            transform.rotation = Quaternion.Slerp(fromRotation, startRotation, t);
            yield return null;
        }

        if (!IsHeld)
        {
            transform.SetParent(startParent, true);
            transform.SetPositionAndRotation(startPosition, startRotation);
        }

        RestoreReturnRigidbodyState();

        returnRoutine = null;
    }

    // Cancela o retorno em curso e repõe o estado físico temporariamente alterado.
    private void StopReturnRoutine()
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        RestoreReturnRigidbodyState();
    }

    // Devolve o Rigidbody ao modo cinemático que tinha antes da animação.
    private void RestoreReturnRigidbodyState()
    {
        if (!returnRoutineChangedKinematic || rb == null)
            return;

        rb.isKinematic = returnRoutinePreviousKinematic;
        returnRoutineChangedKinematic = false;
    }
}
