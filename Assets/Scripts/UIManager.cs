using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // ── Menu ──
    GameObject _menuPanel;
    Button _startBtn, _settingsBtn, _characterBtn;
    Text _menuShadowText;

    // ── Settings (sub-panel of menu) ──
    GameObject _settingsPanel;
    Slider _bgmSlider, _sfxSlider;
    Button _fps30Btn, _fps60Btn, _fps120Btn;
    Button _settingsBackBtn;

    // ── Character (sub-panel of menu) ──
    GameObject _characterPanel;
    Button _characterBackBtn;

    // ── HUD ──
    GameObject _hudPanel;
    Text _statsText, _aiDirectorText, _aiShadowText;
    GameObject _buffGroup;
    Text _buffText;
    Button _pauseBtn;

    // ── Pause ──
    GameObject _pausePanel;
    Button _resumeBtn, _pauseToMenuBtn;

    // ── GameOver ──
    GameObject _gameOverPanel;
    Text _finalScoreText, _highScoreText, _coinResultText, _shadowResultText;
    Text _gameOverTitleText, _gameOverStatsText;
    Button _restartBtn, _goToMenuBtn;

    private Font _font;
    private Font _titleFont;
    private GameManager _gm;

    void Start()
    {
        _gm = GameManager.Instance;
        if (_gm == null) return;

        _font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
        if (_font == null)
            _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
        if (_font == null)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Debug.LogWarning("Bundled Noto Sans CJK font is missing; Chinese text may not render.");
        }
        _titleFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        EnsureCanvas();
        CreateMenuPanel();
        CreateSettingsPanel();
        CreateCharacterPanel();
        CreateHUDPanel();
        CreatePausePanel();
        CreateGameOverPanel();

        _gm.OnStateChanged.AddListener(OnGameStateChanged);
        _gm.OnScoreChanged.AddListener(OnScoreChanged);
        _gm.OnCoinsChanged.AddListener(OnCoinsChanged);
        _gm.OnDistanceChanged.AddListener(OnDistanceChanged);

        OnGameStateChanged(_gm.State);
        LoadCharacterPreset();
    }

    void Update()
    {
        // Buff timer display
        if (_buffGroup != null && _gm != null && _gm.State == GameState.Playing)
        {
            bool active = _gm.BuffTimeRemaining > 0f;
            if (_buffGroup.activeSelf != active)
                _buffGroup.SetActive(active);
            if (active && _buffText != null)
                _buffText.text = string.Format("{0} {1:F1}s", _gm.BuffName ?? "Buff", _gm.BuffTimeRemaining);
        }

        if (_aiDirectorText != null && AITrackDirector.Instance != null)
        {
            string status = AITrackDirector.Instance.CurrentStatus;
            if (_aiDirectorText.text != status) _aiDirectorText.text = status;
        }

        if (_aiShadowText != null && AIShadowRunner.Instance != null)
        {
            string status = AIShadowRunner.Instance.CurrentStatus;
            if (_aiShadowText.text != status) _aiShadowText.text = status;
        }
    }

    // ═══════════════════════════════════════════════════
    //  Canvas
    // ═══════════════════════════════════════════════════

    void EnsureCanvas()
    {
        if (FindObjectOfType<Canvas>() != null) return;

        GameObject cgo = new GameObject("Canvas");
        Canvas canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        cgo.AddComponent<GraphicRaycaster>();

        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    // ═══════════════════════════════════════════════════
    //  Menu Panel
    // ═══════════════════════════════════════════════════

    void CreateMenuPanel()
    {
        _menuPanel = NewPanel("MenuPanel", new Color(0, 0, 0, 0.85f));

        Text title = MakeText("Title", _menuPanel.transform, "TEMPLE RUN", 80, TextAnchor.MiddleCenter);
        if (_titleFont != null) title.font = _titleFont;
        title.color = new Color(1f, 0.85f, 0.1f);
        title.fontStyle = FontStyle.Bold;
        AddOutline(title.gameObject, new Color(0.4f, 0.2f, 0f));
        AddShadow(title.gameObject, new Color(0, 0, 0, 0.8f));
        AnchorText(title.GetComponent<RectTransform>(), 0.5f, 0.68f, 700, 110);

        _menuShadowText = MakeText("ShadowMode", _menuPanel.transform,
            "首局校准：AI 将学习你的跑酷习惯", 28, TextAnchor.MiddleCenter);
        _menuShadowText.color = new Color(0.25f, 0.9f, 1f);
        _menuShadowText.fontStyle = FontStyle.Bold;
        AddOutline(_menuShadowText.gameObject, new Color(0, 0.15f, 0.2f, 0.9f));
        AnchorText(_menuShadowText.GetComponent<RectTransform>(), 0.5f, 0.55f, 760, 60);

        // Three action buttons stacked
        _startBtn = MakeButton("StartBtn", _menuPanel.transform, "开始游戏", 42,
            new Vector2(0.5f, 0.42f), new Vector2(440, 110),
            new Color(0.15f, 0.7f, 0.2f), new Color(0.1f, 0.5f, 0.15f));
        _startBtn.onClick.AddListener(() => _gm.StartGame());

        _settingsBtn = MakeButton("SettingsBtn", _menuPanel.transform, "设置", 36,
            new Vector2(0.5f, 0.28f), new Vector2(320, 80),
            new Color(0.25f, 0.35f, 0.55f), new Color(0.15f, 0.22f, 0.38f));
        _settingsBtn.onClick.AddListener(ShowSettings);

        _characterBtn = MakeButton("CharacterBtn", _menuPanel.transform, "角色", 36,
            new Vector2(0.5f, 0.18f), new Vector2(320, 80),
            new Color(0.35f, 0.28f, 0.5f), new Color(0.22f, 0.17f, 0.35f));
        _characterBtn.onClick.AddListener(ShowCharacter);

        _menuPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════
    //  Settings Panel (sub-menu)
    // ═══════════════════════════════════════════════════

    void CreateSettingsPanel()
    {
        _settingsPanel = NewPanel("SettingsPanel", new Color(0, 0, 0, 0.92f));

        // ScrollRect setup
        ScrollRect scroll = _settingsPanel.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        GameObject viewport = new GameObject("Viewport", typeof(Image), typeof(Mask));
        viewport.transform.SetParent(_settingsPanel.transform, false);
        viewport.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        RectTransform vpRT = viewport.GetComponent<RectTransform>();
        vpRT.anchorMin = new Vector2(0, 0); vpRT.anchorMax = new Vector2(1, 1);
        vpRT.offsetMin = new Vector2(20, 20); vpRT.offsetMax = new Vector2(-20, -20);

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform ctRT = content.AddComponent<RectTransform>();
        ctRT.anchorMin = new Vector2(0.5f, 1f); ctRT.anchorMax = new Vector2(0.5f, 1f);
        ctRT.pivot = new Vector2(0.5f, 1f);
        ctRT.sizeDelta = new Vector2(1080, 850);
        ctRT.anchoredPosition = Vector2.zero;

        scroll.viewport = vpRT;
        scroll.content = ctRT;

        Transform c = content.transform;
        float topY = 0.92f;

        Text title = MakeText("SettingsTitle", c, "设置", 56, TextAnchor.MiddleCenter);
        title.color = Color.white;
        title.fontStyle = FontStyle.Bold;
        AnchorText(title.GetComponent<RectTransform>(), 0.5f, topY, 400, 70);

        MakeLabel("BgmLabel", c, "BGM 音量", new Vector2(0.5f, 0.80f));
        _bgmSlider = MakeSlider("BgmSlider", c, new Vector2(0.5f, 0.73f));
        float savedBgm = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        _bgmSlider.value = savedBgm;
        _bgmSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetMusicVolume(v));

        MakeLabel("SfxLabel", c, "SFX 音量", new Vector2(0.5f, 0.63f));
        _sfxSlider = MakeSlider("SfxSlider", c, new Vector2(0.5f, 0.56f));
        float savedSfx = PlayerPrefs.GetFloat("SfxVolume", 1f);
        _sfxSlider.value = savedSfx;
        _sfxSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetSfxVolume(v));

        MakeLabel("FpsLabel", c, "帧率", new Vector2(0.5f, 0.46f));
        _fps30Btn  = MakeSmallButton("Fps30", c, "30",
            new Vector2(0.25f, 0.38f), new Vector2(140, 60), new Color(0.3f, 0.3f, 0.35f));
        _fps60Btn  = MakeSmallButton("Fps60", c, "60",
            new Vector2(0.5f, 0.38f), new Vector2(140, 60), new Color(0.3f, 0.3f, 0.35f));
        _fps120Btn = MakeSmallButton("Fps120", c, "120",
            new Vector2(0.75f, 0.38f), new Vector2(140, 60), new Color(0.3f, 0.3f, 0.35f));

        _fps30Btn.onClick.AddListener(() => { _gm.SetFrameRate(30);  HighlightFps(); });
        _fps60Btn.onClick.AddListener(() => { _gm.SetFrameRate(60);  HighlightFps(); });
        _fps120Btn.onClick.AddListener(() => { _gm.SetFrameRate(120); HighlightFps(); });
        HighlightFps();

        _settingsBackBtn = MakeButton("SettingsBackBtn", c, "返回", 34,
            new Vector2(0.5f, 0.22f), new Vector2(280, 76),
            new Color(0.5f, 0.25f, 0.2f), new Color(0.35f, 0.15f, 0.1f));
        _settingsBackBtn.onClick.AddListener(HideSettings);

        _settingsPanel.SetActive(false);
    }

    void ShowSettings()
    {
        if (_menuPanel != null) _menuPanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(true);
    }

    void HideSettings()
    {
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_menuPanel != null) _menuPanel.SetActive(true);
    }

    void HighlightFps()
    {
        int cur = _gm != null ? _gm.GetFrameRate() : 60;
        Color active   = new Color(0.2f, 0.75f, 1f);
        Color inactive = new Color(0.3f, 0.3f, 0.35f);
        SetBtnColor(_fps30Btn,  cur == 30  ? active : inactive);
        SetBtnColor(_fps60Btn,  cur == 60  ? active : inactive);
        SetBtnColor(_fps120Btn, cur == 120 ? active : inactive);
    }

    void SetBtnColor(Button btn, Color c)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = c;
    }

    // ═══════════════════════════════════════════════════
    //  Character Panel (sub-menu)
    // ═══════════════════════════════════════════════════

    static readonly (string name, Color cloth, Color pants)[] _presets = {
        ("默认", new Color(0.17f, 0.24f, 0.31f), new Color(0.13f, 0.18f, 0.25f)),
        ("红色", new Color(0.75f, 0.15f, 0.10f), new Color(0.18f, 0.12f, 0.15f)),
        ("蓝色", new Color(0.12f, 0.30f, 0.70f), new Color(0.10f, 0.15f, 0.35f)),
        ("绿色", new Color(0.12f, 0.65f, 0.28f), new Color(0.08f, 0.28f, 0.14f)),
        ("金色", new Color(0.85f, 0.70f, 0.15f), new Color(0.50f, 0.40f, 0.10f)),
        ("暗黑", new Color(0.15f, 0.15f, 0.18f), new Color(0.10f, 0.10f, 0.12f)),
    };

    void CreateCharacterPanel()
    {
        _characterPanel = NewPanel("CharacterPanel", new Color(0, 0, 0, 0.92f));

        // ScrollRect
        ScrollRect scroll = _characterPanel.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        // Viewport
        GameObject vp = new GameObject("Viewport", typeof(Image), typeof(Mask));
        vp.transform.SetParent(_characterPanel.transform, false);
        vp.GetComponent<Image>().color = new Color(1, 1, 1, 0.01f);
        vp.GetComponent<Mask>().showMaskGraphic = false;
        RectTransform vpRT = vp.GetComponent<RectTransform>();
        vpRT.anchorMin = new Vector2(0, 0); vpRT.anchorMax = new Vector2(1, 1);
        vpRT.offsetMin = new Vector2(20, 20); vpRT.offsetMax = new Vector2(-20, -20);

        // Content
        GameObject content = new GameObject("Content");
        content.transform.SetParent(vp.transform, false);
        RectTransform ctRT = content.AddComponent<RectTransform>();
        ctRT.anchorMin = new Vector2(0.5f, 1f); ctRT.anchorMax = new Vector2(0.5f, 1f);
        ctRT.pivot = new Vector2(0.5f, 1f);
        ctRT.sizeDelta = new Vector2(1080, 700);
        ctRT.anchoredPosition = Vector2.zero;

        scroll.viewport = vpRT;
        scroll.content = ctRT;

        Transform c = content.transform;

        Text title = MakeText("CharTitle", c, "角色选择", 50, TextAnchor.MiddleCenter);
        title.color = Color.white;
        title.fontStyle = FontStyle.Bold;
        AnchorText(title.GetComponent<RectTransform>(), 0.5f, 0.90f, 400, 60);

        // 2 rows × 3 columns of color presets inside scroll content
        float[] colX = { 0.18f, 0.5f, 0.82f };
        float[] rowY = { 0.65f, 0.38f };

        for (int r = 0; r < 2; r++)
        {
            for (int col = 0; col < 3; col++)
            {
                int idx = r * 3 + col;
                if (idx >= _presets.Length) break;
                var preset = _presets[idx];
                CreatePresetButton(preset.name, preset.cloth, preset.pants, idx,
                    new Vector2(colX[col], rowY[r]), c);
            }
        }

        _characterBackBtn = MakeButton("CharBackBtn", c, "返回", 34,
            new Vector2(0.5f, 0.12f), new Vector2(280, 76),
            new Color(0.5f, 0.25f, 0.2f), new Color(0.35f, 0.15f, 0.1f));
        _characterBackBtn.onClick.AddListener(HideCharacter);

        _characterPanel.SetActive(false);
    }

    void CreatePresetButton(string label, Color cloth, Color pants, int index,
        Vector2 anchor, Transform parent)
    {
        Button btn = MakeSmallButton("PresetBtn_" + index, parent, "",
            anchor, new Vector2(150, 150), cloth);
        btn.onClick.AddListener(() => ApplyCharacterColor(index));

        Text autoLabel = btn.GetComponentInChildren<Text>();
        if (autoLabel != null) Destroy(autoLabel.gameObject);

        Text nameLabel = MakeText("PresetLabel_" + index, parent,
            label, 26, TextAnchor.MiddleCenter);
        nameLabel.color = new Color(0.85f, 0.85f, 0.85f);
        AnchorText(nameLabel.GetComponent<RectTransform>(), anchor.x, anchor.y - 0.06f, 150, 30);
    }

    void ApplyCharacterColor(int presetIndex)
    {
        if (presetIndex < 0 || presetIndex >= _presets.Length) return;
        var preset = _presets[presetIndex];

        var player = GameObject.Find("player");
        if (player == null) return;
        var model = player.transform.Find("CharacterModel");
        if (model == null) return;

        foreach (var mr in model.GetComponentsInChildren<MeshRenderer>())
        {
            // material access triggers automatic instancing — fine for runtime
            foreach (var mat in mr.materials)
            {
                if (mat.name.Contains("Cloth")) mat.color = preset.cloth;
                else if (mat.name.Contains("Pants")) mat.color = preset.pants;
            }
        }

        PlayerPrefs.SetInt("CharacterPreset", presetIndex);
        PlayerPrefs.Save();
    }

    void LoadCharacterPreset()
    {
        int idx = PlayerPrefs.GetInt("CharacterPreset", 0);
        if (idx > 0) ApplyCharacterColor(idx);
    }

    void ShowCharacter()
    {
        if (_menuPanel != null) _menuPanel.SetActive(false);
        if (_characterPanel != null) _characterPanel.SetActive(true);
    }

    void HideCharacter()
    {
        if (_characterPanel != null) _characterPanel.SetActive(false);
        if (_menuPanel != null) _menuPanel.SetActive(true);
    }

    // ═══════════════════════════════════════════════════
    //  HUD Panel
    // ═══════════════════════════════════════════════════

    void CreateHUDPanel()
    {
        // Top-left bar
        _hudPanel = NewPanel("HudPanel", new Color(0, 0, 0, 0.55f));
        RectTransform rt = _hudPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(620, 270);
        rt.anchoredPosition = new Vector2(20, -20);

        float y = -130f;
        float rowH = 40f;
        float leftX = 16f;

        // Row 4: AI director state
        _aiDirectorText = MakeHUDText("AIDirectorText", _hudPanel.transform,
            "AI导演 · 正在观察", 22, new Vector2(leftX, y), new Vector2(420, rowH));
        _aiDirectorText.color = new Color(0.25f, 0.9f, 1f);
        y -= rowH;

        // Row 5: behavior-cloned opponent state
        _aiShadowText = MakeHUDText("AIShadowText", _hudPanel.transform,
            "AI影子 · 校准中", 22, new Vector2(leftX, y), new Vector2(560, rowH));
        _aiShadowText.color = new Color(0.35f, 1f, 0.75f);
        y -= rowH;

        // Row 6: Buff (hidden by default)
        _buffGroup = new GameObject("BuffGroup", typeof(RectTransform));
        _buffGroup.transform.SetParent(_hudPanel.transform, false);
        RectTransform bgRT = _buffGroup.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 1); bgRT.anchorMax = new Vector2(0, 1);
        bgRT.pivot = new Vector2(0, 1);
        bgRT.anchoredPosition = new Vector2(leftX, y);
        bgRT.sizeDelta = new Vector2(300, 24);

        Text buffIcon = MakeText("BuffIcon", _buffGroup.transform, "▶", 20, TextAnchor.MiddleLeft);
        buffIcon.color = new Color(0.3f, 1f, 0.5f);
        RectTransform biRT = buffIcon.GetComponent<RectTransform>();
        biRT.anchorMin = new Vector2(0, 0.5f); biRT.anchorMax = new Vector2(0, 0.5f);
        biRT.pivot = new Vector2(0, 0.5f);
        biRT.anchoredPosition = new Vector2(0, 0);
        biRT.sizeDelta = new Vector2(24, 24);

        _buffText = MakeText("BuffText", _buffGroup.transform, "", 22, TextAnchor.MiddleLeft);
        _buffText.color = new Color(0.3f, 1f, 0.5f);
        RectTransform btRT = _buffText.GetComponent<RectTransform>();
        btRT.anchorMin = new Vector2(0, 0.5f); btRT.anchorMax = new Vector2(0, 0.5f);
        btRT.pivot = new Vector2(0, 0.5f);
        btRT.anchoredPosition = new Vector2(28, 0);
        btRT.sizeDelta = new Vector2(180, 24);

        _buffGroup.SetActive(false);

        // Created after the dynamic AI rows so the core counters stay on top
        // when WebGL rebuilds the dynamic font atlas.
        _statsText = MakeHUDText("StatsText", _hudPanel.transform,
            "得分  0\n距离  0m\n金币  0", 26,
            new Vector2(leftX, -8f), new Vector2(300, 120));
        _statsText.lineSpacing = 1.05f;

        // Pause button (right side of HUD bar)
        _pauseBtn = MakeIconButton("PauseBtn", _hudPanel.transform, "II",
            new Vector2(1, 0.5f), new Vector2(56, 56),
            new Color(0.3f, 0.3f, 0.35f));
        _pauseBtn.onClick.AddListener(() => _gm.Pause());

        _hudPanel.SetActive(false);
    }

    Text MakeHUDText(string name, Transform parent, string content, int size,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        Text t = MakeText(name, parent, content, size, TextAnchor.MiddleLeft);
        t.color = Color.white;
        t.fontStyle = FontStyle.Bold;
        AddOutline(t.gameObject, new Color(0, 0, 0, 0.6f));
        RectTransform rt = t.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = sizeDelta;
        return t;
    }

    // ═══════════════════════════════════════════════════
    //  Pause Panel
    // ═══════════════════════════════════════════════════

    void CreatePausePanel()
    {
        _pausePanel = NewPanel("PausePanel", new Color(0, 0, 0, 0.75f));

        Text title = MakeText("PauseTitle", _pausePanel.transform, "已暂停", 64, TextAnchor.MiddleCenter);
        title.color = Color.white;
        title.fontStyle = FontStyle.Bold;
        AddOutline(title.gameObject, new Color(0, 0, 0, 0.6f));
        AnchorText(title.GetComponent<RectTransform>(), 0.5f, 0.58f, 400, 80);

        _resumeBtn = MakeButton("ResumeBtn", _pausePanel.transform, "继续游戏", 38,
            new Vector2(0.5f, 0.38f), new Vector2(400, 100),
            new Color(0.15f, 0.7f, 0.2f), new Color(0.1f, 0.5f, 0.15f));
        _resumeBtn.onClick.AddListener(() => _gm.Resume());

        _pauseToMenuBtn = MakeButton("PauseToMenuBtn", _pausePanel.transform, "返回主页", 32,
            new Vector2(0.5f, 0.22f), new Vector2(320, 80),
            new Color(0.5f, 0.3f, 0.25f), new Color(0.35f, 0.18f, 0.15f));
        _pauseToMenuBtn.onClick.AddListener(() => _gm.ReturnToMenu());

        _pausePanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════
    //  GameOver Panel
    // ═══════════════════════════════════════════════════

    void CreateGameOverPanel()
    {
        _gameOverPanel = NewPanel("GameOverPanel", new Color(0, 0, 0, 0.88f));

        Text title = MakeText("GOTitle", _gameOverPanel.transform, "Game Over", 68, TextAnchor.MiddleCenter);
        title.color = new Color(1f, 0.2f, 0.15f);
        title.fontStyle = FontStyle.Bold;
        AddOutline(title.gameObject, new Color(0.5f, 0.05f, 0f));
        AddShadow(title.gameObject, new Color(0, 0, 0, 0.8f));
        AnchorText(title.GetComponent<RectTransform>(), 0.5f, 0.76f, 500, 90);
        title.gameObject.SetActive(false);

        // Session score
        _finalScoreText = MakeText("FinalScore", _gameOverPanel.transform, "得分: 0", 48, TextAnchor.MiddleCenter);
        _finalScoreText.color = Color.white;
        _finalScoreText.fontStyle = FontStyle.Bold;
        AddOutline(_finalScoreText.gameObject, new Color(0, 0, 0, 0.6f));
        AnchorText(_finalScoreText.GetComponent<RectTransform>(), 0.5f, 0.61f, 450, 70);
        _finalScoreText.gameObject.SetActive(false);

        // High score
        _highScoreText = MakeText("HighScore", _gameOverPanel.transform, "最高分: 0", 36, TextAnchor.MiddleCenter);
        _highScoreText.color = new Color(1f, 0.85f, 0.1f);
        _highScoreText.fontStyle = FontStyle.Bold;
        AddOutline(_highScoreText.gameObject, new Color(0.3f, 0.2f, 0f));
        AnchorText(_highScoreText.GetComponent<RectTransform>(), 0.5f, 0.52f, 400, 50);
        _highScoreText.gameObject.SetActive(false);

        // Coins
        _coinResultText = MakeText("CoinResult", _gameOverPanel.transform, "金币: 0", 32, TextAnchor.MiddleCenter);
        _coinResultText.color = new Color(1f, 0.85f, 0.1f);
        AnchorText(_coinResultText.GetComponent<RectTransform>(), 0.5f, 0.45f, 500, 40);
        _coinResultText.gameObject.SetActive(false);

        _shadowResultText = MakeText("ShadowResult", _gameOverPanel.transform,
            "AI影子正在生成赛后分析", 28, TextAnchor.MiddleCenter);
        _shadowResultText.color = new Color(0.3f, 0.95f, 1f);
        _shadowResultText.fontStyle = FontStyle.Bold;
        AnchorText(_shadowResultText.GetComponent<RectTransform>(), 0.5f, 0.35f, 820, 80);

        // Restart
        _restartBtn = MakeButton("RestartBtn", _gameOverPanel.transform, "挑战下一代", 38,
            new Vector2(0.5f, 0.20f), new Vector2(400, 100),
            new Color(0.15f, 0.7f, 0.2f), new Color(0.1f, 0.5f, 0.15f));
        _restartBtn.onClick.AddListener(() => _gm.Restart());

        // Back to menu
        _goToMenuBtn = MakeButton("GoToMenuBtn", _gameOverPanel.transform, "返回主页", 32,
            new Vector2(0.5f, 0.08f), new Vector2(320, 80),
            new Color(0.35f, 0.35f, 0.4f), new Color(0.2f, 0.2f, 0.25f));
        _goToMenuBtn.onClick.AddListener(() => _gm.ReturnToMenu());

        // Create consolidated result text last so WebGL dynamic-font atlas rebuilds
        // cannot leave the earlier score rows without geometry.
        _gameOverTitleText = MakeText("GameOverTitle", _gameOverPanel.transform,
            "跑酷结算", 58, TextAnchor.MiddleCenter);
        _gameOverTitleText.color = new Color(1f, 0.35f, 0.18f);
        _gameOverTitleText.fontStyle = FontStyle.Bold;
        AddOutline(_gameOverTitleText.gameObject, new Color(0.4f, 0.05f, 0f));
        AnchorText(_gameOverTitleText.GetComponent<RectTransform>(), 0.5f, 0.76f, 600, 80);

        _gameOverStatsText = MakeText("GameOverStats", _gameOverPanel.transform,
            "得分  0\n最高  0\n金币  0", 34, TextAnchor.MiddleCenter);
        _gameOverStatsText.color = Color.white;
        _gameOverStatsText.fontStyle = FontStyle.Bold;
        _gameOverStatsText.lineSpacing = 1.05f;
        AddOutline(_gameOverStatsText.gameObject, new Color(0, 0, 0, 0.7f));
        AnchorText(_gameOverStatsText.GetComponent<RectTransform>(), 0.5f, 0.55f, 620, 150);

        _gameOverPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════
    //  State Switching
    // ═══════════════════════════════════════════════════

    void OnGameStateChanged(GameState state)
    {
        // Hide all first
        if (_menuPanel != null) _menuPanel.SetActive(false);
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_characterPanel != null) _characterPanel.SetActive(false);
        if (_hudPanel != null) _hudPanel.SetActive(false);
        if (_pausePanel != null) _pausePanel.SetActive(false);
        if (_gameOverPanel != null) _gameOverPanel.SetActive(false);

        switch (state)
        {
            case GameState.Menu:
                if (_menuPanel != null) _menuPanel.SetActive(true);
                if (AIShadowRunner.Instance != null)
                {
                    string menuStatus = AIShadowRunner.Instance.GetMenuStatus();
                    if (_menuShadowText != null) _menuShadowText.text = menuStatus;
                    Text startLabel = _startBtn != null
                        ? _startBtn.GetComponentInChildren<Text>()
                        : null;
                    if (startLabel != null)
                        startLabel.text = AIShadowRunner.Instance.Generation > 0
                            ? "挑战 AI 影子"
                            : "开始校准";
                }
                break;

            case GameState.Playing:
                if (_hudPanel != null) _hudPanel.SetActive(true);
                OnScoreChanged(_gm != null ? _gm.Score : 0);
                OnCoinsChanged(_gm != null ? _gm.Coins : 0);
                OnDistanceChanged(_gm != null ? _gm.Distance : 0);
                break;

            case GameState.Paused:
                if (_hudPanel != null) _hudPanel.SetActive(true);
                if (_pausePanel != null) _pausePanel.SetActive(true);
                break;

            case GameState.GameOver:
                if (_gameOverPanel != null) _gameOverPanel.SetActive(true);
                if (_shadowResultText != null && AIShadowRunner.Instance != null)
                    _shadowResultText.text = AIShadowRunner.Instance.FinalizeRunIfNeeded();
                if (_gm != null)
                {
                    string newRecord = _gm.IsNewHighScore ? "\n新纪录!" : "";
                    if (_finalScoreText != null)
                        _finalScoreText.text = "得分: " + _gm.Score + newRecord;
                    if (_highScoreText != null)
                        _highScoreText.text = "最高分: " + _gm.HighScore;
                    if (_coinResultText != null)
                        _coinResultText.text = "金币: " + _gm.Coins + "  |  总计: " + _gm.TotalCoins;
                    if (_gameOverStatsText != null)
                        _gameOverStatsText.text = "得分  " + _gm.Score + newRecord
                                                   + "\n最高  " + _gm.HighScore
                                                   + "\n金币  " + _gm.Coins
                                                   + "  ·  总计 " + _gm.TotalCoins;
                }
                break;
        }
    }

    void OnScoreChanged(int score)
    {
        RefreshStats();
    }

    void OnDestroy()
    {
        if (_gm == null) return;
        _gm.OnStateChanged.RemoveListener(OnGameStateChanged);
        _gm.OnScoreChanged.RemoveListener(OnScoreChanged);
        _gm.OnCoinsChanged.RemoveListener(OnCoinsChanged);
        _gm.OnDistanceChanged.RemoveListener(OnDistanceChanged);
    }

    void OnCoinsChanged(int coins)
    {
        RefreshStats();
    }

    void OnDistanceChanged(float dist)
    {
        RefreshStats();
    }

    void RefreshStats()
    {
        if (_statsText == null || _gm == null) return;
        _statsText.text = "得分  " + _gm.Score
                          + "\n距离  " + Mathf.FloorToInt(_gm.Distance) + "m"
                          + "\n金币  " + _gm.Coins;
    }

    // ═══════════════════════════════════════════════════
    //  UI Helpers
    // ═══════════════════════════════════════════════════

    GameObject NewPanel(string name, Color color)
    {
        GameObject panel = new GameObject(name, typeof(Image));
        panel.GetComponent<Image>().color = color;
        Canvas canvas = FindObjectOfType<Canvas>();
        panel.transform.SetParent(canvas != null ? canvas.transform : transform, false);
        Stretch(panel.GetComponent<RectTransform>());
        return panel;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void AnchorText(RectTransform rt, float ax, float ay, float w, float h)
    {
        rt.anchorMin = new Vector2(ax, ay); rt.anchorMax = new Vector2(ax, ay);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
    }

    Text MakeText(string name, Transform parent, string content, int size, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        Text t = go.GetComponent<Text>();
        t.text = content;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        if (_font != null) t.font = _font;
        return t;
    }

    void MakeLabel(string name, Transform parent, string content, Vector2 anchor)
    {
        Text label = MakeText(name, parent, content, 30, TextAnchor.MiddleCenter);
        label.color = new Color(0.8f, 0.8f, 0.8f);
        AnchorText(label.GetComponent<RectTransform>(), anchor.x, anchor.y, 300, 40);
    }

    void AddOutline(GameObject go, Color color)
    {
        Outline o = go.AddComponent<Outline>();
        o.effectColor = color;
        o.effectDistance = new Vector2(2.5f, -2.5f);
    }

    void AddShadow(GameObject go, Color color)
    {
        Shadow s = go.AddComponent<Shadow>();
        s.effectColor = color;
        s.effectDistance = new Vector2(3f, -3f);
    }

    Button MakeButton(string name, Transform parent, string label, int fontSize,
        Vector2 anchor, Vector2 size, Color mainColor, Color edgeColor)
    {
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = mainColor;

        // Border / depth
        GameObject border = new GameObject("Border", typeof(Image));
        border.transform.SetParent(go.transform, false);
        border.GetComponent<Image>().color = edgeColor;
        RectTransform br = border.GetComponent<RectTransform>();
        Stretch(br);
        br.offsetMin = new Vector2(4, 4);
        br.offsetMax = new Vector2(-4, -4);

        Text labelT = MakeText("Label", go.transform, label, fontSize, TextAnchor.MiddleCenter);
        labelT.color = Color.white;
        labelT.fontStyle = FontStyle.Bold;
        AddOutline(labelT.gameObject, new Color(0, 0, 0, 0.5f));
        Stretch(labelT.GetComponent<RectTransform>());

        return go.GetComponent<Button>();
    }

    Button MakeSmallButton(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = color;

        Text labelT = MakeText("Label", go.transform, label, 28, TextAnchor.MiddleCenter);
        labelT.color = Color.white;
        labelT.fontStyle = FontStyle.Bold;
        Stretch(labelT.GetComponent<RectTransform>());

        return go.GetComponent<Button>();
    }

    Button MakeIconButton(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = color;

        Text labelT = MakeText("Label", go.transform, label, 28, TextAnchor.MiddleCenter);
        labelT.color = Color.white;
        labelT.fontStyle = FontStyle.Bold;
        Stretch(labelT.GetComponent<RectTransform>());

        return go.GetComponent<Button>();
    }

    Slider MakeSlider(string name, Transform parent, Vector2 anchor)
    {
        GameObject go = new GameObject(name, typeof(Slider));
        go.transform.SetParent(parent, false);

        // Background
        GameObject bg = new GameObject("Background", typeof(Image));
        bg.transform.SetParent(go.transform, false);
        bg.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f);
        RectTransform bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.5f); bgRT.anchorMax = new Vector2(1, 0.5f);
        bgRT.sizeDelta = new Vector2(0, 16);
        bgRT.anchoredPosition = Vector2.zero;

        // Fill area
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        RectTransform faRT = fillArea.GetComponent<RectTransform>();
        Stretch(faRT);
        faRT.offsetMin = Vector2.zero; faRT.offsetMax = Vector2.zero;

        // Fill
        GameObject fill = new GameObject("Fill", typeof(Image));
        fill.transform.SetParent(fillArea.transform, false);
        fill.GetComponent<Image>().color = new Color(0.2f, 0.7f, 0.3f);
        RectTransform fRT = fill.GetComponent<RectTransform>();
        Stretch(fRT);

        // Handle slide area
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        RectTransform haRT = handleArea.GetComponent<RectTransform>();
        Stretch(haRT);
        haRT.offsetMin = new Vector2(-14, 0); haRT.offsetMax = new Vector2(14, 0);

        // Handle
        GameObject handle = new GameObject("Handle", typeof(Image));
        handle.transform.SetParent(handleArea.transform, false);
        handle.GetComponent<Image>().color = Color.white;
        RectTransform hRT = handle.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0, 0.5f); hRT.anchorMax = new Vector2(0, 0.5f);
        hRT.sizeDelta = new Vector2(32, 32);
        hRT.anchoredPosition = Vector2.zero;

        Slider slider = go.GetComponent<Slider>();
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.fillRect = fRT;
        slider.handleRect = hRT;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        RectTransform sRT = go.GetComponent<RectTransform>();
        sRT.anchorMin = anchor; sRT.anchorMax = anchor;
        sRT.sizeDelta = new Vector2(500, 40);
        sRT.anchoredPosition = Vector2.zero;

        return slider;
    }
}
