using UnityEngine;

/*
 * Mantém uma única instância do XR rig viva entre cenas.
 * Isto evita duplicação do jogador quando novas cenas são carregadas e preserva
 * a continuidade da experiência XR.
 */
public class PersistentXRRig : MonoBehaviour
{
    private static PersistentXRRig instance;

    // Implementa um singleton simples persistente para o rig do jogador.
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
