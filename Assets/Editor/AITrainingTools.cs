using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AITrainingTools
{
    [MenuItem("Tools/AI/Run 20K Segment Simulation")]
    public static void RunDefaultSimulation()
    {
        var config = new AITrainingSimulationConfig();
        AITrainingSimulationResult result = AITrainingSimulator.Run(
            config, EchoRunSaveSystem.GetDirectorWeights());

        string directory = GetTrainingDataDirectory();
        string path = Path.Combine(directory,
            "simulation-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(result, true));
        AssetDatabase.Refresh();
        Debug.Log(string.Format(
            "AI simulation complete: {0} segments, reward {1:0.000}, "
            + "survival {2:0.0%}\n{3}",
            result.totalSegments, result.meanReward,
            result.survivalRate, path));
    }

    [MenuItem("Tools/AI/Export Latest Run Telemetry")]
    public static void ExportLatestRunTelemetry()
    {
        string path = AIRunTelemetry.ExportLatestRun(
            GetTrainingDataDirectory());
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("No completed AI run telemetry is available.");
            return;
        }
        AssetDatabase.Refresh();
        Debug.Log("AI run telemetry exported: " + path);
    }

    private static string GetTrainingDataDirectory()
    {
        string directory = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "TrainingData"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
