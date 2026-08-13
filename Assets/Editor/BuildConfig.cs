using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
        int previousQualityLevel = QualitySettings.GetQualityLevel();
        int previousVSyncCount = QualitySettings.vSyncCount;

        try
        {
            OpenPrimaryScene();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            ConfigureBaseSettings();
            ConfigureWebGL();
            EnsureSceneInBuild();

            const string outputDir = "Builds/WebGL";
            RecreateBuildDirectory(outputDir);

            BuildReport report = BuildPipeline.BuildPlayer(
                GetScenePaths(), outputDir, BuildTarget.WebGL, BuildOptions.None);
            EnsureBuildSucceeded(report, "WebGL");
            OptimizeWebGLShell(outputDir);
            Debug.Log($"WebGL build complete: {outputDir}");
        }
        finally
        {
            QualitySettings.SetQualityLevel(previousQualityLevel, true);
            QualitySettings.vSyncCount = previousVSyncCount;
        }
    }

    [MenuItem("Tools/Build WeChat MiniGame v0")]
    public static void BuildWeixinMiniGameV0()
    {
        EnsureOfficialWeixinSdkInstalled();
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
        bool previousGpuSkinning = PlayerSettings.gpuSkinning;
        int previousQualityLevel = QualitySettings.GetQualityLevel();

        try
        {
            ConfigureBaseSettings();
            ConfigureWeixinMiniGame();
            EnsureSceneInBuild();

            const string outputDir = "Builds/WeixinMiniGameV0-Clean";
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

            FinalizeWeixinMiniGameOutput(outputDir);
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
            PlayerSettings.gpuSkinning = previousGpuSkinning;
            QualitySettings.SetQualityLevel(previousQualityLevel, true);
            AssetDatabase.SaveAssets();
        }
    }

    static void EnsureOfficialWeixinSdkInstalled()
    {
        const string packageName = "com.qq.weixin.minigame";
        UnityEditor.PackageManager.PackageInfo[] packages =
            UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
        for (int i = 0; i < packages.Length; i++)
        {
            if (packages[i] != null && packages[i].name == packageName)
                return;
        }

        const string legacySdkConfigPath =
            "Assets/WX-WASM-SDK-V2/Editor/MiniGameConfig.asset";
        if (AssetDatabase.LoadAssetAtPath<Object>(legacySdkConfigPath) != null)
            return;

        throw new BuildFailedException(
            "Missing official WeChat Mini Game SDK package "
            + "'com.qq.weixin.minigame' (WX-WASM-SDK-V2). "
            + "Install it from the WeChat Build Profile before building.");
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

        // Keep the profile and the generated JavaScript in agreement. The SDK
        // template contains $COMPRESS_DATA_PACKAGE and an interrupted/skipped
        // conversion pass otherwise leaves a ReferenceError in check-version.js.
        var compressDataPackageField =
            projectConf.GetType().GetField("compressDataPackage");
        if (compressDataPackageField == null)
            throw new BuildFailedException(
                "WeChat compressDataPackage field is missing.");
        compressDataPackageField.SetValue(projectConf, false);

        var compileOptionsField = settings.GetType().GetField("CompileOptions");
        object compileOptions = compileOptionsField?.GetValue(settings);
        var cleanBuildField =
            compileOptions?.GetType().GetField("CleanBuild");
        if (cleanBuildField == null)
            throw new BuildFailedException(
                "WeChat CleanBuild field is missing.");
        cleanBuildField.SetValue(compileOptions, true);

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
        SerializedProperty gpuSkinning =
            serializedSettings.FindProperty("gpuSkinning");
        if (gpuSkinning == null)
            throw new BuildFailedException(
                "WeChat Build Profile GPU-skinning setting is missing.");

        gpuSkinning.boolValue = false;
        serializedSettings.ApplyModifiedPropertiesWithoutUndo();

        // BuildProfile serializes its PlayerSettings as YAML lines. Updating the
        // transient PlayerSettings object alone does not persist this value in
        // this editor version, so write the profile's serialized copy as well.
        var serializedProfile = new SerializedObject(profile);
        SerializedProperty settingsYaml =
            serializedProfile.FindProperty("m_PlayerSettingsYaml");
        SerializedProperty settingsLines =
            settingsYaml?.FindPropertyRelative("m_Settings");
        bool updatedGpuSkinning = false;
        if (settingsLines != null && settingsLines.isArray)
        {
            for (int i = 0; i < settingsLines.arraySize; i++)
            {
                SerializedProperty line = settingsLines.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("line");
                if (line == null || !line.stringValue.Contains("gpuSkinning:"))
                    continue;

                line.stringValue = "| gpuSkinning: 0";
                updatedGpuSkinning = true;
                break;
            }
        }

        if (!updatedGpuSkinning)
            throw new BuildFailedException(
                "WeChat Build Profile serialized GPU-skinning setting is missing.");

        serializedProfile.ApplyModifiedPropertiesWithoutUndo();
    }

    static void FinalizeWeixinMiniGameOutput(string outputDir)
    {
        string miniGameDir = Path.GetFullPath(Path.Combine(outputDir, "minigame"));
        string checkVersionPath = Path.Combine(miniGameDir, "check-version.js");
        string gameJsPath = Path.Combine(miniGameDir, "game.js");
        string gameJsonPath = Path.Combine(miniGameDir, "game.json");

        if (!File.Exists(checkVersionPath)
            || !File.Exists(gameJsPath)
            || !File.Exists(gameJsonPath))
        {
            throw new BuildFailedException(
                "WeChat SDK did not generate the required minigame files.");
        }

        ReplaceGeneratedText(
            checkVersionPath, "$COMPRESS_DATA_PACKAGE", "false");
        ReplaceGeneratedText(
            gameJsPath, "$COMPRESS_DATA_PACKAGE", "false");

        string gameJs = File.ReadAllText(gameJsPath, Encoding.UTF8);
        const string checkVersionImport =
            "import checkVersion from './check-version';";
        if (!gameJs.Contains(checkVersionImport))
            throw new BuildFailedException(
                "WeChat game.js does not contain the version-check import.");
        string versionModuleName = "check-version-echorun-"
            + System.DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string versionModulePath = Path.Combine(
            miniGameDir, versionModuleName + ".js");
        File.Copy(checkVersionPath, versionModulePath, true);
        gameJs = gameJs.Replace(
            checkVersionImport,
            "import checkVersion from './" + versionModuleName + "';");

        // WeChat DevTools can retain the unversioned module in its module cache.
        // Every SDK import must point at the cache-busted copy, not only game.js.
        RewriteCheckVersionImports(miniGameDir, versionModuleName);

        string compressBootstrapName = "compress-config-echorun-"
            + System.DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        string compressBootstrapPath = Path.Combine(
            miniGameDir, compressBootstrapName + ".js");
        File.WriteAllText(
            compressBootstrapPath,
            "GameGlobal['$' + 'COMPRESS_DATA_PACKAGE'] = false;\n",
            new UTF8Encoding(false));
        gameJs = "import './" + compressBootstrapName + "';\n" + gameJs;

        const string startGame = "gameManager.startGame();";
        const string preferredFrameRate =
            "wx.setPreferredFramesPerSecond(60);";
        if (!gameJs.Contains(preferredFrameRate))
        {
            if (!gameJs.Contains(startGame))
                throw new BuildFailedException(
                    "WeChat game.js does not contain the game startup call.");
            gameJs = gameJs.Replace(
                startGame, startGame + "\n        " + preferredFrameRate);
        }
        File.WriteAllText(gameJsPath, gameJs, new UTF8Encoding(false));

        string gameJson = File.ReadAllText(gameJsonPath, Encoding.UTF8);
        const string objectArrayPattern =
            @"""parallelPreloadSubpackages""\s*:\s*\[\s*"
            + @"\{\s*""name""\s*:\s*""wasmcode""\s*\}\s*,\s*"
            + @"\{\s*""name""\s*:\s*""data-package""\s*\}\s*\]";
        const string stringArray =
            "\"parallelPreloadSubpackages\" : [\n"
            + "    \"wasmcode\",\n"
            + "    \"data-package\"\n"
            + "  ]";
        gameJson = Regex.Replace(gameJson, objectArrayPattern, stringArray);
        File.WriteAllText(gameJsonPath, gameJson, new UTF8Encoding(false));

        var staleCheckVersionImport = new Regex(
            @"['""](?:\./|\.\./)+check-version['""]");
        foreach (string path in Directory.GetFiles(
                     miniGameDir, "*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".js" && extension != ".json") continue;
            string generatedText = File.ReadAllText(path, Encoding.UTF8);
            if (generatedText.Contains("$COMPRESS_DATA_PACKAGE")
                && path != compressBootstrapPath)
            {
                throw new BuildFailedException(
                    "Unresolved $COMPRESS_DATA_PACKAGE in " + path);
            }
            if (extension == ".js" && staleCheckVersionImport.IsMatch(generatedText))
            {
                throw new BuildFailedException(
                    "Unversioned check-version import in " + path);
            }
        }
    }

    static void RewriteCheckVersionImports(
        string miniGameDir, string versionModuleName)
    {
        var importPattern = new Regex(
            @"(?<quote>['""])(?:\./|\.\./)+check-version(?<end>['""])");

        foreach (string path in Directory.GetFiles(
                     miniGameDir, "*.js", SearchOption.AllDirectories))
        {
            string directory = Path.GetDirectoryName(path);
            string relativeDirectory = directory.Length == miniGameDir.Length
                ? string.Empty
                : directory.Substring(miniGameDir.Length + 1);
            string[] directories = relativeDirectory.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                System.StringSplitOptions.RemoveEmptyEntries);

            var modulePath = new StringBuilder();
            if (directories.Length == 0)
                modulePath.Append("./");
            else
                for (int i = 0; i < directories.Length; i++)
                    modulePath.Append("../");
            modulePath.Append(versionModuleName);

            string text = File.ReadAllText(path, Encoding.UTF8);
            string rewritten = importPattern.Replace(
                text,
                "${quote}" + modulePath + "${end}");
            if (rewritten != text)
                File.WriteAllText(path, rewritten, new UTF8Encoding(false));
        }
    }

    static void ReplaceGeneratedText(
        string path, string oldValue, string newValue)
    {
        string text = File.ReadAllText(path, Encoding.UTF8);
        if (!text.Contains(oldValue)) return;
        File.WriteAllText(
            path, text.Replace(oldValue, newValue), new UTF8Encoding(false));
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
        // The WeChat WebGL 1 renderer can keep late procedural bone poses in the
        // bind pose when GPU skinning is enabled. CPU skinning consumes the final
        // bone transforms and keeps the runner and AI shadow animated.
        PlayerSettings.gpuSkinning = false;
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

    static void RecreateBuildDirectory(string path)
    {
        string projectRoot = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(Application.dataPath, "../"));
        string fullPath = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(projectRoot, path));
        string requiredPrefix = projectRoot.TrimEnd(
            System.IO.Path.DirectorySeparatorChar,
            System.IO.Path.AltDirectorySeparatorChar)
            + System.IO.Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(requiredPrefix,
                System.StringComparison.OrdinalIgnoreCase))
        {
            throw new BuildFailedException(
                $"Refusing to clean a build directory outside the project: {fullPath}");
        }

        if (System.IO.Directory.Exists(fullPath))
            System.IO.Directory.Delete(fullPath, true);

        System.IO.Directory.CreateDirectory(fullPath);
    }
}
