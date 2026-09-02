using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class AITrainingDashboardUI : MonoBehaviour
{
    private GameManager _gameManager;
    private PlayerController _player;
    private MenuScreenRouter _router;
    private GameObject _launcher;
    private GameObject _panel;
    private RectTransform _panelRect;
    private RectTransform _titleRect;
    private RectTransform _metricsRect;
    private RectTransform _insightRect;
    private RectTransform _summaryRect;
    private RectTransform _resetHintRect;
    private Text _metrics;
    private Text _summary;
    private Text _resetHint;
    private GameObject _liveDebugPanel;
    private Text _liveDebugText;
    private RectTransform _liveDebugRect;
    private Button _emergencyReflexButton;
    private Button _liveDebugButton;
    private Button _resetButton;
    private Button _closeButton;
    private bool _liveDebugEnabled;
    private float _nextLiveDebugRefresh;
    private float _resetConfirmUntil;
    private Vector2Int _lastScreenSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<AITrainingDashboardUI>() != null) return;
        new GameObject("AI Training Dashboard UI")
            .AddComponent<AITrainingDashboardUI>();
    }

    IEnumerator Start()
    {
        _gameManager = GameManager.Instance;
        _player = FindObjectOfType<PlayerController>();
        Canvas canvas = null;
        for (int i = 0; i < 60
             && (canvas == null || _router == null); i++)
        {
            canvas = FindObjectOfType<Canvas>();
            _router = FindObjectOfType<MenuScreenRouter>();
            if (canvas == null || _router == null) yield return null;
        }
        if (canvas == null || _gameManager == null) yield break;
        Transform parent = canvas.transform.Find("SafeArea") ?? canvas.transform;
        Build(parent);
        if (_router != null)
        {
            _router.Register(MenuScreen.EchoReport, _panel, _closeButton);
            _router.RegisterHomeNavigation(_launcher);
        }
        _gameManager.OnStateChanged.AddListener(OnStateChanged);
        OnStateChanged(_gameManager.State);
    }

    void Update()
    {
        ApplyLayout(false);
        if (_resetHint != null && _resetConfirmUntil > 0f
            && Time.unscaledTime > _resetConfirmUntil)
        {
            _resetConfirmUntil = 0f;
            _resetHint.text = "重置会让回声忘记所有已学习的跑者习惯。";
        }

        if (_liveDebugPanel != null)
        {
            bool shouldShow = _liveDebugEnabled && _gameManager != null
                              && _gameManager.State == GameState.Playing;
            if (_liveDebugPanel.activeSelf != shouldShow)
                _liveDebugPanel.SetActive(shouldShow);
            if (shouldShow && Time.unscaledTime >= _nextLiveDebugRefresh)
            {
                _nextLiveDebugRefresh = Time.unscaledTime + 0.25f;
                RefreshLiveDebug();
            }
        }
    }

    private void Build(Transform parent)
    {
        bool compactPortrait = UILayoutRules.IsCompactPortrait(
            Screen.width, Screen.height);
        Vector2 launcherAnchor = compactPortrait
            ? new Vector2(0.78f, 0.94f)
            : new Vector2(0.90f, 0.91f);
        Vector2 launcherSize = compactPortrait
            ? new Vector2(260f, 96f)
            : new Vector2(220f, 58f);
        Button launcher = RuntimePanelFactory.Button("AITrainingLauncher", parent,
            "回声报告", launcherAnchor, launcherSize,
            RuntimePanelFactory.Raised, compactPortrait ? 30 : 26);
        launcher.onClick.AddListener(Open);
        _launcher = launcher.gameObject;

        _panel = RuntimePanelFactory.PanelObject("AITrainingDashboard", parent,
            new Vector2(0.5f, 0.5f), compactPortrait
                ? new Vector2(930f, 1500f)
                : new Vector2(1050f, 700f),
            RuntimePanelFactory.Panel);
        _panelRect = _panel.GetComponent<RectTransform>();
        Text title = RuntimePanelFactory.Text("Title", _panel.transform,
            "回声报告", compactPortrait ? 44 : 40, TextAnchor.MiddleLeft,
            RuntimePanelFactory.TextPrimary);
        _titleRect = title.rectTransform;
        title.rectTransform.pivot = new Vector2(0f, 0.5f);
        RuntimePanelFactory.Place(title.rectTransform,
            compactPortrait ? new Vector2(0.08f, 0.93f) : new Vector2(0.08f, 0.89f),
            compactPortrait ? new Vector2(360f, 90f) : new Vector2(430f, 70f),
            Vector2.zero);

        _metrics = RuntimePanelFactory.Text("Metrics", _panel.transform, "",
            compactPortrait ? 30 : 26, TextAnchor.UpperLeft,
            RuntimePanelFactory.TextPrimary);
        _metricsRect = _metrics.rectTransform;
        _metrics.lineSpacing = 1.25f;
        RuntimePanelFactory.Place(_metrics.rectTransform,
            compactPortrait ? new Vector2(0.5f, 0.68f) : new Vector2(0.34f, 0.59f),
            compactPortrait ? new Vector2(780f, 500f) : new Vector2(580f, 300f),
            Vector2.zero);

        GameObject insight = RuntimePanelFactory.PanelObject("Insight", _panel.transform,
            compactPortrait ? new Vector2(0.5f, 0.38f) : new Vector2(0.5f, 0.29f),
            compactPortrait ? new Vector2(780f, 280f) : new Vector2(900f, 150f),
            EchoRunUITheme.Surface);
        _insightRect = insight.GetComponent<RectTransform>();
        _summary = RuntimePanelFactory.Text("Summary", insight.transform, "",
            compactPortrait ? 29 : 25, TextAnchor.MiddleLeft,
            RuntimePanelFactory.TextPrimary);
        _summaryRect = _summary.rectTransform;
        RuntimePanelFactory.Stretch(_summary.rectTransform, 28f);

        _resetHint = RuntimePanelFactory.Text("ResetHint", _panel.transform,
            "重置会让回声忘记所有已学习的跑者习惯。", 19,
            TextAnchor.MiddleLeft, RuntimePanelFactory.TextMuted);
        _resetHintRect = _resetHint.rectTransform;
        RuntimePanelFactory.Place(_resetHint.rectTransform,
            compactPortrait ? new Vector2(0.5f, 0.17f) : new Vector2(0.26f, 0.09f),
            compactPortrait ? new Vector2(760f, 90f) : new Vector2(520f, 50f),
            Vector2.zero);
        _resetButton = RuntimePanelFactory.Button("Reset", _panel.transform, "重置学习数据",
            compactPortrait ? new Vector2(0.30f, 0.07f) : new Vector2(0.64f, 0.09f),
            compactPortrait ? new Vector2(320f, 100f) : new Vector2(210f, 58f),
            EchoRunUITheme.WithAlpha(EchoRunUITheme.Danger, 0.62f),
            compactPortrait ? 28 : 22);
        _resetButton.onClick.AddListener(ConfirmReset);
        _closeButton = RuntimePanelFactory.Button("Close", _panel.transform, "返回",
            compactPortrait ? new Vector2(0.72f, 0.07f) : new Vector2(0.87f, 0.09f),
            compactPortrait ? new Vector2(280f, 100f) : new Vector2(180f, 58f),
            RuntimePanelFactory.Raised, compactPortrait ? 28 : 22);
        _closeButton.onClick.AddListener(Close);
        _liveDebugButton = RuntimePanelFactory.Button("LiveDebug", _panel.transform,
            "实时诊断", compactPortrait
                ? new Vector2(0.86f, 0.93f)
                : new Vector2(0.84f, 0.89f),
            compactPortrait ? new Vector2(210f, 82f) : new Vector2(210f, 58f),
            RuntimePanelFactory.Action, compactPortrait ? 25 : 21);
        _liveDebugButton.onClick.AddListener(ToggleLiveDebug);
        _emergencyReflexButton = RuntimePanelFactory.Button(
            "EmergencyReflex", _panel.transform, "救场：开",
            compactPortrait ? new Vector2(0.61f, 0.93f) : new Vector2(0.61f, 0.89f),
            compactPortrait ? new Vector2(210f, 82f) : new Vector2(210f, 58f),
            RuntimePanelFactory.Raised, compactPortrait ? 25 : 21);
        _emergencyReflexButton.onClick.AddListener(ToggleEmergencyReflex);
        RefreshEmergencyReflexButton();
        bool developerControls = Debug.isDebugBuild && PlayerPrefs.GetInt(
            "EchoRunDeveloperDiagnostics", 0) == 1;
        _liveDebugButton.gameObject.SetActive(developerControls);
        _emergencyReflexButton.gameObject.SetActive(developerControls);
        _panel.SetActive(false);

        BuildLiveDebug(parent, compactPortrait);
        ApplyLayout(true);
    }

    private void Open()
    {
        Refresh();
        RefreshEmergencyReflexButton();
        if (_router != null) _router.Show(MenuScreen.EchoReport);
        else _panel.SetActive(true);
        RuntimePanelFactory.RefreshText(_panel.transform);
    }

    private void Close()
    {
        if (_router != null) _router.BackToHome();
        else _panel.SetActive(false);
    }

    private void Refresh()
    {
        if (_gameManager != null
            && _gameManager.ConfiguredGameplayFlowMode
            == GameplayFlowMode.SingleContract)
        {
            BuildSingleContractReport(
                EchoRunSaveSystem.GetActiveEchoIdentity(),
                out string singleMetrics, out string singleSummary);
            _metrics.text = singleMetrics;
            _summary.text = singleSummary;
            return;
        }

        AIRunTelemetryData telemetry = AIRunTelemetry.FromJson(
            EchoRunSaveSystem.GetLastRunTelemetryJson());
        AITrainingReport report = AITrainingReportBuilder.FromTelemetry(telemetry);
        if (report == null)
        {
            int generation = AIShadowRunner.Instance != null
                ? AIShadowRunner.Instance.Generation
                : 0;
            _metrics.text = generation > 0
                ? "当前回声\n第 " + generation + " 代\n\n等待新的完整跑局"
                : "尚未生成回声\n\n先完成一次校准跑局";
            _summary.text = "下一步\n跑一局，让回声学习你的路线、动作与节奏。";
            return;
        }

        int currentGeneration = AIShadowRunner.Instance != null
            ? AIShadowRunner.Instance.Generation
            : report.generationAfter;
        AIShadowRunner shadow = AIShadowRunner.Instance;
        AIBalance ai = GameBalanceConfig.Current.ai;
        int minimumJumps = shadow != null
            ? shadow.minimumJumpSamples : ai.minimumJumpSamples;
        int minimumSlides = shadow != null
            ? shadow.minimumSlideSamples : ai.minimumSlideSamples;
        EchoMenuViewData next = EchoRunPresentation.BuildMenu(
            currentGeneration, StyleTracker.GetSnapshot(),
            minimumJumps, minimumSlides,
            shadow != null ? shadow.ContractPreview : null);
        _metrics.text = "回声进化\n第 " + report.generationBefore
                        + " 代  →  第 " + report.generationAfter + " 代"
                        + "\n\n它重点观察了\n" + report.learnedAction;
        _summary.text = "它学会了什么\n" + report.summary
                        + "\n\n本轮规则\n" + next.rule
                        + "\n\n下一局目标\n" + next.objective;
    }

    public static void BuildSingleContractReport(
        ActiveEchoIdentity identity, out string metrics, out string summary)
    {
        EchoMenuViewData view =
            EchoRunPresentation.BuildSingleContractMenu(identity);
        metrics = identity == null
            ? "尚未生成回声\n\n" + view.learned
            : view.generation + "\n\n" + view.learned;
        summary = "本轮规则\n" + view.rule
                  + "\n\n下一局目标\n" + view.objective;
    }

    private void ConfirmReset()
    {
        if (_resetConfirmUntil <= Time.unscaledTime)
        {
            _resetConfirmUntil = Time.unscaledTime + 4f;
            _resetHint.text = "再次点击“重置学习数据”确认；此操作无法撤销。";
            AudioManager.Instance?.PlayUIError();
            return;
        }

        if (!EchoRunSaveSystem.CommitTrainingReset())
        {
            _resetConfirmUntil = 0f;
            _resetHint.text = "学习数据重置尚未完成；请再次尝试。";
            AudioManager.Instance?.PlayUIError();
            return;
        }

        AIPlayerSkillEstimator.ResetTrainingInMemory();
        StyleTracker.ResetTrainingInMemory();
        AIRunTelemetry.ResetTrainingInMemory();
        AIShadowRunner.Instance?.ResetTrainingInMemory();
        AITrackDirector.Instance?.ResetTrainingInMemory();
        _resetConfirmUntil = 0f;
        _resetHint.text = "学习记录已重置；下一局会重新观察你的跑法。";
        AudioManager.Instance?.PlayUIConfirm();
        Refresh();
    }

    private void OnStateChanged(GameState state)
    {
        bool menu = state == GameState.Menu;
        if (_router == null && _launcher != null) _launcher.SetActive(menu);
        if (!menu && _panel != null) _panel.SetActive(false);
        if (_liveDebugPanel != null)
        {
            _liveDebugPanel.SetActive(
                _liveDebugEnabled && state == GameState.Playing);
            if (_liveDebugPanel.activeSelf)
                _liveDebugPanel.transform.SetAsLastSibling();
        }
    }

    private void BuildLiveDebug(Transform parent, bool compactPortrait)
    {
        _liveDebugPanel = RuntimePanelFactory.PanelObject("AI Live Debug", parent,
            compactPortrait ? new Vector2(0.5f, 0.76f) : new Vector2(0.23f, 0.72f),
            compactPortrait ? new Vector2(880f, 520f) : new Vector2(650f, 420f),
            new Color(0.025f, 0.04f, 0.06f, 0.92f));
        _liveDebugRect = _liveDebugPanel.GetComponent<RectTransform>();
        _liveDebugText = RuntimePanelFactory.Text("Content",
            _liveDebugPanel.transform, "", compactPortrait ? 24 : 19,
            TextAnchor.UpperLeft, RuntimePanelFactory.TextPrimary);
        _liveDebugText.lineSpacing = 1.12f;
        RuntimePanelFactory.Stretch(_liveDebugText.rectTransform,
            compactPortrait ? 28f : 22f);
        _liveDebugPanel.SetActive(false);
    }

    private void ApplyLayout(bool force)
    {
        Vector2Int screen = new Vector2Int(Screen.width, Screen.height);
        if (!force && screen == _lastScreenSize) return;
        _lastScreenSize = screen;
        if (_panelRect == null) return;

        bool portrait = UILayoutRules.IsCompactPortrait(Screen.width, Screen.height);
        EchoRunAccessibility.SetBaseFontSize(
            _titleRect.GetComponent<Text>(), portrait ? 44 : 40);
        EchoRunAccessibility.SetBaseFontSize(_metrics, portrait ? 30 : 26);
        EchoRunAccessibility.SetBaseFontSize(_summary, portrait ? 29 : 25);
        EchoRunAccessibility.SetBaseFontSize(_resetHint, portrait ? 22 : 19);
        SetButtonBaseFont(_resetButton, portrait ? 28 : 22);
        SetButtonBaseFont(_closeButton, portrait ? 28 : 22);
        Vector2 launcherSize = RuntimePanelFactory.TouchButtonSize(
            portrait ? new Vector2(260f, 96f) : new Vector2(220f, 58f),
            portrait);
        if (_launcher != null)
        {
            RuntimePanelFactory.Place(_launcher.GetComponent<RectTransform>(),
                portrait ? new Vector2(0.78f, 0.94f) : new Vector2(0.90f, 0.91f),
                launcherSize, Vector2.zero);
            Text launcherLabel = _launcher.GetComponentInChildren<Text>();
            if (launcherLabel != null) launcherLabel.fontSize = portrait ? 30 : 26;
            SetButtonBaseFont(_launcher.GetComponent<Button>(), portrait ? 30 : 26);
        }
        _panelRect.sizeDelta = portrait
            ? new Vector2(900f, 1500f)
            : new Vector2(1050f, 720f);
        RuntimePanelFactory.Place(_titleRect,
            portrait ? new Vector2(0.08f, 0.93f) : new Vector2(0.08f, 0.90f),
            portrait ? new Vector2(500f, 90f) : new Vector2(430f, 70f),
            Vector2.zero);
        RuntimePanelFactory.Place(_metricsRect,
            portrait ? new Vector2(0.5f, 0.70f) : new Vector2(0.26f, 0.60f),
            portrait ? new Vector2(760f, 430f) : new Vector2(400f, 310f),
            Vector2.zero);
        RuntimePanelFactory.Place(_insightRect,
            portrait ? new Vector2(0.5f, 0.36f) : new Vector2(0.69f, 0.54f),
            portrait ? new Vector2(780f, 440f) : new Vector2(500f, 360f),
            Vector2.zero);
        RuntimePanelFactory.Stretch(_summaryRect, portrait ? 30f : 26f);
        RuntimePanelFactory.Place(_resetHintRect,
            portrait ? new Vector2(0.5f, 0.16f) : new Vector2(0.27f, 0.09f),
            portrait ? new Vector2(760f, 88f) : new Vector2(540f, 50f),
            Vector2.zero);
        RuntimePanelFactory.Place(_resetButton.GetComponent<RectTransform>(),
            portrait ? new Vector2(0.30f, 0.07f) : new Vector2(0.67f, 0.09f),
            RuntimePanelFactory.TouchButtonSize(portrait
                ? new Vector2(330f, 104f) : new Vector2(230f, 58f), portrait),
            Vector2.zero);
        RuntimePanelFactory.Place(_closeButton.GetComponent<RectTransform>(),
            portrait ? new Vector2(0.72f, 0.07f) : new Vector2(0.88f, 0.09f),
            RuntimePanelFactory.TouchButtonSize(portrait
                ? new Vector2(280f, 104f) : new Vector2(180f, 58f), portrait),
            Vector2.zero);
        if (_liveDebugRect != null)
            RuntimePanelFactory.Place(_liveDebugRect,
                portrait ? new Vector2(0.5f, 0.75f) : new Vector2(0.23f, 0.72f),
                portrait ? new Vector2(880f, 520f) : new Vector2(650f, 420f),
                Vector2.zero);
    }

    private void ToggleLiveDebug()
    {
        _liveDebugEnabled = !_liveDebugEnabled;
        _nextLiveDebugRefresh = 0f;
        if (_resetHint != null)
            _resetHint.text = _liveDebugEnabled
                ? "实时诊断已开启，进入游戏后显示。"
                : "实时诊断已关闭。";
        AudioManager.Instance?.PlayUIConfirm();
    }

    private static void SetButtonBaseFont(Button button, int size)
    {
        if (button == null) return;
        EchoRunAccessibility.SetBaseFontSize(
            button.GetComponentInChildren<Text>(true), size);
    }

    private void ToggleEmergencyReflex()
    {
        AIShadowRunner shadow = AIShadowRunner.Instance;
        if (shadow == null) return;
        shadow.SetEmergencyReflexEnabled(!shadow.EmergencyReflexEnabled);
        RefreshEmergencyReflexButton();
        if (_resetHint != null)
            _resetHint.text = shadow.EmergencyReflexEnabled
                ? "紧急救场已开启（正常游玩模式）。"
                : "紧急救场已关闭（仅用于策略对照）。";
        AudioManager.Instance?.PlayUIConfirm();
    }

    private void RefreshEmergencyReflexButton()
    {
        if (_emergencyReflexButton == null) return;
        AIShadowRunner shadow = AIShadowRunner.Instance;
        Text label = _emergencyReflexButton.GetComponentInChildren<Text>();
        if (label != null)
            label.text = shadow == null || shadow.EmergencyReflexEnabled
                ? "救场：开"
                : "救场：关";
    }

    private void RefreshLiveDebug()
    {
        if (_liveDebugText == null) return;
        PlayerStyleData style = StyleTracker.GetSnapshot();
        AIShadowRunner shadow = AIShadowRunner.Instance;
        ShadowDecisionTrace trace = shadow != null
            ? shadow.LastDecisionTrace : null;
        ShadowAIDirective directive = trace != null
            ? trace.directive
            : (AITrackDirector.Instance != null
                ? AITrackDirector.Instance.CurrentShadowDirective
                : ShadowAIDirective.Neutral);

        string decision = trace == null
            ? "尚无影子决策"
            : "原始动作 " + trace.originalPrediction
              + "  最终动作 " + trace.selectedAction
              + (trace.safetyAdjusted ? "  [安全覆盖]" : "")
              + "\n基础  " + FormatScores(trace.baseScores)
              + "\n风格  " + FormatScores(trace.styleAdjustedScores)
              + "\n最终  " + FormatScores(trace.finalScores);
        _liveDebugText.text = "AI 实时诊断"
            + "\n激进 " + Percent(style.aggressiveness)
            + "  跳时 " + SignedPercent(style.jumpTiming)
            + "  滑铲占比 " + Percent(style.slideFrequency)
            + "  低障碍成功 " + Percent(style.slideOpportunitySuccess)
            + "\n车道 " + SignedPercent(style.lanePreference)
            + "  节奏 " + Percent(style.rhythmStability)
            + "  恢复 " + Percent(style.recoveryStyle)
            + "\n置信 " + Percent(style.Confidence)
            + "  全局风险 " + SignedPercent(directive.riskBias)
            + "  风格强度 " + Percent(directive.styleInfluence)
            + (shadow != null
                ? "\n策略正确 " + shadow.PolicyCorrectDecisionCount
                  + "  安全改写 " + shadow.SafetyOverrideDecisionCount
                  + "  紧急救场 " + shadow.EmergencyReflexSaveCount
                  + (shadow.EmergencyReflexEnabled ? " [开]" : " [观察模式]")
                : "")
            + BuildObstacleContactLine()
            + "\n\n" + decision;
    }

    private string BuildObstacleContactLine()
    {
        return _player != null && _player.LastObstacleContact != null
            ? "\n接触 " + _player.LastObstacleContact.ToDisplayString()
            : "";
    }

    private static string BuildStyleSummary()
    {
        PlayerStyleData style = StyleTracker.GetSnapshot();
        return "风格置信       " + Percent(style.Confidence)
            + "\n激进 " + Percent(style.aggressiveness)
            + "  跳时 " + SignedPercent(style.jumpTiming)
            + "  滑铲占比 " + Percent(style.slideFrequency)
            + "  低障碍成功 " + Percent(style.slideOpportunitySuccess)
            + "\n车道 " + SignedPercent(style.lanePreference)
            + "  节奏 " + Percent(style.rhythmStability)
            + "  恢复 " + Percent(style.recoveryStyle);
    }

    private static string FormatScores(float[] scores)
    {
        if (scores == null || scores.Length < AIShadowPolicy.ActionCount)
            return "--";
        string[] labels = { "K", "L", "R", "J", "S" };
        string result = "";
        for (int i = 0; i < labels.Length; i++)
        {
            if (i > 0) result += "  ";
            result += labels[i] + ":" + (scores[i] <= -998f
                ? "X" : scores[i].ToString("0.00"));
        }
        return result;
    }

    private static string Percent(float value)
    {
        return (Mathf.Clamp01(value) * 100f).ToString("0") + "%";
    }

    private static string SignedPercent(float value)
    {
        return (Mathf.Clamp(value, -1f, 1f) * 100f).ToString("+0;-0;0")
               + "%";
    }

    void OnDestroy()
    {
        if (_gameManager != null)
            _gameManager.OnStateChanged.RemoveListener(OnStateChanged);
    }
}
