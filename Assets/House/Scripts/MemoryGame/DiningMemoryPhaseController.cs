using System;
using System.Collections;
using TMPro;
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

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI taskText;
    [SerializeField] private TextMeshProUGUI centerMessageText;
    [SerializeField] private float centerMessageDuration = 2.5f;

    [Header("Mensagens")]
    [SerializeField] private string completionMessage = "Bom trabalho! Concluiste o jogo da memoria.";

    private DiningPhaseState state = DiningPhaseState.Inactive;
    private Coroutine centerMessageRoutine;

    public bool IsRunning => state != DiningPhaseState.Inactive && state != DiningPhaseState.Completed;

    public event Action PhaseCompleted;

    private void OnEnable()
    {
        ResolveMemoryController();
        SubscribeEvents();
        HideCenterMessage();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public void BeginPhase()
    {
        if (IsRunning)
            return;

        if (diningTableZone != null)
            diningTableZone.ResetZone();

        EnsureMemoryControllerReference();

        ChangeState(DiningPhaseState.GoToDiningRoom);
    }

    public void NotifyPlayerEnteredRoom(string roomID)
    {
        if (state != DiningPhaseState.GoToDiningRoom)
            return;

        if (!string.Equals(roomID, diningRoomID, StringComparison.OrdinalIgnoreCase))
            return;

        ShowCenterMessage("Sala de jantar");
        ChangeState(DiningPhaseState.GoToTable);
    }

    private void SubscribeEvents()
    {
        if (diningTableZone != null)
            diningTableZone.PlayerArrived += OnPlayerArrivedAtTable;

        EnsureMemoryControllerReference();

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

        ShowCenterMessage("Perfeito. Inicia o jogo de memoria.");
        ChangeState(DiningPhaseState.PlayMemoryGame);
    }

    private void OnMemoryRoundCompleted()
    {
        if (state != DiningPhaseState.PlayMemoryGame)
            return;

        state = DiningPhaseState.Completed;
        SetTask("Fase 3 concluida");
        ShowCenterMessage(completionMessage);
        PhaseCompleted?.Invoke();
    }

    private void ChangeState(DiningPhaseState newState)
    {
        state = newState;

        switch (state)
        {
            case DiningPhaseState.GoToDiningRoom:
                SetTask("Fase 3: Va ate a sala de jantar");
                break;

            case DiningPhaseState.GoToTable:
                SetTask("Fase 3: Va ate a mesa da sala de jantar");
                break;

            case DiningPhaseState.PlayMemoryGame:
                SetTask("Fase 3: Jogue uma ronda do jogo de memoria");
                if (memoryGameController != null)
                    memoryGameController.BeginGame();
                break;
        }
    }

    private void ResolveMemoryController()
    {
        if (memoryGameController == null)
        {
            memoryGameController = FindFirstObjectByType<MemoryMiniGame3DController>(FindObjectsInactive.Exclude);

            if (memoryGameController == null)
                Debug.LogWarning("MemoryMiniGame3DController não encontrado na cena.");
        }
    }

    private void EnsureMemoryControllerReference()
    {
        if (memoryGameController == null)
            ResolveMemoryController();
    }

    private void SetTask(string message)
    {
        if (taskText != null)
            taskText.text = message;
    }

    private void ShowCenterMessage(string message)
    {
        if (centerMessageText == null || string.IsNullOrWhiteSpace(message))
            return;

        if (centerMessageRoutine != null)
            StopCoroutine(centerMessageRoutine);

        centerMessageRoutine = StartCoroutine(CenterMessageRoutine(message));
    }

    private IEnumerator CenterMessageRoutine(string message)
    {
        centerMessageText.gameObject.SetActive(true);
        centerMessageText.text = message;

        yield return new WaitForSeconds(centerMessageDuration);

        HideCenterMessage();
        centerMessageRoutine = null;
    }

    private void HideCenterMessage()
    {
        if (centerMessageText == null)
            return;

        centerMessageText.text = string.Empty;
        centerMessageText.gameObject.SetActive(false);
    }
}
