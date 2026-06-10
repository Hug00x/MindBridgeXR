using UnityEngine;

public class WorldArrowIndicator : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset;
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(0f, 0f, -90f);
    [SerializeField] private bool keepUpright = true;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Motion")]
    [SerializeField] private bool useBob = true;
    [SerializeField] private float bobAmplitude = 0.06f;
    [SerializeField] private float bobFrequency = 1.5f;

    private Vector3 baseLocalPosition;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        baseLocalPosition = transform.localPosition;
    }

    private void LateUpdate()
    {
        if (target != null)
            RotateTowardsTarget();

        if (useBob)
        {
            float y = Mathf.Sin(Time.time * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
            transform.localPosition = baseLocalPosition + new Vector3(0f, y, 0f);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void RotateTowardsTarget()
    {
        Vector3 targetPosition = target.position + targetOffset;
        Vector3 direction = targetPosition - transform.position;

        if (keepUpright)
            direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion lookRotation = Quaternion.LookRotation(direction.normalized, Vector3.up) *
                                  Quaternion.Euler(rotationOffsetEuler);

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            lookRotation,
            Time.deltaTime * Mathf.Max(0f, rotationSpeed));
    }
}
