using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
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

    [MenuItem("Tools/Build WeChat MiniGame v0")]
    public static void BuildWeixinMiniGameV0()
    {
        OpenPrimaryScene();
        EditorUserBuildSettings.SwitchActiveBuildTarget(
            BuildTargetGroup.WeixinMiniGame, BuildTarget.WeixinMiniGame);

        UIOrientation previousOrientation = PlayerSettings.defaultInterfaceOrientation;
        bool previousPortrait = PlayerSettings.allowedAutorotateToPortrait;
        bool previousPortraitUpsideDown =
            PlayerSettings.allowedAutorotateToPortraitUpsideDown;
        bool previousLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeLeft;
        bool previousLandscapeRight = PlayerSettings.allowedAutorotateToLandscapeRight;
        ColorSpace previousColorSpace = PlayerSettings.colorSpace;
        int previousQualityLevel = QualitySettings.GetQualityLevel();

        try
        {
            ConfigureBaseSettings();
            ConfigureWeixinMiniGame();
            EnsureSceneInBuild();

            const string legacySdkConfigPath =
                "Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset";
            const string packageSdkConfigPath =
                "Packages/com.qq.weixin.minigame/Editor/MiniGameConfig.asset";
            bool hasOfficialSdk =
                AssetDatabase.LoadAssetAtPath<Object>(packageSdkConfigPath) != null
                || AssetDatabase.LoadAssetAtPath<Object>(legacySdkConfigPath) != null;
            if (!hasOfficialSdk)
            {
                throw new BuildFailedException(
                    "Missing official WeChat Mini Game SDK package "
                    + "'com.qq.weixin.minigame' (WX-WASM-SDK-V2). "
                    + "Install it from the WeChat Build Profile before building.");
            }

            const string outputDir = "Builds/WeixinMiniGameV0-Profile";
            EnsureDirectory(outputDir);

            PlayerSettings.MiniGame.SetActiveSubplatform(
                MiniGameBuildSubtarget.WeChat, true);
            const string profileAssetPath =
                "Assets/WeixinMiniGame/BuildProfiles/WeChatV0.asset";
            BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(
                profileAssetPath);
            if (profile != null
                && (profile.miniGameSettings == null
                    || profile.miniGameSettings.GetType().FullName
                        != "WeChatWASM.WeixinMiniGameSettings"))
            {
                AssetDatabase.DeleteAsset(profileAssetPath);
                profile = null;
            }
            if (profile == null)
            {
                EnsureDirectory("Assets/WeixinMiniGame/BuildProfiles");
                profile = BuildProfile.CreateInstance(
                    BuildTarget.WeixinMiniGame, MiniGameBuildSubtarget.WeChat);
                if (profile != null)
                    AssetDatabase.CreateAsset(profile, profileAssetPath);
            }
            if (profile == null)
                throw new BuildFailedException(
                    "Unable to create WeChat build profile.");

            profile.buildPath = outputDir;
            ConfigureWeixinBuildProfile(profile, outputDir);
            profile.CreatePlayerSettingsFromGlobal();
            ConfigureWeixinProfilePlayerSettings(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            BuildMiniGameError result = BuildPipeline.BuildMiniGame(
                profile, BuildOptions.None);
            if (result != BuildMiniGameError.Succeeded)
                throw new BuildFailedException(
                    $"WeChat MiniGame profile build failed: {result}.");

            Debug.Log($"WeChat MiniGame v0 profile build complete: {outputDir}");
        }
        finally
        {
            PlayerSettings.defaultInterfaceOrientation = previousOrientation;
            PlayerSettings.allowedAutorotateToPortrait = previousPortrait;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown =
                previousPortraitUpsideDown;
            PlayerSettings.allowedAutorotateToLandscapeLeft = previousLandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeRight = previousLandscapeRight;
            PlayerSettings.colorSpace = previousColorSpace;
            QualitySettings.SetQualityLevel(previousQualityLevel, true);
            AssetDatabase.SaveAssets();
        }
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

    static void ConfigureWeixinBuildProfile(BuildProfile profile, string outputDir)
    {
        object settings = profile.miniGameSettings;
        if (settings == null)
            throw new BuildFailedException("WeChat MiniGame settings are missing.");

        var projectConfField = settings.GetType().GetField("ProjectConf");
        object projectConf = projectConfField?.GetValue(settings);
        if (projectConf == null)
            throw new BuildFailedException("WeChat ProjectConf is missing.");

        var relativeDstField = projectConf.GetType().GetField("relativeDST");
        var dstField = projectConf.GetType().GetField("DST");
        if (relativeDstField == null || dstField == null)
            throw new BuildFailedException("WeChat export path fields are missing.");

        relativeDstField.SetValue(projectConf, outputDir);
        dstField.SetValue(projectConf, Path.GetFullPath(outputDir));

        var projectNameField = projectConf.GetType().GetField("projectName");
        projectNameField?.SetValue(projectConf, ProductName);

        string appId = System.Environment.GetEnvironmentVariable(
            "WECHAT_MINIGAME_APPID");
        if (!string.IsNullOrWhiteSpace(appId))
        {
            var appIdField = projectConf.GetType().GetField("Appid");
            if (appIdField == null)
                throw new BuildFailedException("WeChat Appid field is missing.");
            appIdField.SetValue(projectConf, appId.Trim());
        }
    }

    static void ConfigureWeixinProfilePlayerSettings(BuildProfile profile)
    {
        if (profile.playerSettings == null)
            throw new BuildFailedException(
                "WeChat Build Profile PlayerSettings are missing.");

        var serializedSettings = new SerializedObject(profile.playerSettings);
        SerializedProperty colorSpace =
            serializedSettings.FindProperty("m_ActiveColorSpace");
        if (colorSpace == null)
            throw new BuildFailedException(
                "WeChat Build Profile color-space setting is missing.");

        colorSpace.intValue = (int)ColorSpace.Gamma;
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigureBaseSettings()
    {
        PlayerSettings.companyName = CompanyName;
        PlayerSettings.productName = ProductName;

        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, BundleId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.WebGL, BundleId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, BundleId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, BundleId);
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.WeixinMiniGame, BundleId);
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

    static void ConfigureWeixinMiniGame()
    {
        PlayerSettings.MiniGame.SetActiveSubplatform(
            MiniGameBuildSubtarget.WeChat, true);
        PlayerSettings.MiniGame.memorySize = 256;
        PlayerSettings.MiniGame.useSlimMetaFileFormat = true;
        PlayerSettings.MiniGame.serializedFileTypeTreeMemoryOptimization = true;
        PlayerSettings.MiniGame.analyzeBuildSize = true;
        PlayerSettings.MiniGame.useEmbeddedResources = true;
        PlayerSettings.MiniGame.threadsSupport = false;
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.SetManagedStrippingLevel(
            BuildTargetGroup.WeixinMiniGame, ManagedStrippingLevel.High);
        PlayerSettings.insecureHttpOption = InsecureHttpOption.NotAllowed;
        QualitySettings.SetQualityLevel(1, true);
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
