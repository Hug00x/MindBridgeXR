using UnityEngine;

public class SceneSpawnPoint : MonoBehaviour
{
    [Tooltip("Identificador usado pelos gatilhos de transição para encontrar este ponto.")]
    public string spawnID;

    [Header("Segurança")]
    [SerializeField, Min(0f)]
    [Tooltip("Raio livre recomendado em torno da posição da cabeça. Zero desativa a validação.")]
    private float headClearanceRadius = 0.15f;

    public float HeadClearanceRadius => Mathf.Max(0f, headClearanceRadius);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, HeadClearanceRadius);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
    }
}
