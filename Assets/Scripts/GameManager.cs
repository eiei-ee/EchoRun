using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.SceneManagement;

public enum GameState { Menu, Playing, Paused, GameOver }
public enum RunEndReason { None, FinishReached, Collision, Abandoned }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private static bool _startAfterSceneLoad;
    private static int? _nextRunSeed;

    [Header("Speed")]
    public float startSpeed = 10f;
    public float maxSpeed = 40f;
    public float speedIncreaseRate = 0.5f;

    [Header("Score")]
    public int coinScore = 10;

    public float CurrentSpeed { get; private set; }
    public GameState State { get; private set; } = GameState.Menu;
    public int Score { get; private set; }
    public int Coins { get; private set; }
    public float Distance { get; private set; }
    public int HighScore { get; private set; }
    public int TotalCoins { get; private set; }
    public bool IsNewHighScore { get; private set; }
    public bool IsDeathSequence { get; private set; }
    public int RunSeed { get; private set; }
    public float CourseDistance { get; private set; }
    public float CourseTargetDuration { get; private set; }
    public float RunElapsed { get; private set; }
    public float RemainingDistance => Mathf.Max(0f, CourseDistance - Distance);
    public RunEndReason LastEndReason { get; private set; }
    public int CollisionStrikes { get; private set; }
    public int MaximumCollisionStrikes => 2;
    public int SyncRemaining => Mathf.Max(0,
        MaximumCollisionStrikes - CollisionStrikes);
    public float CollisionRecoveryDuration => 1.25f;
    public float CollisionRecoveryTimeRemaining { get; private set; }
    public int ContractMarkerCount { get; private set; }

    [Header("Buff (runtime)")]
    public float BuffTimeRemaining;
    public string BuffName;

    public UnityEvent<GameState> OnStateChanged = new UnityEvent<GameState>();
    public UnityEvent<int> OnScoreChanged = new UnityEvent<int>();
    public UnityEvent<int> OnCoinsChanged = new UnityEvent<int>();
    public UnityEvent<int> OnContractMarkerChanged = new UnityEvent<int>();
    public UnityEvent<int> OnBankedCoinsChanged = new UnityEvent<int>();
    public UnityEvent<float> OnDistanceChanged = new UnityEvent<float>();

    private float _distanceTraveled;
    private float _prePauseTimeScale = 1f;
    private PlayerController _telemetryPlayer;
    private int _lastBaseScore;
    private float _powerUpBonusScore;
    private bool _telemetryFinished;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<GameManager>() != null) return;
        new GameObject("GameManager_Runtime").AddComponent<GameManager>();
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        EnsureSceneRuntimeServices();
        EchoRunSaveSystem.EnsureInitialized();
        GameplayBalance balance = GameBalanceConfig.Current.gameplay;
        startSpeed = balance.startSpeed;
        maxSpeed = balance.maxSpeed;
        speedIncreaseRate = balance.speedIncreaseRate;
        coinScore = balance.coinScore;
        int savedFps = PlayerPrefs.GetInt("TargetFrameRate", 60);
        int runtimeFps = NormalizeFrameRate(
            savedFps > 0 ? savedFps : 60, IsFrameRateConstrainedPlatform());
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = runtimeFps;
        if (runtimeFps != savedFps)
            EchoRunSaveSystem.SaveFrameRate(runtimeFps);
    }

    private void EnsureSceneRuntimeServices()
    {
        EnsureSceneService<TrackManager>("TrackManager_Runtime");
        EnsureSceneService<PowerUpShopUI>("Power Up Shop UI");
        EnsureSceneService<AITrainingDashboardUI>("AI Training Dashboard UI");
    }

    private void EnsureSceneService<T>(string objectName) where T : Component
    {
        if (FindObjectOfType<T>() != null) return;
        GameObject host = new GameObject(objectName);
        host.transform.SetParent(transform, false);
        host.AddComponent<T>();
    }

    public void SetFrameRate(int fps)
    {
        if (fps <= 0) return;
        int runtimeFps = NormalizeFrameRate(
            fps, IsFrameRateConstrainedPlatform());
        Application.targetFrameRate = runtimeFps;
        EchoRunSaveSystem.SaveFrameRate(runtimeFps);
    }

    public int GetFrameRate()
    {
        return Application.targetFrameRate;
    }

    public bool SupportsHighFrameRate => !IsFrameRateConstrainedPlatform();

    public static bool ShouldConstrainHighFrameRate(bool isAndroid,
        bool isWebGl, bool usesTouchLayout)
    {
        // Native Android can request the display's high-refresh mode through
        // Application.targetFrameRate. Mobile WebGL remains capped because
        // browser frame pacing is outside the player's control.
        return isWebGl && usesTouchLayout;
    }

    public static int NormalizeFrameRate(int requested, bool constrainedPlatform)
    {
        if (requested <= 30) return 30;
        if (constrainedPlatform || requested < 90) return 60;
        return 120;
    }

    private static bool IsFrameRateConstrainedPlatform()
    {
#if UNITY_ANDROID
        return ShouldConstrainHighFrameRate(true, false, true);
#elif UNITY_WEBGL
        bool usesTouchLayout = Application.isMobilePlatform || Input.touchSupported;
        return ShouldConstrainHighFrameRate(false, true, usesTouchLayout);
#else
        return false;
#endif
    }

    void Start()
    {
        HighScore = PlayerPrefs.GetInt("HighScore", 0);
        TotalCoins = EchoRunSaveSystem.TotalCoins;

        if (_startAfterSceneLoad)
        {
            _startAfterSceneLoad = false;
            StartGame();
        }
    }

    void Update()
    {
        if (State != GameState.Playing || IsDeathSequence) return;

        RunElapsed += Time.deltaTime;
        CollisionRecoveryTimeRemaining = Mathf.Max(0f,
            CollisionRecoveryTimeRemaining - Time.deltaTime);
        CurrentSpeed = Mathf.Min(CurrentSpeed + speedIncreaseRate * Time.deltaTime, maxSpeed);
        _distanceTraveled += CurrentSpeed * Time.deltaTime;
        if (CourseDistance > 0f)
            _distanceTraveled = Mathf.Min(_distanceTraveled, CourseDistance);

        int newDist = Mathf.FloorToInt(_distanceTraveled);
        if (newDist != Mathf.FloorToInt(Distance))
        {
            Distance = _distanceTraveled;
            OnDistanceChanged.Invoke(Distance);
        }

        int baseScore = Mathf.FloorToInt(_distanceTraveled) + Coins * coinScore;
        int baseGain = Mathf.Max(0, baseScore - _lastBaseScore);
        float multiplier = PowerUpController.Instance != null
            ? PowerUpController.Instance.ScoreMultiplier
            : 1f;
        _powerUpBonusScore += baseGain * Mathf.Max(0f, multiplier - 1f);
        _lastBaseScore = baseScore;
        int newScore = baseScore + Mathf.FloorToInt(_powerUpBonusScore);
        if (newScore != Score)
        {
            Score = newScore;
            OnScoreChanged.Invoke(Score);
        }

        if (_telemetryPlayer == null)
            _telemetryPlayer = FindObjectOfType<PlayerController>();
        AIRunTelemetry.Tick(this, _telemetryPlayer);

        if (CourseDistance > 0f && _distanceTraveled >= CourseDistance)
        {
            CompleteCourse();
            return;
        }

        // Buff countdown
        if (BuffTimeRemaining > 0f)
        {
            BuffTimeRemaining -= Time.deltaTime;
            if (BuffTimeRemaining <= 0f)
            {
                BuffTimeRemaining = 0f;
                BuffName = null;
            }
        }
    }

    public void StartGame()
    {
        if (State != GameState.Menu) return;

        int runSequence = EchoRunSaveSystem.ReserveRunSequence();
        RunSeed = _nextRunSeed ?? CreateRunSeed(runSequence);
        _nextRunSeed = null;
        AIRunRandom.BeginRun(RunSeed);
        AIPlayerSkillEstimator.BeginRun();
        StyleTracker.BeginRun();
        AIRunTelemetry.BeginRun(RunSeed, runSequence, HighScore,
            AIShadowRunner.Instance != null ? AIShadowRunner.Instance.Generation : 0,
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.ModelUpdateCount
                : EchoRunSaveSystem.DirectorModelUpdateCount,
            AIShadowRunner.Instance != null
                ? AIShadowRunner.Instance.GetModelWeightsSnapshot()
                : null,
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.GetModelWeightsSnapshot()
                : EchoRunSaveSystem.GetDirectorWeights(),
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.GetPolicyStateSnapshot()
                : EchoRunSaveSystem.GetDirectorPolicyJson(),
            AIShadowRunner.Instance != null
                ? AIShadowRunner.Instance.GetSequenceStateSnapshot()
                : "");

        Time.timeScale = 1f;
        PowerUpController.Instance?.BeginRun();
        float turboBonus = PowerUpController.Instance != null
            ? PowerUpController.Instance.GetTurboStartBonus()
            : 0f;
        CurrentSpeed = Mathf.Min(maxSpeed, startSpeed + turboBonus);
        Score = 0;
        Coins = 0;
        ContractMarkerCount = 0;
        Distance = 0;
        _distanceTraveled = 0;
        _lastBaseScore = 0;
        _powerUpBonusScore = 0f;
        BuffTimeRemaining = 0;
        BuffName = null;
        IsDeathSequence = false;
        LastEndReason = RunEndReason.None;
        RunElapsed = 0f;
        CollisionStrikes = 0;
        CollisionRecoveryTimeRemaining = 0f;
        _telemetryFinished = false;
        GameplayBalance balance = GameBalanceConfig.Current.gameplay;
        int generation = AIShadowRunner.Instance != null
            ? AIShadowRunner.Instance.Generation
            : 0;
        CourseTargetDuration = SelectCourseDuration(generation,
            balance.calibrationDuration, balance.challengeDuration);
        CourseDistance = EchoTimeRules.DistanceForAcceleratingRun(
            CurrentSpeed, maxSpeed, speedIncreaseRate, CourseTargetDuration);
        _telemetryPlayer = null;
        State = GameState.Playing;
        OnStateChanged.Invoke(State);
        OnScoreChanged.Invoke(0);
        OnCoinsChanged.Invoke(0);
        OnContractMarkerChanged.Invoke(0);
        OnDistanceChanged.Invoke(0);
        InputManager.Instance?.ClearInput();
        AudioManager.Instance?.StartFootsteps();
    }

    public void Pause()
    {
        if (State != GameState.Playing) return;
        _prePauseTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        State = GameState.Paused;
        OnStateChanged.Invoke(State);
        EchoRunSaveSystem.SaveLegacyState();
    }

    public void Resume()
    {
        if (State != GameState.Paused) return;
        Time.timeScale = _prePauseTimeScale;
        State = GameState.Playing;
        OnStateChanged.Invoke(State);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        AudioManager.Instance?.StopFootsteps();
        InputManager.Instance?.ClearInput();
        if (State == GameState.Playing || State == GameState.Paused)
        {
            LastEndReason = RunEndReason.Abandoned;
            AIShadowRunner.Instance?.FinalizeRunIfNeeded();
        }
        FinishTelemetry(LastEndReason == RunEndReason.None
            ? RunEndReason.Abandoned
            : LastEndReason);
        EchoRunSaveSystem.SaveLegacyState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GameOver()
    {
        if (State != GameState.Playing || IsDeathSequence) return;
        LastEndReason = RunEndReason.Collision;
        IsDeathSequence = true;
        var player = GameObject.Find("player");
        if (player != null) ParticleManager.Instance?.EmitDeath(player.transform.position);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayDeath();
        if (AudioManager.Instance != null) AudioManager.Instance.StopFootsteps();
        InputManager.Instance?.ClearInput();
        StartCoroutine(DeathSequenceCoroutine());
    }

    public bool TryRecoverFromCollision()
    {
        if (State != GameState.Playing || IsDeathSequence) return false;
        CollisionStrikes++;
        if (CollisionStrikes >= MaximumCollisionStrikes) return false;

        CurrentSpeed = Mathf.Max(startSpeed * 0.75f, CurrentSpeed * 0.55f);
        CollisionRecoveryTimeRemaining = CollisionRecoveryDuration;
        InputManager.Instance?.ClearInput();
        AIRunTelemetry.RecordEvent("player_collision_recovery",
            CollisionStrikes, -1, CurrentSpeed, CollisionRecoveryTimeRemaining);
        return true;
    }

    System.Collections.IEnumerator DeathSequenceCoroutine()
    {
        bool reducedMotion = EchoRunAccessibility.ReducedMotion;
        Time.timeScale = reducedMotion ? 1f : 0.3f;
        yield return new WaitForSecondsRealtime(reducedMotion ? 0.35f : 1.2f);
        Time.timeScale = 1f;
        State = GameState.GameOver;
        SaveHighScore();
        OnStateChanged.Invoke(State);
        FinishTelemetry(LastEndReason);
        IsDeathSequence = false;
    }

    private void CompleteCourse()
    {
        if (State != GameState.Playing || IsDeathSequence) return;
        LastEndReason = RunEndReason.FinishReached;
        IsDeathSequence = true;
        AudioManager.Instance?.StopFootsteps();
        InputManager.Instance?.ClearInput();
        Distance = CourseDistance;
        _distanceTraveled = CourseDistance;
        OnDistanceChanged.Invoke(Distance);
        State = GameState.GameOver;
        SaveHighScore();
        OnStateChanged.Invoke(State);
        FinishTelemetry(LastEndReason);
        IsDeathSequence = false;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        AudioManager.Instance?.StopFootsteps();
        InputManager.Instance?.ClearInput();
        FinishTelemetry(LastEndReason == RunEndReason.None
            ? RunEndReason.Abandoned
            : LastEndReason);
        EchoRunSaveSystem.SaveLegacyState();
        _startAfterSceneLoad = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void AddCoins(int amount)
    {
        Coins += amount;
        OnCoinsChanged.Invoke(Coins);
    }

    public void AddContractMarker()
    {
        ContractMarkerCount++;
        OnContractMarkerChanged.Invoke(ContractMarkerCount);
    }

    public bool TryPurchasePowerUp(PowerUpId id)
    {
        PowerUpBalance definition = GameBalanceConfig.GetPowerUp(id);
        if (definition == null
            || !EchoRunSaveSystem.TryPurchasePowerUp(id, definition.cost))
        {
            AudioManager.Instance?.PlayUIError();
            return false;
        }
        TotalCoins = EchoRunSaveSystem.TotalCoins;
        OnBankedCoinsChanged.Invoke(TotalCoins);
        AudioManager.Instance?.PlayUIConfirm();
        return true;
    }

    public bool SelectPowerUp(PowerUpId id)
    {
        bool selected = EchoRunSaveSystem.SelectPowerUp(id);
        if (selected) AudioManager.Instance?.PlayUIConfirm();
        else AudioManager.Instance?.PlayUIError();
        return selected;
    }

    void SaveHighScore()
    {
        IsNewHighScore = Score > HighScore;
        if (IsNewHighScore) HighScore = Score;
        TotalCoins += Coins;
        EchoRunSaveSystem.SaveProgress(HighScore, TotalCoins);
        OnBankedCoinsChanged.Invoke(TotalCoins);
    }

    public static void SetNextRunSeed(int seed)
    {
        _nextRunSeed = seed;
    }

    public static float SelectCourseDistance(int generation,
        float calibrationDistance, float challengeDistance)
    {
        float calibration = Mathf.Max(1f, calibrationDistance);
        return generation <= 0
            ? calibration
            : Mathf.Max(calibration, challengeDistance);
    }

    public static float SelectCourseDuration(int generation,
        float calibrationDuration, float challengeDuration)
    {
        float calibration = Mathf.Max(1f, calibrationDuration);
        return generation <= 0
            ? calibration
            : Mathf.Max(calibration, challengeDuration);
    }

    private static int CreateRunSeed(int sequence)
    {
        unchecked
        {
            long ticks = System.DateTime.UtcNow.Ticks;
            return (int)(ticks ^ (ticks >> 32) ^ (sequence * 486187739));
        }
    }

    private void FinishTelemetry(RunEndReason endReason)
    {
        if (_telemetryFinished) return;
        _telemetryFinished = true;
        string reason = ToTelemetryReason(endReason);
        AIPlayerSkillEstimator.EndRun(Distance,
            AIRunTelemetry.IsCompletedTrainingReason(reason));
        StyleTracker.EndRun();
        AIRunTelemetry.FinishRun(this, reason,
            AIShadowRunner.Instance != null ? AIShadowRunner.Instance.Generation : 0,
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.ModelUpdateCount
                : EchoRunSaveSystem.DirectorModelUpdateCount,
            AIShadowRunner.Instance != null
                ? AIShadowRunner.Instance.GetModelWeightsSnapshot()
                : null,
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.GetModelWeightsSnapshot()
                : EchoRunSaveSystem.GetDirectorWeights(),
            AITrackDirector.Instance != null
                ? AITrackDirector.Instance.GetPolicyStateSnapshot()
                : EchoRunSaveSystem.GetDirectorPolicyJson(),
            AIShadowRunner.Instance != null
                ? AIShadowRunner.Instance.GetSequenceStateSnapshot()
                : "");
    }

    public static string ToTelemetryReason(RunEndReason endReason)
    {
        switch (endReason)
        {
            case RunEndReason.FinishReached:
                return "finish_reached";
            case RunEndReason.Collision:
                return "collision";
            default:
                return "abandoned";
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && State == GameState.Playing)
            Pause();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && State == GameState.Playing)
            Pause();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
