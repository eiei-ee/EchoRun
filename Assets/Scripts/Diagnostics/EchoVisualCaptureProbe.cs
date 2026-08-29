using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public sealed class EchoVisualCaptureProbe : MonoBehaviour
{
    public const string CaptureDistancesArgumentPrefix =
        "-echo-qa-capture-distances=";

    private const string CaptureDirectoryName = "VisualCaptures";
    private const float DistanceTolerance = 0.0001f;

    private float[] _targetDistances;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateWhenExplicitlyRequested()
    {
        float[] distances = ParseCaptureDistances(
            Environment.GetCommandLineArgs());
        if (distances.Length == 0) return;

        GameObject host = new GameObject("EchoVisualCaptureProbe_Runtime");
        DontDestroyOnLoad(host);
        EchoVisualCaptureProbe probe =
            host.AddComponent<EchoVisualCaptureProbe>();
        probe._targetDistances = distances;
    }

    private IEnumerator Start()
    {
        if (_targetDistances == null || _targetDistances.Length == 0)
        {
            yield break;
        }

        int targetIndex = 0;
        string outputDirectory = null;

        while (targetIndex < _targetDistances.Length)
        {
            GameManager gameManager = GameManager.Instance;
            float actualDistance = gameManager != null
                ? gameManager.Distance
                : 0f;
            GameState state = gameManager != null
                ? gameManager.State
                : GameState.Menu;
            float targetDistance = _targetDistances[targetIndex];

            Debug.Log("ECHO_VISUAL_CAPTURE_FRAME target="
                      + FormatDistance(targetDistance)
                      + " actual=" + FormatDistance(actualDistance)
                      + " state=" + state);

            if (gameManager == null || state != GameState.Playing
                || actualDistance + DistanceTolerance < targetDistance)
            {
                yield return null;
                continue;
            }

            yield return new WaitForEndOfFrame();

            gameManager = GameManager.Instance;
            actualDistance = gameManager != null
                ? gameManager.Distance
                : actualDistance;
            outputDirectory = outputDirectory ?? CaptureOutputDirectory();
            Directory.CreateDirectory(outputDirectory);

            string fileName = BuildCaptureFileName(targetDistance,
                actualDistance, Screen.width, Screen.height);
            string capturePath = Path.Combine(outputDirectory, fileName);
            ScreenCapture.CaptureScreenshot(capturePath);
            targetIndex++;

            // Give Unity a frame to submit the screenshot request before
            // advancing to the next target or freezing the finished run.
            yield return null;
        }

        Debug.Log("ECHO_VISUAL_CAPTURE_COMPLETE count="
                  + _targetDistances.Length
                  + " directory=" + outputDirectory);
        Time.timeScale = 0f;
        enabled = false;
    }

    public static float[] ParseCaptureDistances(string[] arguments)
    {
        if (arguments == null || arguments.Length == 0)
            return Array.Empty<float>();

        SortedSet<float> parsed = new SortedSet<float>();
        for (int argumentIndex = 0;
             argumentIndex < arguments.Length;
             argumentIndex++)
        {
            string argument = arguments[argumentIndex];
            if (string.IsNullOrEmpty(argument)
                || !argument.StartsWith(CaptureDistancesArgumentPrefix,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            string values = argument.Substring(
                CaptureDistancesArgumentPrefix.Length);
            string[] tokens = values.Split(',');
            for (int tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
            {
                if (!float.TryParse(tokens[tokenIndex].Trim(),
                        NumberStyles.Float, CultureInfo.InvariantCulture,
                        out float distance)
                    || float.IsNaN(distance)
                    || float.IsInfinity(distance)
                    || distance < 0f)
                    continue;

                parsed.Add(distance);
            }
        }

        if (parsed.Count == 0) return Array.Empty<float>();
        float[] result = new float[parsed.Count];
        parsed.CopyTo(result);
        return result;
    }

    private static string CaptureOutputDirectory()
    {
#if UNITY_EDITOR
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..",
            "Builds", "Windows", CaptureDirectoryName));
#else
        string executableDirectory = Path.GetDirectoryName(
            Application.dataPath);
        return Path.GetFullPath(Path.Combine(
            string.IsNullOrEmpty(executableDirectory)
                ? Application.dataPath
                : executableDirectory,
            CaptureDirectoryName));
#endif
    }

    private static string BuildCaptureFileName(float targetDistance,
        float actualDistance, int width, int height)
    {
        return "target-" + FileSafeDistance(targetDistance)
               + "m_actual-" + FileSafeDistance(actualDistance)
               + "m_" + Mathf.Max(0, width)
               + "x" + Mathf.Max(0, height) + ".png";
    }

    private static string FileSafeDistance(float distance)
    {
        return FormatDistance(distance).Replace('.', 'p');
    }

    private static string FormatDistance(float distance)
    {
        return distance.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
