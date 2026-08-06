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
        _panel.SetActive(false);
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
                            + "\n\n完成一局后显示训练前后差异。";
            _summary.text = "尚无完整训练局。下一局会记录动作、能力估计和模型变化。";
            return;
        }

        _metrics.text = "影子代数       " + report.generationBefore + "  →  " + report.generationAfter
                        + "\n导演更新       " + report.directorUpdatesBefore + "  →  " + report.directorUpdatesAfter
                        + "\n玩家能力       " + (report.skillBefore * 100f).ToString("0") + "%  →  "
                        + (report.skillAfter * 100f).ToString("0") + "%"
                        + "\n影子权重变化   " + report.shadowWeightDelta.ToString("0.000")
                        + "\n导演权重变化   " + report.directorWeightDelta.ToString("0.000")
                        + "\n本代样本重点   " + report.learnedAction;
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
    }

    void OnDestroy()
    {
        if (_gameManager != null)
            _gameManager.OnStateChanged.RemoveListener(OnStateChanged);
    }
}
