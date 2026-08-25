using UnityEngine;
using UnityEngine.UI;

public sealed class EchoHudView : MonoBehaviour
{
    [Header("Layers")]
    [SerializeField] private GameObject staticLayer;
    [SerializeField] private GameObject dynamicLayer;

    [Header("Primary")]
    [SerializeField] private Text statsText;
    [SerializeField] private Text announcementText;
    [SerializeField] private Text directiveText;
    [SerializeField] private Text predictionText;
    [SerializeField] private Text calibrationObservationText;
    [SerializeField] private Text distanceText;
    [SerializeField] private GameObject stageRail;
    [SerializeField] private Text[] stageNodes;
    [SerializeField] private GameObject calibrationRail;

    [Header("Meter")]
    [SerializeField] private GameObject meterGroup;
    [SerializeField] private Text meterLabel;
    [SerializeField] private Image meterFill;

    [Header("Lead")]
    [SerializeField] private GameObject leadGroup;
    [SerializeField] private Text leadText;
    [SerializeField] private RectTransform leadMarker;

    [Header("Sync")]
    [SerializeField] private Image[] syncCells;
    [SerializeField] private Text recoveryText;

    [Header("Edges")]
    [SerializeField] private GameObject markerGroup;
    [SerializeField] private Text markerText;
    [SerializeField] private GameObject buffGroup;
    [SerializeField] private Text buffText;
    [SerializeField] private Text feedbackText;
    [SerializeField] private Button pauseButton;

    private static readonly Color Cyan = EchoRunUITheme.RouteCyan;
    private static readonly Color Coral = EchoRunUITheme.Danger;
    private static readonly Color Gold = EchoRunUITheme.Reward;
    private static readonly Color Muted = EchoRunUITheme.TextMuted;
    private static readonly Color EmptyCell = new Color(0.13f, 0.20f, 0.27f, 0.92f);

    public Button PauseButton => pauseButton;

    public void Present(EchoHudViewData data, bool showAnnouncement)
    {
        bool calibrating = data.mode == EchoHudMode.Calibration;
        SetActiveIfChanged(stageRail, !calibrating);
        SetActiveIfChanged(calibrationRail, calibrating);
        SetActiveIfChanged(leadGroup, !calibrating);

        SetTextIfChanged(announcementText, data.announcement);
        SetActiveIfChanged(announcementText != null
            ? announcementText.gameObject : null, showAnnouncement);
        SetTextIfChanged(directiveText, data.directiveShort);
        SetTextIfChanged(predictionText, data.predictionShort);
        SetActiveIfChanged(predictionText != null ? predictionText.gameObject : null,
            data.showPrediction && !string.IsNullOrEmpty(data.predictionShort));

        SetTextIfChanged(calibrationObservationText,
            calibrating ? "路线  记录中    节奏  采集中" : "");
        SetTextIfChanged(distanceText,
            data.remainingDistance > 0f
                ? "终点 " + Mathf.CeilToInt(data.remainingDistance) + "m"
                : "终点已定位");

        PresentStage(data.phaseIndex);
        PresentMeter(data);
        PresentLead(data);
        PresentSync(data);

        SetActiveIfChanged(markerGroup, data.showContractMarkers);
        SetTextIfChanged(markerText, "契约标记 " + data.contractMarkerCount);

        bool showBuff = data.showBuff && !string.IsNullOrEmpty(data.buffText);
        SetActiveIfChanged(buffGroup, showBuff);
        SetTextIfChanged(buffText, data.buffText);
    }

    public void SetStats(int score, float distance)
    {
        SetTextIfChanged(statsText, string.Format(
            "SCORE {0:D5}   RANGE {1:000}m", Mathf.Max(0, score),
            Mathf.Max(0, Mathf.FloorToInt(distance))));
    }

    public void ShowFeedback(string text, Color color, bool visible)
    {
        SetTextIfChanged(feedbackText, text);
        if (feedbackText != null && feedbackText.color != color)
            feedbackText.color = color;
        SetActiveIfChanged(feedbackText != null ? feedbackText.gameObject : null,
            visible && !string.IsNullOrEmpty(text));
    }

    private void PresentStage(int phaseIndex)
    {
        if (stageNodes == null) return;
        for (int i = 0; i < stageNodes.Length; i++)
        {
            Text node = stageNodes[i];
            if (node == null) continue;
            Color target = i < phaseIndex ? Muted
                : i == phaseIndex ? Cyan : new Color(Muted.r, Muted.g, Muted.b, 0.45f);
            if (node.color != target) node.color = target;
            FontStyle style = i == phaseIndex ? FontStyle.Bold : FontStyle.Normal;
            if (node.fontStyle != style) node.fontStyle = style;
        }
    }

    private void PresentMeter(EchoHudViewData data)
    {
        bool visible = data.meterKind != EchoHudMeterKind.None;
        SetActiveIfChanged(meterGroup, visible);
        if (!visible) return;

        string label = data.meterKind == EchoHudMeterKind.Calibration
            ? "校准" : data.meterKind == EchoHudMeterKind.Phase
                ? "阶段" : "回声锁定";
        SetTextIfChanged(meterLabel, label);
        SetFillIfChanged(meterFill, data.displayedMeter01);
        if (meterFill != null)
        {
            Color target = data.meterKind == EchoHudMeterKind.EchoLock
                ? Coral : Cyan;
            if (data.meterKind == EchoHudMeterKind.EchoLock
                && data.displayedMeter01 <= 0.01f)
                target = Gold;
            else if (data.meterKind != EchoHudMeterKind.EchoLock
                     && data.displayedMeter01 >= 1f)
                target = Gold;
            if (meterFill.color != target) meterFill.color = target;
        }
    }

    private void PresentLead(EchoHudViewData data)
    {
        if (data.mode == EchoHudMode.Calibration) return;
        string sign = data.leadMeters > 0.05f ? "+" : "";
        SetTextIfChanged(leadText, sign + data.leadMeters.ToString("0.0") + "m");
        if (leadText != null)
        {
            Color target = data.leadMeters > 0.25f ? Gold
                : data.leadMeters < -0.25f ? Coral : Muted;
            if (leadText.color != target) leadText.color = target;
        }
        if (leadMarker != null)
        {
            Vector2 anchor = leadMarker.anchorMin;
            float x = Mathf.Clamp01(data.leadPosition01);
            if (!Mathf.Approximately(anchor.x, x))
            {
                leadMarker.anchorMin = new Vector2(x, 0.5f);
                leadMarker.anchorMax = new Vector2(x, 0.5f);
            }
        }
    }

    private void PresentSync(EchoHudViewData data)
    {
        if (syncCells != null)
        {
            for (int i = 0; i < syncCells.Length; i++)
            {
                Image cell = syncCells[i];
                if (cell == null) continue;
                Color target = i < data.syncRemaining ? Cyan : EmptyCell;
                if (cell.color != target) cell.color = target;
            }
        }

        bool recovering = data.recoverySeconds > 0.01f;
        SetTextIfChanged(recoveryText, recovering
            ? "失步 · 重同步 " + data.recoverySeconds.ToString("0.0") + "s"
            : "");
        SetActiveIfChanged(recoveryText != null ? recoveryText.gameObject : null,
            recovering);
    }

    private static void SetTextIfChanged(Text target, string value)
    {
        if (target == null) return;
        string safe = value ?? "";
        if (target.text != safe) target.text = safe;
    }

    private static void SetFillIfChanged(Image target, float value)
    {
        if (target == null) return;
        float safe = Mathf.Clamp01(value);
        if (!Mathf.Approximately(target.fillAmount, safe))
            target.fillAmount = safe;
    }

    private static void SetActiveIfChanged(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }
}
