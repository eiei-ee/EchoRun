using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public enum GameState { Menu, Playing, GameOver }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Speed")]
    public float startSpeed = 10f;
    public float maxSpeed = 40f;
    public float speedIncreaseRate = 0.5f;

    [Header("Score")]
    public int coinScore = 10;

    [Header("Debug")]
    public bool autoStart = true;

    [Header("Frame Rate")]
    public int targetFrameRate = 60;

    public float CurrentSpeed { get; private set; }
    public GameState State { get; private set; } = GameState.Menu;
    public int Score { get; private set; }
    public int Coins { get; private set; }

    public UnityEvent<GameState> OnStateChanged;
    public UnityEvent<int> OnScoreChanged;
    public UnityEvent<int> OnCoinsChanged;

    private float _distanceTraveled;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        ApplyFrameRate();
    }

    void Start()
    {
        if (autoStart && State == GameState.Menu)
            StartGame();
    }

    public void SetFrameRate(int fps)
    {
        targetFrameRate = fps;
        if (targetFrameRate > 0)
            Application.targetFrameRate = targetFrameRate;
    }

    void ApplyFrameRate()
    {
        if (targetFrameRate > 0)
            Application.targetFrameRate = targetFrameRate;
    }

    void Update()
    {
        // Enter to start/restart (Space is reserved for jump via InputManager)
        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (State == GameState.Menu) StartGame();
            else if (State == GameState.GameOver) Restart();
        }

        if (State != GameState.Playing) return;

        CurrentSpeed = Mathf.Min(CurrentSpeed + speedIncreaseRate * Time.deltaTime, maxSpeed);
        _distanceTraveled += CurrentSpeed * Time.deltaTime;

        int newScore = Mathf.FloorToInt(_distanceTraveled) + Coins * coinScore;
        if (newScore != Score)
        {
            Score = newScore;
            OnScoreChanged.Invoke(Score);
        }
    }

    public void StartGame()
    {
        CurrentSpeed = startSpeed;
        Score = 0;
        Coins = 0;
        _distanceTraveled = 0;
        State = GameState.Playing;
        OnStateChanged.Invoke(State);
        OnScoreChanged.Invoke(0);
        OnCoinsChanged.Invoke(0);
    }

    public void GameOver()
    {
        State = GameState.GameOver;
        OnStateChanged.Invoke(State);
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void AddCoins(int amount)
    {
        Coins += amount;
        OnCoinsChanged.Invoke(Coins);
    }
}
