using System;
using System.Collections.Generic;

[Serializable]
public class SessionMetricsData
{
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
    public string phaseName;
    public string startedUtc;
    public string endedUtc;
    public float durationSeconds;
    public bool completed;
}

[Serializable]
public class InterruptionMetric
{
    public string reason;
    public string phase;
    public string startedUtc;
    public string endedUtc;
    public float durationSeconds;
}

[Serializable]
public class RoomVisitMetric
{
    public int order;
    public string roomId;
    public string sceneName;
    public float phaseElapsedSeconds;
    public bool firstVisit;
}

[Serializable]
public class RoomAggregateMetric
{
    public string roomId;
    public float firstDiscoverySeconds = -1f;
    public int visitCount;
    public int revisitCount;
    public float timeSpentSeconds;
}

[Serializable]
public class Phase1MetricsData
{
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
    public string cardId;
    public int selectionCount;
}

[Serializable]
public class MemoryAttemptMetric
{
    public int attemptIndex;
    public float durationSeconds;
    public bool correct;
    public float gameElapsedSeconds;
}

[Serializable]
public class PairFoundMetric
{
    public string pairId;
    public float phaseElapsedSeconds;
    public float gameElapsedSeconds;
}

[Serializable]
public class Phase3MetricsData
{
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
    public string key;
    public string value;

    public MetricEventField(string key, string value)
    {
        this.key = key;
        this.value = value;
    }
}
