using UnityEngine;

/*
 * Mantém um painel ou texto virado horizontalmente para a câmara do jogador.
 * É usado em elementos informativos no mundo para melhorar legibilidade em VR
 * sem inclinar o objeto no eixo vertical.
 */
public class LookAtPlayer : MonoBehaviour
{
    public Transform playerCamera;

    // Tenta recuperar a câmara quando o objeto volta a ficar ativo.
    void OnEnable()
    {
        if (playerCamera == null)
        {
            FindPlayerCamera();
        }
    }

    // Atualiza a rotação no fim do frame para acompanhar a pose mais recente.
    void LateUpdate()
    {
        if (playerCamera == null)
        {
            FindPlayerCamera();
            return;
        }

        Vector3 direction = transform.position - playerCamera.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    // Usa a câmara principal como referência padrão do jogador.
    void FindPlayerCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            playerCamera = mainCamera.transform;
        }
    }
}
