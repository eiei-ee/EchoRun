using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.SceneManagement;

public enum GameState { Menu, Playing, Paused, GameOver }

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

    [Header("Buff (runtime)")]
    public float BuffTimeRemaining;
    public string BuffName;

    public UnityEvent<GameState> OnStateChanged = new UnityEvent<GameState>();
    public UnityEvent<int> OnScoreChanged = new UnityEvent<int>();
    public UnityEvent<int> OnCoinsChanged = new UnityEvent<int>();
    public UnityEvent<int> OnBankedCoinsChanged = new UnityEvent<int>();
    public UnityEvent<float> OnDistanceChanged = new UnityEvent<float>();

    private float _distanceTraveled;
    private float _prePauseTimeScale = 1f;
    private PlayerController _telemetryPlayer;
    private int _lastBaseScore;
    private float _powerUpBonusScore;

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

    public static int NormalizeFrameRate(int requested, bool constrainedPlatform)
    {
        if (requested <= 30) return 30;
        if (constrainedPlatform || requested < 90) return 60;
        return 120;
    }

    private static bool IsFrameRateConstrainedPlatform()
    {
#if UNITY_WEBGL || UNITY_ANDROID
        return true;
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
        if (State != GameState.Playing) return;

        CurrentSpeed = Mathf.Min(CurrentSpeed + speedIncreaseRate * Time.deltaTime, maxSpeed);
        _distanceTraveled += CurrentSpeed * Time.deltaTime;

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
        int runSequence = EchoRunSaveSystem.ReserveRunSequence();
        RunSeed = _nextRunSeed ?? CreateRunSeed(runSequence);
        _nextRunSeed = null;
        AIRunRandom.BeginRun(RunSeed);
        AIPlayerSkillEstimator.BeginRun();
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
        Distance = 0;
        _distanceTraveled = 0;
        _lastBaseScore = 0;
        _powerUpBonusScore = 0f;
        BuffTimeRemaining = 0;
        BuffName = null;
        IsDeathSequence = false;
        _telemetryPlayer = null;
        State = GameState.Playing;
        OnStateChanged.Invoke(State);
        OnScoreChanged.Invoke(0);
        OnCoinsChanged.Invoke(0);
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
        FinishTelemetry("menu");
        EchoRunSaveSystem.SaveLegacyState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void GameOver()
    {
        if (IsDeathSequence) return;
        IsDeathSequence = true;
        var player = GameObject.Find("player");
        if (player != null) ParticleManager.Instance?.EmitDeath(player.transform.position);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayDeath();
        if (AudioManager.Instance != null) AudioManager.Instance.StopFootsteps();
        StartCoroutine(DeathSequenceCoroutine());
    }

    System.Collections.IEnumerator DeathSequenceCoroutine()
    {
        Time.timeScale = 0.3f;
        yield return new WaitForSecondsRealtime(1.2f);
        Time.timeScale = 1f;
        State = GameState.GameOver;
        SaveHighScore();
        OnStateChanged.Invoke(State);
        FinishTelemetry("game_over");
        IsDeathSequence = false;
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        AudioManager.Instance?.StopFootsteps();
        InputManager.Instance?.ClearInput();
        FinishTelemetry("restart");
        EchoRunSaveSystem.SaveLegacyState();
        _startAfterSceneLoad = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void AddCoins(int amount)
    {
        Coins += amount;
        OnCoinsChanged.Invoke(Coins);
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

    private static int CreateRunSeed(int sequence)
    {
        unchecked
        {
            long ticks = System.DateTime.UtcNow.Ticks;
            return (int)(ticks ^ (ticks >> 32) ^ (sequence * 486187739));
        }
    }

    private void FinishTelemetry(string reason)
    {
        AIPlayerSkillEstimator.EndRun(Distance);
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
