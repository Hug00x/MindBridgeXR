using UnityEngine;
using TMPro;

public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance;

    [Header("Lista de divisões da cena atual")]
    public RoomZone[] rooms;

    [Header("UI")]
    public TextMeshProUGUI currentRoomText;
    public TextMeshProUGUI taskText;

    private RoomZone currentRoom;
    private RoomZone targetRoom;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        UpdateCurrentRoomUI();
        UpdateTaskUI();
    }

    public void SetSceneRooms(RoomZone[] newRooms)
    {
        rooms = newRooms;

        ClearAllHighlights();

        Debug.Log("TaskManager recebeu " + rooms.Length + " divisões da nova cena.");

        if (targetRoom != null)
        {
            bool targetExistsInThisScene = false;

            foreach (RoomZone room in rooms)
            {
                if (room == targetRoom)
                {
                    targetExistsInThisScene = true;
                    break;
                }
            }

            if (targetExistsInThisScene)
            {
                targetRoom.SetHighlight(true);
            }
            else
            {
                StartNewTask();
            }
        }
        else
        {
            StartNewTask();
        }
    }

    public void PlayerEnteredRoom(RoomZone room)
    {
        currentRoom = room;
        UpdateCurrentRoomUI();

        Debug.Log("Entraste em: " + room.roomName);

        if (targetRoom != null && room == targetRoom)
        {
            Debug.Log("Tarefa concluída! Chegaste a: " + targetRoom.roomName);
            StartNewTask();
        }
    }

    public void StartNewTask()
    {
        if (rooms == null || rooms.Length < 2)
        {
            Debug.LogWarning("Precisas de pelo menos 2 divisões na cena atual.");
            return;
        }

        ClearAllHighlights();

        RoomZone newTarget = null;
        int safety = 0;

        while (newTarget == null && safety < 50)
        {
            RoomZone candidate = rooms[Random.Range(0, rooms.Length)];

            if (candidate != currentRoom)
                newTarget = candidate;

            safety++;
        }

        targetRoom = newTarget;

        if (targetRoom != null)
        {
            targetRoom.SetHighlight(true);
            UpdateTaskUI();
            Debug.Log("Nova tarefa: Vai para " + targetRoom.roomName);
        }
    }

    private void ClearAllHighlights()
    {
        if (rooms == null)
            return;

        foreach (RoomZone room in rooms)
        {
            if (room != null)
                room.SetHighlight(false);
        }
    }

    private void UpdateCurrentRoomUI()
    {
        if (currentRoomText == null)
            return;

        if (currentRoom != null)
            currentRoomText.text = "Divisão: " + currentRoom.roomName;
        else
            currentRoomText.text = "Divisão: -";
    }

    private void UpdateTaskUI()
    {
        if (taskText == null)
            return;

        if (targetRoom != null)
            taskText.text = "Tarefa: Vai para " + targetRoom.roomName;
        else
            taskText.text = "Tarefa: -";
    }
}