using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/*
 * Exportador complementar das métricas recolhidas durante a experiência.
 * A partir dos resumos JSON, cria uma tabela comparativa para análise e
 * relatórios individuais em texto com linguagem mais legível.
 */
public static class MetricsReportExporter
{
    // Cultura portuguesa usada nos números apresentados nos relatórios.
    private static readonly CultureInfo PortugueseCulture =
        CultureInfo.GetCultureInfo("pt-PT");

    // Colunas fixas que aparecem antes das colunas dinâmicas por sala/tarefa/alimento.
    private static readonly string[] BaseComparisonColumns =
    {
        "Participante",
        "Sessão",
        "Início UTC",
        "Fim UTC",
        "Versão",
        "Experiência concluída",
        "Motivo de fim",
        "Duração total (s)",
        "Duração total formatada",
        "Mudanças de cena totais",
        "Interrupções",
        "Duração das interrupções (s)",
        "Reinício detetado",
        "Plataforma",
        "Dispositivo",

        "Fase 1 concluída",
        "Fase 1 duração (s)",
        "Fase 1 tempo até primeira divisão (s)",
        "Fase 1 divisões únicas",
        "Fase 1 entradas em divisões",
        "Fase 1 revisitas",
        "Fase 1 mudanças de cena",
        "Fase 1 distância percorrida (m)",
        "Fase 1 sequência de divisões",

        "Fase 2 concluída",
        "Fase 2 duração (s)",
        "Fase 2 tarefas concluídas",
        "Fase 2 tempo médio por tarefa (s)",
        "Fase 2 tarefa mais rápida",
        "Fase 2 tempo mais rápido (s)",
        "Fase 2 tarefa mais lenta",
        "Fase 2 tempo mais lento (s)",
        "Fase 2 mudanças de cena",
        "Fase 2 revisitas ou regressos",
        "Fase 2 evolução dos tempos (s)",

        "Fase 3 concluída",
        "Fase 3 duração (s)",
        "Fase 3 tempo até sala de jantar (s)",
        "Fase 3 tempo até mesa (s)",
        "Fase 3 mudanças de cena até ao jogo",
        "Fase 3 duração do jogo (s)",
        "Fase 3 tentativas",
        "Fase 3 pares corretos",
        "Fase 3 pares incorretos",
        "Fase 3 taxa de acerto (%)",
        "Fase 3 tempo médio por tentativa (s)",
        "Fase 3 mínimo teórico de tentativas",
        "Fase 3 eficiência (%)",
        "Fase 3 resultado das tentativas",
        "Fase 3 duração das tentativas (s)",
        "Fase 3 ordem dos pares encontrados",

        "Fase 4 concluída",
        "Fase 4 duração (s)",
        "Fase 4 tempo até à lista (s)",
        "Fase 4 mudanças de cena até à lista",
        "Fase 4 alimentos agarrados antes da lista",
        "Fase 4 entregas tentadas antes da lista",
        "Fase 4 duração da recolha e entrega (s)",
        "Fase 4 alimentos agarrados",
        "Fase 4 largados sem entrega",
        "Fase 4 tentativas de entrega",
        "Fase 4 entregas corretas",
        "Fase 4 entregas incorretas",
        "Fase 4 taxa de acerto (%)",
        "Fase 4 tempo médio por alimento correto (s)",
        "Fase 4 mudanças de cena",
        "Fase 4 manipulações desnecessárias",
        "Fase 4 ordem das entregas",
        "Fase 4 motivos de rejeição"
    };

    // Ponto de entrada chamado pelo MetricsManager depois de reconstruir os ficheiros base.
    public static void RebuildAll(string metricsDirectory)
    {
        if (string.IsNullOrWhiteSpace(metricsDirectory) || !Directory.Exists(metricsDirectory))
            return;

        try
        {
            List<SessionMetricsData> sessions = LoadSessions(metricsDirectory);
            if (sessions.Count == 0)
                return;

            WriteComparisonCsv(metricsDirectory, sessions);
            WriteSessionReports(metricsDirectory, sessions);
        }
        catch (Exception)
        {
            // A geração de relatórios é complementar e não deve bloquear a sessão.
        }
    }

    // Carrega todos os resumos JSON válidos no diretório de métricas.
    private static List<SessionMetricsData> LoadSessions(string metricsDirectory)
    {
        string[] summaryFiles = Directory.GetFiles(metricsDirectory, "*_summary.json");
        Array.Sort(summaryFiles, StringComparer.OrdinalIgnoreCase);

        List<SessionMetricsData> sessions = new List<SessionMetricsData>();

        foreach (string filePath in summaryFiles)
        {
            string json = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(json))
                continue;

            SessionMetricsData session = JsonUtility.FromJson<SessionMetricsData>(json);
            if (session == null || string.IsNullOrWhiteSpace(session.sessionId))
                continue;

            sessions.Add(session);
        }

        return sessions;
    }

    // Escreve CSV e TSV comparativos, incluindo colunas dinâmicas descobertas nos dados.
    private static void WriteComparisonCsv(
        string metricsDirectory,
        List<SessionMetricsData> sessions)
    {
        List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();
        SortedSet<string> dynamicColumns =
            new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);

        foreach (SessionMetricsData session in sessions)
        {
            Dictionary<string, string> row = BuildComparisonRow(session);
            rows.Add(row);

            foreach (string column in row.Keys)
            {
                if (!BaseComparisonColumns.Contains(column))
                    dynamicColumns.Add(column);
            }
        }

        List<string> columns = new List<string>(BaseComparisonColumns);
        columns.AddRange(dynamicColumns);

        string csvContent = BuildDelimitedTable(columns, rows, ';');
        string tsvContent = BuildDelimitedTable(columns, rows, '\t');

        File.WriteAllText(
            Path.Combine(metricsDirectory, "MindBridgeXR_Comparacao.csv"),
            csvContent,
            new UTF8Encoding(true));

        File.WriteAllText(
            Path.Combine(metricsDirectory, "MindBridgeXR_Comparacao_Excel.tsv"),
            tsvContent,
            new UTF8Encoding(true));
    }

    // Constrói uma tabela delimitada com escaping adequado para CSV/TSV.
    private static string BuildDelimitedTable(
        List<string> columns,
        List<Dictionary<string, string>> rows,
        char delimiter)
    {
        StringBuilder output = new StringBuilder();
        AppendDelimitedRow(output, columns, delimiter);

        foreach (Dictionary<string, string> row in rows)
        {
            List<string> values = new List<string>(columns.Count);
            foreach (string column in columns)
                values.Add(row.TryGetValue(column, out string value) ? value : string.Empty);

            AppendDelimitedRow(output, values, delimiter);
        }

        return output.ToString();
    }

    // Converte uma sessão inteira numa linha de comparação.
    private static Dictionary<string, string> BuildComparisonRow(SessionMetricsData session)
    {
        Dictionary<string, string> row =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Set(row, "Participante", session.participantId);
        Set(row, "Sessão", session.sessionId);
        Set(row, "Início UTC", session.experienceStartedUtc);
        Set(row, "Fim UTC", session.experienceEndedUtc);
        Set(row, "Versão", session.applicationVersion);
        Set(row, "Experiência concluída", YesNo(session.experienceCompleted));
        Set(row, "Motivo de fim", TranslateEndReason(session.endReason));
        SetNumber(row, "Duração total (s)", session.durationSeconds);
        Set(row, "Duração total formatada", FormatDuration(session.durationSeconds));
        SetNumber(row, "Mudanças de cena totais", session.totalSceneChanges);
        SetNumber(row, "Interrupções", session.interruptionCount);
        SetNumber(row, "Duração das interrupções (s)", session.totalInterruptionDurationSeconds);
        Set(row, "Reinício detetado", YesNo(session.restartDetected));
        Set(row, "Plataforma", session.platform);
        Set(row, "Dispositivo", session.deviceModel);

        AddPhase1Columns(row, session.phase1);
        AddPhase2Columns(row, session.phase2);
        AddPhase3Columns(row, session.phase3);
        AddPhase4Columns(row, session.phase4);

        return row;
    }

    // Acrescenta métricas da exploração livre à linha comparativa.
    private static void AddPhase1Columns(
        Dictionary<string, string> row,
        Phase1MetricsData phase)
    {
        if (phase == null)
            return;

        AddTimingColumns(row, "Fase 1", phase.timing);
        SetOptionalNumber(row, "Fase 1 tempo até primeira divisão (s)", phase.timeToFirstRoomSeconds);
        SetNumber(row, "Fase 1 divisões únicas", phase.uniqueRoomsVisited);
        SetNumber(row, "Fase 1 entradas em divisões", phase.totalRoomEntries);
        SetNumber(row, "Fase 1 revisitas", phase.totalRevisits);
        SetNumber(row, "Fase 1 mudanças de cena", phase.sceneChanges);
        SetNumber(row, "Fase 1 distância percorrida (m)", phase.distanceTravelledMeters);

        if (phase.visitSequence != null)
        {
            Set(
                row,
                "Fase 1 sequência de divisões",
                string.Join(
                    " > ",
                    phase.visitSequence
                        .Where(visit => visit != null)
                        .Select(visit => visit.roomId)));
        }

        if (phase.rooms == null)
            return;

        foreach (RoomAggregateMetric room in phase.rooms)
        {
            if (room == null || string.IsNullOrWhiteSpace(room.roomId))
                continue;

            string prefix = "Fase 1 divisão [" + room.roomId + "] ";
            SetOptionalNumber(row, prefix + "primeira descoberta (s)", room.firstDiscoverySeconds);
            SetNumber(row, prefix + "visitas", room.visitCount);
            SetNumber(row, prefix + "revisitas", room.revisitCount);
            SetNumber(row, prefix + "tempo passado (s)", room.timeSpentSeconds);
        }
    }

    // Acrescenta métricas das tarefas de navegação guiada.
    private static void AddPhase2Columns(
        Dictionary<string, string> row,
        Phase2MetricsData phase)
    {
        if (phase == null)
            return;

        AddTimingColumns(row, "Fase 2", phase.timing);
        SetNumber(row, "Fase 2 tarefas concluídas", phase.completedTasks);
        SetNumber(row, "Fase 2 tempo médio por tarefa (s)", phase.averageTaskTimeSeconds);
        SetNumber(row, "Fase 2 tarefa mais rápida", phase.fastestTaskIndex);
        SetNumber(row, "Fase 2 tempo mais rápido (s)", phase.fastestTaskTimeSeconds);
        SetNumber(row, "Fase 2 tarefa mais lenta", phase.slowestTaskIndex);
        SetNumber(row, "Fase 2 tempo mais lento (s)", phase.slowestTaskTimeSeconds);
        SetNumber(row, "Fase 2 mudanças de cena", phase.totalSceneChanges);
        SetNumber(row, "Fase 2 revisitas ou regressos", phase.totalRevisitsOrRegressions);

        if (phase.tasks == null)
            return;

        List<GuidedTaskMetric> orderedTasks = phase.tasks
            .Where(task => task != null)
            .OrderBy(task => task.taskIndex)
            .ToList();

        Set(
            row,
            "Fase 2 evolução dos tempos (s)",
            string.Join(
                " > ",
                orderedTasks.Select(
                    task => "T" + task.taskIndex + "=" + Number(task.durationSeconds))));

        foreach (GuidedTaskMetric task in orderedTasks)
        {
            string prefix = "Fase 2 tarefa " + task.taskIndex + " ";
            Set(row, prefix + "destino", task.targetRoomId);
            SetNumber(row, prefix + "duração (s)", task.durationSeconds);
            SetNumber(row, prefix + "mudanças de cena", task.sceneChanges);
            SetNumber(row, prefix + "revisitas ou regressos", task.revisitsOrRegressions);
            Set(row, prefix + "concluída", YesNo(task.completed));
            Set(row, prefix + "percurso", Join(task.roomSequence, " > "));
        }
    }

    // Acrescenta métricas da sala de jantar e do jogo da memória.
    private static void AddPhase3Columns(
        Dictionary<string, string> row,
        Phase3MetricsData phase)
    {
        if (phase == null)
            return;

        AddTimingColumns(row, "Fase 3", phase.timing);
        SetOptionalNumber(row, "Fase 3 tempo até sala de jantar (s)", phase.timeToDiningRoomSeconds);
        SetOptionalNumber(row, "Fase 3 tempo até mesa (s)", phase.timeToTableSeconds);
        SetNumber(row, "Fase 3 mudanças de cena até ao jogo", phase.sceneChangesUntilGame);
        SetNumber(row, "Fase 3 duração do jogo (s)", phase.memoryGameDurationSeconds);
        SetNumber(row, "Fase 3 tentativas", phase.totalAttempts);
        SetNumber(row, "Fase 3 pares corretos", phase.correctPairs);
        SetNumber(row, "Fase 3 pares incorretos", phase.incorrectPairs);
        SetNumber(row, "Fase 3 taxa de acerto (%)", phase.accuracy * 100f);
        SetNumber(row, "Fase 3 tempo médio por tentativa (s)", phase.averageAttemptTimeSeconds);
        SetNumber(row, "Fase 3 mínimo teórico de tentativas", phase.theoreticalMinimumAttempts);
        SetNumber(row, "Fase 3 eficiência (%)", phase.efficiency * 100f);

        if (phase.attempts != null)
        {
            List<MemoryAttemptMetric> attempts = phase.attempts
                .Where(attempt => attempt != null)
                .OrderBy(attempt => attempt.attemptIndex)
                .ToList();

            Set(
                row,
                "Fase 3 resultado das tentativas",
                string.Join(
                    " > ",
                    attempts.Select(
                        attempt => "T" + attempt.attemptIndex + "=" +
                                   (attempt.correct ? "Certa" : "Errada"))));
            Set(
                row,
                "Fase 3 duração das tentativas (s)",
                string.Join(
                    " > ",
                    attempts.Select(
                        attempt => "T" + attempt.attemptIndex + "=" +
                                   Number(attempt.durationSeconds))));
        }

        if (phase.pairsFound != null)
        {
            Set(
                row,
                "Fase 3 ordem dos pares encontrados",
                string.Join(
                    " > ",
                    phase.pairsFound
                        .Where(pair => pair != null)
                        .Select(pair => pair.pairId + " (" + Number(pair.gameElapsedSeconds) + "s)")));
        }

        if (phase.cardSelections == null)
            return;

        foreach (CardSelectionMetric card in phase.cardSelections)
        {
            if (card == null || string.IsNullOrWhiteSpace(card.cardId))
                continue;

            SetNumber(
                row,
                "Fase 3 carta [" + card.cardId + "] seleções",
                card.selectionCount);
        }
    }

    // Acrescenta métricas da recolha e entrega de alimentos.
    private static void AddPhase4Columns(
        Dictionary<string, string> row,
        Phase4MetricsData phase)
    {
        if (phase == null)
            return;

        AddTimingColumns(row, "Fase 4", phase.timing);
        SetOptionalNumber(row, "Fase 4 tempo até à lista (s)", phase.timeToListPickupSeconds);
        SetNumber(row, "Fase 4 mudanças de cena até à lista", phase.sceneChangesUntilListPickup);
        SetNumber(row, "Fase 4 alimentos agarrados antes da lista", phase.foodGrabsBeforeListPickup);
        SetNumber(row, "Fase 4 entregas tentadas antes da lista", phase.deliveryAttemptsBeforeListPickup);
        SetNumber(row, "Fase 4 duração da recolha e entrega (s)",
            phase.collectionAndDeliveryDurationSeconds);
        SetNumber(row, "Fase 4 alimentos agarrados", phase.totalFoodGrabs);
        SetNumber(row, "Fase 4 largados sem entrega", phase.releasesWithoutDelivery);
        SetNumber(row, "Fase 4 tentativas de entrega", phase.totalDeliveryAttempts);
        SetNumber(row, "Fase 4 entregas corretas", phase.acceptedDeliveries);
        SetNumber(row, "Fase 4 entregas incorretas", phase.rejectedDeliveries);
        SetNumber(row, "Fase 4 taxa de acerto (%)", phase.deliveryAccuracy * 100f);
        SetNumber(row, "Fase 4 tempo médio por alimento correto (s)",
            phase.averageSecondsPerAcceptedFood);
        SetNumber(row, "Fase 4 mudanças de cena", phase.totalSceneChanges);
        SetNumber(row, "Fase 4 manipulações desnecessárias", phase.unnecessaryManipulations);

        AddFoodTypeColumns(row, phase.foods);
        AddDeliverySequenceColumns(row, phase.deliveryAttempts);
    }

    // Agrega os alimentos por tipo para criar colunas dinâmicas.
    private static void AddFoodTypeColumns(
        Dictionary<string, string> row,
        List<FoodAggregateMetric> foods)
    {
        if (foods == null)
            return;

        Dictionary<string, FoodTypeTotals> totals =
            new Dictionary<string, FoodTypeTotals>(StringComparer.OrdinalIgnoreCase);

        foreach (FoodAggregateMetric food in foods)
        {
            if (food == null)
                continue;

            string foodType = string.IsNullOrWhiteSpace(food.foodType)
                ? "Desconhecido"
                : food.foodType;

            if (!totals.TryGetValue(foodType, out FoodTypeTotals total))
            {
                total = new FoodTypeTotals();
                totals.Add(foodType, total);
            }

            total.grabs += food.grabCount;
            total.releasesWithoutDelivery += food.releaseWithoutDeliveryCount;
            total.accepted += food.acceptedDeliveries;
            total.rejected += food.rejectedDeliveries;
        }

        foreach (KeyValuePair<string, FoodTypeTotals> item in totals)
        {
            string prefix = "Fase 4 alimento [" + item.Key + "] ";
            SetNumber(row, prefix + "vezes agarrado", item.Value.grabs);
            SetNumber(row, prefix + "largado sem entrega", item.Value.releasesWithoutDelivery);
            SetNumber(row, prefix + "entregas corretas", item.Value.accepted);
            SetNumber(row, prefix + "entregas incorretas", item.Value.rejected);
        }
    }

    // Resume a ordem das tentativas de entrega e motivos de rejeição.
    private static void AddDeliverySequenceColumns(
        Dictionary<string, string> row,
        List<FoodDeliveryAttemptMetric> attempts)
    {
        if (attempts == null)
            return;

        List<FoodDeliveryAttemptMetric> orderedAttempts = attempts
            .Where(attempt => attempt != null)
            .OrderBy(attempt => attempt.attemptOrder)
            .ToList();

        Set(
            row,
            "Fase 4 ordem das entregas",
            string.Join(
                " > ",
                orderedAttempts.Select(
                    attempt =>
                        attempt.attemptOrder + ":" +
                        attempt.foodType + "=" +
                        TranslateDeliveryResult(attempt.result))));

        Set(
            row,
            "Fase 4 motivos de rejeição",
            string.Join(
                " | ",
                orderedAttempts
                    .Where(attempt =>
                        !string.Equals(attempt.result, "accepted", StringComparison.OrdinalIgnoreCase))
                    .Select(attempt =>
                        attempt.foodType + ": " + TranslateDeliveryReason(attempt.reason))));
    }

    // Cria um relatório de texto individual para cada sessão.
    private static void WriteSessionReports(
        string metricsDirectory,
        List<SessionMetricsData> sessions)
    {
        string reportsDirectory = Path.Combine(metricsDirectory, "Relatorios");
        Directory.CreateDirectory(reportsDirectory);

        foreach (SessionMetricsData session in sessions)
        {
            string reportPath = Path.Combine(
                reportsDirectory,
                SanitizeFileName(session.sessionId) + "_relatorio.txt");

            File.WriteAllText(
                reportPath,
                BuildSessionReport(session),
                new UTF8Encoding(true));
        }
    }

    // Monta o relatório textual completo de uma sessão.
    private static string BuildSessionReport(SessionMetricsData session)
    {
        StringBuilder report = new StringBuilder();

        report.AppendLine("MINDBRIDGEXR — RELATÓRIO DA SESSÃO");
        report.AppendLine(new string('=', 42));
        report.AppendLine("Participante: " + session.participantId);
        report.AppendLine("Sessão: " + session.sessionId);
        report.AppendLine("Início UTC: " + session.experienceStartedUtc);
        report.AppendLine("Fim UTC: " + EmptyAsDash(session.experienceEndedUtc));
        report.AppendLine("Versão: " + session.applicationVersion);
        report.AppendLine("Dispositivo: " + session.deviceModel);
        report.AppendLine("Experiência concluída: " + YesNo(session.experienceCompleted));
        report.AppendLine("Duração total: " + FormatDuration(session.durationSeconds));
        report.AppendLine("Mudanças de cena: " + session.totalSceneChanges);
        report.AppendLine("Interrupções: " + session.interruptionCount);

        AppendPhase1Report(report, session.phase1);
        AppendPhase2Report(report, session.phase2);
        AppendPhase3Report(report, session.phase3);
        AppendPhase4Report(report, session.phase4);

        return report.ToString();
    }

    // Escreve a secção textual da fase de exploração livre.
    private static void AppendPhase1Report(StringBuilder report, Phase1MetricsData phase)
    {
        report.AppendLine();
        report.AppendLine("FASE 1 — EXPLORAÇÃO LIVRE");
        report.AppendLine(new string('-', 32));

        if (!HasStarted(phase?.timing))
        {
            report.AppendLine("Fase não iniciada.");
            return;
        }

        report.AppendLine("Concluída: " + YesNo(phase.timing.completed));
        report.AppendLine("Duração: " + FormatDuration(phase.timing.durationSeconds));
        report.AppendLine("Tempo até à primeira divisão: " +
                          FormatOptionalDuration(phase.timeToFirstRoomSeconds));
        report.AppendLine("Divisões únicas: " + phase.uniqueRoomsVisited);
        report.AppendLine("Entradas em divisões: " + phase.totalRoomEntries);
        report.AppendLine("Revisitas: " + phase.totalRevisits);
        report.AppendLine("Mudanças de cena: " + phase.sceneChanges);

        if (phase.distanceTravelledMeters > 0f)
            report.AppendLine("Distância percorrida: " + Number(phase.distanceTravelledMeters) + " m");

        if (phase.visitSequence != null && phase.visitSequence.Count > 0)
        {
            report.AppendLine("Sequência:");
            foreach (RoomVisitMetric visit in phase.visitSequence)
            {
                if (visit == null)
                    continue;

                report.AppendLine(
                    "  " + visit.order + ". " + visit.roomId +
                    " aos " + Number(visit.phaseElapsedSeconds) + " s" +
                    (visit.firstVisit ? " — primeira visita" : " — revisita"));
            }
        }

        if (phase.rooms == null || phase.rooms.Count == 0)
            return;

        report.AppendLine("Resultados por divisão:");
        foreach (RoomAggregateMetric room in phase.rooms
                     .Where(room => room != null)
                     .OrderBy(room => room.firstDiscoverySeconds))
        {
            report.AppendLine(
                "  • " + room.roomId +
                ": descoberta aos " + Number(room.firstDiscoverySeconds) + " s; " +
                room.visitCount + " visitas; " +
                room.revisitCount + " revisitas; " +
                Number(room.timeSpentSeconds) + " s passados na divisão");
        }
    }

    // Escreve a secção textual das tarefas guiadas.
    private static void AppendPhase2Report(StringBuilder report, Phase2MetricsData phase)
    {
        report.AppendLine();
        report.AppendLine("FASE 2 — NAVEGAÇÃO GUIADA");
        report.AppendLine(new string('-', 32));

        if (!HasStarted(phase?.timing))
        {
            report.AppendLine("Fase não iniciada.");
            return;
        }

        report.AppendLine("Concluída: " + YesNo(phase.timing.completed));
        report.AppendLine("Duração: " + FormatDuration(phase.timing.durationSeconds));
        report.AppendLine("Tarefas concluídas: " + phase.completedTasks);
        report.AppendLine("Tempo médio por tarefa: " + Number(phase.averageTaskTimeSeconds) + " s");
        report.AppendLine(
            "Mais rápida: tarefa " + phase.fastestTaskIndex +
            " em " + Number(phase.fastestTaskTimeSeconds) + " s");
        report.AppendLine(
            "Mais lenta: tarefa " + phase.slowestTaskIndex +
            " em " + Number(phase.slowestTaskTimeSeconds) + " s");
        report.AppendLine("Mudanças de cena: " + phase.totalSceneChanges);
        report.AppendLine("Revisitas ou regressos: " + phase.totalRevisitsOrRegressions);

        if (phase.tasks == null)
            return;

        report.AppendLine("Resultados por tarefa:");
        foreach (GuidedTaskMetric task in phase.tasks
                     .Where(task => task != null)
                     .OrderBy(task => task.taskIndex))
        {
            report.AppendLine(
                "  • Tarefa " + task.taskIndex + " — " + task.targetRoomId +
                ": " + Number(task.durationSeconds) + " s; " +
                task.sceneChanges + " mudanças de cena; " +
                task.revisitsOrRegressions + " revisitas/regressos");

            if (task.roomSequence != null && task.roomSequence.Count > 0)
                report.AppendLine("    Percurso: " + Join(task.roomSequence, " > "));
        }
    }

    // Escreve a secção textual do jogo da memória.
    private static void AppendPhase3Report(StringBuilder report, Phase3MetricsData phase)
    {
        report.AppendLine();
        report.AppendLine("FASE 3 — JOGO DA MEMÓRIA");
        report.AppendLine(new string('-', 32));

        if (!HasStarted(phase?.timing))
        {
            report.AppendLine("Fase não iniciada.");
            return;
        }

        report.AppendLine("Concluída: " + YesNo(phase.timing.completed));
        report.AppendLine("Duração da fase: " + FormatDuration(phase.timing.durationSeconds));
        report.AppendLine("Tempo até à sala de jantar: " +
                          FormatOptionalDuration(phase.timeToDiningRoomSeconds));
        report.AppendLine("Tempo até à mesa: " +
                          FormatOptionalDuration(phase.timeToTableSeconds));
        report.AppendLine("Duração do jogo: " + FormatDuration(phase.memoryGameDurationSeconds));
        report.AppendLine("Tentativas: " + phase.totalAttempts);
        report.AppendLine("Pares corretos: " + phase.correctPairs);
        report.AppendLine("Pares incorretos: " + phase.incorrectPairs);
        report.AppendLine("Taxa de acerto: " + Percentage(phase.accuracy));
        report.AppendLine("Tempo médio por tentativa: " +
                          Number(phase.averageAttemptTimeSeconds) + " s");
        report.AppendLine("Eficiência: " + Percentage(phase.efficiency));

        if (phase.attempts != null && phase.attempts.Count > 0)
        {
            report.AppendLine("Tentativas:");
            foreach (MemoryAttemptMetric attempt in phase.attempts
                         .Where(attempt => attempt != null)
                         .OrderBy(attempt => attempt.attemptIndex))
            {
                report.AppendLine(
                    "  • " + attempt.attemptIndex + ": " +
                    (attempt.correct ? "correta" : "incorreta") +
                    "; " + Number(attempt.durationSeconds) + " s");
            }
        }

        if (phase.cardSelections != null && phase.cardSelections.Count > 0)
        {
            report.AppendLine("Seleções por carta:");
            foreach (CardSelectionMetric card in phase.cardSelections
                         .Where(card => card != null)
                         .OrderByDescending(card => card.selectionCount)
                         .ThenBy(card => card.cardId))
            {
                report.AppendLine(
                    "  • " + card.cardId + ": " + card.selectionCount);
            }
        }
    }

    // Escreve a secção textual da recolha de alimentos.
    private static void AppendPhase4Report(StringBuilder report, Phase4MetricsData phase)
    {
        report.AppendLine();
        report.AppendLine("FASE 4 — RECOLHA DE ALIMENTOS");
        report.AppendLine(new string('-', 35));

        if (!HasStarted(phase?.timing))
        {
            report.AppendLine("Fase não iniciada.");
            return;
        }

        report.AppendLine("Concluída: " + YesNo(phase.timing.completed));
        report.AppendLine("Duração da fase: " + FormatDuration(phase.timing.durationSeconds));
        report.AppendLine("Tempo até recolher a lista: " +
                          FormatOptionalDuration(phase.timeToListPickupSeconds));
        report.AppendLine("Duração da recolha e entrega: " +
                          FormatDuration(phase.collectionAndDeliveryDurationSeconds));
        report.AppendLine("Alimentos agarrados: " + phase.totalFoodGrabs);
        report.AppendLine("Tentativas de entrega: " + phase.totalDeliveryAttempts);
        report.AppendLine("Entregas corretas: " + phase.acceptedDeliveries);
        report.AppendLine("Entregas incorretas: " + phase.rejectedDeliveries);
        report.AppendLine("Taxa de acerto: " + Percentage(phase.deliveryAccuracy));
        report.AppendLine("Manipulações desnecessárias: " + phase.unnecessaryManipulations);

        Dictionary<string, FoodTypeTotals> foodTotals = GetFoodTypeTotals(phase.foods);
        if (foodTotals.Count > 0)
        {
            report.AppendLine("Resultados por tipo de alimento:");
            foreach (KeyValuePair<string, FoodTypeTotals> item in foodTotals
                         .OrderBy(item => item.Key))
            {
                report.AppendLine(
                    "  • " + item.Key +
                    ": " + item.Value.grabs + " vezes agarrado; " +
                    item.Value.accepted + " entregas corretas; " +
                    item.Value.rejected + " entregas incorretas");
            }
        }

        if (phase.deliveryAttempts == null || phase.deliveryAttempts.Count == 0)
            return;

        report.AppendLine("Tentativas de entrega:");
        foreach (FoodDeliveryAttemptMetric attempt in phase.deliveryAttempts
                     .Where(attempt => attempt != null)
                     .OrderBy(attempt => attempt.attemptOrder))
        {
            report.AppendLine(
                "  • " + attempt.attemptOrder + ". " + attempt.foodType +
                ": " + TranslateDeliveryResult(attempt.result) +
                (string.Equals(attempt.result, "accepted", StringComparison.OrdinalIgnoreCase)
                    ? string.Empty
                    : " — " + TranslateDeliveryReason(attempt.reason)));
        }
    }

    // Soma manipulações, entregas aceites e rejeitadas por tipo de alimento.
    private static Dictionary<string, FoodTypeTotals> GetFoodTypeTotals(
        List<FoodAggregateMetric> foods)
    {
        Dictionary<string, FoodTypeTotals> totals =
            new Dictionary<string, FoodTypeTotals>(StringComparer.OrdinalIgnoreCase);

        if (foods == null)
            return totals;

        foreach (FoodAggregateMetric food in foods)
        {
            if (food == null)
                continue;

            string foodType = string.IsNullOrWhiteSpace(food.foodType)
                ? "Desconhecido"
                : food.foodType;

            if (!totals.TryGetValue(foodType, out FoodTypeTotals total))
            {
                total = new FoodTypeTotals();
                totals.Add(foodType, total);
            }

            total.grabs += food.grabCount;
            total.releasesWithoutDelivery += food.releaseWithoutDeliveryCount;
            total.accepted += food.acceptedDeliveries;
            total.rejected += food.rejectedDeliveries;
        }

        return totals;
    }

    // Adiciona colunas comuns de conclusão e duração de fase.
    private static void AddTimingColumns(
        Dictionary<string, string> row,
        string phaseName,
        PhaseTimingMetric timing)
    {
        if (timing == null)
            return;

        Set(row, phaseName + " concluída", YesNo(timing.completed));
        if (!string.IsNullOrWhiteSpace(timing.startedUtc))
            SetNumber(row, phaseName + " duração (s)", timing.durationSeconds);
    }

    // Confirma se uma fase tem início registado.
    private static bool HasStarted(PhaseTimingMetric timing)
    {
        return timing != null && !string.IsNullOrWhiteSpace(timing.startedUtc);
    }

    // Define valores textuais, evitando nulos no CSV final.
    private static void Set(
        Dictionary<string, string> row,
        string column,
        string value)
    {
        row[column] = value ?? string.Empty;
    }

    // Formata números reais segundo a cultura portuguesa.
    private static void SetNumber(
        Dictionary<string, string> row,
        string column,
        float value)
    {
        row[column] = Number(value);
    }

    // Formata números inteiros segundo a cultura portuguesa.
    private static void SetNumber(
        Dictionary<string, string> row,
        string column,
        int value)
    {
        row[column] = value.ToString(PortugueseCulture);
    }

    // Esconde valores negativos usados como "não registado".
    private static void SetOptionalNumber(
        Dictionary<string, string> row,
        string column,
        float value)
    {
        row[column] = value < 0f ? string.Empty : Number(value);
    }

    // Formatação curta de valores numéricos.
    private static string Number(float value)
    {
        return value.ToString("0.###", PortugueseCulture);
    }

    // Converte rácios em percentagens legíveis.
    private static string Percentage(float ratio)
    {
        return (ratio * 100f).ToString("0.#", PortugueseCulture) + "%";
    }

    // Converte segundos para um formato de duração compacto.
    private static string FormatDuration(float seconds)
    {
        if (seconds < 0f)
            return "—";

        TimeSpan duration = TimeSpan.FromSeconds(seconds);
        if (duration.TotalHours >= 1d)
            return duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture);

        return duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    // Apresenta durações opcionais com texto quando não existem dados.
    private static string FormatOptionalDuration(float seconds)
    {
        return seconds < 0f ? "não registado" : FormatDuration(seconds);
    }

    // Normaliza booleanos para texto português.
    private static string YesNo(bool value)
    {
        return value ? "Sim" : "Não";
    }

    // Junta sequências preservando a ordem registada.
    private static string Join(List<string> values, string separator)
    {
        if (values == null || values.Count == 0)
            return string.Empty;

        return string.Join(separator, values);
    }

    // Usa travessão visual para campos vazios nos relatórios.
    private static string EmptyAsDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "—" : value;
    }

    // Traduz motivos técnicos de fim para texto legível.
    private static string TranslateEndReason(string reason)
    {
        switch (reason)
        {
            case "experience_completed":
                return "Experiência concluída";
            case "application_quit":
                return "Aplicação encerrada";
            default:
                return string.IsNullOrWhiteSpace(reason) ? "—" : reason;
        }
    }

    // Traduz o resultado técnico da entrega.
    private static string TranslateDeliveryResult(string result)
    {
        return string.Equals(result, "accepted", StringComparison.OrdinalIgnoreCase)
            ? "aceite"
            : "rejeitada";
    }

    // Traduz motivos técnicos de rejeição de entrega.
    private static string TranslateDeliveryReason(string reason)
    {
        switch (reason)
        {
            case "list_not_collected":
                return "lista ainda não recolhida";
            case "food_not_requested":
                return "alimento não pedido";
            case "required_quantity_already_complete":
                return "quantidade necessária já completa";
            case "accepted":
                return "aceite";
            default:
                return string.IsNullOrWhiteSpace(reason) ? "motivo desconhecido" : reason;
        }
    }

    // Escreve uma linha completa respeitando o delimitador escolhido.
    private static void AppendDelimitedRow(
        StringBuilder output,
        IEnumerable<string> values,
        char delimiter)
    {
        bool first = true;
        foreach (string value in values)
        {
            if (!first)
                output.Append(delimiter);

            AppendDelimitedCell(output, value, delimiter);
            first = false;
        }

        output.AppendLine();
    }

    // Escapa células quando contêm delimitadores, aspas ou quebras de linha.
    private static void AppendDelimitedCell(
        StringBuilder output,
        string value,
        char delimiter)
    {
        string safeValue = value ?? string.Empty;
        bool requiresQuotes =
            safeValue.IndexOf(delimiter) >= 0 ||
            safeValue.Contains("\"") ||
            safeValue.Contains("\r") ||
            safeValue.Contains("\n");

        if (!requiresQuotes)
        {
            output.Append(safeValue);
            return;
        }

        output.Append('"');
        output.Append(safeValue.Replace("\"", "\"\""));
        output.Append('"');
    }

    // Remove caracteres inválidos antes de criar nomes de ficheiro.
    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "sessao";

        string result = value;
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
            result = result.Replace(invalidCharacter, '_');

        return result;
    }

    // Acumulador interno usado nos resumos por tipo de alimento.
    private sealed class FoodTypeTotals
    {
        public int grabs;
        public int releasesWithoutDelivery;
        public int accepted;
        public int rejected;
    }
}
