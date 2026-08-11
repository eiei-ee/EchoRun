using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class AITrainingDashboardUI : MonoBehaviour
{
    private GameManager _gameManager;
    private GameObject _launcher;
    private GameObject _panel;
    private Text _metrics;
    private Text _summary;
    private Text _resetHint;
    private GameObject _liveDebugPanel;
    private Text _liveDebugText;
    private bool _liveDebugEnabled;
    private float _nextLiveDebugRefresh;
    private float _resetConfirmUntil;

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
        Canvas canvas = null;
        for (int i = 0; i < 60 && canvas == null; i++)
        {
            canvas = FindObjectOfType<Canvas>();
            if (canvas == null) yield return null;
        }
        if (canvas == null || _gameManager == null) yield break;
        Transform parent = canvas.transform.Find("SafeArea") ?? canvas.transform;
        Build(parent);
        _gameManager.OnStateChanged.AddListener(OnStateChanged);
        OnStateChanged(_gameManager.State);
    }

    void Update()
    {
        if (_resetHint != null && _resetConfirmUntil > 0f
            && Time.unscaledTime > _resetConfirmUntil)
        {
            _resetConfirmUntil = 0f;
            _resetHint.text = "重置会清空影子、导演和玩家能力模型。";
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
            ? new Vector2(0.82f, 0.07f)
            : new Vector2(0.90f, 0.09f);
        Vector2 launcherSize = compactPortrait
            ? new Vector2(260f, 96f)
            : new Vector2(220f, 68f);
        Button launcher = RuntimePanelFactory.Button("AITrainingLauncher", parent,
            "AI 训练", launcherAnchor, launcherSize,
            RuntimePanelFactory.Raised, compactPortrait ? 30 : 26);
        launcher.onClick.AddListener(Open);
        _launcher = launcher.gameObject;

        _panel = RuntimePanelFactory.PanelObject("AITrainingDashboard", parent,
            new Vector2(0.5f, 0.5f), compactPortrait
                ? new Vector2(930f, 1500f)
                : new Vector2(1050f, 700f),
            RuntimePanelFactory.Panel);
        Text title = RuntimePanelFactory.Text("Title", _panel.transform,
            "AI 训练档案", compactPortrait ? 44 : 40, TextAnchor.MiddleLeft,
            RuntimePanelFactory.TextPrimary);
        title.rectTransform.pivot = new Vector2(0f, 0.5f);
        RuntimePanelFactory.Place(title.rectTransform,
            compactPortrait ? new Vector2(0.08f, 0.93f) : new Vector2(0.08f, 0.89f),
            compactPortrait ? new Vector2(760f, 90f) : new Vector2(650f, 70f),
            Vector2.zero);

        _metrics = RuntimePanelFactory.Text("Metrics", _panel.transform, "",
            compactPortrait ? 30 : 26, TextAnchor.UpperLeft,
            RuntimePanelFactory.TextPrimary);
        _metrics.lineSpacing = 1.25f;
        RuntimePanelFactory.Place(_metrics.rectTransform,
            compactPortrait ? new Vector2(0.5f, 0.68f) : new Vector2(0.34f, 0.59f),
            compactPortrait ? new Vector2(780f, 500f) : new Vector2(580f, 300f),
            Vector2.zero);

        GameObject insight = RuntimePanelFactory.PanelObject("Insight", _panel.transform,
            compactPortrait ? new Vector2(0.5f, 0.38f) : new Vector2(0.5f, 0.29f),
            compactPortrait ? new Vector2(780f, 280f) : new Vector2(900f, 150f),
            new Color(0.10f, 0.16f, 0.22f, 1f));
        _summary = RuntimePanelFactory.Text("Summary", insight.transform, "",
            compactPortrait ? 29 : 25, TextAnchor.MiddleLeft,
            RuntimePanelFactory.TextPrimary);
        RuntimePanelFactory.Stretch(_summary.rectTransform, 28f);

        _resetHint = RuntimePanelFactory.Text("ResetHint", _panel.transform,
            "重置会清空影子、导演和玩家能力模型。", 19,
            TextAnchor.MiddleLeft, RuntimePanelFactory.TextMuted);
        RuntimePanelFactory.Place(_resetHint.rectTransform,
            compactPortrait ? new Vector2(0.5f, 0.17f) : new Vector2(0.26f, 0.09f),
            compactPortrait ? new Vector2(760f, 90f) : new Vector2(520f, 50f),
            Vector2.zero);
        Button reset = RuntimePanelFactory.Button("Reset", _panel.transform, "重置训练",
            compactPortrait ? new Vector2(0.30f, 0.07f) : new Vector2(0.64f, 0.09f),
            compactPortrait ? new Vector2(320f, 100f) : new Vector2(210f, 58f),
            new Color(0.55f, 0.22f, 0.22f), compactPortrait ? 28 : 22);
        reset.onClick.AddListener(ConfirmReset);
        Button close = RuntimePanelFactory.Button("Close", _panel.transform, "返回",
            compactPortrait ? new Vector2(0.72f, 0.07f) : new Vector2(0.87f, 0.09f),
            compactPortrait ? new Vector2(280f, 100f) : new Vector2(180f, 58f),
            RuntimePanelFactory.Raised, compactPortrait ? 28 : 22);
        close.onClick.AddListener(() => _panel.SetActive(false));
        Button liveDebug = RuntimePanelFactory.Button("LiveDebug", _panel.transform,
            "实时诊断", compactPortrait
                ? new Vector2(0.77f, 0.93f)
                : new Vector2(0.84f, 0.89f),
            compactPortrait ? new Vector2(250f, 82f) : new Vector2(210f, 58f),
            RuntimePanelFactory.Action, compactPortrait ? 25 : 21);
        liveDebug.onClick.AddListener(ToggleLiveDebug);
        _panel.SetActive(false);

        BuildLiveDebug(parent, compactPortrait);
    }

    private void Open()
    {
        Refresh();
        _panel.SetActive(true);
    }

    private void Refresh()
    {
        AIRunTelemetryData telemetry = AIRunTelemetry.FromJson(
            EchoRunSaveSystem.GetLastRunTelemetryJson());
        AITrainingReport report = AITrainingReportBuilder.FromTelemetry(telemetry);
        if (report == null)
        {
            int generation = AIShadowRunner.Instance != null
                ? AIShadowRunner.Instance.Generation
                : 0;
            _metrics.text = "当前影子代数  " + generation
                            + "\n导演更新次数  " + EchoRunSaveSystem.DirectorModelUpdateCount
                            + "\n\n" + BuildStyleSummary();
            _summary.text = "尚无完整训练局。下一局会记录动作、能力估计和模型变化。";
            return;
        }

        _metrics.text = "影子代数       " + report.generationBefore + "  →  " + report.generationAfter
                        + "\n导演更新       " + report.directorUpdatesBefore + "  →  " + report.directorUpdatesAfter
                        + "\n玩家能力       " + (report.skillBefore * 100f).ToString("0") + "%  →  "
                        + (report.skillAfter * 100f).ToString("0") + "%"
                        + "\n影子权重变化   " + report.shadowWeightDelta.ToString("0.000")
                        + "\n导演权重变化   " + report.directorWeightDelta.ToString("0.000")
                        + "\n本代样本重点   " + report.learnedAction
                        + "\n\n" + BuildStyleSummary();
        _summary.text = "本代学会了什么\n" + report.summary;
    }

    private void ConfirmReset()
    {
        if (_resetConfirmUntil <= Time.unscaledTime)
        {
            _resetConfirmUntil = Time.unscaledTime + 4f;
            _resetHint.text = "再次点击“重置训练”确认清空。";
            AudioManager.Instance?.PlayUIError();
            return;
        }

        EchoRunSaveSystem.ResetAITraining();
        AIPlayerSkillEstimator.ResetTraining();
        StyleTracker.ResetTraining();
        AIShadowRunner.Instance?.ResetTraining();
        AITrackDirector.Instance?.ResetTraining();
        _resetConfirmUntil = 0f;
        _resetHint.text = "训练已重置，下一局将重新校准。";
        AudioManager.Instance?.PlayUIConfirm();
        Refresh();
    }

    private void OnStateChanged(GameState state)
    {
        bool menu = state == GameState.Menu;
        if (_launcher != null) _launcher.SetActive(menu);
        if (!menu && _panel != null) _panel.SetActive(false);
        if (_liveDebugPanel != null)
            _liveDebugPanel.SetActive(
                _liveDebugEnabled && state == GameState.Playing);
    }

    private void BuildLiveDebug(Transform parent, bool compactPortrait)
    {
        _liveDebugPanel = RuntimePanelFactory.PanelObject("AI Live Debug", parent,
            compactPortrait ? new Vector2(0.5f, 0.76f) : new Vector2(0.23f, 0.72f),
            compactPortrait ? new Vector2(880f, 520f) : new Vector2(650f, 420f),
            new Color(0.025f, 0.04f, 0.06f, 0.92f));
        _liveDebugText = RuntimePanelFactory.Text("Content",
            _liveDebugPanel.transform, "", compactPortrait ? 24 : 19,
            TextAnchor.UpperLeft, RuntimePanelFactory.TextPrimary);
        _liveDebugText.lineSpacing = 1.12f;
        RuntimePanelFactory.Stretch(_liveDebugText.rectTransform,
            compactPortrait ? 28f : 22f);
        _liveDebugPanel.SetActive(false);
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
            : "最终动作 " + trace.selectedAction
              + (trace.safetyAdjusted ? "  [安全覆盖]" : "")
              + "\n基础  " + FormatScores(trace.baseScores)
              + "\n风格  " + FormatScores(trace.styleAdjustedScores)
              + "\n最终  " + FormatScores(trace.finalScores);
        _liveDebugText.text = "AI 实时诊断"
            + "\n激进 " + Percent(style.aggressiveness)
            + "  跳时 " + SignedPercent(style.jumpTiming)
            + "  滑铲 " + Percent(style.slideFrequency)
            + "\n车道 " + SignedPercent(style.lanePreference)
            + "  节奏 " + Percent(style.rhythmStability)
            + "  恢复 " + Percent(style.recoveryStyle)
            + "\n置信 " + Percent(style.Confidence)
            + "  全局风险 " + SignedPercent(directive.riskBias)
            + "  风格强度 " + Percent(directive.styleInfluence)
            + "\n\n" + decision;
    }

    private static string BuildStyleSummary()
    {
        PlayerStyleData style = StyleTracker.GetSnapshot();
        return "风格置信       " + Percent(style.Confidence)
            + "\n激进 " + Percent(style.aggressiveness)
            + "  跳时 " + SignedPercent(style.jumpTiming)
            + "  滑铲 " + Percent(style.slideFrequency)
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
