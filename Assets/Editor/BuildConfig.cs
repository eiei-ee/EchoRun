using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BuildConfig
{
    const string BundleId = "com.eieiee.echorun";
    const string CompanyName = "Eiei-ee";
    const string ProductName = "EchoRun";

    static string[] GetScenePaths()
    {
        var list = new List<string>();
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled)
                list.Add(s.path);

        if (list.Count == 0)
        {
            string path = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(path))
                list.Add(path);
        }

        return list.ToArray();
    }

    static void OpenPrimaryScene()
    {
        string[] paths = GetScenePaths();
        if (paths.Length == 0 || string.IsNullOrEmpty(paths[0])) return;
        if (SceneManager.GetActiveScene().path == paths[0]) return;
        EditorSceneManager.OpenScene(paths[0], OpenSceneMode.Single);
    }

    [MenuItem("Tools/Build Android")]
    public static void BuildAndroid()
    {
        OpenPrimaryScene();
        BuildScene.Build();

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        ConfigureBaseSettings();
        ConfigureAndroid();
        EnsureSceneInBuild();

        string outputPath = "Builds/Android/EchoRun.apk";
        EnsureDirectory("Builds/Android");

        BuildPipeline.BuildPlayer(GetScenePaths(), outputPath, BuildTarget.Android, BuildOptions.None);
        Debug.Log($"Android build complete: {outputPath}");
    }

    [MenuItem("Tools/Build iOS")]
    public static void BuildIOS()
    {
        OpenPrimaryScene();
        BuildScene.Build();

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

        ConfigureBaseSettings();
        ConfigureIOS();
        EnsureSceneInBuild();

        string outputDir = "Builds/iOS";
        EnsureDirectory(outputDir);

        BuildPipeline.BuildPlayer(GetScenePaths(), outputDir, BuildTarget.iOS, BuildOptions.None);
        Debug.Log($"iOS Xcode project generated: {outputDir}");
    }

    [MenuItem("Tools/Build WebGL")]
    public static void BuildWebGL()
    {
        OpenPrimaryScene();
        BuildScene.Build();
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        ConfigureBaseSettings();
        ConfigureWebGL();
        EnsureSceneInBuild();

        string outputDir = "Builds/WebGL";
        EnsureDirectory(outputDir);

        BuildPipeline.BuildPlayer(GetScenePaths(), outputDir, BuildTarget.WebGL, BuildOptions.None);
        Debug.Log($"WebGL build complete: {outputDir}");
    }

    static void ConfigureBaseSettings()
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.WebGL, BundleId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleId);
    }

    static void ConfigureAndroid()
    {
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.Android.androidIsGame = true;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        QualitySettings.vSyncCount = 0;
    }

    static void ConfigureIOS()
    {
        PlayerSettings.iOS.targetOSVersionString = "13.0";
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.iOS.buildNumber = "1";
        PlayerSettings.iOS.appleDeveloperTeamID = "";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
        PlayerSettings.iOS.hideHomeButton = true;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        QualitySettings.vSyncCount = 0;
    }

    static void ConfigureWebGL()
    {
        PlayerSettings.WebGL.memorySize = 256;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = false;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        PlayerSettings.WebGL.threadsSupport = false;

        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 600;
    }

    static void EnsureSceneInBuild()
    {
        var scenes = EditorBuildSettings.scenes;
        string currentPath = SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(currentPath)) return;

        foreach (var s in scenes)
            if (s.path == currentPath) return;

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
