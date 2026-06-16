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
        GuidedNavigation,
        DiningMemory,
        OutdoorFood
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

    [Header("Textos de introdução das fases")]
    [SerializeField] private GameObject phase1TextsRoot;
    [SerializeField] private GameObject phase2TextsRoot;
    [SerializeField] private TMP_Text phase2TaskListText;
    [SerializeField] private string phase2TaskListTextName = "Phase2TaskListText";
    [SerializeField] private string phase2TaskListHeader = "Fase 2 - Encontrar divisões\n\nSegue as instruções e vai até as divisões indicadas.";
    [SerializeField] private GameObject phase3TextsRoot;
    [SerializeField] private GameObject phase4TextsRoot;

    [Header("Fim da Fase 1")]
    [SerializeField] private string initialSceneName;
    [SerializeField] private string initialSpawnID = "Spawn_Initial";
    [SerializeField] private bool returnToInitialSpawnBetweenPhases = true;
    [SerializeField] private float phaseStartDelayAfterTeleport = 0.2f;

    [Header("Feedback de fim de fase")]
    [SerializeField] private float phaseCompletionMessageHoldSeconds = 2.5f;
    [SerializeField] private string phase1CompletionMessage = "Fase 1 concluída!\n\nBoa exploração.";
    [SerializeField] private string phase2CompletionMessage = "Fase 2 concluída!\n\nEncontraste todas as divisões.";
    [SerializeField] private string phase3CompletionMessage = "Fase 3 concluída!\n\nCompletaste o jogo da memória.";
    [SerializeField] private string phase4CompletionMessage = "Fase 4 concluída!\n\nObrigado por participares na experiência.\n\nJá podes retirar os óculos.";

    [Header("Fase 3 - Memória na Sala de Jantar")]
    [SerializeField] private bool startDiningMemoryPhaseAfterPhase2 = true;
    [SerializeField] private DiningMemoryPhaseController diningMemoryPhaseController;
    [SerializeField] private float guidedCompletionDisplaySeconds = 2.5f;
    [SerializeField] private float guidedTaskStartGraceSeconds = 1.25f;

    [Header("Fase 4 - Alimentos no Exterior")]
    [SerializeField] private bool startOutdoorFoodPhaseAfterMemory = true;
    [SerializeField] private OutdoorFoodPhaseController outdoorFoodPhaseController;
    [SerializeField] private float memoryCompletionToOutdoorDelay = 2.5f;

    [Header("Definições")]
    [SerializeField] private bool shuffleTaskOrder = true;
    [SerializeField] private int maxTasksPerRound = 10;

    private RoomZone[] rooms;
    private string currentRoomID;
    private GlobalRoomData targetRoomData;
    private GamePhase currentPhase = GamePhase.TutorialExploration;
    private const string Phase1TextsRootName = "Texts1Fase";
    private const string Phase2TextsRootName = "Texts2Fase";
    private const string Phase3TextsRootName = "Texts3Fase";
    private const string Phase4TextsRootName = "Texts4Fase";

    private readonly HashSet<string> allRoomIDs = new HashSet<string>();
    private readonly HashSet<string> visitedRoomIDs = new HashSet<string>();

    private List<GlobalRoomData> taskOrder = new List<GlobalRoomData>();
    private int currentTaskIndex = 0;
    private int totalTaskCount = 0;
    private bool allTasksCompleted = false;
    private bool tutorialEndSequenceRunning = false;
    private Coroutine guidedCompletionRoutine;
    private Coroutine outdoorFoodStartRoutine;
    private bool diningMemoryPhaseStarted = false;
    private bool diningMemoryPhaseCompleted = false;
    private bool outdoorFoodPhaseStarted = false;
    private bool outdoorFoodPhaseCompleted = false;
    private float guidedTasksBlockedUntil = -1f;
    private DiningMemoryPhaseController subscribedDiningMemoryPhaseController;
    private OutdoorFoodPhaseController subscribedOutdoorFoodPhaseController;

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
        EnsureDiningMemoryControllerReference();
        EnsureOutdoorFoodControllerReference();
        RefreshPhaseTextsVisibility();
    }

    private void OnDestroy()
    {
        UnsubscribeDiningMemoryPhaseEvents();
        UnsubscribeOutdoorFoodPhaseEvents();
    }

    public void SetSceneRooms(RoomZone[] newRooms)
    {
        rooms = newRooms;

        ClearAllHighlights();
        ClearScenePhaseTextReferences();
        RefreshPhaseTextsVisibility();

        if (currentPhase == GamePhase.TutorialExploration)
            ApplyVisitedMarksInCurrentScene();
        else
            ClearAllVisitedMarksInCurrentScene();

        if (currentPhase == GamePhase.TutorialExploration)
        {
            return;
        }

        if (outdoorFoodPhaseStarted && !outdoorFoodPhaseCompleted)
        {
            EnsureOutdoorFoodControllerReference();
            if (outdoorFoodPhaseController != null && !outdoorFoodPhaseController.IsRunning)
                outdoorFoodPhaseController.BeginPhase(true, false);
            return;
        }

        if (allTasksCompleted)
        {
            if (diningMemoryPhaseStarted)
                return;

            return;
        }

        if (targetRoomData == null)
        {
            SetNextTaskFromList();
            return;
        }

        HighlightTargetIfPresentInCurrentScene();
    }

    public void PlayerEnteredRoom(RoomZone room)
    {
        if (room == null)
            return;

        currentRoomID = room.roomID;

        if (diningMemoryPhaseStarted && !diningMemoryPhaseCompleted)
        {
            EnsureDiningMemoryControllerReference();
            if (diningMemoryPhaseController != null)
                diningMemoryPhaseController.NotifyPlayerEnteredRoom(room.roomID);
        }

        if (outdoorFoodPhaseStarted && !outdoorFoodPhaseCompleted)
        {
            EnsureOutdoorFoodControllerReference();
            if (outdoorFoodPhaseController != null)
                outdoorFoodPhaseController.NotifyPlayerEnteredRoom(room.roomID);
        }

        if (currentPhase == GamePhase.TutorialExploration)
        {
            HandleTutorialRoomVisit(room);
            return;
        }

        if (currentPhase == GamePhase.OutdoorFood)
            return;

        if (allTasksCompleted)
            return;

        if (targetRoomData != null && room.roomID == targetRoomData.roomID)
        {
            if (Time.time < guidedTasksBlockedUntil)
                return;

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

    }

    private void HandleTutorialRoomVisit(RoomZone room)
    {
        if (tutorialEndSequenceRunning)
            return;

        if (string.IsNullOrWhiteSpace(room.roomID))
            return;

        bool isNewVisit = visitedRoomIDs.Add(room.roomID);

        if (isNewVisit)
            SetVisitedMarkForRoomIDInCurrentScene(room.roomID, true);

        if (allRoomIDs.Count > 0 && visitedRoomIDs.Count >= allRoomIDs.Count)
        {
            StartCoroutine(FinishTutorialThenStartPhase2());
            return;
        }
    }

    private IEnumerator FinishTutorialThenStartPhase2()
    {
        tutorialEndSequenceRunning = true;
        ClearAllVisitedMarksInCurrentScene();

        StartGuidedNavigationPhase();
        yield return StartCoroutine(ReturnToInitialSpawnThen(phase1CompletionMessage, null));
        tutorialEndSequenceRunning = false;
    }

    private void StartGuidedNavigationPhase()
    {
        if (currentPhase == GamePhase.GuidedNavigation)
            return;

        currentPhase = GamePhase.GuidedNavigation;
        guidedTasksBlockedUntil = Time.time + Mathf.Max(0f, guidedTaskStartGraceSeconds);
        RefreshPhaseTextsVisibility();
        BuildTaskList();
    }

    private IEnumerator ReturnToInitialSpawnThen(string transitionMessage, System.Action afterReturn)
    {
        if (!returnToInitialSpawnBetweenPhases)
        {
            afterReturn?.Invoke();
            yield break;
        }

        string sceneToLoad = string.IsNullOrWhiteSpace(initialSceneName)
            ? SceneManager.GetActiveScene().name
            : initialSceneName;

        if (SceneTransitionManager.Instance == null)
        {
            Debug.LogWarning("SceneTransitionManager não encontrado. Não foi possível voltar ao spawn inicial automaticamente.");
            afterReturn?.Invoke();
            yield break;
        }

        if (string.IsNullOrWhiteSpace(initialSpawnID))
        {
            Debug.LogWarning("initialSpawnID está vazio no TaskManager.");
            afterReturn?.Invoke();
            yield break;
        }

        if (SceneTransitionManager.Instance.IsTransitioning)
        {
            while (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)
                yield return null;
        }

        SceneTransitionManager.Instance.TransitionToScene(
            sceneToLoad,
            initialSpawnID,
            transitionMessage,
            phaseCompletionMessageHoldSeconds);

        while (SceneTransitionManager.Instance != null && SceneTransitionManager.Instance.IsTransitioning)
            yield return null;

        if (phaseStartDelayAfterTeleport > 0f)
            yield return new WaitForSeconds(phaseStartDelayAfterTeleport);

        ClearScenePhaseTextReferences();
        RefreshPhaseTextsVisibility();
        afterReturn?.Invoke();
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
            return;
        }

        HashSet<string> uniqueRoomIDs = new HashSet<string>();

        foreach (GlobalRoomData room in allRooms)
        {
            if (room == null)
                continue;

            if (string.IsNullOrWhiteSpace(room.roomID))
                continue;

            if (!uniqueRoomIDs.Add(room.roomID))
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

        UpdatePhase2TaskListText();
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

    private void SetVisitedMarkForRoomIDInCurrentScene(string roomID, bool state)
    {
        if (rooms == null || string.IsNullOrWhiteSpace(roomID))
            return;

        foreach (RoomZone room in rooms)
        {
            if (room == null)
                continue;

            if (room.roomID == roomID)
                room.SetVisitedMark(state);
        }
    }

    private void SetNextTaskFromList()
    {
        ClearAllHighlights();

        if (taskOrder == null || taskOrder.Count == 0)
        {
            targetRoomData = null;
            allTasksCompleted = true;
            UpdatePhase2TaskListText();
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
                UpdatePhase2TaskListText();

                return;
            }

            currentTaskIndex++;
        }

        targetRoomData = null;
        allTasksCompleted = true;
        UpdatePhase2TaskListText();
        if (guidedCompletionRoutine != null)
            StopCoroutine(guidedCompletionRoutine);

        guidedCompletionRoutine = StartCoroutine(CompleteGuidedPhaseThenStartNext());
    }

    private IEnumerator CompleteGuidedPhaseThenStartNext()
    {
        if (guidedCompletionDisplaySeconds > 0f)
            yield return new WaitForSeconds(guidedCompletionDisplaySeconds);

        bool shouldStartDiningPhase = startDiningMemoryPhaseAfterPhase2 && !diningMemoryPhaseStarted;

        if (shouldStartDiningPhase)
        {
            yield return StartCoroutine(ReturnToInitialSpawnThen(phase2CompletionMessage, () =>
            {
                TryStartDiningMemoryPhase();
            }));
        }
        else
        {
            TryStartDiningMemoryPhase();
        }

        guidedCompletionRoutine = null;
    }

    private bool TryStartDiningMemoryPhase()
    {
        if (!startDiningMemoryPhaseAfterPhase2)
            return false;

        if (diningMemoryPhaseStarted)
            return true;

        EnsureDiningMemoryControllerReference();

        if (diningMemoryPhaseController == null)
        {
            Debug.LogWarning("DiningMemoryPhaseController não atribuído no TaskManager.");
            return false;
        }

        diningMemoryPhaseStarted = true;
        currentPhase = GamePhase.DiningMemory;
        RefreshPhaseTextsVisibility();
        diningMemoryPhaseController.BeginPhase();

        if (!string.IsNullOrWhiteSpace(currentRoomID))
            diningMemoryPhaseController.NotifyPlayerEnteredRoom(currentRoomID);

        return true;
    }

    private void EnsureDiningMemoryControllerReference()
    {
        if (diningMemoryPhaseController == null)
            diningMemoryPhaseController = FindFirstObjectByType<DiningMemoryPhaseController>(FindObjectsInactive.Exclude);

        SubscribeDiningMemoryPhaseEvents();
    }

    public void RegisterOutdoorFoodPhaseController(OutdoorFoodPhaseController controller)
    {
        if (controller == null)
            return;

        if (outdoorFoodPhaseController != controller)
        {
            UnsubscribeOutdoorFoodPhaseEvents();
            outdoorFoodPhaseController = controller;
            SubscribeOutdoorFoodPhaseEvents();
        }

        if (outdoorFoodPhaseStarted && !outdoorFoodPhaseCompleted && !outdoorFoodPhaseController.IsRunning)
            outdoorFoodPhaseController.BeginPhase(true, false);
    }

    private void EnsureOutdoorFoodControllerReference()
    {
        if (outdoorFoodPhaseController == null)
            outdoorFoodPhaseController = FindFirstObjectByType<OutdoorFoodPhaseController>(FindObjectsInactive.Exclude);

        SubscribeOutdoorFoodPhaseEvents();
    }

    private void SubscribeDiningMemoryPhaseEvents()
    {
        if (subscribedDiningMemoryPhaseController == diningMemoryPhaseController)
            return;

        UnsubscribeDiningMemoryPhaseEvents();

        if (diningMemoryPhaseController == null)
            return;

        subscribedDiningMemoryPhaseController = diningMemoryPhaseController;
        subscribedDiningMemoryPhaseController.PhaseCompleted += OnDiningMemoryPhaseCompleted;
    }

    private void UnsubscribeDiningMemoryPhaseEvents()
    {
        if (subscribedDiningMemoryPhaseController != null)
            subscribedDiningMemoryPhaseController.PhaseCompleted -= OnDiningMemoryPhaseCompleted;

        subscribedDiningMemoryPhaseController = null;
    }

    private void SubscribeOutdoorFoodPhaseEvents()
    {
        if (subscribedOutdoorFoodPhaseController == outdoorFoodPhaseController)
            return;

        UnsubscribeOutdoorFoodPhaseEvents();

        if (outdoorFoodPhaseController == null)
            return;

        subscribedOutdoorFoodPhaseController = outdoorFoodPhaseController;
        subscribedOutdoorFoodPhaseController.PhaseCompleted += OnOutdoorFoodPhaseCompleted;
    }

    private void UnsubscribeOutdoorFoodPhaseEvents()
    {
        if (subscribedOutdoorFoodPhaseController != null)
            subscribedOutdoorFoodPhaseController.PhaseCompleted -= OnOutdoorFoodPhaseCompleted;

        subscribedOutdoorFoodPhaseController = null;
    }

    private void OnDiningMemoryPhaseCompleted()
    {
        if (diningMemoryPhaseCompleted)
            return;

        diningMemoryPhaseCompleted = true;

        if (!startOutdoorFoodPhaseAfterMemory)
            return;

        if (outdoorFoodStartRoutine != null)
            StopCoroutine(outdoorFoodStartRoutine);

        outdoorFoodStartRoutine = StartCoroutine(StartOutdoorFoodPhaseAfterDelay());
    }

    private IEnumerator StartOutdoorFoodPhaseAfterDelay()
    {
        if (memoryCompletionToOutdoorDelay > 0f)
            yield return new WaitForSeconds(memoryCompletionToOutdoorDelay);

        yield return StartCoroutine(ReturnToInitialSpawnThen(phase3CompletionMessage, () => TryStartOutdoorFoodPhase()));
        outdoorFoodStartRoutine = null;
    }

    private bool TryStartOutdoorFoodPhase()
    {
        if (!startOutdoorFoodPhaseAfterMemory)
            return false;

        if (outdoorFoodPhaseStarted)
            return true;

        outdoorFoodPhaseStarted = true;
        outdoorFoodPhaseCompleted = false;
        currentPhase = GamePhase.OutdoorFood;
        RefreshPhaseTextsVisibility();
        ClearAllHighlights();

        EnsureOutdoorFoodControllerReference();

        if (outdoorFoodPhaseController != null)
        {
            outdoorFoodPhaseController.BeginPhase(false, true);
        }

        return true;
    }

    private void OnOutdoorFoodPhaseCompleted()
    {
        outdoorFoodPhaseCompleted = true;
        currentPhase = GamePhase.OutdoorFood;
        RefreshPhaseTextsVisibility();

        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.ShowFinalMessage(phase4CompletionMessage, phaseCompletionMessageHoldSeconds);
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

    private void RefreshPhaseTextsVisibility()
    {
        ResolvePhaseTextRoots();
        ResolvePhaseTextComponents();
        UpdatePhase2TaskListText();

        SetPhaseTextRootVisible(phase1TextsRoot, currentPhase == GamePhase.TutorialExploration);
        SetPhaseTextRootVisible(phase2TextsRoot, currentPhase == GamePhase.GuidedNavigation);
        SetPhaseTextRootVisible(phase3TextsRoot, currentPhase == GamePhase.DiningMemory);
        SetPhaseTextRootVisible(phase4TextsRoot, currentPhase == GamePhase.OutdoorFood && !outdoorFoodPhaseCompleted);
    }

    private void ResolvePhaseTextRoots()
    {
        if (phase1TextsRoot == null)
            phase1TextsRoot = FindGameObjectByNameIncludingInactive(Phase1TextsRootName);

        if (phase2TextsRoot == null)
            phase2TextsRoot = FindGameObjectByNameIncludingInactive(Phase2TextsRootName);

        if (phase3TextsRoot == null)
            phase3TextsRoot = FindGameObjectByNameIncludingInactive(Phase3TextsRootName);

        if (phase4TextsRoot == null)
            phase4TextsRoot = FindGameObjectByNameIncludingInactive(Phase4TextsRootName);
    }

    private void ResolvePhaseTextComponents()
    {
        if (phase2TaskListText != null)
            return;

        GameObject textObject = FindGameObjectByNameIncludingInactive(phase2TaskListTextName);
        if (textObject != null)
            phase2TaskListText = textObject.GetComponent<TMP_Text>();

        if (phase2TaskListText == null && phase2TextsRoot != null)
            phase2TaskListText = phase2TextsRoot.GetComponentInChildren<TMP_Text>(true);
    }

    private void ClearScenePhaseTextReferences()
    {
        phase1TextsRoot = null;
        phase2TextsRoot = null;
        phase3TextsRoot = null;
        phase4TextsRoot = null;
        phase2TaskListText = null;
    }

    private void UpdatePhase2TaskListText()
    {
        if (phase2TaskListText == null)
            return;

        if (taskOrder == null || taskOrder.Count == 0)
        {
            phase2TaskListText.text = phase2TaskListHeader + "\n\nLista ainda nao preparada.";
            return;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        if (!string.IsNullOrWhiteSpace(phase2TaskListHeader))
            builder.AppendLine(phase2TaskListHeader);

        builder.AppendLine();

        for (int i = 0; i < taskOrder.Count; i++)
        {
            GlobalRoomData room = taskOrder[i];
            string roomName = room != null && !string.IsNullOrWhiteSpace(room.roomName)
                ? room.roomName
                : "Divisao " + (i + 1);

            builder.AppendLine((i + 1) + "º - " + roomName);
        }

        if (allTasksCompleted)
        {
            builder.AppendLine();
            builder.AppendLine("Todas as divisoes foram encontradas.");
        }

        phase2TaskListText.text = builder.ToString();
    }

    private void SetPhaseTextRootVisible(GameObject root, bool visible)
    {
        if (root == null)
            return;

        if (root.activeSelf != visible)
            root.SetActive(visible);
    }

    private GameObject FindGameObjectByNameIncludingInactive(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Transform item in transforms)
        {
            if (item != null && item.gameObject.name == objectName)
                return item.gameObject;
        }

        return null;
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
