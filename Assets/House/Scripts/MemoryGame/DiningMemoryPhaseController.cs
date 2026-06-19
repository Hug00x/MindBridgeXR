using System;
using UnityEngine;

public class DiningMemoryPhaseController : MonoBehaviour
{
    private enum DiningPhaseState
    {
        Inactive,
        GoToDiningRoom,
        GoToTable,
        PlayMemoryGame,
        Completed
    }

    [Header("Objetivo")]
    [SerializeField] private string diningRoomID = "DiningRoom";

    [Header("Referências")]
    [SerializeField] private DiningTableZone diningTableZone;
    [SerializeField] private MemoryMiniGame3DController memoryGameController;

    private DiningPhaseState state = DiningPhaseState.Inactive;

    public bool IsRunning => state != DiningPhaseState.Inactive && state != DiningPhaseState.Completed;

    public event Action PhaseCompleted;

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public void BeginPhase()
    {
        if (IsRunning)
            return;

        UnsubscribeEvents();
        ResolveReferences();
        SubscribeEvents();

        if (diningTableZone != null)
            diningTableZone.ResetZone();

        ChangeState(DiningPhaseState.GoToDiningRoom);
    }

    public void NotifyPlayerEnteredRoom(string roomID)
    {
        if (state != DiningPhaseState.GoToDiningRoom)
            return;

        if (!string.Equals(roomID, diningRoomID, StringComparison.OrdinalIgnoreCase))
            return;

        MetricsManager.Instance?.RecordDiningRoomReached();
        ChangeState(DiningPhaseState.GoToTable);
    }

    private void SubscribeEvents()
    {
        ResolveReferences();

        if (diningTableZone != null)
            diningTableZone.PlayerArrived += OnPlayerArrivedAtTable;

        if (memoryGameController != null)
            memoryGameController.RoundCompleted += OnMemoryRoundCompleted;
    }

    private void UnsubscribeEvents()
    {
        if (diningTableZone != null)
            diningTableZone.PlayerArrived -= OnPlayerArrivedAtTable;

        if (memoryGameController != null)
            memoryGameController.RoundCompleted -= OnMemoryRoundCompleted;
    }

    private void OnPlayerArrivedAtTable()
    {
        if (state != DiningPhaseState.GoToTable)
            return;

        MetricsManager.Instance?.RecordDiningTableReached();
        ChangeState(DiningPhaseState.PlayMemoryGame);
    }

    private void OnMemoryRoundCompleted()
    {
        if (state != DiningPhaseState.PlayMemoryGame)
            return;

        state = DiningPhaseState.Completed;
        PhaseCompleted?.Invoke();
    }

    private void ChangeState(DiningPhaseState newState)
    {
        state = newState;

        switch (state)
        {
            case DiningPhaseState.GoToDiningRoom:
                break;

            case DiningPhaseState.GoToTable:
                break;

            case DiningPhaseState.PlayMemoryGame:
                if (memoryGameController != null)
                    memoryGameController.BeginGame();
                break;
        }
    }

    private void ResolveReferences()
    {
        if (diningTableZone == null)
        {
            diningTableZone = FindFirstObjectByType<DiningTableZone>(FindObjectsInactive.Include);
        }

        if (memoryGameController == null)
        {
            memoryGameController = FindFirstObjectByType<MemoryMiniGame3DController>(FindObjectsInactive.Include);
        }
    }

    private void EnsureMemoryControllerReference()
    {
        if (memoryGameController == null)
            ResolveReferences();
    }
}
