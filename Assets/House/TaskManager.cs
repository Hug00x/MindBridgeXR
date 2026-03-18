using UnityEngine;
using TMPro;

public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance;

    [System.Serializable]
    public class GlobalRoomData
    {
        public string roomID;
        public string roomName;
        public string sceneName;
    }

    [Header("Lista global de todas as divisões")]
    [SerializeField] private GlobalRoomData[] allRooms;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI currentRoomText;
    [SerializeField] private TextMeshProUGUI taskText;

    private RoomZone[] rooms;
    private string currentRoomID;
    private string currentRoomName;
    private GlobalRoomData targetRoomData;

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

        if (targetRoomData == null)
        {
            StartNewTask();
            return;
        }

        HighlightTargetIfPresentInCurrentScene();
        UpdateTaskUI();
    }

    public void PlayerEnteredRoom(RoomZone room)
    {
        if (room == null)
            return;

        currentRoomID = room.roomID;
        currentRoomName = room.roomName;

        UpdateCurrentRoomUI();

        Debug.Log("Entraste em: " + room.roomName + " | roomID=" + room.roomID);

        if (targetRoomData != null && room.roomID == targetRoomData.roomID)
        {
            Debug.Log("Tarefa concluída! Chegaste a: " + targetRoomData.roomName);
            StartNewTask();
        }
    }

    public void StartNewTask()
    {
        if (allRooms == null || allRooms.Length < 2)
        {
            Debug.LogWarning("Precisas de pelo menos 2 divisões na lista global allRooms.");
            return;
        }

        ClearAllHighlights();

        GlobalRoomData newTarget = null;
        int safety = 0;

        while (newTarget == null && safety < 100)
        {
            GlobalRoomData candidate = allRooms[Random.Range(0, allRooms.Length)];

            if (!string.IsNullOrWhiteSpace(candidate.roomID) &&
                candidate.roomID != currentRoomID)
            {
                newTarget = candidate;
            }

            safety++;
        }

        targetRoomData = newTarget;

        if (targetRoomData != null)
        {
            HighlightTargetIfPresentInCurrentScene();
            UpdateTaskUI();

            Debug.Log("Nova tarefa: Vai para " + targetRoomData.roomName +
                      " | scene=" + targetRoomData.sceneName +
                      " | roomID=" + targetRoomData.roomID);
        }
    }

    private void HighlightTargetIfPresentInCurrentScene()
    {
        if (targetRoomData == null || rooms == null)
            return;

        foreach (RoomZone room in rooms)
        {
            if (room == null)
                continue;

            bool isTarget = room.roomID == targetRoomData.roomID;
            room.SetHighlight(isTarget);
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

        if (!string.IsNullOrEmpty(currentRoomName))
            currentRoomText.text = "Divisão atual: " + currentRoomName;
        else
            currentRoomText.text = "Divisão atual: -";
    }

    private void UpdateTaskUI()
    {
        if (taskText == null)
            return;

        if (targetRoomData != null)
            taskText.text = "Tarefa: Vai para " + targetRoomData.roomName;
        else
            taskText.text = "Tarefa: -";
    }
}