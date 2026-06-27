using UnityEngine;

/*
 * Indicador visual em mundo que aponta para um alvo relevante da fase exterior.
 * Pode manter-se vertical e aplicar uma pequena oscilação para chamar a atenção
 * sem depender de UI fixa no ecrã.
 */
public class WorldArrowIndicator : MonoBehaviour
{
    // Alvo, offset e suavização da rotação da seta.
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset;
    [SerializeField] private Vector3 rotationOffsetEuler = new Vector3(0f, 0f, -90f);
    [SerializeField] private bool keepUpright = true;
    [SerializeField] private float rotationSpeed = 10f;

    // Movimento vertical opcional para tornar o indicador mais visível.
    [Header("Motion")]
    [SerializeField] private bool useBob = true;
    [SerializeField] private float bobAmplitude = 0.06f;
    [SerializeField] private float bobFrequency = 1.5f;

    private Vector3 baseLocalPosition;

    // Guarda a posição local base para a animação de oscilação.
    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
    }

    // Recalcula a base quando o indicador é reativado em cena.
    private void OnEnable()
    {
        baseLocalPosition = transform.localPosition;
    }

    // Atualiza a orientação para o alvo e a oscilação visual.
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

    // Permite ao controlador trocar o alvo dinamicamente.
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    // Calcula uma rotação horizontal suave na direção do alvo.
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
