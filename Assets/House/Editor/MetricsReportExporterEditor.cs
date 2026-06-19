using System.IO;
using UnityEditor;
using UnityEngine;

public static class MetricsReportExporterEditor
{
    [MenuItem("MindBridgeXR/Métricas/Gerar relatórios da pasta copiada")]
    public static void RebuildCopiedMetricsFolder()
    {
        string defaultFolder = Path.Combine(
            Directory.GetParent(Application.dataPath).FullName,
            "MindBridgeXR_Metrics");

        if (!Directory.Exists(defaultFolder))
        {
            RebuildSelectedFolder();
            return;
        }

        Generate(defaultFolder);
    }

    [MenuItem("MindBridgeXR/Métricas/Escolher pasta e gerar relatórios")]
    public static void RebuildSelectedFolder()
    {
        string selectedFolder = EditorUtility.OpenFolderPanel(
            "Escolher pasta com os ficheiros de métricas",
            Directory.GetParent(Application.dataPath).FullName,
            string.Empty);

        if (string.IsNullOrWhiteSpace(selectedFolder))
            return;

        Generate(selectedFolder);
    }

    private static void Generate(string metricsDirectory)
    {
        MetricsReportExporter.RebuildAll(metricsDirectory);

        EditorUtility.DisplayDialog(
            "Relatórios gerados",
            "Foram gerados:\n\n" +
            "• MindBridgeXR_Comparacao.csv\n" +
            "• MindBridgeXR_Comparacao_Excel.tsv\n" +
            "• Relatorios/<sessão>_relatorio.txt",
            "OK");

        EditorUtility.RevealInFinder(metricsDirectory);
    }
}
