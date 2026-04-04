using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class TaskManager : MonoBehaviour
{
    public static TaskManager Instance;

    private enum GamePhase
    {
        TutorialExploration,
        GuidedNavigation
    }

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

    [Header("UI Centro (Fase 1)")]
    [SerializeField] private TextMeshProUGUI centerMessageText;
    [SerializeField] private float roomNameDisplayDuration = 3.5f;
    [SerializeField] private float tutorialCompletionMessageDuration = 3f;
    [SerializeField] private string tutorialCompletionMessage = "Bom trabalho! Vamos comecar a fase 2.";

    [Header("Fim da Fase 1")]
    [SerializeField] private string initialSceneName;
    [SerializeField] private string initialSpawnID = "Spawn_Initial";

    [Header("Definições")]
    [SerializeField] private bool shuffleTaskOrder = true;
    [SerializeField] private int maxTasksPerRound = 10;

    private RoomZone[] rooms;
    private string currentRoomID;
    private string currentRoomName;
    private GlobalRoomData targetRoomData;
    private GamePhase currentPhase = GamePhase.TutorialExploration;

    private readonly HashSet<string> allRoomIDs = new HashSet<string>();
    private readonly HashSet<string> visitedRoomIDs = new HashSet<string>();

    private List<GlobalRoomData> taskOrder = new List<GlobalRoomData>();
    private int currentTaskIndex = 0;
    private int totalTaskCount = 0;
    private bool allTasksCompleted = false;
    private bool tutorialEndSequenceRunning = false;
    private Coroutine centerMessageRoutine;

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
        BuildGlobalRoomCatalog();
        UpdateCurrentRoomUI();
        HideCenterMessage();
        UpdateTaskUI();
    }

    public void SetSceneRooms(RoomZone[] newRooms)
    {
        rooms = newRooms;

        ClearAllHighlights();

        if (currentPhase == GamePhase.TutorialExploration)
            ApplyVisitedMarksInCurrentScene();
        else
            ClearAllVisitedMarksInCurrentScene();

        Debug.Log("TaskManager recebeu " + rooms.Length + " divisões da nova cena.");

        if (currentPhase == GamePhase.TutorialExploration)
        {
            UpdateTaskUI();
            return;
        }

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

        if (currentPhase == GamePhase.TutorialExploration)
        {
            ShowRoomNameInCenterTemporarily(room.roomName);
            HandleTutorialRoomVisit(room);
            return;
        }

        if (allTasksCompleted)
            return;

        if (targetRoomData != null && room.roomID == targetRoomData.roomID)
        {
            Debug.Log("Tarefa concluída! Chegaste a: " + targetRoomData.roomName);

            currentTaskIndex++;
            SetNextTaskFromList();
        }
    }

    private void BuildGlobalRoomCatalog()
    {
        allRoomIDs.Clear();

        if (allRooms == null || allRooms.Length == 0)
        {
            Debug.LogWarning("A lista global allRooms está vazia.");
            return;
        }

        foreach (GlobalRoomData room in allRooms)
        {
            if (room == null || string.IsNullOrWhiteSpace(room.roomID))
                continue;

            allRoomIDs.Add(room.roomID);
        }

        Debug.Log("Total de divisões globais para tutorial: " + allRoomIDs.Count);
    }

    private void HandleTutorialRoomVisit(RoomZone room)
    {
        if (tutorialEndSequenceRunning)
            return;

        if (string.IsNullOrWhiteSpace(room.roomID))
            return;

        bool isNewVisit = visitedRoomIDs.Add(room.roomID);

        if (isNewVisit)
        {
            room.SetVisitedMark(true);
            Debug.Log("Tutorial: nova divisão visitada -> " + room.roomName +
                      " | progresso=" + visitedRoomIDs.Count + "/" + allRoomIDs.Count);
        }

        UpdateTaskUI();

        if (allRoomIDs.Count > 0 && visitedRoomIDs.Count >= allRoomIDs.Count)
        {
            StartCoroutine(FinishTutorialThenStartPhase2(room.roomName));
            return;
        }
    }

    private IEnumerator FinishTutorialThenStartPhase2(string lastRoomName)
    {
        tutorialEndSequenceRunning = true;

        if (!string.IsNullOrWhiteSpace(lastRoomName))
        {
            SetCenterMessage(lastRoomName);
            yield return new WaitForSeconds(roomNameDisplayDuration);
        }

        if (!string.IsNullOrWhiteSpace(tutorialCompletionMessage))
        {
            SetCenterMessage(tutorialCompletionMessage);
            yield return new WaitForSeconds(tutorialCompletionMessageDuration);
        }

        HideCenterMessage();
        ClearAllVisitedMarksInCurrentScene();

        StartGuidedNavigationPhase();
        ReturnToInitialSpawn();
        tutorialEndSequenceRunning = false;
    }

    private void StartGuidedNavigationPhase()
    {
        if (currentPhase == GamePhase.GuidedNavigation)
            return;

        currentPhase = GamePhase.GuidedNavigation;
        Debug.Log("Tutorial concluído. A iniciar Fase 2 (navegação orientada).");
        BuildTaskList();
    }

    private void ReturnToInitialSpawn()
    {
        string sceneToLoad = string.IsNullOrWhiteSpace(initialSceneName)
            ? SceneManager.GetActiveScene().name
            : initialSceneName;

        if (SceneTransitionManager.Instance == null)
        {
            Debug.LogWarning("SceneTransitionManager não encontrado. Não foi possível voltar ao spawn inicial automaticamente.");
            return;
        }

        if (string.IsNullOrWhiteSpace(initialSpawnID))
        {
            Debug.LogWarning("initialSpawnID está vazio no TaskManager.");
            return;
        }

        if (SceneTransitionManager.Instance.IsTransitioning)
            return;

        Debug.Log("A regressar ao início da experiência: cena=" + sceneToLoad + " | spawn=" + initialSpawnID);
        SceneTransitionManager.Instance.TransitionToScene(sceneToLoad, initialSpawnID);
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

    private void ApplyVisitedMarksInCurrentScene()
    {
        if (rooms == null)
            return;

        foreach (RoomZone room in rooms)
        {
            if (room == null)
                continue;

            bool wasVisited = !string.IsNullOrWhiteSpace(room.roomID) && visitedRoomIDs.Contains(room.roomID);
            room.SetVisitedMark(wasVisited);
        }
    }

    private void ClearAllVisitedMarksInCurrentScene()
    {
        if (rooms == null)
            return;

        foreach (RoomZone room in rooms)
        {
            if (room != null)
                room.SetVisitedMark(false);
        }
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

        currentRoomText.text = string.Empty;

        if (currentRoomText.gameObject.activeSelf)
            currentRoomText.gameObject.SetActive(false);
    }

    private void UpdateTaskUI()
    {
        if (taskText == null)
            return;

        if (currentPhase == GamePhase.TutorialExploration)
        {
            int totalRooms = allRoomIDs.Count;
            int visitedRooms = visitedRoomIDs.Count;
            taskText.text = "Tutorial (" + visitedRooms + "/" + totalRooms + "): Explora e visita todas as divisões";
            return;
        }

        if (allTasksCompleted)
        {
            taskText.text = "Tarefa concluída";
            return;
        }

        if (targetRoomData != null)
        {
            taskText.text = "Tarefa (" + (currentTaskIndex + 1) + "/" + totalTaskCount + "): Vai para " + targetRoomData.roomName;
        }
        else
        {
            taskText.text = "Tarefa: -";
        }
    }

    private void ShowRoomNameInCenterTemporarily(string roomName)
    {
        if (tutorialEndSequenceRunning || string.IsNullOrWhiteSpace(roomName))
            return;

        if (centerMessageRoutine != null)
            StopCoroutine(centerMessageRoutine);

        centerMessageRoutine = StartCoroutine(CenterMessageRoutine(roomName, roomNameDisplayDuration));
    }

    private IEnumerator CenterMessageRoutine(string message, float duration)
    {
        SetCenterMessage(message);
        yield return new WaitForSeconds(duration);

        if (!tutorialEndSequenceRunning)
            HideCenterMessage();

        centerMessageRoutine = null;
    }

    private void SetCenterMessage(string message)
    {
        if (centerMessageText == null)
            return;

        centerMessageText.gameObject.SetActive(true);
        centerMessageText.text = message;
    }

    private void HideCenterMessage()
    {
        if (centerMessageText == null)
            return;

        centerMessageText.text = string.Empty;
        centerMessageText.gameObject.SetActive(false);
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