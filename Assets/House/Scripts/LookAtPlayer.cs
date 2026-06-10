using UnityEngine;

public class LookAtPlayer : MonoBehaviour
{
    public Transform playerCamera;

    void OnEnable()
    {
        // Reatribui a câmera quando o script é ativado
        if (playerCamera == null)
        {
            FindPlayerCamera();
        }
    }

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

    void FindPlayerCamera()
    {
        // Procura pela câmera com a tag "MainCamera"
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            playerCamera = mainCamera.transform;
        }
    }
}