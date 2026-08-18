using System;
using UnityEngine;

/// <summary>
/// Competition demo shortcut. Seeds a complete generation-1 echo archive so a
/// judge meets the trained shadow and its echo contract within the first
/// minute, without playing the 75-second calibration run.
///
/// WebGL: open the build with ?demo=echo to seed, ?demo=off to restore the
/// previous archive. Append &play=1 to also auto-start the run (recording /
/// kiosk flow). Any build: Shift+F9 seeds, Shift+F10 restores.
///
/// Seeding always backs up the current archive first and can be reverted.
/// </summary>
public static class EchoDemoMode
{
    public const int DemoGeneration = 1;
    public const float DemoPace = 26.5f;
    public const float DemoClarity = 0.9f;
    public const float DemoBestProgress = 2100f;
    public const int DemoTrainingSeed = 20260817;
    public const int DemoTrainingIterations = 640;
    public const int DemoProfileVersion = 5;

    private const string SeededFlagKey = "EchoRunDemo.Seeded";
    private const string BackupFlagKey = "EchoRunDemo.HasBackup";
    private const string BackupShadowKey = "EchoRunDemo.BackupShadow";
    private const string BackupStyleKey = "EchoRunDemo.BackupStyle";

    public enum DemoCommand
    {
        None,
        Seed,
        Restore
    }

    /// <summary>
    /// Field-for-field mirror of AIShadowRunner's private profile layout. The
    /// JSON written here is consumed by JsonUtility.FromJson there, so field
    /// names must stay in sync with the runtime profile class.
    /// </summary>
    [Serializable]
    public sealed class DemoShadowProfile
    {
        public int version;
        public int generation;
        public int sampleCount;
        public int activeSampleCount;
        public int[] actionCounts;
        public float pace;
        public float bestProgress;
        public float[] weights;
        public float[] sequenceTransitions;
        public int sequencePairCount;
        public float clarity;
        public string activeGenerationJson;
    }

    public static bool IsDemoSeeded
    {
        get { return PlayerPrefs.GetInt(SeededFlagKey, 0) == 1; }
    }

    public static bool AutoStartRequested { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyUrlCommand()
    {
        string url = Application.absoluteURL ?? "";
        AutoStartRequested = ParseAutoStart(url);
        DemoCommand command = ParseUrlCommand(url);
        if (command == DemoCommand.Restore)
            RestorePreviousArchive();
        else if (command == DemoCommand.Seed)
            SeedDemoArchive(false);
    }

    public static DemoCommand ParseUrlCommand(string url)
    {
        if (string.IsNullOrEmpty(url)) return DemoCommand.None;
        string lowered = url.ToLowerInvariant();
        if (ContainsQueryValue(lowered, "demo", "off")
            || ContainsQueryValue(lowered, "demo", "0"))
            return DemoCommand.Restore;
        if (ContainsQueryValue(lowered, "demo", "echo")
            || ContainsQueryValue(lowered, "demo", "1")
            || ContainsQueryValue(lowered, "demo", "true"))
            return DemoCommand.Seed;
        return DemoCommand.None;
    }

    public static bool ParseAutoStart(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        string lowered = url.ToLowerInvariant();
        return ContainsQueryValue(lowered, "play", "1")
               || ContainsQueryValue(lowered, "play", "true")
               || ContainsQueryValue(lowered, "autostart", "1")
               || ContainsQueryValue(lowered, "autostart", "true");
    }

    public static bool SeedDemoArchive(bool force)
    {
        EchoRunSaveSystem.EnsureInitialized();
        bool alreadySeeded = IsDemoSeeded
            && !string.IsNullOrEmpty(
                EchoRunSaveSystem.GetShadowProfileJson());
        if (alreadySeeded && !force) return false;

        if (!IsDemoSeeded) BackupCurrentArchive();

        EchoRunSaveSystem.SaveShadowProfile(BuildDemoShadowProfileJson());
        EchoRunSaveSystem.SavePlayerStyle(BuildDemoStyleJson());
        PlayerPrefs.SetInt(SeededFlagKey, 1);
        PlayerPrefs.Save();
        Debug.Log("[EchoDemoMode] Demo archive seeded: generation "
                  + DemoGeneration + " echo and contract are ready.");
        return true;
    }

    public static bool RestorePreviousArchive()
    {
        EchoRunSaveSystem.EnsureInitialized();
        if (!IsDemoSeeded) return false;

        bool hadBackup = PlayerPrefs.GetInt(BackupFlagKey, 0) == 1;
        string shadow = hadBackup
            ? PlayerPrefs.GetString(BackupShadowKey, "")
            : "";
        string style = hadBackup
            ? PlayerPrefs.GetString(BackupStyleKey, "")
            : "";
        EchoRunSaveSystem.SaveShadowProfile(shadow);
        EchoRunSaveSystem.SavePlayerStyle(style);

        PlayerPrefs.DeleteKey(SeededFlagKey);
        PlayerPrefs.DeleteKey(BackupFlagKey);
        PlayerPrefs.DeleteKey(BackupShadowKey);
        PlayerPrefs.DeleteKey(BackupStyleKey);
        PlayerPrefs.Save();
        Debug.Log("[EchoDemoMode] Demo archive restored to previous state.");
        return true;
    }

    public static string BuildDemoShadowProfileJson()
    {
        float[] weights = TrainDemoPolicyWeights();
        var snapshot = new EchoGenerationSnapshot
        {
            generation = DemoGeneration,
            policyWeights = weights,
            sequenceTransitions = null,
            sequencePairCount = 0,
            styleJson = BuildDemoStyleJson(),
            pace = DemoPace,
            clarity = DemoClarity
        };
        var profile = new DemoShadowProfile
        {
            version = DemoProfileVersion,
            generation = DemoGeneration,
            sampleCount = 104,
            activeSampleCount = 70,
            actionCounts = new[] { 34, 10, 24, 20, 16 },
            pace = DemoPace,
            bestProgress = DemoBestProgress,
            weights = weights,
            sequenceTransitions = null,
            sequencePairCount = 0,
            clarity = DemoClarity,
            activeGenerationJson = snapshot.ToJson()
        };
        return JsonUtility.ToJson(profile);
    }

    public static string BuildDemoStyleJson()
    {
        return JsonUtility.ToJson(BuildDemoStyle());
    }

    /// <summary>
    /// The demo persona: a casual player who leans on the right lane, jumps a
    /// touch late but reliably, and slides a little less than they jump. The
    /// seeded contract therefore asks the judge to break a right-lane habit,
    /// which is the most readable contract for a first-time viewer.
    /// </summary>
    public static PlayerStyleData BuildDemoStyle()
    {
        var style = new PlayerStyleData
        {
            version = PlayerStyleData.CurrentVersion,
            aggressiveness = 0.58f,
            jumpTiming = 0.12f,
            slideFrequency = 0.44f,
            slideOpportunitySuccess = 0.72f,
            lanePreference = 0.72f,
            rhythmStability = 0.6f,
            recoveryStyle = 0.5f,
            aggressivenessSamples = 20,
            jumpTimingSamples = 12,
            verticalActionSamples = 16,
            jumpActionSamples = 9,
            slideActionSamples = 7,
            slideOpportunitySamples = 10,
            laneSamples = 26,
            rhythmSamples = 8,
            recoverySamples = 6
        };
        style.Normalize();
        return style;
    }

    /// <summary>
    /// Trains the demo shadow with the game's own online classifier so the
    /// exported weights are always format-compatible. The synthetic persona
    /// mirrors BuildDemoStyle: right-lane bias, dependable jump/slide reflexes,
    /// right-dodge habit against barriers.
    /// </summary>
    public static float[] TrainDemoPolicyWeights()
    {
        var policy = new AIShadowPolicy();
        var random = new System.Random(DemoTrainingSeed);
        for (int i = 0; i < DemoTrainingIterations; i++)
            TrainDemoSample(policy, random);
        return policy.ExportWeights();
    }

    private static void TrainDemoSample(AIShadowPolicy policy,
        System.Random random)
    {
        double roll = random.NextDouble();
        int lane = PickDemoLane(random);
        float laneFeature = lane - 1f;
        float speed = 0.15f + 0.75f * (float)random.NextDouble();

        if (roll < 0.38)
        {
            // No threat in sight: hold the lane, sometimes drift right.
            var calm = new float[]
                { 1f, laneFeature, speed, 0f, 0f, 0f, 0f, 0f };
            ShadowAction calmLabel = lane < 2 && random.NextDouble() < 0.30
                ? ShadowAction.Right
                : ShadowAction.Keep;
            policy.Learn((int)calmLabel, calm, 0.10f);
            return;
        }

        if (roll < 0.48)
        {
            // Recovery frames: airborne or sliding, wait until landing.
            float jumping = random.NextDouble() < 0.55 ? 1f : 0f;
            float sliding = jumping > 0.5f ? 0f : 1f;
            var busy = new float[]
                { 1f, laneFeature, speed, 0.35f, 0f, 0f, jumping, sliding };
            policy.Learn((int)ShadowAction.Keep, busy, 0.10f);
            return;
        }

        ObstacleType type = PickDemoObstacle(random);
        float typeFeature = ((int)type + 1) / 3f;

        if (roll < 0.62)
        {
            // Threat in an adjacent lane: no action needed.
            float relativeLane = random.NextDouble() < 0.5 ? -0.5f : 0.5f;
            float adjacentProximity =
                0.5f + 0.4f * (float)random.NextDouble();
            var adjacent = new float[]
            {
                1f, laneFeature, speed, adjacentProximity,
                relativeLane, typeFeature, 0f, 0f
            };
            policy.Learn((int)ShadowAction.Keep, adjacent, 0.11f);
            return;
        }

        // Threat dead ahead in the current lane.
        float proximity = 0.55f + 0.4f * (float)random.NextDouble();
        var threat = new float[]
            { 1f, laneFeature, speed, proximity, 0f, typeFeature, 0f, 0f };
        ShadowAction label;
        if (type == ObstacleType.High)
            label = ShadowAction.Jump;
        else if (type == ObstacleType.Low)
            label = ShadowAction.Slide;
        else
            label = lane < 2 ? ShadowAction.Right : ShadowAction.Left;

        // A little label noise keeps the clone human instead of perfect.
        if (random.NextDouble() < 0.06)
            label = ShadowAction.Keep;
        policy.Learn((int)label, threat, 0.12f);
    }

    private static int PickDemoLane(System.Random random)
    {
        double roll = random.NextDouble();
        if (roll < 0.20) return 0;
        if (roll < 0.50) return 1;
        return 2;
    }

    private static ObstacleType PickDemoObstacle(System.Random random)
    {
        double roll = random.NextDouble();
        if (roll < 0.30) return ObstacleType.Low;
        if (roll < 0.70) return ObstacleType.High;
        return ObstacleType.Barrier;
    }

    private static void BackupCurrentArchive()
    {
        PlayerPrefs.SetString(BackupShadowKey,
            EchoRunSaveSystem.GetShadowProfileJson() ?? "");
        PlayerPrefs.SetString(BackupStyleKey,
            EchoRunSaveSystem.GetPlayerStyleJson() ?? "");
        PlayerPrefs.SetInt(BackupFlagKey, 1);
    }

    private static bool ContainsQueryValue(string loweredUrl, string key,
        string value)
    {
        string token = key + "=" + value;
        int index = loweredUrl.IndexOf(token, StringComparison.Ordinal);
        while (index >= 0)
        {
            bool startOk = index == 0
                           || loweredUrl[index - 1] == '?'
                           || loweredUrl[index - 1] == '&'
                           || loweredUrl[index - 1] == '#'
                           || loweredUrl[index - 1] == '/';
            int end = index + token.Length;
            bool endOk = end >= loweredUrl.Length
                         || loweredUrl[end] == '&'
                         || loweredUrl[end] == '#'
                         || loweredUrl[end] == '/'
                         || loweredUrl[end] == ' ';
            if (startOk && endOk) return true;
            index = loweredUrl.IndexOf(token, index + 1,
                StringComparison.Ordinal);
        }
        return false;
    }
}

/// <summary>
/// Hotkey and auto-start companion for EchoDemoMode. Auto-spawned after scene
/// load like the other runtime services.
/// </summary>
public sealed class EchoDemoModeAgent : MonoBehaviour
{
    private const float AutoStartDelaySeconds = 2f;

    private float _autoStartTimer = -1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<EchoDemoModeAgent>() != null) return;
        new GameObject("EchoDemoMode").AddComponent<EchoDemoModeAgent>();
    }

    void Start()
    {
        if (EchoDemoMode.AutoStartRequested)
            _autoStartTimer = AutoStartDelaySeconds;
    }

    void Update()
    {
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            if (Input.GetKeyDown(KeyCode.F9))
                EchoDemoMode.SeedDemoArchive(true);
            else if (Input.GetKeyDown(KeyCode.F10))
                EchoDemoMode.RestorePreviousArchive();
        }

        if (_autoStartTimer < 0f) return;
        _autoStartTimer -= Time.unscaledDeltaTime;
        if (_autoStartTimer > 0f) return;
        _autoStartTimer = -1f;
        if (GameManager.Instance != null
            && GameManager.Instance.State == GameState.Menu)
            GameManager.Instance.StartGame();
    }
}
