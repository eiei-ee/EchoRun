using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class AsyncChallengePlayModeTests
{
    private static readonly string[] StringKeys =
    {
        EchoRunSaveSystem.SaveKey, EchoRunSaveSystem.SaveSlotAKey,
        EchoRunSaveSystem.SaveSlotBKey, EchoRunSaveSystem.SingleContractSaveSlotAKey,
        EchoRunSaveSystem.SingleContractSaveSlotBKey, EchoRunSaveSystem.TelemetryKey,
        "AIShadowProfileV1"
    };
    private static readonly string[] IntKeys =
    {
        EchoRunSaveSystem.ActiveSaveSlotKey, EchoRunSaveSystem.SingleContractActiveSaveSlotKey,
        EchoRunSaveSystem.TrainingResetPendingKey, "HighScore", "TotalCoins",
        "TargetFrameRate", "AudioMuted", "CharacterPreset", RunDifficultySettings.PreferenceKey,
        "EchoRunLargeText", "EchoRunHighContrast", "EchoRunReducedMotion", "VisualQuality"
    };
    private static readonly string[] FloatKeys = { "MasterVolume", "MusicVolume", "SfxVolume" };
    private readonly Dictionary<string, string> _strings = new Dictionary<string, string>();
    private readonly Dictionary<string, int> _ints = new Dictionary<string, int>();
    private readonly Dictionary<string, float> _floats = new Dictionary<string, float>();
    private readonly Dictionary<FieldInfo, object> _statics = new Dictionary<FieldInfo, object>();
    private float _timeScale;
    private int _frameRate;
    private int _vSync;
    private bool _snapshotTaken;
    private string _entryScenePath;

    [SetUp]
    public void IsolatePlayerProgress()
    {
        _timeScale = Time.timeScale;
        _frameRate = Application.targetFrameRate;
        _vSync = QualitySettings.vSyncCount;
        Scene entryScene = SceneManager.GetActiveScene();
        _entryScenePath = entryScene.IsValid() && entryScene.buildIndex >= 0
            ? entryScene.path : "SampleScene";
        foreach (string key in StringKeys)
            if (PlayerPrefs.HasKey(key)) _strings[key] = PlayerPrefs.GetString(key);
        foreach (string key in IntKeys)
            if (PlayerPrefs.HasKey(key)) _ints[key] = PlayerPrefs.GetInt(key);
        foreach (string key in FloatKeys)
            if (PlayerPrefs.HasKey(key)) _floats[key] = PlayerPrefs.GetFloat(key);
        foreach (Type type in new[] { typeof(EchoRunSaveSystem), typeof(StyleTracker),
                     typeof(AIPlayerSkillEstimator), typeof(AIRunTelemetry), typeof(AIRunRandom),
                     typeof(EchoRunAccessibility), typeof(VisualQualityController) })
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
                if (!field.IsLiteral && !field.IsInitOnly
                    && !typeof(Delegate).IsAssignableFrom(field.FieldType))
                    _statics[field] = field.GetValue(null);
        }
        // Only scene-transfer data: restoring a destroyed Unity singleton would be invalid.
        foreach (string name in new[] { "_startAfterSceneLoad", "_nextRunSeed",
                     "_gameplayFlowAfterSceneLoad", "_validationAfterSceneLoad",
                     "_asyncOpponentAfterSceneLoad", "_asyncParametersAfterSceneLoad" })
        {
            FieldInfo field = StaticField(typeof(GameManager), name);
            _statics[field] = field.GetValue(null);
        }
        FieldInfo directorPolicy = StaticField(typeof(AITrackDirector), "_sessionPolicy");
        _statics[directorPolicy] = directorPolicy.GetValue(null);
        _snapshotTaken = true;
        DeleteTestPreferences();
        foreach (var entry in _statics)
        {
            if (entry.Key.DeclaringType == typeof(GameManager))
                entry.Key.SetValue(null, entry.Key.FieldType == typeof(bool) ? (object)false : null);
        }
        // Replace references before any scene destruction can write into the original cache.
        SetStatic(typeof(EchoRunSaveSystem), "_data", null);
        SetStatic(typeof(EchoRunSaveSystem), "_initialized", false);
        SetStatic(typeof(EchoRunSaveSystem), "_activeSlot", -1);
        SetStatic(typeof(EchoRunSaveSystem), "_generation", 0L);
        SetStatic(typeof(EchoRunSaveSystem), "_singleContractData", null);
        SetStatic(typeof(EchoRunSaveSystem), "_singleContractInitialized", false);
        SetStatic(typeof(EchoRunSaveSystem), "_singleContractActiveSlot", -1);
        SetStatic(typeof(EchoRunSaveSystem), "_singleContractGeneration", 0L);
        SetStatic(typeof(EchoRunSaveSystem), "_trainingResetInProgress", false);
        SetStatic(typeof(EchoRunSaveSystem), "_trainingWritesEnabled", true);
        StyleTracker.ResetTrainingInMemory();
        AIPlayerSkillEstimator.ResetTrainingInMemory();
        AIRunTelemetry.ResetTrainingInMemory();
        AIRunRandom.BeginRun(1337);
        directorPolicy.SetValue(null, null);
        Time.timeScale = 1f;
        PlayerPrefs.Save();
    }

    [UnityTearDown]
    public IEnumerator RestorePlayerProgressAfterSceneDestruction()
    {
        if (!_snapshotTaken) yield break;
        // Finish while the isolated archive is installed. Unload before restoring any data;
        // runner/director OnDestroy and pending frame callbacks must never see the real archive.
        if (GameManager.Instance != null && GameManager.Instance.IsAsyncChallengeRun)
            AIShadowRunner.Instance?.FinalizeRunIfNeeded();
        Scene scene = SceneManager.GetActiveScene();
        Scene cleanup = SceneManager.CreateScene("AsyncChallengeTestCleanup");
        SceneManager.SetActiveScene(cleanup);
        if (scene.IsValid() && scene.isLoaded)
        {
            AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
            if (unload != null) yield return unload;
        }
        yield return null;
        RestoreSnapshot();
        // A few existing smoke tests assume the gameplay menu already exists. Restore
        // that scene only after the test run is completely destroyed; no Async transfer
        // or captured auto-start flag may make this restoration start another run.
        SetStatic(typeof(GameManager), "_startAfterSceneLoad", false);
        SetStatic(typeof(GameManager), "_nextRunSeed", null);
        SetStatic(typeof(GameManager), "_gameplayFlowAfterSceneLoad", GameplayFlowMode.SingleContract);
        SetStatic(typeof(GameManager), "_validationAfterSceneLoad", null);
        SetStatic(typeof(GameManager), "_asyncOpponentAfterSceneLoad", null);
        SetStatic(typeof(GameManager), "_asyncParametersAfterSceneLoad", null);
        try
        {
            SceneManager.LoadScene(_entryScenePath);
            yield return null;
            yield return null;
        }
        finally
        {
            // Awake/Start may normalize preferences. Retain their exact pre-test bytes
            // and cache state, as well as restoring the application-wide settings.
            RestoreSnapshot();
            RestoreRuntimeSettings();
            _strings.Clear();
            _ints.Clear();
            _floats.Clear();
            _statics.Clear();
            _snapshotTaken = false;
        }
    }

    private void RestoreSnapshot()
    {
        DeleteTestPreferences();
        foreach (var entry in _strings) PlayerPrefs.SetString(entry.Key, entry.Value);
        foreach (var entry in _ints) PlayerPrefs.SetInt(entry.Key, entry.Value);
        foreach (var entry in _floats) PlayerPrefs.SetFloat(entry.Key, entry.Value);
        foreach (var entry in _statics) entry.Key.SetValue(null, entry.Value);
        PlayerPrefs.Save();
    }

    private void RestoreRuntimeSettings()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio != null)
        {
            audio.masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            audio.musicVolume = PlayerPrefs.GetFloat("MusicVolume", .5f);
            audio.sfxVolume = PlayerPrefs.GetFloat("SfxVolume", 1f);
            audio.muted = PlayerPrefs.GetInt("AudioMuted", 0) != 0;
            audio.SendMessage("ApplyOutputVolumes");
        }
        Time.timeScale = _timeScale;
        Application.targetFrameRate = _frameRate;
        QualitySettings.vSyncCount = _vSync;
    }

    [UnityTest]
    public IEnumerator AsyncRestartAndMenuReloadPreserveFrozenOpponentAndLocalArchive()
    {
        GameManager previous = GameManager.Instance;
        SceneManager.LoadScene("SampleScene");
        yield return WaitForScene(previous, GameState.Menu);
        InstallLocalIdentityInBothSlots();
        var before = new ProtectedProgress();
        GameManager first = GameManager.Instance;
        var opponent = SingleContractValidationIdentity.Create();
        string frozenJson = opponent.ToJson();
        var parameters = new AsyncChallengeRunParameters("playmode-first", 1, 1337);
        Assert.IsTrue(first.TryConfigureAsyncChallenge(opponent, parameters, out string error), error);
        first.ChallengeIdFactory = () => "playmode-retry";
        var completed = new List<AsyncChallengeResult>();
        first.AsyncChallengeCompleted += completed.Add;
        Assert.IsTrue(first.TryStartConfiguredRun(out error), error);
        yield return ObserveShortNormalRun();
        // Exercise the ordinary ghost smoothing path after the opening replay has ended.
        float replayDeadline = Time.realtimeSinceStartup + 8f;
        while (first.State == GameState.Playing
               && first.RunElapsed <= SingleContractFlow.OpeningMemoryDurationSeconds + .1f
               && Time.realtimeSinceStartup < replayDeadline)
            yield return null;
        Assert.AreEqual(GameState.Playing, first.State);
        Assert.Greater(first.RunElapsed, SingleContractFlow.OpeningMemoryDurationSeconds);
        first.Pause();
        yield return new WaitForSecondsRealtime(.1f);
        first.Resume();
        yield return null;
        yield return null;
        GameObject ghost = GameObject.Find("AI Shadow Runner");
        Assert.IsNotNull(ghost);
        Vector3 resumedPose = ghost.transform.position;
        Assert.IsFalse(float.IsNaN(resumedPose.x) || float.IsInfinity(resumedPose.x));
        Assert.IsFalse(float.IsNaN(resumedPose.y) || float.IsInfinity(resumedPose.y));
        Assert.IsFalse(float.IsNaN(resumedPose.z) || float.IsInfinity(resumedPose.z));
        Assert.AreEqual(frozenJson, AIShadowRunner.Instance.OpponentIdentityPreview.ToJson());
        Assert.AreEqual(1337, first.RunSeed);
        before.AssertUnchanged();

        first.Restart();
        yield return WaitForScene(first, GameState.Playing);
        GameManager retry = GameManager.Instance;
        Assert.IsTrue(retry.IsAsyncChallengeRun);
        Assert.AreEqual("playmode-retry", retry.ConfiguredAsyncChallengeParameters.challengeId);
        Assert.AreNotEqual(parameters.challengeId, retry.ConfiguredAsyncChallengeParameters.challengeId);
        Assert.AreEqual(parameters.runSeed, retry.RunSeed);
        Assert.AreEqual(parameters.rulesVersion, retry.ConfiguredAsyncChallengeParameters.rulesVersion);
        Assert.AreEqual(frozenJson, retry.ActiveOpponentIdentityPreview.ToJson());
        Assert.AreEqual(frozenJson, AIShadowRunner.Instance.OpponentIdentityPreview.ToJson());
        Assert.AreEqual(1, completed.Count, "Reload must settle the previous challenge exactly once.");
        Assert.AreEqual(RunEndReason.Abandoned, completed[0].reason);
        Assert.AreEqual(parameters.challengeId, completed[0].challengeId);
        retry.AsyncChallengeCompleted += completed.Add;
        yield return ObserveShortNormalRun();
        before.AssertUnchanged();

        // Cover real OnDestroy before GameManager teardown, not just the explicit
        // FinalizeRunIfNeeded path used by the menu/restart buttons.
        UnityEngine.Object.Destroy(AIShadowRunner.Instance.gameObject);
        yield return null;
        Assert.AreEqual(2, completed.Count);
        Assert.AreEqual(RunEndReason.Abandoned, completed[1].reason);
        before.AssertUnchanged();
        retry.ReturnToMenu();
        yield return WaitForScene(retry, GameState.Menu);
        GameManager menu = GameManager.Instance;
        Assert.AreEqual(GameplayFlowMode.SingleContract, menu.ConfiguredGameplayFlowMode);
        Assert.IsNull(menu.ConfiguredAsyncChallengeParameters);
        Assert.IsNull(menu.ActiveOpponentIdentityPreview);
        Assert.IsFalse(menu.IsAsyncChallengeRun);
        Assert.IsTrue(menu.UsesSingleContractRules);
        Assert.AreEqual(2, completed.Count);
        Assert.AreEqual("playmode-retry", completed[1].challengeId);
        Assert.AreEqual(RunEndReason.Abandoned, completed[1].reason);
        before.AssertUnchanged();
    }

    private static IEnumerator WaitForScene(GameManager previous, GameState expected)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        do
        {
            yield return null;
            GameManager current = GameManager.Instance;
            if (current != null && !ReferenceEquals(previous, current)
                && current.State == expected && TrackManager.Instance != null
                && AIShadowRunner.Instance != null)
            {
                yield return null;
                yield break;
            }
        } while (Time.realtimeSinceStartup < deadline);
        Assert.Fail("SampleScene did not reload into " + expected + ".");
    }

    private static IEnumerator ObserveShortNormalRun()
    {
        GameManager game = GameManager.Instance;
        float deadline = Time.realtimeSinceStartup + 8f;
        while (game.State == GameState.Playing && game.Distance < 3f
               && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.AreEqual(GameState.Playing, game.State);
        Assert.GreaterOrEqual(game.Distance, 3f, "Real frame updates must advance the run.");
        Assert.Greater(TrackManager.Instance.ContentPreparedRouteDistance, game.Distance);
        Assert.Greater(TrackManager.Instance.ObstacleRowsSpawned, 0,
            "Async must prepare normal obstacles, not the obstacle-free validation director.");
        Assert.IsFalse(game.ActiveSingleContractValidationConfig.enabled);
    }

    private static void InstallLocalIdentityInBothSlots()
    {
        var local = SingleContractValidationIdentity.Create();
        local.sourceRunSequence = 1;
        local.identityId = ActiveEchoIdentity.CreateIdentityId(local);
        local.memoryContract.identityId = local.identityId;
        SaveCommitResult calibration = EchoRunSaveSystem.TryCommitSingleContractSettlement(
            new RunSettlementCommit
            {
                transactionId = "playmode-own-calibration", runSequence = 1,
                endReason = RunEndReason.FinishReached, calibrationCompleted = true,
                promotedIdentity = local
            });
        Assert.IsTrue(calibration.succeeded);
        SaveCommitResult retry = EchoRunSaveSystem.TryCommitSingleContractSettlement(
            new RunSettlementCommit
            {
                transactionId = "playmode-own-retry", runSequence = 2,
                hasActiveOpponent = true, endReason = RunEndReason.Collision
            });
        Assert.IsTrue(retry.succeeded);
        Assert.IsTrue(PlayerPrefs.HasKey(EchoRunSaveSystem.SingleContractSaveSlotAKey));
        Assert.IsTrue(PlayerPrefs.HasKey(EchoRunSaveSystem.SingleContractSaveSlotBKey));
    }

    private sealed class ProtectedProgress
    {
        private readonly string _data = JsonUtility.ToJson(EchoRunSaveSystem.GetSingleContractSaveData());
        private readonly string _slotA = PlayerPrefs.GetString(EchoRunSaveSystem.SingleContractSaveSlotAKey);
        private readonly string _slotB = PlayerPrefs.GetString(EchoRunSaveSystem.SingleContractSaveSlotBKey);
        private readonly int _active = PlayerPrefs.GetInt(EchoRunSaveSystem.SingleContractActiveSaveSlotKey);
        private readonly string _shadow = EchoRunSaveSystem.GetShadowProfileJson();
        private readonly string _style = EchoRunSaveSystem.GetPlayerStyleJson();
        private readonly string _skill = EchoRunSaveSystem.GetSkillProfileJson();
        private readonly string _director = EchoRunSaveSystem.GetDirectorPolicyJson();
        private readonly int _updates = EchoRunSaveSystem.DirectorModelUpdateCount;
        private readonly int _coins = EchoRunSaveSystem.TotalCoins;
        private readonly int _highScore = PlayerPrefs.GetInt("HighScore");

        public void AssertUnchanged()
        {
            Assert.AreEqual(_data, JsonUtility.ToJson(EchoRunSaveSystem.GetSingleContractSaveData()));
            Assert.AreEqual(_slotA, PlayerPrefs.GetString(EchoRunSaveSystem.SingleContractSaveSlotAKey));
            Assert.AreEqual(_slotB, PlayerPrefs.GetString(EchoRunSaveSystem.SingleContractSaveSlotBKey));
            Assert.AreEqual(_active, PlayerPrefs.GetInt(EchoRunSaveSystem.SingleContractActiveSaveSlotKey));
            Assert.AreEqual(_shadow, EchoRunSaveSystem.GetShadowProfileJson());
            Assert.AreEqual(_style, EchoRunSaveSystem.GetPlayerStyleJson());
            Assert.AreEqual(_skill, EchoRunSaveSystem.GetSkillProfileJson());
            Assert.AreEqual(_director, EchoRunSaveSystem.GetDirectorPolicyJson());
            Assert.AreEqual(_updates, EchoRunSaveSystem.DirectorModelUpdateCount);
            Assert.AreEqual(_coins, EchoRunSaveSystem.TotalCoins);
            Assert.AreEqual(_highScore, PlayerPrefs.GetInt("HighScore"));
        }
    }

    private static void DeleteTestPreferences()
    {
        foreach (string key in StringKeys) PlayerPrefs.DeleteKey(key);
        foreach (string key in IntKeys) PlayerPrefs.DeleteKey(key);
        foreach (string key in FloatKeys) PlayerPrefs.DeleteKey(key);
    }

    private static FieldInfo StaticField(Type type, string name)
    {
        FieldInfo field = type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(field, type.Name + "." + name);
        return field;
    }

    private static void SetStatic(Type type, string name, object value) =>
        StaticField(type, name).SetValue(null, value);
}
