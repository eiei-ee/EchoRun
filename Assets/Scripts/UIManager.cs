using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Menu")]
    public GameObject menuPanel;
    public Button startButton;

    [Header("Frame Rate")]
    public Button fps30Button;
    public Button fps60Button;
    public Button fps120Button;

    [Header("HUD")]
    public GameObject hudPanel;
    public Text scoreText;
    public Text coinText;

    [Header("Game Over")]
    public GameObject gameOverPanel;
    public Text finalScoreText;
    public Text coinResultText;
    public Button restartButton;

    private Color _fpsActiveColor = new Color(0.2f, 0.75f, 1f);
    private Color _fpsInactiveColor = new Color(0.3f, 0.3f, 0.35f);

    void Start()
    {
        if (GameManager.Instance == null) return;

        if (startButton != null) startButton.onClick.AddListener(() => GameManager.Instance.StartGame());
        if (restartButton != null) restartButton.onClick.AddListener(() => GameManager.Instance.Restart());

        if (fps30Button != null) fps30Button.onClick.AddListener(() => SetFps(30));
        if (fps60Button != null) fps60Button.onClick.AddListener(() => SetFps(60));
        if (fps120Button != null) fps120Button.onClick.AddListener(() => SetFps(120));

        GameManager.Instance.OnStateChanged.AddListener(OnGameStateChanged);
        GameManager.Instance.OnScoreChanged.AddListener(UpdateScore);
        GameManager.Instance.OnCoinsChanged.AddListener(UpdateCoins);

        HighlightFpsButton(60);
        ShowMenu();
    }

    void SetFps(int fps)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.SetFrameRate(fps);
        HighlightFpsButton(fps);
    }

    void HighlightFpsButton(int fps)
    {
        SetBtnColor(fps30Button, fps == 30 ? _fpsActiveColor : _fpsInactiveColor);
        SetBtnColor(fps60Button, fps == 60 ? _fpsActiveColor : _fpsInactiveColor);
        SetBtnColor(fps120Button, fps == 120 ? _fpsActiveColor : _fpsInactiveColor);
    }

    void SetBtnColor(Button btn, Color c)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = c;
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
        if (GameManager.Instance != null)
        {
            if (finalScoreText != null) finalScoreText.text = $"Score: {GameManager.Instance.Score}";
            if (coinResultText != null) coinResultText.text = $"Coins: {GameManager.Instance.Coins}";
        }
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
