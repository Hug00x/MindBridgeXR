using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class FoodCollectible : MonoBehaviour
{
    [Header("Food")]
    [SerializeField] private FoodType foodType;
    [SerializeField] private string displayName;

    [Header("Return")]
    [SerializeField] private float returnToStartDuration = 0.35f;
    [SerializeField] private bool deactivateWhenDelivered = true;

    public FoodType FoodType => foodType;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? foodType.ToString() : displayName;
    public bool IsHeld { get; private set; }
    public bool IsDelivered { get; private set; }

    public event Action<FoodCollectible> Grabbed;
    public event Action<FoodCollectible> Released;

    private XRGrabInteractable grabInteractable;
    private Rigidbody rb;
    private Transform startParent;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Coroutine returnRoutine;
    private bool returnRoutineChangedKinematic;
    private bool returnRoutinePreviousKinematic;

    private void Awake()
    {
        grabInteractable = GetComponent<XRGrabInteractable>();
        rb = GetComponent<Rigidbody>();
        CacheStartPose();
    }

    private void OnEnable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.AddListener(OnSelectEntered);
            grabInteractable.selectExited.AddListener(OnSelectExited);
        }
    }

    private void OnDisable()
    {
        if (grabInteractable != null)
        {
            grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
            grabInteractable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    public void CacheStartPose()
    {
        startParent = transform.parent;
        startPosition = transform.position;
        startRotation = transform.rotation;
    }

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

    public void ResetToStart()
    {
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

    public void ReturnToStart()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (returnRoutine != null)
            StopReturnRoutine();

        returnRoutine = StartCoroutine(ReturnToStartRoutine());
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        IsHeld = true;

        if (returnRoutine != null)
            StopReturnRoutine();

        Grabbed?.Invoke(this);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        IsHeld = false;
        Released?.Invoke(this);
    }

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

    private void StopReturnRoutine()
    {
        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        RestoreReturnRigidbodyState();
    }

    private void RestoreReturnRigidbodyState()
    {
        if (!returnRoutineChangedKinematic || rb == null)
            return;

        rb.isKinematic = returnRoutinePreviousKinematic;
        returnRoutineChangedKinematic = false;
    }
}
