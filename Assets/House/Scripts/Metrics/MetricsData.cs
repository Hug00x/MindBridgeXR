using System;
using System.Collections.Generic;

/*
 * Modelos serializáveis usados pelo sistema de métricas do MindBridgeXR.
 * Estas classes guardam os tempos, eventos, resultados por fase e dados
 * necessários para exportar relatórios JSON, CSV e texto.
 */
[Serializable]
public class SessionMetricsData
{
    // Identificação da sessão e contexto técnico da execução.
    public string participantId;
    public string sessionId;
    public string experienceStartedUtc;
    public string experienceEndedUtc;
    public string applicationVersion;
    public string unityVersion;
    public string platform;
    public string deviceModel;
    public string endReason;
    public float durationSeconds;
    public bool experienceCompleted;
    public bool interrupted;
    public bool restartDetected;
    public int interruptionCount;
    public float totalInterruptionDurationSeconds;
    public int totalSceneChanges;
    public int completedPhaseCount;

    // Resultados agregados e detalhados de todas as fases da experiência.
    public List<string> completedPhases = new List<string>();
    public List<InterruptionMetric> interruptions = new List<InterruptionMetric>();
    public Phase1MetricsData phase1 = new Phase1MetricsData();
    public Phase2MetricsData phase2 = new Phase2MetricsData();
    public Phase3MetricsData phase3 = new Phase3MetricsData();
    public Phase4MetricsData phase4 = new Phase4MetricsData();
}

[Serializable]
public class PhaseTimingMetric
{
    // Janela temporal comum a qualquer fase da experiência.
    public string phaseName;
    public string startedUtc;
    public string endedUtc;
    public float durationSeconds;
    public bool completed;
}

[Serializable]
public class InterruptionMetric
{
    // Regista pausas, perda de foco ou interrupções da aplicação.
    public string reason;
    public string phase;
    public string startedUtc;
    public string endedUtc;
    public float durationSeconds;
}

[Serializable]
public class RoomVisitMetric
{
    // Entrada individual numa divisão durante a exploração livre.
    public int order;
    public string roomId;
    public string sceneName;
    public float phaseElapsedSeconds;
    public bool firstVisit;
}

[Serializable]
public class RoomAggregateMetric
{
    // Estatísticas acumuladas por divisão visitada.
    public string roomId;
    public float firstDiscoverySeconds = -1f;
    public int visitCount;
    public int revisitCount;
    public float timeSpentSeconds;
}

[Serializable]
public class Phase1MetricsData
{
    // Métricas da exploração livre da casa.
    public PhaseTimingMetric timing = new PhaseTimingMetric();
    public float timeToFirstRoomSeconds = -1f;
    public int uniqueRoomsVisited;
    public int totalRoomEntries;
    public int totalRevisits;
    public int sceneChanges;
    public float distanceTravelledMeters;
    public List<RoomVisitMetric> visitSequence = new List<RoomVisitMetric>();
    public List<RoomAggregateMetric> rooms = new List<RoomAggregateMetric>();
}

[Serializable]
public class GuidedTaskMetric
{
    // Resultado de uma tarefa de navegação guiada.
    public int taskIndex;
    public string targetRoomId;
    public string startedUtc;
    public string endedUtc;
    public float durationSeconds;
    public int sceneChanges;
    public int revisitsOrRegressions;
    public bool completed;
    public List<string> roomSequence = new List<string>();
}

[Serializable]
public class Phase2MetricsData
{
    // Métricas agregadas das tarefas de navegação guiada.
    public PhaseTimingMetric timing = new PhaseTimingMetric();
    public int completedTasks;
    public float averageTaskTimeSeconds;
    public float fastestTaskTimeSeconds;
    public int fastestTaskIndex = -1;
    public float slowestTaskTimeSeconds;
    public int slowestTaskIndex = -1;
    public int totalSceneChanges;
    public int totalRevisitsOrRegressions;
    public List<GuidedTaskMetric> tasks = new List<GuidedTaskMetric>();
}

[Serializable]
public class CardSelectionMetric
{
    // Contador de seleções por carta no jogo da memória.
    public string cardId;
    public int selectionCount;
}

[Serializable]
public class MemoryAttemptMetric
{
    // Resultado de uma tentativa de correspondência no jogo da memória.
    public int attemptIndex;
    public float durationSeconds;
    public bool correct;
    public float gameElapsedSeconds;
}

[Serializable]
public class PairFoundMetric
{
    // Momento em que um par correto foi descoberto.
    public string pairId;
    public float phaseElapsedSeconds;
    public float gameElapsedSeconds;
}

[Serializable]
public class Phase3MetricsData
{
    // Métricas da deslocação até à sala de jantar e do jogo da memória.
    public PhaseTimingMetric timing = new PhaseTimingMetric();
    public float timeToDiningRoomSeconds = -1f;
    public float timeToTableSeconds = -1f;
    public int sceneChangesUntilGame;
    public float memoryGameDurationSeconds;
    public int totalAttempts;
    public int correctPairs;
    public int incorrectPairs;
    public float accuracy;
    public float averageAttemptTimeSeconds;
    public int theoreticalMinimumAttempts;
    public float efficiency;
    public List<MemoryAttemptMetric> attempts = new List<MemoryAttemptMetric>();
    public List<PairFoundMetric> pairsFound = new List<PairFoundMetric>();
    public List<CardSelectionMetric> cardSelections = new List<CardSelectionMetric>();
}

[Serializable]
public class FoodAggregateMetric
{
    // Estatísticas acumuladas por alimento manipulável.
    public string foodId;
    public string foodType;
    public int grabCount;
    public int releaseWithoutDeliveryCount;
    public int acceptedDeliveries;
    public int rejectedDeliveries;
}

[Serializable]
public class FoodDeliveryAttemptMetric
{
    // Registo individual de uma tentativa de entrega de alimento.
    public int attemptOrder;
    public int acceptedOrder;
    public string foodId;
    public string foodType;
    public string result;
    public string reason;
    public bool beforeListPickup;
    public float phaseElapsedSeconds;
    public float secondsSinceLastGrab = -1f;
}

[Serializable]
public class Phase4MetricsData
{
    // Métricas da recolha da lista e entrega de alimentos no exterior.
    public PhaseTimingMetric timing = new PhaseTimingMetric();
    public float timeToListPickupSeconds = -1f;
    public int sceneChangesUntilListPickup;
    public int foodGrabsBeforeListPickup;
    public int deliveryAttemptsBeforeListPickup;
    public float collectionAndDeliveryDurationSeconds;
    public int totalFoodGrabs;
    public int releasesWithoutDelivery;
    public int totalDeliveryAttempts;
    public int acceptedDeliveries;
    public int rejectedDeliveries;
    public float deliveryAccuracy;
    public float averageSecondsPerAcceptedFood;
    public int totalSceneChanges;
    public int unnecessaryManipulations;
    public List<FoodAggregateMetric> foods = new List<FoodAggregateMetric>();
    public List<FoodDeliveryAttemptMetric> deliveryAttempts = new List<FoodDeliveryAttemptMetric>();
}

[Serializable]
public class MetricEventRecord
{
    // Evento cronológico usado para reconstruir a sessão em detalhe.
    public string utcTimestamp;
    public float sessionElapsedSeconds;
    public string eventType;
    public string phase;
    public string sceneName;
    public List<MetricEventField> data = new List<MetricEventField>();
}

[Serializable]
public class MetricEventField
{
    // Par chave/valor associado a um evento registado.
    public string key;
    public string value;

    public MetricEventField(string key, string value)
    {
        this.key = key;
        this.value = value;
    }
}
