using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/*
 * Serviço persistente de recolha de métricas do MindBridgeXR.
 * Acompanha a sessão do participante, regista eventos por fase, calcula
 * indicadores de desempenho e exporta os resultados para JSON, JSONL e CSV.
 */
public class MetricsManager : MonoBehaviour
{
    // Chaves usadas para detetar se a sessão anterior terminou corretamente.
    private const string LastSessionCompletedKey = "MindBridgeXR.Metrics.LastSessionCompleted";
    private const string LastSessionIdKey = "MindBridgeXR.Metrics.LastSessionId";

    // Acesso global ao gestor persistente.
    public static MetricsManager Instance { get; private set; }

    // Caminhos expostos para diagnóstico ou apresentação ao utilizador.
    public string SummaryFilePath => summaryFilePath;
    public string EventsFilePath => eventsFilePath;
    public string CombinedCsvFilePath => combinedCsvFilePath;
    public bool IsSessionActive => sessionActive;

    // Estado base da sessão e ficheiros de exportação.
    private SessionMetricsData data;
    private bool sessionActive;
    private bool collectDistanceTravelled;
    private float movementSampleInterval = 0.5f;
    private double sessionStartedRealtime;
    private string activePhase = string.Empty;
    private double activePhaseStartedRealtime;
    private string summaryFilePath;
    private string eventsFilePath;
    private string combinedCsvFilePath;

    // Estado temporário da tarefa guiada atualmente em curso.
    private GuidedTaskMetric activeGuidedTask;
    private double activeGuidedTaskStartedRealtime;
    private readonly HashSet<string> activeTaskVisitedRooms = new HashSet<string>();

    // Estado do jogo da memória usado para calcular duração, tentativas e eficiência.
    private bool memoryGameRunning;
    private double memoryGameStartedRealtime;
    private float memoryAttemptDurationTotal;

    // Estado da fase exterior e tempos entre agarrar e entregar alimentos.
    private bool listPickedUp;
    private double listPickedUpRealtime;
    private float acceptedFoodDeliveryTimeTotal;
    private readonly Dictionary<int, double> lastFoodGrabRealtime = new Dictionary<int, double>();

    // Estado de navegação usado nas fases de exploração e tarefas guiadas.
    private string currentPhase1RoomId;
    private double currentPhase1RoomEnteredRealtime;
    private string lastRoomEntryId;
    private double lastRoomEntryRealtime = -10d;

    // Amostragem opcional da distância percorrida pelo jogador.
    private Transform trackedPlayer;
    private Vector3 lastPlayerPosition;
    private bool hasLastPlayerPosition;
    private float nextMovementSampleTime;

    // Estado de interrupções da aplicação, como pausa ou perda de foco.
    private bool interruptionActive;
    private string activeInterruptionReason;
    private double interruptionStartedRealtime;
    private InterruptionMetric activeInterruption;

    // Garante que existe um MetricsManager na cena, criando-o quando necessário.
    public static MetricsManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        MetricsManager existing = FindFirstObjectByType<MetricsManager>(FindObjectsInactive.Include);
        if (existing != null)
            return existing;

        GameObject metricsObject = new GameObject("MetricsManager");
        return metricsObject.AddComponent<MetricsManager>();
    }

    // Configura o singleton persistente entre cenas.
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

    // Amostra periodicamente a distância percorrida durante a exploração livre.
    private void Update()
    {
        if (!sessionActive || !collectDistanceTravelled || activePhase != "Phase1_Exploration")
            return;

        if (Time.unscaledTime < nextMovementSampleTime)
            return;

        nextMovementSampleTime = Time.unscaledTime + movementSampleInterval;
        SamplePlayerDistance();
    }

    // Regista interrupções quando a aplicação é colocada em pausa.
    private void OnApplicationPause(bool paused)
    {
        if (paused)
            BeginInterruption("application_pause");
        else
            EndInterruption();
    }

    // Regista interrupções quando a aplicação perde ou recupera foco.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            BeginInterruption("application_focus_lost");
        else
            EndInterruption();
    }

    // Fecha a sessão como interrompida se a aplicação terminar antes do fim.
    private void OnApplicationQuit()
    {
        if (sessionActive)
            EndSession(false, "application_quit");
    }

    // Remove a subscrição de mudança de cena quando o singleton é destruído.
    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Instance = null;
    }

    // Inicia uma nova sessão anónima e prepara os ficheiros de métricas.
    public void BeginSession(
        string anonymousParticipantId,
        bool enableDistanceTravelled,
        float distanceSampleInterval)
    {
        if (sessionActive)
            return;

        string participantId = SanitizeIdentifier(anonymousParticipantId);
        if (string.IsNullOrWhiteSpace(participantId))
            participantId = "ANON";

        collectDistanceTravelled = enableDistanceTravelled;
        movementSampleInterval = Mathf.Max(0.1f, distanceSampleInterval);
        sessionStartedRealtime = Time.realtimeSinceStartupAsDouble;

        bool hasPreviousSession = PlayerPrefs.HasKey(LastSessionCompletedKey);
        bool previousSessionCompleted = PlayerPrefs.GetInt(LastSessionCompletedKey, 1) == 1;
        string previousSessionId = PlayerPrefs.GetString(LastSessionIdKey, string.Empty);
        string utcStamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        string shortGuid = Guid.NewGuid().ToString("N").Substring(0, 6);
        string sessionId = participantId + "_" + utcStamp + "_" + shortGuid;

        data = new SessionMetricsData
        {
            participantId = participantId,
            sessionId = sessionId,
            experienceStartedUtc = UtcNow(),
            applicationVersion = Application.version,
            unityVersion = Application.unityVersion,
            platform = Application.platform.ToString(),
            deviceModel = SystemInfo.deviceModel,
            restartDetected = hasPreviousSession && !previousSessionCompleted
        };

        string metricsDirectory = Path.Combine(Application.persistentDataPath, "Metrics");
        Directory.CreateDirectory(metricsDirectory);
        summaryFilePath = Path.Combine(metricsDirectory, sessionId + "_summary.json");
        eventsFilePath = Path.Combine(metricsDirectory, sessionId + "_events.jsonl");
        combinedCsvFilePath = Path.Combine(metricsDirectory, "MindBridgeXR_AllMetrics.csv");
        sessionActive = true;

        PlayerPrefs.SetInt(LastSessionCompletedKey, 0);
        PlayerPrefs.SetString(LastSessionIdKey, sessionId);
        PlayerPrefs.Save();

        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        LogEvent(
            "session_started",
            Field("participantId", participantId),
            Field("sessionId", sessionId),
            Field("applicationVersion", Application.version),
            Field("restartDetected", data.restartDetected));

        if (data.restartDetected)
        {
            LogEvent(
                "session_restarted_after_incomplete_session",
                Field("previousSessionId", previousSessionId));
        }

        SaveSummary();
        RebuildCombinedCsv();
    }

    // Começa a fase de exploração livre.
    public void BeginPhase1()
    {
        if (!sessionActive || !string.IsNullOrEmpty(data.phase1.timing.startedUtc))
            return;

        BeginPhase(data.phase1.timing, "Phase1_Exploration");
    }

    // Fecha a exploração livre e consolida visitas às divisões.
    public void CompletePhase1()
    {
        if (!sessionActive || data.phase1.timing.completed)
            return;

        CloseCurrentPhase1RoomTime();
        RefreshPhase1Totals();
        CompletePhase(data.phase1.timing, "Phase1_Exploration");
    }

    // Começa a fase de navegação guiada.
    public void BeginPhase2()
    {
        if (!sessionActive || !string.IsNullOrEmpty(data.phase2.timing.startedUtc))
            return;

        BeginPhase(data.phase2.timing, "Phase2_GuidedNavigation");
    }

    // Regista o início de uma tarefa guiada específica.
    public void BeginGuidedTask(int taskIndex, string targetRoomId)
    {
        if (!sessionActive || activePhase != "Phase2_GuidedNavigation")
            return;

        activeGuidedTask = new GuidedTaskMetric
        {
            taskIndex = taskIndex,
            targetRoomId = targetRoomId,
            startedUtc = UtcNow()
        };

        activeGuidedTaskStartedRealtime = Time.realtimeSinceStartupAsDouble;
        activeTaskVisitedRooms.Clear();
        data.phase2.tasks.Add(activeGuidedTask);

        LogEvent(
            "guided_task_started",
            Field("taskIndex", taskIndex),
            Field("targetRoomId", targetRoomId));
    }

    // Fecha a tarefa guiada ativa e atualiza médias da fase 2.
    public void CompleteGuidedTask(int taskIndex)
    {
        if (!sessionActive || activeGuidedTask == null || activeGuidedTask.completed)
            return;

        activeGuidedTask.completed = true;
        activeGuidedTask.endedUtc = UtcNow();
        activeGuidedTask.durationSeconds = ElapsedSince(activeGuidedTaskStartedRealtime);
        data.phase2.completedTasks++;

        LogEvent(
            "guided_task_completed",
            Field("taskIndex", taskIndex),
            Field("targetRoomId", activeGuidedTask.targetRoomId),
            Field("durationSeconds", activeGuidedTask.durationSeconds),
            Field("sceneChanges", activeGuidedTask.sceneChanges),
            Field("revisitsOrRegressions", activeGuidedTask.revisitsOrRegressions));

        activeGuidedTask = null;
        activeTaskVisitedRooms.Clear();
        RefreshPhase2Totals();
    }

    // Finaliza a fase de navegação guiada.
    public void CompletePhase2()
    {
        if (!sessionActive || data.phase2.timing.completed)
            return;

        RefreshPhase2Totals();
        CompletePhase(data.phase2.timing, "Phase2_GuidedNavigation");
    }

    // Começa a fase da sala de jantar e jogo da memória.
    public void BeginPhase3()
    {
        if (!sessionActive || !string.IsNullOrEmpty(data.phase3.timing.startedUtc))
            return;

        phase3SceneChanges = 0;
        BeginPhase(data.phase3.timing, "Phase3_DiningMemory");
    }

    // Marca o tempo até o jogador chegar à sala de jantar.
    public void RecordDiningRoomReached()
    {
        if (!sessionActive || activePhase != "Phase3_DiningMemory" || data.phase3.timeToDiningRoomSeconds >= 0f)
            return;

        data.phase3.timeToDiningRoomSeconds = ActivePhaseElapsed();
        LogEvent("dining_room_reached", Field("phaseElapsedSeconds", data.phase3.timeToDiningRoomSeconds));
    }

    // Marca o tempo até o jogador alcançar a mesa da atividade.
    public void RecordDiningTableReached()
    {
        if (!sessionActive || activePhase != "Phase3_DiningMemory" || data.phase3.timeToTableSeconds >= 0f)
            return;

        data.phase3.timeToTableSeconds = ActivePhaseElapsed();
        LogEvent("dining_table_reached", Field("phaseElapsedSeconds", data.phase3.timeToTableSeconds));
    }

    // Inicia a medição do jogo da memória.
    public void BeginMemoryGame(int theoreticalMinimumAttempts)
    {
        if (!sessionActive || activePhase != "Phase3_DiningMemory" || memoryGameRunning)
            return;

        memoryGameRunning = true;
        memoryGameStartedRealtime = Time.realtimeSinceStartupAsDouble;
        memoryAttemptDurationTotal = 0f;
        data.phase3.theoreticalMinimumAttempts = Mathf.Max(0, theoreticalMinimumAttempts);
        data.phase3.sceneChangesUntilGame = data.phase3.timing.completed
            ? data.phase3.sceneChangesUntilGame
            : CountSceneChangesForPhase3();

        LogEvent(
            "memory_game_started",
            Field("theoreticalMinimumAttempts", data.phase3.theoreticalMinimumAttempts),
            Field("sceneChangesUntilGame", data.phase3.sceneChangesUntilGame));
    }

    // Conta seleções por carta para perceber padrões de tentativa.
    public void RecordMemoryCardSelected(string cardId)
    {
        if (!sessionActive || !memoryGameRunning)
            return;

        CardSelectionMetric cardMetric = FindCardSelection(cardId);
        cardMetric.selectionCount++;

        LogEvent(
            "memory_card_selected",
            Field("cardId", cardId),
            Field("selectionCount", cardMetric.selectionCount));
    }

    // Regista uma tentativa do jogo da memória e atualiza precisão/eficiência.
    public void RecordMemoryAttempt(bool correct, float durationSeconds, string matchedPairId)
    {
        if (!sessionActive || !memoryGameRunning)
            return;

        data.phase3.totalAttempts++;
        if (correct)
            data.phase3.correctPairs++;
        else
            data.phase3.incorrectPairs++;

        memoryAttemptDurationTotal += Mathf.Max(0f, durationSeconds);
        float gameElapsed = ElapsedSince(memoryGameStartedRealtime);

        data.phase3.attempts.Add(new MemoryAttemptMetric
        {
            attemptIndex = data.phase3.totalAttempts,
            durationSeconds = Mathf.Max(0f, durationSeconds),
            correct = correct,
            gameElapsedSeconds = gameElapsed
        });

        if (correct)
        {
            data.phase3.pairsFound.Add(new PairFoundMetric
            {
                pairId = matchedPairId,
                phaseElapsedSeconds = ActivePhaseElapsed(),
                gameElapsedSeconds = gameElapsed
            });
        }

        RefreshPhase3Totals();

        LogEvent(
            "memory_attempt_completed",
            Field("attemptIndex", data.phase3.totalAttempts),
            Field("correct", correct),
            Field("durationSeconds", durationSeconds),
            Field("matchedPairId", correct ? matchedPairId : string.Empty),
            Field("gameElapsedSeconds", gameElapsed));
    }

    // Fecha a medição do jogo da memória.
    public void CompleteMemoryGame()
    {
        if (!sessionActive || !memoryGameRunning)
            return;

        memoryGameRunning = false;
        data.phase3.memoryGameDurationSeconds = ElapsedSince(memoryGameStartedRealtime);
        RefreshPhase3Totals();

        LogEvent(
            "memory_game_completed",
            Field("durationSeconds", data.phase3.memoryGameDurationSeconds),
            Field("attempts", data.phase3.totalAttempts),
            Field("correctPairs", data.phase3.correctPairs),
            Field("incorrectPairs", data.phase3.incorrectPairs),
            Field("accuracy", data.phase3.accuracy),
            Field("efficiency", data.phase3.efficiency));
    }

    // Finaliza a fase da sala de jantar.
    public void CompletePhase3()
    {
        if (!sessionActive || data.phase3.timing.completed)
            return;

        RefreshPhase3Totals();
        CompletePhase(data.phase3.timing, "Phase3_DiningMemory");
    }

    // Começa a fase exterior de recolha de alimentos.
    public void BeginPhase4()
    {
        if (!sessionActive || !string.IsNullOrEmpty(data.phase4.timing.startedUtc))
            return;

        listPickedUp = false;
        acceptedFoodDeliveryTimeTotal = 0f;
        lastFoodGrabRealtime.Clear();
        BeginPhase(data.phase4.timing, "Phase4_OutdoorFood");
    }

    // Marca o momento em que o participante recolhe a lista de alimentos.
    public void RecordFoodListPickedUp()
    {
        if (!sessionActive || activePhase != "Phase4_OutdoorFood" || listPickedUp)
            return;

        listPickedUp = true;
        listPickedUpRealtime = Time.realtimeSinceStartupAsDouble;
        data.phase4.timeToListPickupSeconds = ActivePhaseElapsed();
        data.phase4.sceneChangesUntilListPickup = data.phase4.totalSceneChanges;

        LogEvent(
            "food_list_picked_up",
            Field("phaseElapsedSeconds", data.phase4.timeToListPickupSeconds),
            Field("sceneChanges", data.phase4.sceneChangesUntilListPickup),
            Field("foodGrabsBeforeList", data.phase4.foodGrabsBeforeListPickup),
            Field("deliveryAttemptsBeforeList", data.phase4.deliveryAttemptsBeforeListPickup));
    }

    // Regista que um alimento foi agarrado.
    public void RecordFoodGrab(FoodCollectible food)
    {
        if (!sessionActive || activePhase != "Phase4_OutdoorFood" || food == null)
            return;

        data.phase4.totalFoodGrabs++;
        if (!listPickedUp)
            data.phase4.foodGrabsBeforeListPickup++;

        lastFoodGrabRealtime[food.GetInstanceID()] = Time.realtimeSinceStartupAsDouble;
        FoodAggregateMetric foodMetric = FindFoodMetric(food);
        foodMetric.grabCount++;
        RefreshPhase4Totals();

        LogEvent(
            "food_grabbed",
            Field("foodId", food.MetricsId),
            Field("foodType", food.FoodType),
            Field("beforeListPickup", !listPickedUp),
            Field("grabCount", foodMetric.grabCount));
    }

    // Regista que um alimento foi largado e se terminou entregue.
    public void RecordFoodReleased(FoodCollectible food, bool delivered)
    {
        if (!sessionActive || activePhase != "Phase4_OutdoorFood" || food == null)
            return;

        FoodAggregateMetric foodMetric = FindFoodMetric(food);

        if (!delivered)
        {
            data.phase4.releasesWithoutDelivery++;
            foodMetric.releaseWithoutDeliveryCount++;
        }

        LogEvent(
            "food_released",
            Field("foodId", food.MetricsId),
            Field("foodType", food.FoodType),
            Field("delivered", delivered));

        RefreshPhase4Totals();
    }

    // Regista uma tentativa de entrega aceite ou rejeitada.
    public void RecordFoodDeliveryAttempt(FoodCollectible food, string result, string reason)
    {
        if (!sessionActive || activePhase != "Phase4_OutdoorFood" || food == null)
            return;

        bool beforeList = !listPickedUp;
        data.phase4.totalDeliveryAttempts++;
        if (beforeList)
            data.phase4.deliveryAttemptsBeforeListPickup++;

        bool accepted = string.Equals(result, "accepted", StringComparison.OrdinalIgnoreCase);
        int acceptedOrder = 0;
        float secondsSinceGrab = -1f;

        if (lastFoodGrabRealtime.TryGetValue(food.GetInstanceID(), out double grabbedAt))
            secondsSinceGrab = ElapsedSince(grabbedAt);

        FoodAggregateMetric foodMetric = FindFoodMetric(food);
        if (accepted)
        {
            data.phase4.acceptedDeliveries++;
            foodMetric.acceptedDeliveries++;
            acceptedOrder = data.phase4.acceptedDeliveries;

            if (secondsSinceGrab >= 0f)
                acceptedFoodDeliveryTimeTotal += secondsSinceGrab;
        }
        else
        {
            data.phase4.rejectedDeliveries++;
            foodMetric.rejectedDeliveries++;
        }

        FoodDeliveryAttemptMetric attempt = new FoodDeliveryAttemptMetric
        {
            attemptOrder = data.phase4.totalDeliveryAttempts,
            acceptedOrder = acceptedOrder,
            foodId = food.MetricsId,
            foodType = food.FoodType.ToString(),
            result = result,
            reason = reason,
            beforeListPickup = beforeList,
            phaseElapsedSeconds = ActivePhaseElapsed(),
            secondsSinceLastGrab = secondsSinceGrab
        };

        data.phase4.deliveryAttempts.Add(attempt);
        RefreshPhase4Totals();

        LogEvent(
            "food_delivery_attempt",
            Field("attemptOrder", attempt.attemptOrder),
            Field("acceptedOrder", attempt.acceptedOrder),
            Field("foodId", attempt.foodId),
            Field("foodType", attempt.foodType),
            Field("result", result),
            Field("reason", reason),
            Field("beforeListPickup", beforeList),
            Field("secondsSinceLastGrab", secondsSinceGrab));
    }

    // Finaliza a fase exterior e calcula duração de recolha/entrega.
    public void CompletePhase4()
    {
        if (!sessionActive || data.phase4.timing.completed)
            return;

        if (listPickedUp)
            data.phase4.collectionAndDeliveryDurationSeconds = ElapsedSince(listPickedUpRealtime);

        RefreshPhase4Totals();
        CompletePhase(data.phase4.timing, "Phase4_OutdoorFood");
    }

    // Encerra a experiência completa como concluída.
    public void CompleteExperience()
    {
        if (!sessionActive)
            return;

        EndSession(true, "experience_completed");
    }

    // Regista entrada numa divisão e encaminha para a fase ativa.
    public void RecordRoomEntered(string roomId)
    {
        if (!sessionActive || string.IsNullOrWhiteSpace(roomId))
            return;

        double now = Time.realtimeSinceStartupAsDouble;
        if (string.Equals(lastRoomEntryId, roomId, StringComparison.Ordinal) &&
            now - lastRoomEntryRealtime < 0.75d)
        {
            return;
        }

        lastRoomEntryId = roomId;
        lastRoomEntryRealtime = now;

        if (activePhase == "Phase1_Exploration")
            RecordPhase1RoomEntered(roomId, now);
        else if (activePhase == "Phase2_GuidedNavigation")
            RecordPhase2RoomEntered(roomId);

        LogEvent("room_entered", Field("roomId", roomId));
    }

    // Atualiza sequência, revisitas e tempo de descoberta na exploração livre.
    private void RecordPhase1RoomEntered(string roomId, double now)
    {
        CloseCurrentPhase1RoomTime();

        RoomAggregateMetric roomMetric = FindRoomMetric(roomId);
        bool firstVisit = roomMetric.visitCount == 0;
        roomMetric.visitCount++;

        if (firstVisit)
        {
            roomMetric.firstDiscoverySeconds = ActivePhaseElapsed();
            data.phase1.uniqueRoomsVisited++;

            if (data.phase1.timeToFirstRoomSeconds < 0f)
                data.phase1.timeToFirstRoomSeconds = roomMetric.firstDiscoverySeconds;
        }
        else
        {
            roomMetric.revisitCount++;
            data.phase1.totalRevisits++;
        }

        data.phase1.totalRoomEntries++;
        data.phase1.visitSequence.Add(new RoomVisitMetric
        {
            order = data.phase1.totalRoomEntries,
            roomId = roomId,
            sceneName = SceneManager.GetActiveScene().name,
            phaseElapsedSeconds = ActivePhaseElapsed(),
            firstVisit = firstVisit
        });

        currentPhase1RoomId = roomId;
        currentPhase1RoomEnteredRealtime = now;
        RefreshPhase1Totals();
    }

    // Atualiza percurso e regressões dentro da tarefa guiada ativa.
    private void RecordPhase2RoomEntered(string roomId)
    {
        if (activeGuidedTask == null)
            return;

        activeGuidedTask.roomSequence.Add(roomId);
        if (!activeTaskVisitedRooms.Add(roomId))
            activeGuidedTask.revisitsOrRegressions++;

        RefreshPhase2Totals();
    }

    // Preenche dados comuns ao início de qualquer fase.
    private void BeginPhase(PhaseTimingMetric timing, string phaseName)
    {
        activePhase = phaseName;
        activePhaseStartedRealtime = Time.realtimeSinceStartupAsDouble;
        timing.phaseName = phaseName;
        timing.startedUtc = UtcNow();
        timing.completed = false;

        ResetMovementTracking();
        LogEvent("phase_started", Field("phaseName", phaseName));
    }

    // Preenche dados comuns ao fim de qualquer fase e exporta resultados.
    private void CompletePhase(PhaseTimingMetric timing, string phaseName)
    {
        timing.endedUtc = UtcNow();
        timing.durationSeconds = ElapsedSince(activePhaseStartedRealtime);
        timing.completed = true;
        data.completedPhaseCount++;
        data.completedPhases.Add(phaseName);

        LogEvent(
            "phase_completed",
            Field("phaseName", phaseName),
            Field("durationSeconds", timing.durationSeconds));

        activePhase = string.Empty;
        activeGuidedTask = null;
        hasLastPlayerPosition = false;
        SaveSummary();
        RebuildCombinedCsv();
    }

    // Fecha a sessão, grava o motivo de fim e atualiza os ficheiros finais.
    private void EndSession(bool completed, string reason)
    {
        EndInterruption();

        data.experienceCompleted = completed;
        data.interrupted = !completed;
        data.endReason = reason;
        data.experienceEndedUtc = UtcNow();
        data.durationSeconds = ElapsedSince(sessionStartedRealtime);

        LogEvent(
            completed ? "session_completed" : "session_interrupted",
            Field("reason", reason),
            Field("durationSeconds", data.durationSeconds));

        sessionActive = false;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        PlayerPrefs.SetInt(LastSessionCompletedKey, completed ? 1 : 0);
        PlayerPrefs.Save();
        SaveSummary();
        RebuildCombinedCsv();
    }

    // Abre um intervalo de interrupção quando a experiência é suspensa.
    private void BeginInterruption(string reason)
    {
        if (!sessionActive || interruptionActive)
            return;

        interruptionActive = true;
        activeInterruptionReason = reason;
        interruptionStartedRealtime = Time.realtimeSinceStartupAsDouble;
        activeInterruption = new InterruptionMetric
        {
            reason = reason,
            phase = activePhase,
            startedUtc = UtcNow()
        };

        data.interruptionCount++;
        data.interruptions.Add(activeInterruption);
        LogEvent("interruption_started", Field("reason", reason));
    }

    // Fecha a interrupção ativa e acumula a sua duração.
    private void EndInterruption()
    {
        if (!sessionActive || !interruptionActive)
            return;

        activeInterruption.endedUtc = UtcNow();
        activeInterruption.durationSeconds = ElapsedSince(interruptionStartedRealtime);
        data.totalInterruptionDurationSeconds += activeInterruption.durationSeconds;

        LogEvent(
            "interruption_ended",
            Field("reason", activeInterruptionReason),
            Field("durationSeconds", activeInterruption.durationSeconds));

        interruptionActive = false;
        activeInterruptionReason = null;
        activeInterruption = null;
    }

    // Conta mudanças de cena e atribui-as à fase que estava ativa.
    private void OnActiveSceneChanged(Scene previousScene, Scene nextScene)
    {
        if (!sessionActive)
            return;

        data.totalSceneChanges++;

        switch (activePhase)
        {
            case "Phase1_Exploration":
                CloseCurrentPhase1RoomTime();
                data.phase1.sceneChanges++;
                break;

            case "Phase2_GuidedNavigation":
                data.phase2.totalSceneChanges++;
                if (activeGuidedTask != null)
                    activeGuidedTask.sceneChanges++;
                break;

            case "Phase3_DiningMemory":
                IncrementPhase3SceneChanges();
                break;

            case "Phase4_OutdoorFood":
                data.phase4.totalSceneChanges++;
                break;
        }

        trackedPlayer = null;
        hasLastPlayerPosition = false;
        lastRoomEntryId = null;

        LogEvent(
            "scene_changed",
            Field("fromScene", previousScene.name),
            Field("toScene", nextScene.name));
    }

    // Contador dedicado às mudanças de cena antes do jogo da memória.
    private int phase3SceneChanges;

    // Incrementa o contador específico da fase 3.
    private void IncrementPhase3SceneChanges()
    {
        phase3SceneChanges++;
    }

    // Devolve quantas mudanças ocorreram antes do início do jogo.
    private int CountSceneChangesForPhase3()
    {
        return phase3SceneChanges;
    }

    // Soma deslocações entre amostras sucessivas do objeto Player.
    private void SamplePlayerDistance()
    {
        if (trackedPlayer == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
                return;

            trackedPlayer = playerObject.transform;
            hasLastPlayerPosition = false;
        }

        Vector3 currentPosition = trackedPlayer.position;
        if (hasLastPlayerPosition)
            data.phase1.distanceTravelledMeters += Vector3.Distance(lastPlayerPosition, currentPosition);

        lastPlayerPosition = currentPosition;
        hasLastPlayerPosition = true;
    }

    // Reinicia a amostragem de movimento ao mudar de fase ou cena.
    private void ResetMovementTracking()
    {
        trackedPlayer = null;
        hasLastPlayerPosition = false;
        nextMovementSampleTime = Time.unscaledTime;
    }

    // Fecha o tempo passado na divisão atualmente ativa na fase 1.
    private void CloseCurrentPhase1RoomTime()
    {
        if (string.IsNullOrWhiteSpace(currentPhase1RoomId))
            return;

        RoomAggregateMetric roomMetric = FindRoomMetric(currentPhase1RoomId);
        roomMetric.timeSpentSeconds += ElapsedSince(currentPhase1RoomEnteredRealtime);
        currentPhase1RoomId = null;
    }

    // Obtém ou cria o acumulador de métricas de uma divisão.
    private RoomAggregateMetric FindRoomMetric(string roomId)
    {
        foreach (RoomAggregateMetric room in data.phase1.rooms)
        {
            if (room.roomId == roomId)
                return room;
        }

        RoomAggregateMetric created = new RoomAggregateMetric { roomId = roomId };
        data.phase1.rooms.Add(created);
        return created;
    }

    // Obtém ou cria o contador de seleções de uma carta.
    private CardSelectionMetric FindCardSelection(string cardId)
    {
        foreach (CardSelectionMetric card in data.phase3.cardSelections)
        {
            if (card.cardId == cardId)
                return card;
        }

        CardSelectionMetric created = new CardSelectionMetric { cardId = cardId };
        data.phase3.cardSelections.Add(created);
        return created;
    }

    // Obtém ou cria o acumulador de métricas de um alimento.
    private FoodAggregateMetric FindFoodMetric(FoodCollectible food)
    {
        string foodId = food.MetricsId;
        foreach (FoodAggregateMetric item in data.phase4.foods)
        {
            if (item.foodId == foodId)
                return item;
        }

        FoodAggregateMetric created = new FoodAggregateMetric
        {
            foodId = foodId,
            foodType = food.FoodType.ToString()
        };

        data.phase4.foods.Add(created);
        return created;
    }

    // Recalcula totais derivados da exploração livre.
    private void RefreshPhase1Totals()
    {
        data.phase1.uniqueRoomsVisited = 0;
        data.phase1.totalRevisits = 0;

        foreach (RoomAggregateMetric room in data.phase1.rooms)
        {
            if (room.visitCount > 0)
                data.phase1.uniqueRoomsVisited++;

            data.phase1.totalRevisits += room.revisitCount;
        }
    }

    // Recalcula médias, tarefa mais rápida/lenta e regressões da fase 2.
    private void RefreshPhase2Totals()
    {
        float totalTime = 0f;
        float fastest = float.MaxValue;
        float slowest = 0f;
        int completed = 0;
        int revisits = 0;

        foreach (GuidedTaskMetric task in data.phase2.tasks)
        {
            revisits += task.revisitsOrRegressions;
            if (!task.completed)
                continue;

            completed++;
            totalTime += task.durationSeconds;

            if (task.durationSeconds < fastest)
            {
                fastest = task.durationSeconds;
                data.phase2.fastestTaskIndex = task.taskIndex;
            }

            if (task.durationSeconds > slowest)
            {
                slowest = task.durationSeconds;
                data.phase2.slowestTaskIndex = task.taskIndex;
            }
        }

        data.phase2.completedTasks = completed;
        data.phase2.averageTaskTimeSeconds = completed > 0 ? totalTime / completed : 0f;
        data.phase2.fastestTaskTimeSeconds = completed > 0 ? fastest : 0f;
        data.phase2.slowestTaskTimeSeconds = completed > 0 ? slowest : 0f;
        data.phase2.totalRevisitsOrRegressions = revisits;
    }

    // Recalcula precisão, tempo médio e eficiência do jogo da memória.
    private void RefreshPhase3Totals()
    {
        data.phase3.accuracy = data.phase3.totalAttempts > 0
            ? (float)data.phase3.correctPairs / data.phase3.totalAttempts
            : 0f;

        data.phase3.averageAttemptTimeSeconds = data.phase3.totalAttempts > 0
            ? memoryAttemptDurationTotal / data.phase3.totalAttempts
            : 0f;

        data.phase3.efficiency = data.phase3.totalAttempts > 0
            ? Mathf.Clamp01((float)data.phase3.theoreticalMinimumAttempts / data.phase3.totalAttempts)
            : 0f;
    }

    // Recalcula precisão de entregas e manipulações desnecessárias.
    private void RefreshPhase4Totals()
    {
        data.phase4.deliveryAccuracy = data.phase4.totalDeliveryAttempts > 0
            ? (float)data.phase4.acceptedDeliveries / data.phase4.totalDeliveryAttempts
            : 0f;

        data.phase4.averageSecondsPerAcceptedFood = data.phase4.acceptedDeliveries > 0
            ? acceptedFoodDeliveryTimeTotal / data.phase4.acceptedDeliveries
            : 0f;

        data.phase4.unnecessaryManipulations = Mathf.Max(
            0,
            data.phase4.totalFoodGrabs - data.phase4.acceptedDeliveries);
    }

    // Acrescenta um evento cronológico ao ficheiro JSONL e atualiza o resumo.
    private void LogEvent(string eventType, params MetricEventField[] fields)
    {
        if (!sessionActive || data == null)
            return;

        MetricEventRecord record = new MetricEventRecord
        {
            utcTimestamp = UtcNow(),
            sessionElapsedSeconds = ElapsedSince(sessionStartedRealtime),
            eventType = eventType,
            phase = activePhase,
            sceneName = SceneManager.GetActiveScene().name
        };

        if (fields != null)
            record.data.AddRange(fields);

        try
        {
            File.AppendAllText(eventsFilePath, JsonUtility.ToJson(record) + Environment.NewLine);
        }
        catch (Exception)
        {
            // A falha de escrita de eventos não deve interromper a experiência em VR.
        }

        SaveSummary();
    }

    // Grava o resumo JSON mais recente da sessão.
    private void SaveSummary()
    {
        if (data == null || string.IsNullOrWhiteSpace(summaryFilePath))
            return;

        if (sessionActive)
            data.durationSeconds = ElapsedSince(sessionStartedRealtime);

        try
        {
            File.WriteAllText(summaryFilePath, JsonUtility.ToJson(data, true));
        }
        catch (Exception)
        {
            // A falha de escrita do resumo não deve interromper a sessão.
        }
    }

    // Reconstrói o CSV combinado a partir de todos os resumos disponíveis.
    public void RebuildCombinedCsv()
    {
        if (string.IsNullOrWhiteSpace(summaryFilePath))
            return;

        string metricsDirectory = Path.GetDirectoryName(summaryFilePath);
        if (string.IsNullOrWhiteSpace(metricsDirectory) || !Directory.Exists(metricsDirectory))
            return;

        if (string.IsNullOrWhiteSpace(combinedCsvFilePath))
            combinedCsvFilePath = Path.Combine(metricsDirectory, "MindBridgeXR_AllMetrics.csv");

        try
        {
            string[] summaryFiles = Directory.GetFiles(metricsDirectory, "*_summary.json");
            Array.Sort(summaryFiles, StringComparer.OrdinalIgnoreCase);

            StringBuilder csv = new StringBuilder();
            AppendCsvHeader(csv);

            foreach (string filePath in summaryFiles)
            {
                string json = File.ReadAllText(filePath);
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                SessionMetricsData session = JsonUtility.FromJson<SessionMetricsData>(json);
                if (session == null || string.IsNullOrWhiteSpace(session.sessionId))
                    continue;

                AppendSessionToCsv(csv, session);
                AppendRawEventsToCsv(csv, session, metricsDirectory);
            }

            File.WriteAllText(combinedCsvFilePath, csv.ToString(), Encoding.UTF8);
            MetricsReportExporter.RebuildAll(metricsDirectory);
        }
        catch (Exception)
        {
            // A exportação agregada é complementar e não deve bloquear o fluxo principal.
        }
    }

    // Escreve o cabeçalho padrão do CSV combinado.
    private static void AppendCsvHeader(StringBuilder csv)
    {
        csv.AppendLine(
            "participant_id;session_id;session_started_utc;session_ended_utc;" +
            "application_version;experience_completed;record_group;phase;item_type;" +
            "item_id;item_index;recorded_utc;scene_name;metric_name;numeric_value;" +
            "text_value;unit");
    }

    // Adiciona ao CSV todos os grupos de métricas de uma sessão.
    private static void AppendSessionToCsv(StringBuilder csv, SessionMetricsData session)
    {
        AddNumericCsvMetric(csv, session, "session", string.Empty, "session", session.sessionId, 0,
            string.Empty, string.Empty, "duration", session.durationSeconds, "seconds");
        AddNumericCsvMetric(csv, session, "session", string.Empty, "session", session.sessionId, 0,
            string.Empty, string.Empty, "total_scene_changes", session.totalSceneChanges, "count");
        AddNumericCsvMetric(csv, session, "session", string.Empty, "session", session.sessionId, 0,
            string.Empty, string.Empty, "completed_phase_count", session.completedPhaseCount, "count");
        AddNumericCsvMetric(csv, session, "session", string.Empty, "session", session.sessionId, 0,
            string.Empty, string.Empty, "interruption_count", session.interruptionCount, "count");
        AddNumericCsvMetric(csv, session, "session", string.Empty, "session", session.sessionId, 0,
            string.Empty, string.Empty, "total_interruption_duration",
            session.totalInterruptionDurationSeconds, "seconds");
        AddTextCsvMetric(csv, session, "session", string.Empty, "session", session.sessionId, 0,
            string.Empty, string.Empty, "end_reason", session.endReason);
        AddTextCsvMetric(csv, session, "session", string.Empty, "session", session.sessionId, 0,
            string.Empty, string.Empty, "restart_detected", BoolText(session.restartDetected));
        AddTextCsvMetric(csv, session, "session", string.Empty, "session", session.sessionId, 0,
            string.Empty, string.Empty, "completed_phases", JoinStrings(session.completedPhases, ">"));
        AddTextCsvMetric(csv, session, "session", string.Empty, "session", session.sessionId, 0,
            string.Empty, string.Empty, "platform", session.platform);
        AddTextCsvMetric(csv, session, "session", string.Empty, "session", session.sessionId, 0,
            string.Empty, string.Empty, "device_model", session.deviceModel);
        AddTextCsvMetric(csv, session, "session", string.Empty, "session", session.sessionId, 0,
            string.Empty, string.Empty, "unity_version", session.unityVersion);

        AppendInterruptions(csv, session);
        AppendPhaseTiming(csv, session, session.phase1.timing, "phase1");
        AppendPhase1(csv, session);
        AppendPhaseTiming(csv, session, session.phase2.timing, "phase2");
        AppendPhase2(csv, session);
        AppendPhaseTiming(csv, session, session.phase3.timing, "phase3");
        AppendPhase3(csv, session);
        AppendPhaseTiming(csv, session, session.phase4.timing, "phase4");
        AppendPhase4(csv, session);
    }

    // Exporta interrupções registadas durante a sessão.
    private static void AppendInterruptions(StringBuilder csv, SessionMetricsData session)
    {
        if (session.interruptions == null)
            return;

        for (int i = 0; i < session.interruptions.Count; i++)
        {
            InterruptionMetric interruption = session.interruptions[i];
            if (interruption == null)
                continue;

            AddNumericCsvMetric(csv, session, "interruption", interruption.phase, "interruption",
                interruption.reason, i + 1, interruption.startedUtc, string.Empty,
                "duration", interruption.durationSeconds, "seconds");
            AddTextCsvMetric(csv, session, "interruption", interruption.phase, "interruption",
                interruption.reason, i + 1, interruption.endedUtc, string.Empty,
                "ended_utc", interruption.endedUtc);
        }
    }

    // Exporta tempos comuns de início, fim, duração e conclusão de fase.
    private static void AppendPhaseTiming(
        StringBuilder csv,
        SessionMetricsData session,
        PhaseTimingMetric timing,
        string phase)
    {
        if (timing == null || string.IsNullOrWhiteSpace(timing.startedUtc))
            return;

        AddNumericCsvMetric(csv, session, "phase_timing", phase, "phase", phase, 0,
            timing.endedUtc, string.Empty, "duration", timing.durationSeconds, "seconds");
        AddTextCsvMetric(csv, session, "phase_timing", phase, "phase", phase, 0,
            timing.startedUtc, string.Empty, "started_utc", timing.startedUtc);
        AddTextCsvMetric(csv, session, "phase_timing", phase, "phase", phase, 0,
            timing.endedUtc, string.Empty, "ended_utc", timing.endedUtc);
        AddTextCsvMetric(csv, session, "phase_timing", phase, "phase", phase, 0,
            timing.endedUtc, string.Empty, "completed", BoolText(timing.completed));
    }

    // Exporta métricas específicas da fase de exploração livre.
    private static void AppendPhase1(StringBuilder csv, SessionMetricsData session)
    {
        Phase1MetricsData phase = session.phase1;
        if (phase == null)
            return;

        AddNumericCsvMetric(csv, session, "phase_summary", "phase1", "phase", "phase1", 0,
            string.Empty, string.Empty, "time_to_first_room", phase.timeToFirstRoomSeconds, "seconds");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase1", "phase", "phase1", 0,
            string.Empty, string.Empty, "unique_rooms_visited", phase.uniqueRoomsVisited, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase1", "phase", "phase1", 0,
            string.Empty, string.Empty, "total_room_entries", phase.totalRoomEntries, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase1", "phase", "phase1", 0,
            string.Empty, string.Empty, "total_revisits", phase.totalRevisits, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase1", "phase", "phase1", 0,
            string.Empty, string.Empty, "scene_changes", phase.sceneChanges, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase1", "phase", "phase1", 0,
            string.Empty, string.Empty, "distance_travelled", phase.distanceTravelledMeters, "meters");

        if (phase.rooms != null)
        {
            foreach (RoomAggregateMetric room in phase.rooms)
            {
                if (room == null)
                    continue;

                AddNumericCsvMetric(csv, session, "phase1_room", "phase1", "room", room.roomId, 0,
                    string.Empty, string.Empty, "first_discovery", room.firstDiscoverySeconds, "seconds");
                AddNumericCsvMetric(csv, session, "phase1_room", "phase1", "room", room.roomId, 0,
                    string.Empty, string.Empty, "visit_count", room.visitCount, "count");
                AddNumericCsvMetric(csv, session, "phase1_room", "phase1", "room", room.roomId, 0,
                    string.Empty, string.Empty, "revisit_count", room.revisitCount, "count");
                AddNumericCsvMetric(csv, session, "phase1_room", "phase1", "room", room.roomId, 0,
                    string.Empty, string.Empty, "time_spent", room.timeSpentSeconds, "seconds");
            }
        }

        if (phase.visitSequence == null)
            return;

        foreach (RoomVisitMetric visit in phase.visitSequence)
        {
            if (visit == null)
                continue;

            AddNumericCsvMetric(csv, session, "phase1_visit_sequence", "phase1", "room",
                visit.roomId, visit.order, string.Empty, visit.sceneName,
                "phase_elapsed", visit.phaseElapsedSeconds, "seconds");
            AddTextCsvMetric(csv, session, "phase1_visit_sequence", "phase1", "room",
                visit.roomId, visit.order, string.Empty, visit.sceneName,
                "first_visit", BoolText(visit.firstVisit));
        }
    }

    // Exporta métricas específicas das tarefas guiadas.
    private static void AppendPhase2(StringBuilder csv, SessionMetricsData session)
    {
        Phase2MetricsData phase = session.phase2;
        if (phase == null)
            return;

        AddNumericCsvMetric(csv, session, "phase_summary", "phase2", "phase", "phase2", 0,
            string.Empty, string.Empty, "completed_tasks", phase.completedTasks, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase2", "phase", "phase2", 0,
            string.Empty, string.Empty, "average_task_time", phase.averageTaskTimeSeconds, "seconds");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase2", "phase", "phase2", 0,
            string.Empty, string.Empty, "fastest_task_time", phase.fastestTaskTimeSeconds, "seconds");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase2", "phase", "phase2", 0,
            string.Empty, string.Empty, "fastest_task_index", phase.fastestTaskIndex, "index");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase2", "phase", "phase2", 0,
            string.Empty, string.Empty, "slowest_task_time", phase.slowestTaskTimeSeconds, "seconds");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase2", "phase", "phase2", 0,
            string.Empty, string.Empty, "slowest_task_index", phase.slowestTaskIndex, "index");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase2", "phase", "phase2", 0,
            string.Empty, string.Empty, "total_scene_changes", phase.totalSceneChanges, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase2", "phase", "phase2", 0,
            string.Empty, string.Empty, "total_revisits_or_regressions",
            phase.totalRevisitsOrRegressions, "count");

        if (phase.tasks == null)
            return;

        foreach (GuidedTaskMetric task in phase.tasks)
        {
            if (task == null)
                continue;

            AddNumericCsvMetric(csv, session, "phase2_task", "phase2", "task",
                task.targetRoomId, task.taskIndex, task.endedUtc, string.Empty,
                "duration", task.durationSeconds, "seconds");
            AddNumericCsvMetric(csv, session, "phase2_task", "phase2", "task",
                task.targetRoomId, task.taskIndex, task.endedUtc, string.Empty,
                "scene_changes", task.sceneChanges, "count");
            AddNumericCsvMetric(csv, session, "phase2_task", "phase2", "task",
                task.targetRoomId, task.taskIndex, task.endedUtc, string.Empty,
                "revisits_or_regressions", task.revisitsOrRegressions, "count");
            AddTextCsvMetric(csv, session, "phase2_task", "phase2", "task",
                task.targetRoomId, task.taskIndex, task.endedUtc, string.Empty,
                "completed", BoolText(task.completed));
            AddTextCsvMetric(csv, session, "phase2_task", "phase2", "task",
                task.targetRoomId, task.taskIndex, task.endedUtc, string.Empty,
                "room_sequence", JoinStrings(task.roomSequence, ">"));
        }
    }

    // Exporta métricas específicas do jogo da memória.
    private static void AppendPhase3(StringBuilder csv, SessionMetricsData session)
    {
        Phase3MetricsData phase = session.phase3;
        if (phase == null)
            return;

        AddNumericCsvMetric(csv, session, "phase_summary", "phase3", "phase", "phase3", 0,
            string.Empty, string.Empty, "time_to_dining_room", phase.timeToDiningRoomSeconds, "seconds");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase3", "phase", "phase3", 0,
            string.Empty, string.Empty, "time_to_table", phase.timeToTableSeconds, "seconds");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase3", "phase", "phase3", 0,
            string.Empty, string.Empty, "scene_changes_until_game", phase.sceneChangesUntilGame, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase3", "phase", "phase3", 0,
            string.Empty, string.Empty, "memory_game_duration", phase.memoryGameDurationSeconds, "seconds");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase3", "phase", "phase3", 0,
            string.Empty, string.Empty, "total_attempts", phase.totalAttempts, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase3", "phase", "phase3", 0,
            string.Empty, string.Empty, "correct_pairs", phase.correctPairs, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase3", "phase", "phase3", 0,
            string.Empty, string.Empty, "incorrect_pairs", phase.incorrectPairs, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase3", "phase", "phase3", 0,
            string.Empty, string.Empty, "accuracy", phase.accuracy, "ratio");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase3", "phase", "phase3", 0,
            string.Empty, string.Empty, "average_attempt_time", phase.averageAttemptTimeSeconds, "seconds");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase3", "phase", "phase3", 0,
            string.Empty, string.Empty, "theoretical_minimum_attempts",
            phase.theoreticalMinimumAttempts, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase3", "phase", "phase3", 0,
            string.Empty, string.Empty, "efficiency", phase.efficiency, "ratio");

        if (phase.attempts != null)
        {
            foreach (MemoryAttemptMetric attempt in phase.attempts)
            {
                if (attempt == null)
                    continue;

                AddNumericCsvMetric(csv, session, "phase3_attempt", "phase3", "attempt",
                    attempt.attemptIndex.ToString(CultureInfo.InvariantCulture), attempt.attemptIndex,
                    string.Empty, string.Empty, "duration", attempt.durationSeconds, "seconds");
                AddNumericCsvMetric(csv, session, "phase3_attempt", "phase3", "attempt",
                    attempt.attemptIndex.ToString(CultureInfo.InvariantCulture), attempt.attemptIndex,
                    string.Empty, string.Empty, "game_elapsed", attempt.gameElapsedSeconds, "seconds");
                AddTextCsvMetric(csv, session, "phase3_attempt", "phase3", "attempt",
                    attempt.attemptIndex.ToString(CultureInfo.InvariantCulture), attempt.attemptIndex,
                    string.Empty, string.Empty, "correct", BoolText(attempt.correct));
            }
        }

        if (phase.pairsFound != null)
        {
            for (int i = 0; i < phase.pairsFound.Count; i++)
            {
                PairFoundMetric pair = phase.pairsFound[i];
                if (pair == null)
                    continue;

                AddNumericCsvMetric(csv, session, "phase3_pair_found", "phase3", "pair",
                    pair.pairId, i + 1, string.Empty, string.Empty,
                    "phase_elapsed", pair.phaseElapsedSeconds, "seconds");
                AddNumericCsvMetric(csv, session, "phase3_pair_found", "phase3", "pair",
                    pair.pairId, i + 1, string.Empty, string.Empty,
                    "game_elapsed", pair.gameElapsedSeconds, "seconds");
            }
        }

        if (phase.cardSelections == null)
            return;

        foreach (CardSelectionMetric card in phase.cardSelections)
        {
            if (card == null)
                continue;

            AddNumericCsvMetric(csv, session, "phase3_card", "phase3", "card",
                card.cardId, 0, string.Empty, string.Empty,
                "selection_count", card.selectionCount, "count");
        }
    }

    // Exporta métricas específicas da recolha de alimentos.
    private static void AppendPhase4(StringBuilder csv, SessionMetricsData session)
    {
        Phase4MetricsData phase = session.phase4;
        if (phase == null)
            return;

        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "time_to_list_pickup", phase.timeToListPickupSeconds, "seconds");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "scene_changes_until_list_pickup",
            phase.sceneChangesUntilListPickup, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "food_grabs_before_list",
            phase.foodGrabsBeforeListPickup, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "delivery_attempts_before_list",
            phase.deliveryAttemptsBeforeListPickup, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "collection_and_delivery_duration",
            phase.collectionAndDeliveryDurationSeconds, "seconds");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "total_food_grabs", phase.totalFoodGrabs, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "releases_without_delivery",
            phase.releasesWithoutDelivery, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "total_delivery_attempts",
            phase.totalDeliveryAttempts, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "accepted_deliveries", phase.acceptedDeliveries, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "rejected_deliveries", phase.rejectedDeliveries, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "delivery_accuracy", phase.deliveryAccuracy, "ratio");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "average_seconds_per_accepted_food",
            phase.averageSecondsPerAcceptedFood, "seconds");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "total_scene_changes", phase.totalSceneChanges, "count");
        AddNumericCsvMetric(csv, session, "phase_summary", "phase4", "phase", "phase4", 0,
            string.Empty, string.Empty, "unnecessary_manipulations",
            phase.unnecessaryManipulations, "count");

        if (phase.foods != null)
        {
            foreach (FoodAggregateMetric food in phase.foods)
            {
                if (food == null)
                    continue;

                AddNumericCsvMetric(csv, session, "phase4_food", "phase4", "food",
                    food.foodId, 0, string.Empty, string.Empty, "grab_count", food.grabCount, "count");
                AddNumericCsvMetric(csv, session, "phase4_food", "phase4", "food",
                    food.foodId, 0, string.Empty, string.Empty,
                    "release_without_delivery_count", food.releaseWithoutDeliveryCount, "count");
                AddNumericCsvMetric(csv, session, "phase4_food", "phase4", "food",
                    food.foodId, 0, string.Empty, string.Empty,
                    "accepted_deliveries", food.acceptedDeliveries, "count");
                AddNumericCsvMetric(csv, session, "phase4_food", "phase4", "food",
                    food.foodId, 0, string.Empty, string.Empty,
                    "rejected_deliveries", food.rejectedDeliveries, "count");
                AddTextCsvMetric(csv, session, "phase4_food", "phase4", "food",
                    food.foodId, 0, string.Empty, string.Empty, "food_type", food.foodType);
            }
        }

        if (phase.deliveryAttempts == null)
            return;

        foreach (FoodDeliveryAttemptMetric attempt in phase.deliveryAttempts)
        {
            if (attempt == null)
                continue;

            AddNumericCsvMetric(csv, session, "phase4_delivery_attempt", "phase4", "food",
                attempt.foodId, attempt.attemptOrder, string.Empty, string.Empty,
                "phase_elapsed", attempt.phaseElapsedSeconds, "seconds");
            AddNumericCsvMetric(csv, session, "phase4_delivery_attempt", "phase4", "food",
                attempt.foodId, attempt.attemptOrder, string.Empty, string.Empty,
                "seconds_since_last_grab", attempt.secondsSinceLastGrab, "seconds");
            AddNumericCsvMetric(csv, session, "phase4_delivery_attempt", "phase4", "food",
                attempt.foodId, attempt.attemptOrder, string.Empty, string.Empty,
                "accepted_order", attempt.acceptedOrder, "index");
            AddTextCsvMetric(csv, session, "phase4_delivery_attempt", "phase4", "food",
                attempt.foodId, attempt.attemptOrder, string.Empty, string.Empty,
                "food_type", attempt.foodType);
            AddTextCsvMetric(csv, session, "phase4_delivery_attempt", "phase4", "food",
                attempt.foodId, attempt.attemptOrder, string.Empty, string.Empty,
                "result", attempt.result);
            AddTextCsvMetric(csv, session, "phase4_delivery_attempt", "phase4", "food",
                attempt.foodId, attempt.attemptOrder, string.Empty, string.Empty,
                "reason", attempt.reason);
            AddTextCsvMetric(csv, session, "phase4_delivery_attempt", "phase4", "food",
                attempt.foodId, attempt.attemptOrder, string.Empty, string.Empty,
                "before_list_pickup", BoolText(attempt.beforeListPickup));
        }
    }

    // Anexa eventos brutos JSONL para permitir auditoria detalhada da sessão.
    private static void AppendRawEventsToCsv(
        StringBuilder csv,
        SessionMetricsData session,
        string metricsDirectory)
    {
        string eventFilePath = Path.Combine(metricsDirectory, session.sessionId + "_events.jsonl");
        if (!File.Exists(eventFilePath))
            return;

        foreach (string line in File.ReadLines(eventFilePath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            MetricEventRecord eventRecord = JsonUtility.FromJson<MetricEventRecord>(line);
            if (eventRecord == null)
                continue;

            string eventData = string.Empty;
            if (eventRecord.data != null)
            {
                StringBuilder fields = new StringBuilder();
                foreach (MetricEventField field in eventRecord.data)
                {
                    if (field == null)
                        continue;

                    if (fields.Length > 0)
                        fields.Append(" | ");

                    fields.Append(field.key);
                    fields.Append('=');
                    fields.Append(field.value);
                }

                eventData = fields.ToString();
            }

            AddCsvRow(
                csv,
                session,
                "raw_event",
                eventRecord.phase,
                "event",
                eventRecord.eventType,
                0,
                eventRecord.utcTimestamp,
                eventRecord.sceneName,
                "session_elapsed",
                eventRecord.sessionElapsedSeconds.ToString("0.###", CultureInfo.InvariantCulture),
                eventData,
                "seconds");
        }
    }

    // Acrescenta uma linha de métrica numérica ao CSV.
    private static void AddNumericCsvMetric(
        StringBuilder csv,
        SessionMetricsData session,
        string recordGroup,
        string phase,
        string itemType,
        string itemId,
        int itemIndex,
        string recordedUtc,
        string sceneName,
        string metricName,
        float value,
        string unit)
    {
        AddCsvRow(
            csv,
            session,
            recordGroup,
            phase,
            itemType,
            itemId,
            itemIndex,
            recordedUtc,
            sceneName,
            metricName,
            value.ToString("0.###", CultureInfo.InvariantCulture),
            string.Empty,
            unit);
    }

    // Acrescenta uma linha de métrica textual ao CSV.
    private static void AddTextCsvMetric(
        StringBuilder csv,
        SessionMetricsData session,
        string recordGroup,
        string phase,
        string itemType,
        string itemId,
        int itemIndex,
        string recordedUtc,
        string sceneName,
        string metricName,
        string value)
    {
        AddCsvRow(
            csv,
            session,
            recordGroup,
            phase,
            itemType,
            itemId,
            itemIndex,
            recordedUtc,
            sceneName,
            metricName,
            string.Empty,
            value,
            string.Empty);
    }

    // Escreve uma linha normalizada de métrica no CSV combinado.
    private static void AddCsvRow(
        StringBuilder csv,
        SessionMetricsData session,
        string recordGroup,
        string phase,
        string itemType,
        string itemId,
        int itemIndex,
        string recordedUtc,
        string sceneName,
        string metricName,
        string numericValue,
        string textValue,
        string unit)
    {
        AppendCsvCell(csv, session.participantId);
        AppendCsvCell(csv, session.sessionId);
        AppendCsvCell(csv, session.experienceStartedUtc);
        AppendCsvCell(csv, session.experienceEndedUtc);
        AppendCsvCell(csv, session.applicationVersion);
        AppendCsvCell(csv, BoolText(session.experienceCompleted));
        AppendCsvCell(csv, recordGroup);
        AppendCsvCell(csv, phase);
        AppendCsvCell(csv, itemType);
        AppendCsvCell(csv, itemId);
        AppendCsvCell(csv, itemIndex.ToString(CultureInfo.InvariantCulture));
        AppendCsvCell(csv, recordedUtc);
        AppendCsvCell(csv, sceneName);
        AppendCsvCell(csv, metricName);
        AppendCsvCell(csv, numericValue);
        AppendCsvCell(csv, textValue);
        AppendCsvCell(csv, unit, true);
    }

    // Escapa uma célula CSV quando contém separadores ou quebras de linha.
    private static void AppendCsvCell(StringBuilder csv, string value, bool endOfRow = false)
    {
        string safeValue = value ?? string.Empty;
        bool requiresQuotes =
            safeValue.Contains(";") ||
            safeValue.Contains("\"") ||
            safeValue.Contains("\r") ||
            safeValue.Contains("\n");

        if (requiresQuotes)
        {
            csv.Append('"');
            csv.Append(safeValue.Replace("\"", "\"\""));
            csv.Append('"');
        }
        else
        {
            csv.Append(safeValue);
        }

        if (endOfRow)
            csv.AppendLine();
        else
            csv.Append(';');
    }

    // Converte booleanos para texto estável em ficheiros de dados.
    private static string BoolText(bool value)
    {
        return value ? "true" : "false";
    }

    // Junta listas de texto evitando nulos ou sequências vazias.
    private static string JoinStrings(List<string> values, string separator)
    {
        if (values == null || values.Count == 0)
            return string.Empty;

        return string.Join(separator, values);
    }

    // Calcula o tempo desde o início da fase ativa.
    private float ActivePhaseElapsed()
    {
        return ElapsedSince(activePhaseStartedRealtime);
    }

    // Calcula segundos decorridos a partir do relógio realtime do Unity.
    private static float ElapsedSince(double startedRealtime)
    {
        return Mathf.Max(0f, (float)(Time.realtimeSinceStartupAsDouble - startedRealtime));
    }

    // Normaliza valores de evento para texto independente da cultura do sistema.
    private static MetricEventField Field(string key, object value)
    {
        string text;

        if (value == null)
        {
            text = string.Empty;
        }
        else if (value is float floatValue)
        {
            text = floatValue.ToString("0.###", CultureInfo.InvariantCulture);
        }
        else if (value is double doubleValue)
        {
            text = doubleValue.ToString("0.###", CultureInfo.InvariantCulture);
        }
        else if (value is bool boolValue)
        {
            text = boolValue ? "true" : "false";
        }
        else
        {
            text = Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return new MetricEventField(key, text);
    }

    // Gera timestamps UTC em formato ISO 8601.
    private static string UtcNow()
    {
        return DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
    }

    // Remove caracteres problemáticos antes de usar o ID em nomes de ficheiro.
    private static string SanitizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = value.Trim();

        foreach (char invalidChar in invalidChars)
            sanitized = sanitized.Replace(invalidChar, '_');

        return sanitized.Replace(' ', '_');
    }
}
