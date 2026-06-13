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
   private Font _runtimeFont;

    void Start()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.OnStateChanged.AddListener(OnGameStateChanged);
        GameManager.Instance.OnScoreChanged.AddListener(UpdateScore);
        GameManager.Instance.OnCoinsChanged.AddListener(UpdateCoins);

        if (startButton != null) startButton.onClick.AddListener(() => GameManager.Instance.StartGame());
        if (restartButton != null) restartButton.onClick.AddListener(() => GameManager.Instance.Restart());

        if (fps30Button != null) fps30Button.onClick.AddListener(() => SetFps(30));
        if (fps60Button != null) fps60Button.onClick.AddListener(() => SetFps(60));
        if (fps120Button != null) fps120Button.onClick.AddListener(() => SetFps(120));

        HighlightFpsButton(60);

        if (scoreText == null || coinText == null || hudPanel == null)
        {
           BuildRuntimeHUD();
       }

        OnGameStateChanged(GameManager.Instance.State);
    }

    void BuildRuntimeHUD()
    {
        // Create font once
        _runtimeFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
        if (_runtimeFont == null)
            _runtimeFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Find or create Canvas
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject cgo = new GameObject("Canvas_Runtime");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            cgo.AddComponent<CanvasScaler>();
            cgo.AddComponent<GraphicRaycaster>();
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        // HUD panel
        if (hudPanel == null)
        {
            hudPanel = new GameObject("HudPanel_Runtime");
            hudPanel.transform.SetParent(canvas.transform, false);
            RectTransform rt = hudPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // Score text
        scoreText = MakeText("ScoreText_Runtime", "Score: 0", 38, new Color(0.1f, 1f, 0.3f),
            new Vector2(16, -16), new Vector2(0, 1));

        // Coin text
        coinText = MakeText("CoinText_Runtime", "0", 38, new Color(1f, 0.9f, 0.1f),
            new Vector2(320, -16), new Vector2(0, 1));

        Debug.Log("[UIManager] Runtime HUD created");
    }

    Text MakeText(string name, string content, int size, Color color,
        Vector2 pos, Vector2 anchor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(hudPanel.transform, false);
        Text t = go.AddComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleLeft;
        if (_runtimeFont != null) t.font = _runtimeFont;

        Outline o = go.AddComponent<Outline>();
        o.effectColor = new Color(0, 0, 0, 0.8f);
        o.effectDistance = new Vector2(2.5f, -2.5f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(anchor.x, anchor.y);
        rt.anchorMax = new Vector2(anchor.x, anchor.y);
        rt.pivot = new Vector2(anchor.x, anchor.y);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(400, 48);

        return t;
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
            case GameState.Menu:      ShowMenu(); break;
            case GameState.Playing:   ShowHUD(); break;
            case GameState.GameOver:  ShowGameOver(); break;
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
        UpdateScore(GameManager.Instance != null ? GameManager.Instance.Score : 0);
        UpdateCoins(GameManager.Instance != null ? GameManager.Instance.Coins : 0);
    }

    void ShowGameOver()
    {
        if (hudPanel != null) hudPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
       if (GameManager.Instance != null)
       {
            string hsTag = GameManager.Instance.IsNewHighScore ? "  NEW HIGH SCORE!" : "";
            if (finalScoreText != null)
                finalScoreText.text = "Score: " + GameManager.Instance.Score + "\nBest: " + GameManager.Instance.HighScore + hsTag;
            if (coinResultText != null)
                coinResultText.text = "Coins: " + GameManager.Instance.Coins + " | Total: " + GameManager.Instance.TotalCoins;
       }
    }

    void UpdateScore(int score)
    {
        if (scoreText != null) scoreText.text = "Score: " + score;
    }

    void UpdateCoins(int coins)
    {
        if (coinText != null) coinText.text = coins.ToString();
    }
}
