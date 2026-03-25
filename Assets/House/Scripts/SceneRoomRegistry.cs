using UnityEngine;

public class SceneRoomRegistry : MonoBehaviour
{
    [Header("Divisões desta cena")]
    public RoomZone[] roomsInScene;

    private void Start()
    {
        if (TaskManager.Instance != null)
        {
            TaskManager.Instance.SetSceneRooms(roomsInScene);
        }
        else
        {
            Debug.LogWarning("TaskManager não encontrado na cena.");
        }
    }
}