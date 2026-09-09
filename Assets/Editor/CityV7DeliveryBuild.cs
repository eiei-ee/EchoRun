using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CityV7DeliveryBuild
{
    public static void Build()
    {
        if (Resources.Load<GameObject>("CityV7/Chunk0") == null)
            throw new InvalidOperationException("City V7 resources missing.");
        string previousProduct = PlayerSettings.productName;
        try
        {
            PlayerSettings.productName = "EchoRun-CityV7-MainCharacter";
            const string output = "TestResults/CityV7Main/Windows/EchoRun.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(
                new[] { "Assets/Scenes/SampleScene.scene" }, output,
                BuildTarget.StandaloneWindows64, BuildOptions.CompressWithLz4HC);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("City V7 build failed: " + report.summary.result);
            Debug.Log("CITY_V7_MAIN_BUILD_OK " + Path.GetFullPath(output));
        }
        finally { PlayerSettings.productName = previousProduct; }
    }
}
