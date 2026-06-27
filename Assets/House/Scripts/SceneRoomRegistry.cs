using UnityEngine;

/*
 * Liga as divisões da cena atual ao gestor global.
 * Cada cena mantém a sua lista local de RoomZone e regista-a quando fica ativa,
 * permitindo ao TaskManager atualizar destaques e marcas depois de transições.
 */
public class SceneRoomRegistry : MonoBehaviour
{
    // Lista configurada no Inspector com as zonas de divisão desta cena.
    [Header("Divisões desta cena")]
    public RoomZone[] roomsInScene;

    // Regista as zonas locais quando a cena terminou de carregar.
    private void Start()
    {
        if (TaskManager.Instance != null)
        {
            TaskManager.Instance.SetSceneRooms(roomsInScene);
        }
    }
}
