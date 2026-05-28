using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BuildConfig
{
    const string BundleId = "com.eieiee.templerun";
    const string CompanyName = "Eiei-ee";
    const string ProductName = "TempleRun";

    [MenuItem("Tools/Build Android")]
    static void BuildAndroid()
    {
        ConfigurePlayerSettings();
        ConfigureAndroid();
        EnsureSceneInBuild();

        string outputDir = "Builds/Android";
        EnsureDirectory(outputDir);
        string outputPath = $"{outputDir}/TempleRun.apk";

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        }

        BuildPipeline.BuildPlayer(opts);
        Debug.Log($"Android build complete: {outputPath}");
    }

    [MenuItem("Tools/Build WebGL")]
    static void BuildWebGL()
    {
        ConfigurePlayerSettings();
        ConfigureWebGL();
        EnsureSceneInBuild();

        string outputDir = "Builds/WebGL";
        EnsureDirectory(outputDir);

        BuildPlayerOptions opts = new BuildPlayerOptions
        {
            scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
            locationPathName = outputDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        }

        BuildPipeline.BuildPlayer(opts);
        Debug.Log($"WebGL build complete: {outputDir}");
    }

    static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.WebGL, BundleId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleId);
    }

    static void ConfigureAndroid()
    {
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.bundleVersionCode = 1;

        // Use default debug keystore
        PlayerSettings.Android.useCustomKeystore = false;

        // App category
        PlayerSettings.Android.androidIsGame = true;

        // Screen
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
    }

    static void ConfigureWebGL()
    {
        // Memory: 256MB initial, 2048MB max, geometric growth
        PlayerSettings.WebGL.memorySize = 256;
        PlayerSettings.WebGL.initialMemorySize = 256;
        PlayerSettings.WebGL.maximumMemorySize = 2048;
        PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;
        PlayerSettings.WebGL.memoryGeometricGrowthStep = 0.2f;
        PlayerSettings.WebGL.memoryGeometricGrowthCap = 256;

        // Strip exceptions for smaller build
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;

        // Gzip compression
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        // Default resolution
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 600;

        // Disable threads for compatibility
        PlayerSettings.WebGL.threadsSupport = false;

        // Data caching for faster reloads
        PlayerSettings.WebGL.dataCaching = true;

        // Linker target
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
    }

    static void EnsureSceneInBuild()
    {
        var scenes = EditorBuildSettings.scenes;
        string currentPath = SceneManager.GetActiveScene().path;

        foreach (var s in scenes)
        {
            if (s.path == currentPath)
                return;
        }

        var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
        scenes.CopyTo(newScenes, 0);
        newScenes[scenes.Length] = new EditorBuildSettingsScene(currentPath, true);
        EditorBuildSettings.scenes = newScenes;

        Debug.Log($"Added scene to build: {currentPath}");
    }

    static void EnsureDirectory(string path)
    {
        string fullPath = System.IO.Path.Combine(Application.dataPath, "../", path);
        if (!System.IO.Directory.Exists(fullPath))
            System.IO.Directory.CreateDirectory(fullPath);
    }
}
