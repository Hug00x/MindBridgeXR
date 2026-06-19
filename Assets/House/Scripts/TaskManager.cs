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
    [SerializeField] private float tutorialCompletionToGuidedDelay = 2.5f;
    [SerializeField] private float phaseStartDelayAfterTeleport = 0.2f;

    [Header("Feedback de fim de fase")]
    [SerializeField] private float phaseCompletionMessageHoldSeconds = 2.5f;
    [SerializeField] private string phase1CompletionMessage = "Fase 1 concluida.\n\nBoa exploração.";
    [SerializeField] private string phase2CompletionMessage = "Fase 2 concluida.\n\nEncontraste todas as divisões.";
    [SerializeField] private string phase3CompletionMessage = "Fase 3 concluida.\n\nCompletaste o jogo da memória.";
    [SerializeField] private string phase4CompletionMessage = "Fase 4 concluida.\n\nObrigado por participares na experiência.\n\nJá podes retirar os óculos.";

    [Header("Fase 3 - Memória na Sala de Jantar")]
    [SerializeField] private bool startDiningMemoryPhaseAfterPhase2 = true;
    [SerializeField] private DiningMemoryPhaseController diningMemoryPhaseController;
    [SerializeField] private float guidedCompletionDisplaySeconds = 2.5f;
    [SerializeField] private float guidedTaskStartGraceSeconds = 1.25f;

    [Header("Fase 4 - Alimentos no Exterior")]
    [SerializeField] private bool startOutdoorFoodPhaseAfterMemory = true;
    [SerializeField] private OutdoorFoodPhaseController outdoorFoodPhaseController;
    [SerializeField] private float memoryCompletionToOutdoorDelay = 2.5f;

    [Header("Métricas")]
    [Tooltip("Pede o ID do participante através de um teclado numérico ao iniciar a aplicação.")]
    [SerializeField] private bool requestParticipantIdOnStartup = true;
    [Tooltip("Usado apenas quando o pedido de ID no arranque está desativado.")]
    [SerializeField] private string fallbackParticipantId = "P001";
    [SerializeField] private bool collectPhase1DistanceTravelled = false;
    [SerializeField, Min(0.1f)] private float distanceSampleInterval = 0.5f;

    private RoomZone[] rooms;
    private string currentRoomID;
    private GlobalRoomData targetRoomData;
    private GamePhase currentPhase = GamePhase.TutorialExploration;
    private const string Phase1TextsRootName = "Texts1Fase";
    private const string Phase2TextsRootName = "Texts2Fase";
    private const string Phase3TextsRootName = "Texts3Fase";
    private const string Phase4TextsRootName = "Texts4Fase";
    private static readonly string[] GuidedTaskRoomOrder =
    {
        "floor2_bathroom2",
        "floor2_bedroomB",
        "floor1_bathroom1",
        "exterior_patio",
        "floor1_livingroom"
    };

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
    private bool guidedMetricsStarted = false;
    private bool metricsSessionStarted = false;
    private RoomZone pendingRoomBeforeSessionStart;
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

        if (requestParticipantIdOnStartup)
            ParticipantIdEntryUI.Show(StartMetricsSession);
        else
            StartMetricsSession(fallbackParticipantId);
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
                outdoorFoodPhaseController.BeginPhase(resetSavedProgress: false);
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

        if (!metricsSessionStarted)
        {
            pendingRoomBeforeSessionStart = room;
            return;
        }

        MetricsManager.Instance?.RecordRoomEntered(room.roomID);
        currentRoomID = room.roomID;

        if (diningMemoryPhaseStarted && !diningMemoryPhaseCompleted)
        {
            EnsureDiningMemoryControllerReference();
            if (diningMemoryPhaseController != null)
                diningMemoryPhaseController.NotifyPlayerEnteredRoom(room.roomID);
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

            if (guidedMetricsStarted)
                MetricsManager.Instance?.CompleteGuidedTask(currentTaskIndex + 1);

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

    public void StartMetricsSession(string participantId)
    {
        if (metricsSessionStarted)
            return;

        MetricsManager metrics = MetricsManager.GetOrCreate();
        metrics.BeginSession(
            participantId,
            collectPhase1DistanceTravelled,
            distanceSampleInterval);
        metrics.BeginPhase1();
        metricsSessionStarted = true;

        if (pendingRoomBeforeSessionStart != null)
        {
            RoomZone initialRoom = pendingRoomBeforeSessionStart;
            pendingRoomBeforeSessionStart = null;
            PlayerEnteredRoom(initialRoom);
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
            MetricsManager.Instance?.CompletePhase1();
            StartCoroutine(FinishTutorialThenStartPhase2());
            return;
        }
    }

    private IEnumerator FinishTutorialThenStartPhase2()
    {
        tutorialEndSequenceRunning = true;

        if (tutorialCompletionToGuidedDelay > 0f)
            yield return new WaitForSeconds(tutorialCompletionToGuidedDelay);

        ClearAllVisitedMarksInCurrentScene();

        StartGuidedNavigationPhase();
        yield return StartCoroutine(ReturnToInitialSpawnThen(phase1CompletionMessage, BeginGuidedMetrics));
        tutorialEndSequenceRunning = false;
    }

    private void BeginGuidedMetrics()
    {
        if (guidedMetricsStarted)
            return;

        guidedMetricsStarted = true;
        MetricsManager.Instance?.BeginPhase2();

        if (targetRoomData != null)
        {
            MetricsManager.Instance?.BeginGuidedTask(
                currentTaskIndex + 1,
                targetRoomData.roomID);
        }
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

        Dictionary<string, GlobalRoomData> roomsByID = new Dictionary<string, GlobalRoomData>();

        foreach (GlobalRoomData room in allRooms)
        {
            if (room == null)
                continue;

            if (string.IsNullOrWhiteSpace(room.roomID))
                continue;

            if (!roomsByID.ContainsKey(room.roomID))
                roomsByID.Add(room.roomID, room);
        }

        foreach (string roomID in GuidedTaskRoomOrder)
        {
            if (roomsByID.TryGetValue(roomID, out GlobalRoomData room))
            {
                taskOrder.Add(room);
            }
            else
            {
                Debug.LogWarning("A divisão da Fase 2 com o ID '" + roomID + "' não foi encontrada em allRooms.");
            }
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

                if (guidedMetricsStarted)
                {
                    MetricsManager.Instance?.BeginGuidedTask(
                        currentTaskIndex + 1,
                        targetRoomData.roomID);
                }

                return;
            }

            currentTaskIndex++;
        }

        targetRoomData = null;
        allTasksCompleted = true;
        UpdatePhase2TaskListText();

        if (guidedMetricsStarted)
        {
            MetricsManager.Instance?.CompletePhase2();
            guidedMetricsStarted = false;
        }

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
        MetricsManager.Instance?.BeginPhase3();
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
            outdoorFoodPhaseController.BeginPhase(resetSavedProgress: false);
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
        MetricsManager.Instance?.CompletePhase3();

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
        MetricsManager.Instance?.BeginPhase4();

        EnsureOutdoorFoodControllerReference();

        if (outdoorFoodPhaseController != null)
        {
            outdoorFoodPhaseController.BeginPhase(resetSavedProgress: true);
        }

        return true;
    }

    private void OnOutdoorFoodPhaseCompleted()
    {
        outdoorFoodPhaseCompleted = true;
        currentPhase = GamePhase.OutdoorFood;
        RefreshPhaseTextsVisibility();
        MetricsManager.Instance?.CompletePhase4();
        MetricsManager.Instance?.CompleteExperience();

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

}
