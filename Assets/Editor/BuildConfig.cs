using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        ConfigureBaseSettings();
        ConfigureAndroid();
        EnsureSceneInBuild();

        string outputPath = "Builds/Android/EchoRun.apk";
        EnsureDirectory("Builds/Android");

        BuildReport report = BuildPipeline.BuildPlayer(
            GetScenePaths(), outputPath, BuildTarget.Android, BuildOptions.None);
        EnsureBuildSucceeded(report, "Android");
        Debug.Log($"Android build complete: {outputPath}");
    }

    [MenuItem("Tools/Build iOS")]
    public static void BuildIOS()
    {
        OpenPrimaryScene();

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);

        ConfigureBaseSettings();
        ConfigureIOS();
        EnsureSceneInBuild();

        string outputDir = "Builds/iOS";
        EnsureDirectory(outputDir);

        BuildReport report = BuildPipeline.BuildPlayer(
            GetScenePaths(), outputDir, BuildTarget.iOS, BuildOptions.None);
        EnsureBuildSucceeded(report, "iOS");
        Debug.Log($"iOS Xcode project generated: {outputDir}");
    }

    [MenuItem("Tools/Build WebGL")]
    public static void BuildWebGL()
    {
        OpenPrimaryScene();
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        ConfigureBaseSettings();
        ConfigureWebGL();
        EnsureSceneInBuild();

        string outputDir = "Builds/WebGL";
        EnsureDirectory(outputDir);

        BuildReport report = BuildPipeline.BuildPlayer(
            GetScenePaths(), outputDir, BuildTarget.WebGL, BuildOptions.None);
        EnsureBuildSucceeded(report, "WebGL");
        OptimizeWebGLShell(outputDir);
        Debug.Log($"WebGL build complete: {outputDir}");
    }

    [MenuItem("Tools/Build Windows 64-bit")]
    public static void BuildWindows()
    {
        OpenPrimaryScene();
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

        ConfigureBaseSettings();
        ConfigureWindows();
        EnsureSceneInBuild();

        string outputPath = "Builds/Windows/EchoRun.exe";
        EnsureDirectory("Builds/Windows");

        BuildReport report = BuildPipeline.BuildPlayer(
            GetScenePaths(), outputPath, BuildTarget.StandaloneWindows64,
            BuildOptions.CompressWithLz4HC);
        EnsureBuildSucceeded(report, "Windows 64-bit");
        Debug.Log($"Windows build complete: {outputPath}");
    }

    static void EnsureBuildSucceeded(BuildReport report, string platform)
    {
        if (report != null && report.summary.result == BuildResult.Succeeded) return;

        string result = report == null ? "No build report" : report.summary.result.ToString();
        int errors = report == null ? 0 : report.summary.totalErrors;
        throw new BuildFailedException(
            $"{platform} build failed: {result} ({errors} errors).");
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
        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCompilerConfiguration(
            NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.Android.androidIsGame = true;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        QualitySettings.SetQualityLevel(2, true);
        QualitySettings.vSyncCount = 0;
    }

    static void ConfigureIOS()
    {
        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCompilerConfiguration(
            NamedBuildTarget.iOS, Il2CppCompilerConfiguration.Release);
        PlayerSettings.iOS.targetOSVersionString = "13.0";
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.iOS.buildNumber = "1";
        PlayerSettings.iOS.appleDeveloperTeamID = "";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.insecureHttpOption = InsecureHttpOption.NotAllowed;
        PlayerSettings.iOS.hideHomeButton = true;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

        QualitySettings.vSyncCount = 0;
    }

    static void ConfigureWebGL()
    {
        PlayerSettings.WebGL.memorySize = 256;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.linkerTarget = WebGLLinkerTarget.Wasm;
        PlayerSettings.WebGL.threadsSupport = false;

        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        QualitySettings.SetQualityLevel(2, true);
        QualitySettings.vSyncCount = 0;
    }

    static void ConfigureWindows()
    {
        PlayerSettings.SetScriptingBackend(
            NamedBuildTarget.Standalone, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetIl2CppCompilerConfiguration(
            NamedBuildTarget.Standalone, Il2CppCompilerConfiguration.Release);
        PlayerSettings.SetManagedStrippingLevel(
            NamedBuildTarget.Standalone, ManagedStrippingLevel.Medium);
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        QualitySettings.SetQualityLevel(3, true);
        QualitySettings.vSyncCount = 0;
    }

    static void OptimizeWebGLShell(string outputDir)
    {
        string indexPath = Path.Combine(Application.dataPath, "../", outputDir, "index.html");
        if (!File.Exists(indexPath))
            throw new BuildFailedException("WebGL index.html was not generated.");

        const string commented = "// config.autoSyncPersistentDataPath = true;";
        const string enabled = "config.autoSyncPersistentDataPath = true;";
        string html = File.ReadAllText(indexPath);

        if (html.Contains(commented))
            html = html.Replace(commented, enabled);

        if (!html.Contains(enabled))
        {
            throw new BuildFailedException(
                "WebGL template does not expose autoSyncPersistentDataPath.");
        }

        const string responsiveStyles = @"
    <meta name=""viewport"" content=""width=device-width,height=device-height,initial-scale=1,maximum-scale=1,user-scalable=no,viewport-fit=cover"">
    <style id=""echorun-responsive-shell"">
      html, body { width: 100%; height: 100%; margin: 0; overflow: hidden; background: #090c12; }
      body { display: flex; align-items: center; justify-content: center; overscroll-behavior: none; }
      canvas { display: block; touch-action: none; -webkit-user-select: none; user-select: none; }
      #tuanjie-container.tuanjie-desktop, #unity-container.unity-desktop {
        width: min(100vw, calc((100vh - 42px) * 16 / 9));
        max-width: 100vw;
      }
      #tuanjie-container.tuanjie-desktop canvas, #unity-container.unity-desktop canvas {
        width: 100% !important;
        height: auto !important;
        aspect-ratio: 16 / 9;
      }
      #tuanjie-container.tuanjie-mobile, #unity-container.unity-mobile,
      #tuanjie-container.tuanjie-mobile canvas, #unity-container.unity-mobile canvas {
        width: 100% !important;
        height: 100% !important;
      }
      @media (max-width: 900px), (max-height: 620px) {
        #tuanjie-footer, #unity-footer { display: none; }
        #tuanjie-container.tuanjie-desktop, #unity-container.unity-desktop {
          width: min(100vw, calc(100vh * 16 / 9));
        }
      }
    </style>
";
        if (!html.Contains("echorun-responsive-shell"))
            html = html.Replace("</head>", responsiveStyles + "  </head>");

        html = html.Replace(
            "// config.devicePixelRatio = 1;",
            "config.devicePixelRatio = Math.min(window.devicePixelRatio || 1, 1.25);");
        html = html.Replace(
            "if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {",
            "config.devicePixelRatio = Math.min(window.devicePixelRatio || 1, 1.5);\n\n"
            + "      if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {");

        File.WriteAllText(indexPath, html, new UTF8Encoding(false));
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
