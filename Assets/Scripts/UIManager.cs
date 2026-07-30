using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    // Restrained graphite palette: neutral surfaces, muted steel-blue actions,
    // brass rewards, and red reserved for failures.
    private static readonly Color Backdrop = new Color(0.035f, 0.045f, 0.06f);
    private static readonly Color Surface = new Color(0.075f, 0.09f, 0.115f);
    private static readonly Color SurfaceRaised = new Color(0.12f, 0.145f, 0.18f);
    private static readonly Color Primary = new Color(0.42f, 0.58f, 0.72f);
    private static readonly Color PrimaryStrong = new Color(0.22f, 0.36f, 0.50f);
    private static readonly Color Reward = new Color(0.78f, 0.61f, 0.36f);
    private static readonly Color Danger = new Color(0.72f, 0.34f, 0.34f);
    private static readonly Color Success = new Color(0.44f, 0.62f, 0.68f);
    private static readonly Color TextPrimary = new Color(0.92f, 0.94f, 0.96f);
    private static readonly Color TextMuted = new Color(0.64f, 0.68f, 0.74f);
    private static readonly Color Ink = new Color(0.02f, 0.025f, 0.035f);

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
    GameObject _controlHint;
    Text _controlHintText;
    GameObject _landscapeGuard;

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
    private RectTransform _safeAreaRoot;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;
    private float _controlHintTimer;
    private Sprite _roundedSprite;
    private Texture2D _roundedTexture;

    private const float ControlHintDuration = 7f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<UIManager>() != null) return;
        new GameObject("UIManager_Runtime").AddComponent<UIManager>();
    }

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
        CreateControlHint();
        CreatePausePanel();
        CreateGameOverPanel();
        CreateLandscapeGuard();

        _gm.OnStateChanged.AddListener(OnGameStateChanged);
        _gm.OnScoreChanged.AddListener(OnScoreChanged);
        _gm.OnCoinsChanged.AddListener(OnCoinsChanged);
        _gm.OnDistanceChanged.AddListener(OnDistanceChanged);

        OnGameStateChanged(_gm.State);
        LoadCharacterPreset();
    }

    void Update()
    {
        ApplySafeArea();
        UpdateLandscapeGuard();

        if (_gm != null && _gm.State == GameState.Menu
            && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            _gm.StartGame();
        }

        if (_controlHint != null && _controlHint.activeSelf)
        {
            _controlHintTimer -= Time.unscaledDeltaTime;
            if (_controlHintTimer <= 0f)
                _controlHint.SetActive(false);
        }

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
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject cgo = new GameObject("Canvas");
            canvas = cgo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        if (canvas.GetComponent<GraphicRaycaster>() == null)
            canvas.gameObject.AddComponent<GraphicRaycaster>();

        Transform existingSafeArea = canvas.transform.Find("SafeArea");
        if (existingSafeArea != null)
        {
            _safeAreaRoot = existingSafeArea.GetComponent<RectTransform>();
        }
        else
        {
            GameObject safeArea = new GameObject("SafeArea", typeof(RectTransform));
            safeArea.transform.SetParent(canvas.transform, false);
            _safeAreaRoot = safeArea.GetComponent<RectTransform>();
            Stretch(_safeAreaRoot);
        }
        ApplySafeArea(true);

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
        _menuPanel = NewPanel("MenuPanel", WithAlpha(Backdrop, 0.78f));
        AddMenuGrid(_menuPanel.transform);

        Text protocol = MakeText("Protocol", _menuPanel.transform,
            "ADAPTIVE RIVAL PROTOCOL  //  07", 16, TextAnchor.MiddleCenter);
        protocol.color = Primary;
        protocol.fontStyle = FontStyle.Bold;
        AnchorText(protocol.GetComponent<RectTransform>(), 0.5f, 0.78f, 620, 30);

        Text title = MakeText("Title", _menuPanel.transform, "ECHO//RUN", 76, TextAnchor.MiddleCenter);
        if (_titleFont != null) title.font = _titleFont;
        title.color = TextPrimary;
        title.fontStyle = FontStyle.Bold;
        AddShadow(title.gameObject, WithAlpha(Ink, 0.9f));
        AnchorText(title.GetComponent<RectTransform>(), 0.5f, 0.68f, 760, 92);

        Text subtitle = MakeText("Subtitle", _menuPanel.transform,
            "回声竞速实验", 22, TextAnchor.MiddleCenter);
        subtitle.color = Reward;
        AnchorText(subtitle.GetComponent<RectTransform>(), 0.5f, 0.61f, 420, 36);

        _menuShadowText = MakeText("ShadowMode", _menuPanel.transform,
            "校准阶段  //  等待跑者数据", 21, TextAnchor.MiddleCenter);
        _menuShadowText.color = TextMuted;
        _menuShadowText.fontStyle = FontStyle.Bold;
        AnchorText(_menuShadowText.GetComponent<RectTransform>(), 0.5f, 0.53f, 780, 38);

        _startBtn = MakeButton("StartBtn", _menuPanel.transform, "启动校准", 28,
            new Vector2(0.5f, 0.39f), new Vector2(360, 68),
            WithAlpha(PrimaryStrong, 0.98f), Primary);
        _startBtn.onClick.AddListener(() => _gm.StartGame());

        _settingsBtn = MakeButton("SettingsBtn", _menuPanel.transform, "设置", 20,
            new Vector2(0.43f, 0.25f), new Vector2(150, 48),
            WithAlpha(SurfaceRaised, 0.96f), TextMuted);
        _settingsBtn.onClick.AddListener(ShowSettings);

        _characterBtn = MakeButton("CharacterBtn", _menuPanel.transform, "跑者", 20,
            new Vector2(0.57f, 0.25f), new Vector2(150, 48),
            WithAlpha(SurfaceRaised, 0.96f), TextMuted);
        _characterBtn.onClick.AddListener(ShowCharacter);

        _menuPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════
    //  Settings Panel (sub-menu)
    // ═══════════════════════════════════════════════════

    void CreateSettingsPanel()
    {
        _settingsPanel = NewPanel("SettingsPanel", WithAlpha(Backdrop, 0.96f));

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
            new Vector2(0.25f, 0.38f), new Vector2(140, 60), SurfaceRaised);
        _fps60Btn  = MakeSmallButton("Fps60", c, "60",
            new Vector2(0.5f, 0.38f), new Vector2(140, 60), SurfaceRaised);
        _fps120Btn = MakeSmallButton("Fps120", c, "120",
            new Vector2(0.75f, 0.38f), new Vector2(140, 60), SurfaceRaised);

        _fps30Btn.onClick.AddListener(() => { _gm.SetFrameRate(30);  HighlightFps(); });
        _fps60Btn.onClick.AddListener(() => { _gm.SetFrameRate(60);  HighlightFps(); });
        _fps120Btn.onClick.AddListener(() => { _gm.SetFrameRate(120); HighlightFps(); });
        if (_gm != null && !_gm.SupportsHighFrameRate)
        {
            _fps120Btn.gameObject.SetActive(false);
            SetButtonAnchor(_fps30Btn, new Vector2(0.36f, 0.38f));
            SetButtonAnchor(_fps60Btn, new Vector2(0.64f, 0.38f));
        }
        HighlightFps();

        _settingsBackBtn = MakeButton("SettingsBackBtn", c, "返回", 34,
            new Vector2(0.5f, 0.22f), new Vector2(280, 76),
            SurfaceRaised, TextMuted);
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
        EchoRunSaveSystem.SaveLegacyState();
        if (_settingsPanel != null) _settingsPanel.SetActive(false);
        if (_menuPanel != null) _menuPanel.SetActive(true);
    }

    void HighlightFps()
    {
        int cur = _gm != null ? _gm.GetFrameRate() : 60;
        Color active = PrimaryStrong;
        Color inactive = SurfaceRaised;
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
        _characterPanel = NewPanel("CharacterPanel", WithAlpha(Backdrop, 0.96f));

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
            SurfaceRaised, TextMuted);
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
        nameLabel.color = TextMuted;
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

        EchoRunSaveSystem.SaveCharacterPreset(presetIndex);
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
        _hudPanel = NewPanel("HudPanel", WithAlpha(Backdrop, 0.58f));
        RectTransform rt = _hudPanel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.sizeDelta = new Vector2(450, 122);
        rt.anchoredPosition = new Vector2(18, -18);

        AddPanelRule(_hudPanel.transform, Primary);

        float y = -45f;
        float rowH = 24f;
        float leftX = 16f;

        // Row 4: AI director state
        _aiDirectorText = MakeHUDText("AIDirectorText", _hudPanel.transform,
            "AI DIRECTOR  //  OBSERVING", 15, new Vector2(leftX, y), new Vector2(380, rowH));
        _aiDirectorText.color = Primary;
        y -= rowH;

        // Row 5: behavior-cloned opponent state
        _aiShadowText = MakeHUDText("AIShadowText", _hudPanel.transform,
            "ECHO RIVAL  //  CALIBRATING", 15, new Vector2(leftX, y), new Vector2(390, rowH));
        _aiShadowText.color = Reward;
        y -= rowH;

        // Row 6: Buff (hidden by default)
        _buffGroup = new GameObject("BuffGroup", typeof(RectTransform));
        _buffGroup.transform.SetParent(_hudPanel.transform, false);
        RectTransform bgRT = _buffGroup.GetComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 1); bgRT.anchorMax = new Vector2(0, 1);
        bgRT.pivot = new Vector2(0, 1);
        bgRT.anchoredPosition = new Vector2(leftX, y);
        bgRT.sizeDelta = new Vector2(300, 22);

        Text buffIcon = MakeText("BuffIcon", _buffGroup.transform, "▶", 20, TextAnchor.MiddleLeft);
        buffIcon.color = Success;
        RectTransform biRT = buffIcon.GetComponent<RectTransform>();
        biRT.anchorMin = new Vector2(0, 0.5f); biRT.anchorMax = new Vector2(0, 0.5f);
        biRT.pivot = new Vector2(0, 0.5f);
        biRT.anchoredPosition = new Vector2(0, 0);
        biRT.sizeDelta = new Vector2(24, 24);

        _buffText = MakeText("BuffText", _buffGroup.transform, "", 22, TextAnchor.MiddleLeft);
        _buffText.color = Success;
        RectTransform btRT = _buffText.GetComponent<RectTransform>();
        btRT.anchorMin = new Vector2(0, 0.5f); btRT.anchorMax = new Vector2(0, 0.5f);
        btRT.pivot = new Vector2(0, 0.5f);
        btRT.anchoredPosition = new Vector2(28, 0);
        btRT.sizeDelta = new Vector2(180, 24);

        _buffGroup.SetActive(false);

        // Created after the dynamic AI rows so the core counters stay on top
        // when WebGL rebuilds the dynamic font atlas.
        _statsText = MakeHUDText("StatsText", _hudPanel.transform,
            "SCORE 00000   RANGE 000m   SHARDS 00", 17,
            new Vector2(leftX, -11f), new Vector2(380, 30));

        // Pause button (right side of HUD bar)
        _pauseBtn = MakeIconButton("PauseBtn", _hudPanel.transform, "Ⅱ",
            new Vector2(1, 1), new Vector2(48, 48),
            WithAlpha(SurfaceRaised, 0.96f));
        _pauseBtn.onClick.AddListener(() => _gm.Pause());

        _hudPanel.SetActive(false);
    }

    void CreateControlHint()
    {
        _controlHint = new GameObject("ControlHint", typeof(Image));
        _controlHint.transform.SetParent(_safeAreaRoot, false);
        Image background = _controlHint.GetComponent<Image>();
        background.color = WithAlpha(Backdrop, 0.94f);
        ApplyRounded(background);

        RectTransform rt = _controlHint.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 34f);
        rt.sizeDelta = new Vector2(760f, 64f);

        _controlHintText = MakeText("ControlHintText", _controlHint.transform,
            "", 24, TextAnchor.MiddleCenter);
        _controlHintText.fontStyle = FontStyle.Bold;
        _controlHintText.color = TextPrimary;
        Stretch(_controlHintText.GetComponent<RectTransform>());
        AddOutline(_controlHintText.gameObject, WithAlpha(Ink, 0.65f));
        AddPanelRule(_controlHint.transform, Primary);
        _controlHint.SetActive(false);
    }

    void CreateLandscapeGuard()
    {
        Transform canvasRoot = _safeAreaRoot != null
            ? _safeAreaRoot.parent
            : FindObjectOfType<Canvas>()?.transform;
        if (canvasRoot == null) return;

        _landscapeGuard = new GameObject(
            "LandscapeGuard", typeof(Image));
        _landscapeGuard.transform.SetParent(canvasRoot, false);
        _landscapeGuard.GetComponent<Image>().color =
            WithAlpha(Backdrop, 0.99f);
        Stretch(_landscapeGuard.GetComponent<RectTransform>());

        Text message = MakeText("Message", _landscapeGuard.transform,
            "请横屏游玩\n旋转设备以继续", 42, TextAnchor.MiddleCenter);
        message.fontStyle = FontStyle.Bold;
        message.color = TextPrimary;
        message.lineSpacing = 1.25f;
        Stretch(message.GetComponent<RectTransform>());
        AddOutline(message.gameObject, new Color(0f, 0f, 0f, 0.7f));
        _landscapeGuard.SetActive(false);
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
        _pausePanel = NewPanel("PausePanel", WithAlpha(Backdrop, 0.92f));

        Text title = MakeText("PauseTitle", _pausePanel.transform, "PROTOCOL PAUSED", 42, TextAnchor.MiddleCenter);
        title.color = Color.white;
        title.fontStyle = FontStyle.Bold;
        AddOutline(title.gameObject, new Color(0, 0, 0, 0.6f));
        AnchorText(title.GetComponent<RectTransform>(), 0.5f, 0.58f, 400, 80);

        _resumeBtn = MakeButton("ResumeBtn", _pausePanel.transform, "继续游戏", 38,
            new Vector2(0.5f, 0.38f), new Vector2(400, 100),
            PrimaryStrong, Primary);
        _resumeBtn.onClick.AddListener(() => _gm.Resume());

        _pauseToMenuBtn = MakeButton("PauseToMenuBtn", _pausePanel.transform, "返回主页", 32,
            new Vector2(0.5f, 0.22f), new Vector2(320, 80),
            SurfaceRaised, TextMuted);
        _pauseToMenuBtn.onClick.AddListener(() => _gm.ReturnToMenu());

        _pausePanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════
    //  GameOver Panel
    // ═══════════════════════════════════════════════════

    void CreateGameOverPanel()
    {
        _gameOverPanel = NewPanel("GameOverPanel", WithAlpha(Backdrop, 0.94f));

        Text title = MakeText("GOTitle", _gameOverPanel.transform, "Game Over", 68, TextAnchor.MiddleCenter);
        title.color = Danger;
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
        _highScoreText.color = Reward;
        _highScoreText.fontStyle = FontStyle.Bold;
        AddOutline(_highScoreText.gameObject, new Color(0.3f, 0.2f, 0f));
        AnchorText(_highScoreText.GetComponent<RectTransform>(), 0.5f, 0.52f, 400, 50);
        _highScoreText.gameObject.SetActive(false);

        // Coins
        _coinResultText = MakeText("CoinResult", _gameOverPanel.transform, "金币: 0", 32, TextAnchor.MiddleCenter);
        _coinResultText.color = Reward;
        AnchorText(_coinResultText.GetComponent<RectTransform>(), 0.5f, 0.45f, 500, 40);
        _coinResultText.gameObject.SetActive(false);

        _shadowResultText = MakeText("ShadowResult", _gameOverPanel.transform,
            "AI影子正在生成赛后分析", 28, TextAnchor.MiddleCenter);
        _shadowResultText.color = Primary;
        _shadowResultText.fontStyle = FontStyle.Bold;
        _shadowResultText.resizeTextForBestFit = true;
        _shadowResultText.resizeTextMinSize = 18;
        _shadowResultText.resizeTextMaxSize = 28;
        _shadowResultText.horizontalOverflow = HorizontalWrapMode.Wrap;
        AnchorText(_shadowResultText.GetComponent<RectTransform>(), 0.5f, 0.34f, 1100, 90);

        // Restart
        _restartBtn = MakeButton("RestartBtn", _gameOverPanel.transform, "挑战下一代", 30,
            new Vector2(0.5f, 0.18f), new Vector2(380, 76),
            PrimaryStrong, Primary);
        _restartBtn.onClick.AddListener(() => _gm.Restart());

        // Back to menu
        _goToMenuBtn = MakeButton("GoToMenuBtn", _gameOverPanel.transform, "返回主页", 24,
            new Vector2(0.5f, 0.07f), new Vector2(280, 60),
            SurfaceRaised, TextMuted);
        _goToMenuBtn.onClick.AddListener(() => _gm.ReturnToMenu());

        // Create consolidated result text last so WebGL dynamic-font atlas rebuilds
        // cannot leave the earlier score rows without geometry.
        _gameOverTitleText = MakeText("GameOverTitle", _gameOverPanel.transform,
            "RUN DECODED", 48, TextAnchor.MiddleCenter);
        _gameOverTitleText.color = Danger;
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
                if (_controlHint != null) _controlHint.SetActive(false);
                if (_menuPanel != null) _menuPanel.SetActive(true);
                if (AIShadowRunner.Instance != null)
                {
                    int generation = AIShadowRunner.Instance.Generation;
                    string menuStatus = generation > 0
                        ? "本地档案已载入 · 第" + generation
                          + "代 · 最高分" + (_gm != null ? _gm.HighScore : 0)
                        : "本地档案已创建 · 首局将训练个人AI影子";
                    if (_menuShadowText != null) _menuShadowText.text = menuStatus;
                    Text startLabel = _startBtn != null
                        ? _startBtn.GetComponentInChildren<Text>()
                        : null;
                    if (startLabel != null)
                        startLabel.text = AIShadowRunner.Instance.Generation > 0
                            ? "挑战 AI 回声"
                            : "开始校准";
                }
                break;

            case GameState.Playing:
                if (_hudPanel != null) _hudPanel.SetActive(true);
                ShowControlHintIfNeeded();
                OnScoreChanged(_gm != null ? _gm.Score : 0);
                OnCoinsChanged(_gm != null ? _gm.Coins : 0);
                OnDistanceChanged(_gm != null ? _gm.Distance : 0);
                break;

            case GameState.Paused:
                if (_controlHint != null) _controlHint.SetActive(false);
                if (_hudPanel != null) _hudPanel.SetActive(true);
                if (_pausePanel != null) _pausePanel.SetActive(true);
                break;

            case GameState.GameOver:
                if (_controlHint != null) _controlHint.SetActive(false);
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
        if (_gm != null)
        {
            _gm.OnStateChanged.RemoveListener(OnGameStateChanged);
            _gm.OnScoreChanged.RemoveListener(OnScoreChanged);
            _gm.OnCoinsChanged.RemoveListener(OnCoinsChanged);
            _gm.OnDistanceChanged.RemoveListener(OnDistanceChanged);
        }
        if (_roundedSprite != null) Destroy(_roundedSprite);
        if (_roundedTexture != null) Destroy(_roundedTexture);
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
        _statsText.text = "SCORE " + _gm.Score.ToString("D5")
                          + "   RANGE " + Mathf.FloorToInt(_gm.Distance).ToString("D3") + "m"
                          + "   SHARDS " + _gm.Coins.ToString("D2");
    }

    // ═══════════════════════════════════════════════════
    //  UI Helpers
    // ═══════════════════════════════════════════════════

    GameObject NewPanel(string name, Color color)
    {
        GameObject panel = new GameObject(name, typeof(Image));
        Image image = panel.GetComponent<Image>();
        image.color = color;
        ApplyRounded(image);
        panel.transform.SetParent(_safeAreaRoot != null ? _safeAreaRoot : transform, false);
        Stretch(panel.GetComponent<RectTransform>());
        return panel;
    }

    void ShowControlHintIfNeeded()
    {
        if (_controlHint == null || _controlHintText == null) return;
        bool firstCalibration = AIShadowRunner.Instance == null
                                || AIShadowRunner.Instance.Generation <= 0;
        if (!firstCalibration)
        {
            _controlHint.SetActive(false);
            return;
        }

        _controlHintText.text = UsesTouchLayout()
            ? "左右滑动变道  ·  上滑跳跃  ·  下滑滑铲"
            : "A / D 或拖动变道  ·  W / 空格跳跃  ·  S / Ctrl 滑铲";
        _controlHintTimer = ControlHintDuration;
        _controlHint.SetActive(true);
    }

    void UpdateLandscapeGuard()
    {
        if (_landscapeGuard == null) return;
        bool shouldShow = ShouldShowLandscapeGuard(
            Screen.width, Screen.height, UsesTouchLayout());
        if (_landscapeGuard.activeSelf == shouldShow) return;

        _landscapeGuard.SetActive(shouldShow);
        if (shouldShow)
        {
            _landscapeGuard.transform.SetAsLastSibling();
            if (_gm != null && _gm.State == GameState.Playing)
                _gm.Pause();
        }
    }

    public static bool ShouldShowLandscapeGuard(
        int width, int height, bool touchLayout)
    {
        return touchLayout && width > 0 && height > width;
    }

    private static bool UsesTouchLayout()
    {
        return Application.isMobilePlatform || Input.touchSupported;
    }

    void ApplySafeArea(bool force = false)
    {
        if (_safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0) return;
        Rect safeArea = Screen.safeArea;
        Vector2Int screenSize = new Vector2Int(Screen.width, Screen.height);
        if (!force && safeArea == _lastSafeArea && screenSize == _lastScreenSize) return;

        _lastSafeArea = safeArea;
        _lastScreenSize = screenSize;
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;
        _safeAreaRoot.anchorMin = anchorMin;
        _safeAreaRoot.anchorMax = anchorMax;
        _safeAreaRoot.offsetMin = Vector2.zero;
        _safeAreaRoot.offsetMax = Vector2.zero;
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

    void AddMenuGrid(Transform parent)
    {
        for (int i = 0; i < 12; i++)
        {
            float angle = i / 12f * Mathf.PI * 2f;
            Vector2 anchor = new Vector2(
                0.5f + Mathf.Cos(angle) * 0.43f,
                0.5f + Mathf.Sin(angle) * 0.36f);
            GameObject node = new GameObject("TransitNode", typeof(Image));
            node.transform.SetParent(parent, false);
            Image image = node.GetComponent<Image>();
            image.color = WithAlpha(Primary, i % 3 == 0 ? 0.09f : 0.045f);
            ApplyRounded(image);
            RectTransform rt = node.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = i % 3 == 0 ? new Vector2(8f, 8f) : new Vector2(5f, 5f);
            rt.anchoredPosition = Vector2.zero;
        }
    }

    void AddPanelRule(Transform parent, Color color)
    {
        GameObject accent = new GameObject("SignalRule", typeof(Image));
        accent.transform.SetParent(parent, false);
        Image image = accent.GetComponent<Image>();
        image.color = color;
        ApplyRounded(image);
        RectTransform rt = accent.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(58f, 6f);
        rt.anchoredPosition = new Vector2(18f, -10f);
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
        label.color = TextMuted;
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
        if (UsesTouchLayout())
            size.y = Mathf.Max(size.y, 104f);
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image background = go.GetComponent<Image>();
        background.color = mainColor;
        ApplyRounded(background);

        GameObject edge = new GameObject("SignalRule", typeof(Image));
        edge.transform.SetParent(go.transform, false);
        Image edgeImage = edge.GetComponent<Image>();
        edgeImage.color = edgeColor;
        ApplyRounded(edgeImage);
        RectTransform edgeRt = edge.GetComponent<RectTransform>();
        edgeRt.anchorMin = new Vector2(0f, 0f);
        edgeRt.anchorMax = new Vector2(1f, 0f);
        edgeRt.sizeDelta = new Vector2(0f, 3f);
        edgeRt.anchoredPosition = Vector2.zero;

        Text labelT = MakeText("Label", go.transform, label, fontSize, TextAnchor.MiddleCenter);
        labelT.color = Color.white;
        labelT.fontStyle = FontStyle.Bold;
        Stretch(labelT.GetComponent<RectTransform>());

        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        colors.pressedColor = new Color(0.78f, 0.84f, 0.81f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        return button;
    }

    Button MakeSmallButton(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Color color)
    {
        if (UsesTouchLayout())
            size.y = Mathf.Max(size.y, 104f);
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.color = color;
        ApplyRounded(image);

        Text labelT = MakeText("Label", go.transform, label, 28, TextAnchor.MiddleCenter);
        labelT.color = Color.white;
        labelT.fontStyle = FontStyle.Bold;
        Stretch(labelT.GetComponent<RectTransform>());

        return go.GetComponent<Button>();
    }

    Button MakeIconButton(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Color color)
    {
        if (UsesTouchLayout())
        {
            size.x = Mathf.Max(size.x, 104f);
            size.y = Mathf.Max(size.y, 104f);
        }
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.color = color;
        ApplyRounded(image);

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
        Image bgImage = bg.GetComponent<Image>();
        bgImage.color = SurfaceRaised;
        ApplyRounded(bgImage);
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
        Image fillImage = fill.GetComponent<Image>();
        fillImage.color = Primary;
        ApplyRounded(fillImage);
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
        Image handleImage = handle.GetComponent<Image>();
        handleImage.color = Color.white;
        ApplyRounded(handleImage);
        RectTransform hRT = handle.GetComponent<RectTransform>();
        hRT.anchorMin = new Vector2(0, 0.5f); hRT.anchorMax = new Vector2(0, 0.5f);
        float handleSize = UsesTouchLayout() ? 56f : 32f;
        hRT.sizeDelta = new Vector2(handleSize, handleSize);
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
        sRT.sizeDelta = UsesTouchLayout()
            ? new Vector2(600, 72)
            : new Vector2(500, 40);
        sRT.anchoredPosition = Vector2.zero;

        return slider;
    }

    void ApplyRounded(Image image)
    {
        if (image == null) return;
        if (_roundedSprite == null)
        {
            const int size = 64;
            const float radius = 15f;
            _roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RuntimeRoundedUI",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            Vector2 inner = new Vector2(center.x - radius, center.y - radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 q = new Vector2(
                        Mathf.Abs(x - center.x) - inner.x,
                        Mathf.Abs(y - center.y) - inner.y);
                    float outside = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude;
                    float inside = Mathf.Min(Mathf.Max(q.x, q.y), 0f);
                    float distance = outside + inside - radius;
                    byte alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(0.5f - distance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            _roundedTexture.SetPixels32(pixels);
            _roundedTexture.Apply(false, true);
            _roundedSprite = Sprite.Create(_roundedTexture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(16f, 16f, 16f, 16f));
            _roundedSprite.name = "RuntimeRoundedUISprite";
        }
        image.sprite = _roundedSprite;
        image.type = Image.Type.Sliced;
    }

    static void SetButtonAnchor(Button button, Vector2 anchor)
    {
        if (button == null) return;
        RectTransform rt = button.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
    }

    static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
