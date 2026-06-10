using System;
using System.Collections;
using UnityEngine;

public class MemoryCard3D : MonoBehaviour
{
    [Header("Identificação")]
    [SerializeField] private string pairID;

    [Header("Visual (Opcional)")]
    [SerializeField] private GameObject frontVisual;
    [SerializeField] private GameObject backVisual;
    [SerializeField] private bool useFrontBackToggle = false;

    [Header("Regras")]
    [SerializeField] private bool excludeFromCompletion;

    [Header("Flip por rotação")]
    [SerializeField] private bool useRotationFlip = true;
    [SerializeField] private float revealedXAngle = 0f;
    [SerializeField] private float hiddenXAngle = 180f;
    [SerializeField] private bool useAnimatedFlip = true;
    [SerializeField] private float flipDuration = 0.12f;
    [SerializeField] private bool startsFaceDown = true;
    [SerializeField] private float hiddenYOffset = 0.003f;
    [SerializeField] private bool preserveWorldXZDuringFlip = true;

    public string PairID => pairID;
    public bool ExcludeFromCompletion => excludeFromCompletion;
    public bool IsRevealed { get; private set; }
    public bool IsMatched { get; private set; }

    public event Action<MemoryCard3D> Selected;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private Vector3 baseVisualAnchorWorld;
    private Vector3 baseVisualAnchorParentLocal;
    private Vector3 baseVisualAnchorLocal;
    private bool hasVisualAnchor;
    private Coroutine flipRoutine;

    private void Start()
    {
        CacheBasePose();
        SetRevealState(!startsFaceDown);
    }

    public void CacheBasePose()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalRotation = transform.localRotation;
        CacheVisualAnchor();
    }

    public void NotifySelected()
    {
        if (IsMatched || IsRevealed)
            return;

        Selected?.Invoke(this);
    }

    public void Reveal()
    {
        if (IsMatched)
            return;

        SetRevealState(true);
    }

    public void Hide()
    {
        if (IsMatched)
            return;

        SetRevealState(false);
    }

    public void SetMatched()
    {
        IsMatched = true;
        SetRevealState(true);
    }

    public void ResetCard()
    {
        IsMatched = false;
        SetRevealState(false);
    }

    public void SetInstantReveal(bool revealed)
    {
        IsRevealed = revealed;

        if (flipRoutine != null)
        {
            StopCoroutine(flipRoutine);
            flipRoutine = null;
        }

        if (useRotationFlip)
            SnapToAngle(revealed ? revealedXAngle : hiddenXAngle);

        transform.localPosition = GetPositionForState(revealed);
        ApplyFlipPositionCorrection(revealed);

        ApplyOptionalVisuals(revealed);
    }

    private void SetRevealState(bool revealed)
    {
        IsRevealed = revealed;

        if (useRotationFlip)
        {
            float targetX = revealed ? revealedXAngle : hiddenXAngle;

            if (useAnimatedFlip && Application.isPlaying)
            {
                if (flipRoutine != null)
                    StopCoroutine(flipRoutine);

                flipRoutine = StartCoroutine(AnimateFlipRoutine(targetX, revealed));
            }
            else
            {
                SnapToAngle(targetX);
                transform.localPosition = GetPositionForState(revealed);
                ApplyFlipPositionCorrection(revealed);
            }
        }
        else
        {
            transform.localPosition = GetPositionForState(revealed);
            ApplyFlipPositionCorrection(revealed);
        }

        ApplyOptionalVisuals(revealed);
    }

    private IEnumerator AnimateFlipRoutine(float targetX, bool revealed)
    {
        Quaternion startRotation = transform.localRotation;
        Quaternion endRotation = GetRotationFromBase(targetX);
        Vector3 startPos = transform.localPosition;
        Vector3 endPos = GetPositionForState(revealed);

        if (flipDuration > 0f)
        {
            float elapsedFlip = 0f;
            while (elapsedFlip < flipDuration)
            {
                elapsedFlip += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedFlip / flipDuration);
                transform.localRotation = Quaternion.Slerp(startRotation, endRotation, t);
                transform.localPosition = Vector3.Lerp(startPos, endPos, t);
                ApplyFlipPositionCorrection(revealed);
                yield return null;
            }
        }

        transform.localRotation = endRotation;
        transform.localPosition = endPos;
        ApplyFlipPositionCorrection(revealed);
        flipRoutine = null;
    }

    private void SnapToAngle(float targetX)
    {
        transform.localRotation = GetRotationFromBase(targetX);
        transform.localPosition = baseLocalPosition;
    }

    private Quaternion GetRotationFromBase(float xOffset)
    {
        return baseLocalRotation * Quaternion.Euler(xOffset, 0f, 0f);
    }

    private Vector3 GetPositionForState(bool revealed)
    {
        if (revealed || Mathf.Approximately(hiddenYOffset, 0f))
            return baseLocalPosition;

        Vector3 worldLift = Vector3.up * hiddenYOffset;
        Transform parentTransform = transform.parent;

        if (parentTransform == null)
            return baseLocalPosition + worldLift;

        return baseLocalPosition + parentTransform.InverseTransformVector(worldLift);
    }

    private void CacheVisualAnchor()
    {
        hasVisualAnchor = TryGetVisualBoundsCenter(out baseVisualAnchorWorld);

        if (hasVisualAnchor)
        {
            baseVisualAnchorLocal = transform.InverseTransformPoint(baseVisualAnchorWorld);
            baseVisualAnchorParentLocal = transform.parent != null
                ? transform.parent.InverseTransformPoint(baseVisualAnchorWorld)
                : baseVisualAnchorWorld;
        }
    }

    private bool TryGetVisualBoundsCenter(out Vector3 center)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        Bounds bounds = default;
        bool hasBounds = false;

        foreach (Renderer rendererComponent in renderers)
        {
            if (rendererComponent == null || !rendererComponent.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = rendererComponent.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(rendererComponent.bounds);
            }
        }

        center = hasBounds ? bounds.center : transform.position;
        return hasBounds;
    }

    private void ApplyFlipPositionCorrection(bool revealed)
    {
        if (!preserveWorldXZDuringFlip || !hasVisualAnchor)
            return;

        Vector3 currentAnchorWorld = transform.TransformPoint(baseVisualAnchorLocal);
        Vector3 targetAnchorWorld = GetCurrentBaseVisualAnchorWorld();

        if (!revealed)
            targetAnchorWorld += Vector3.up * hiddenYOffset;

        Vector3 correction = new Vector3(
            targetAnchorWorld.x - currentAnchorWorld.x,
            0f,
            targetAnchorWorld.z - currentAnchorWorld.z);

        if (correction.sqrMagnitude > 0.00000001f)
            transform.position += correction;
    }

    private Vector3 GetCurrentBaseVisualAnchorWorld()
    {
        return transform.parent != null
            ? transform.parent.TransformPoint(baseVisualAnchorParentLocal)
            : baseVisualAnchorParentLocal;
    }

    private void ApplyOptionalVisuals(bool revealed)
    {
        if (!useFrontBackToggle)
            return;

        if (frontVisual == null && backVisual == null)
            return;

        // If both fields point to the same object, keep it visible and rely on rotation flip only.
        if (frontVisual != null && frontVisual == backVisual)
        {
            frontVisual.SetActive(true);
            return;
        }

        if (frontVisual != null)
            frontVisual.SetActive(revealed);

        if (backVisual != null)
            backVisual.SetActive(!revealed);
    }
}
