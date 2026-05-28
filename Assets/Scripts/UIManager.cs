using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Menu")]
    public GameObject menuPanel;
    public Button startButton;

    [Header("HUD")]
    public GameObject hudPanel;
    public Text scoreText;
    public Text coinText;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public Text finalScoreText;
    public Button restartButton;

    void Start()
    {
        if (startButton != null) startButton.onClick.AddListener(() => GameManager.Instance.StartGame());
        if (restartButton != null) restartButton.onClick.AddListener(() => GameManager.Instance.Restart());

        GameManager.Instance.OnStateChanged.AddListener(OnGameStateChanged);
        GameManager.Instance.OnScoreChanged.AddListener(UpdateScore);
        GameManager.Instance.OnCoinsChanged.AddListener(UpdateCoins);

        ShowMenu();
    }

    void OnGameStateChanged(GameState state)
    {
        switch (state)
        {
            case GameState.Menu:
                ShowMenu();
                break;
            case GameState.Playing:
                ShowHUD();
                break;
            case GameState.GameOver:
                ShowGameOver();
                break;
        }
    }

    void ShowMenu()
    {
        if (menuPanel != null) menuPanel.SetActive(true);
        if (hudPanel != null) hudPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    void ShowHUD()
    {
        if (menuPanel != null) menuPanel.SetActive(false);
        if (hudPanel != null) hudPanel.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        UpdateScore(0);
        UpdateCoins(0);
    }

    void ShowGameOver()
    {
        if (hudPanel != null) hudPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (finalScoreText != null) finalScoreText.text = $"Score: {GameManager.Instance.Score}";
    }

    void UpdateScore(int score)
    {
        if (scoreText != null) scoreText.text = $"Score: {score}";
    }

    void UpdateCoins(int coins)
    {
        if (coinText != null) coinText.text = $"{coins}";
    }
}
