using System.Collections.Generic;
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

    [Header("Definições")]
    [SerializeField] private bool shuffleTaskOrder = true;
    [SerializeField] private int maxTasksPerRound = 10;

    private RoomZone[] rooms;
    private string currentRoomID;
    private string currentRoomName;
    private GlobalRoomData targetRoomData;

    private List<GlobalRoomData> taskOrder = new List<GlobalRoomData>();
    private int currentTaskIndex = 0;
    private int totalTaskCount = 0;
    private bool allTasksCompleted = false;

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
        BuildTaskList();
        UpdateCurrentRoomUI();
        UpdateTaskUI();
    }

    public void SetSceneRooms(RoomZone[] newRooms)
    {
        rooms = newRooms;

        ClearAllHighlights();

        Debug.Log("TaskManager recebeu " + rooms.Length + " divisões da nova cena.");

        if (allTasksCompleted)
        {
            UpdateTaskUI();
            return;
        }

        if (targetRoomData == null)
        {
            SetNextTaskFromList();
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

        if (allTasksCompleted)
            return;

        if (targetRoomData != null && room.roomID == targetRoomData.roomID)
        {
            Debug.Log("Tarefa concluída! Chegaste a: " + targetRoomData.roomName);

            currentTaskIndex++;
            SetNextTaskFromList();
        }
    }

    private void BuildTaskList()
    {
        taskOrder.Clear();
        currentTaskIndex = 0;
        totalTaskCount = 0;
        targetRoomData = null;
        allTasksCompleted = false;

        if (allRooms == null || allRooms.Length == 0)
        {
            Debug.LogWarning("A lista global allRooms está vazia.");
            UpdateTaskUI();
            return;
        }

        foreach (GlobalRoomData room in allRooms)
        {
            if (room == null)
                continue;

            if (string.IsNullOrWhiteSpace(room.roomID))
                continue;

            taskOrder.Add(room);
        }

        if (shuffleTaskOrder)
            ShuffleList(taskOrder);

        if (maxTasksPerRound > 0 && taskOrder.Count > maxTasksPerRound)
        {
            taskOrder = taskOrder.GetRange(0, maxTasksPerRound);
        }

        totalTaskCount = taskOrder.Count;

        Debug.Log("Total de tarefas nesta ronda: " + totalTaskCount);

        for (int i = 0; i < taskOrder.Count; i++)
        {
            Debug.Log("Ordem[" + i + "] = " + taskOrder[i].roomName + " | cena=" + taskOrder[i].sceneName);
        }

        SetNextTaskFromList();
    }

    private void SetNextTaskFromList()
    {
        ClearAllHighlights();

        if (taskOrder == null || taskOrder.Count == 0)
        {
            targetRoomData = null;
            allTasksCompleted = true;
            UpdateTaskUI();
            return;
        }

        while (currentTaskIndex < taskOrder.Count)
        {
            GlobalRoomData candidate = taskOrder[currentTaskIndex];

            if (candidate != null &&
                !string.IsNullOrWhiteSpace(candidate.roomID) &&
                candidate.roomID != currentRoomID)
            {
                targetRoomData = candidate;

                HighlightTargetIfPresentInCurrentScene();
                UpdateTaskUI();

                Debug.Log("Nova tarefa: Vai para " + targetRoomData.roomName +
                          " | scene=" + targetRoomData.sceneName +
                          " | roomID=" + targetRoomData.roomID +
                          " | progresso=" + currentTaskIndex + "/" + totalTaskCount);
                return;
            }

            currentTaskIndex++;
        }

        targetRoomData = null;
        allTasksCompleted = true;
        UpdateTaskUI();

        Debug.Log("Todas as tarefas foram concluídas.");
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

        if (allTasksCompleted)
        {
            taskText.text = "Tarefa concluída";
            return;
        }

        if (targetRoomData != null)
        {
            taskText.text = "Tarefa (" + currentTaskIndex + "/" + totalTaskCount + "): Vai para " + targetRoomData.roomName;
        }
        else
        {
            taskText.text = "Tarefa: -";
        }
    }

    private void ShuffleList(List<GlobalRoomData> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);
            GlobalRoomData temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
}