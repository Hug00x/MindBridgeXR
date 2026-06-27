using UnityEngine;

/*
 * Define um ponto de entrada para transições entre cenas.
 * O SceneTransitionManager usa o spawnID para encontrar a posição, orientação
 * e alinhamento do jogador quando a nova cena fica ativa.
 */
public class SceneSpawnPoint : MonoBehaviour
{
    // ID lógico usado pelos gatilhos de transição.
    [Tooltip("Identificador usado pelos gatilhos de transição para encontrar este ponto.")]
    public string spawnID;

    // Raio visual usado no editor para avaliar a zona livre em torno da cabeça.
    [Header("Segurança")]
    [SerializeField, Min(0f)]
    [Tooltip("Raio visual recomendado em torno da posição da cabeça.")]
    private float headClearanceRadius = 0.15f;

    public float HeadClearanceRadius => Mathf.Max(0f, headClearanceRadius);

    // Ajuda visual no editor para orientar e validar o ponto de spawn.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, HeadClearanceRadius);
        Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.5f);
    }
}
