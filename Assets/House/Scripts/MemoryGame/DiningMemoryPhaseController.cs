using System;
using UnityEngine;

/*
 * Controla a terceira fase da experiência: chegar à sala de jantar, aproximar-se
 * da mesa, iniciar o jogo de memória e avisar o TaskManager quando a ronda termina.
 */
public class DiningMemoryPhaseController : MonoBehaviour
{
    // Estados internos da fase para impedir saltos ou eventos fora de ordem.
    private enum DiningPhaseState
    {
        Inactive,
        GoToDiningRoom,
        GoToTable,
        PlayMemoryGame,
        Completed
    }

    // Divisão que deve ser alcançada antes de o jogador poder ir para a mesa.
    [Header("Objetivo")]
    [SerializeField] private string diningRoomID = "DiningRoom";

    // Referências locais usadas para detetar chegada à mesa e iniciar o minijogo.
    [Header("Referências")]
    [SerializeField] private DiningTableZone diningTableZone;
    [SerializeField] private MemoryMiniGame3DController memoryGameController;

    private DiningPhaseState state = DiningPhaseState.Inactive;

    public bool IsRunning => state != DiningPhaseState.Inactive && state != DiningPhaseState.Completed;

    public event Action PhaseCompleted;

    // Resolve referências e subscreve eventos quando a cena fica ativa.
    private void OnEnable()
    {
        ResolveReferences();
        SubscribeEvents();
    }

    // Evita manter subscrições para objetos que podem ser destruídos com a cena.
    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    // Reinicia a fase e obriga o jogador a começar pela sala de jantar.
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

    // Chamado pelo TaskManager quando o jogador entra numa divisão.
    public void NotifyPlayerEnteredRoom(string roomID)
    {
        if (state != DiningPhaseState.GoToDiningRoom)
            return;

        if (!string.Equals(roomID, diningRoomID, StringComparison.OrdinalIgnoreCase))
            return;

        MetricsManager.Instance?.RecordDiningRoomReached();
        ChangeState(DiningPhaseState.GoToTable);
    }

    // Liga eventos locais da mesa e do jogo ao fluxo desta fase.
    private void SubscribeEvents()
    {
        ResolveReferences();

        if (diningTableZone != null)
            diningTableZone.PlayerArrived += OnPlayerArrivedAtTable;

        if (memoryGameController != null)
            memoryGameController.RoundCompleted += OnMemoryRoundCompleted;
    }

    // Remove eventos para impedir notificações duplicadas ao recarregar cenas.
    private void UnsubscribeEvents()
    {
        if (diningTableZone != null)
            diningTableZone.PlayerArrived -= OnPlayerArrivedAtTable;

        if (memoryGameController != null)
            memoryGameController.RoundCompleted -= OnMemoryRoundCompleted;
    }

    // Ao chegar à mesa, regista a métrica e começa o jogo.
    private void OnPlayerArrivedAtTable()
    {
        if (state != DiningPhaseState.GoToTable)
            return;

        MetricsManager.Instance?.RecordDiningTableReached();
        ChangeState(DiningPhaseState.PlayMemoryGame);
    }

    // Encaminha a conclusão do minijogo para o coordenador global.
    private void OnMemoryRoundCompleted()
    {
        if (state != DiningPhaseState.PlayMemoryGame)
            return;

        state = DiningPhaseState.Completed;
        PhaseCompleted?.Invoke();
    }

    // Centraliza transições de estado e efeitos associados.
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

    // Procura automaticamente referências quando não foram ligadas no Inspector.
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

    // Mantido como proteção para casos em que o controlador seja limpo externamente.
    private void EnsureMemoryControllerReference()
    {
        if (memoryGameController == null)
            ResolveReferences();
    }
}
