using UnityEngine;

public enum EchoLeadState
{
    Calibrating,
    Tied,
    Leading,
    Trailing
}

public enum EchoHudMode
{
    Calibration,
    Detection,
    Reveal,
    Resistance,
    Counterattack,
    Rewrite,
    FinaleClean,
    FinaleContract
}

public enum EchoHudMeterKind
{
    None,
    Calibration,
    Phase,
    Stability
}

public struct EchoHudViewData
{
    public EchoHudMode mode;
    public EchoHudMeterKind meterKind;

    public int phaseIndex;
    public float calibrationProgress01;
    public float phaseProgress01;
    public float contractStability01;
    public float displayedMeter01;

    public string announcement;
    public string directiveShort;
    public string predictionShort;

    public float leadMeters;
    public float leadPosition01;

    public int syncRemaining;
    public float recoveryProgress01;
    public float recoverySeconds;
    public float remainingDistance;

    public int contractMarkerCount;
    public bool showContractMarkers;

    public bool showPrediction;
    public bool showBuff;
    public string buffText;
}

public struct EchoMenuViewData
{
    public string generation;
    public string learned;
    public string rule;
    public string objective;
    public string primaryAction;
}

public struct EchoDuelViewData
{
    public string phase;
    public string contract;
    public string progress;
    public float progress01;
    public string lead;
    public EchoLeadState leadState;
    public string feedback;
    public string prediction;
    public int feedbackSequence;
}

public static class EchoRunPresentation
{
    public static EchoMenuViewData BuildMenu(int generation,
        PlayerStyleData style, int minimumJumpSamples, int minimumSlideSamples,
        EchoContractData contractPreview = null, float echoClarity = 1f)
    {
        if (generation <= 0)
        {
            return new EchoMenuViewData
            {
                generation = "首次回声校准",
                learned = "跑一局，让回声学习你的路线、动作与节奏",
                rule = "校准完成后会生成第 1 代回声",
                objective = "至少跳跃 " + minimumJumpSamples
                            + " 次并滑铲 " + minimumSlideSamples + " 次",
                primaryAction = "开始校准"
            };
        }

        EchoContractData contract = contractPreview != null
            ? contractPreview.ResetForRun()
            : EchoContractPolicy.Create(style, generation);
        return new EchoMenuViewData
        {
            generation = "第 " + generation + " 代回声"
                         + (echoClarity < 0.995f
                             ? " · 清晰度 "
                               + Mathf.RoundToInt(Mathf.Clamp01(echoClarity)
                                                  * 100f) + "%"
                             : ""),
            learned = TrimPrefix(contract.learnedTrait, "AI识别："),
            rule = contract.ruleDescription,
            objective = contract.objective,
            primaryAction = "挑战第 " + generation + " 代回声"
        };
    }

    public static EchoHudViewData BuildHud(bool hasOpponent,
        EchoContractData contract, float playerLead,
        int minimumJumpSamples, int minimumSlideSamples,
        int jumpSamples = 0, int slideSamples = 0,
        float calibrationProgress01 = 0f,
        EchoDuelPhase duelPhase = EchoDuelPhase.None,
        float phaseProgress01 = 0f, string publicPrediction = "",
        int syncRemaining = 2, float recoverySeconds = 0f,
        float recoveryDuration = 1.25f, float remainingDistance = 0f,
        int contractMarkerCount = 0, bool showBuff = false,
        string buffText = "")
    {
        bool calibrating = !hasOpponent || contract == null
                           || contract.type == EchoContractType.None;
        float calibration = Mathf.Clamp01(calibrationProgress01);
        float phaseProgress = Mathf.Clamp01(phaseProgress01);
        float stability = contract != null ? contract.Progress01 : 0f;
        if (calibrating)
        {
            return new EchoHudViewData
            {
                mode = EchoHudMode.Calibration,
                meterKind = EchoHudMeterKind.Calibration,
                phaseIndex = -1,
                calibrationProgress01 = calibration,
                phaseProgress01 = phaseProgress,
                contractStability01 = stability,
                displayedMeter01 = calibration,
                announcement = "回声校准",
                directiveShort = "跳跃 "
                    + Mathf.Min(jumpSamples, minimumJumpSamples) + "/"
                    + minimumJumpSamples + " · 滑铲 "
                    + Mathf.Min(slideSamples, minimumSlideSamples) + "/"
                    + minimumSlideSamples,
                predictionShort = "",
                leadMeters = 0f,
                leadPosition01 = 0.5f,
                syncRemaining = Mathf.Max(0, syncRemaining),
                recoveryProgress01 = RecoveryProgress(recoverySeconds,
                    recoveryDuration),
                recoverySeconds = Mathf.Max(0f, recoverySeconds),
                remainingDistance = Mathf.Max(0f, remainingDistance),
                showPrediction = false,
                showBuff = showBuff,
                buffText = buffText ?? ""
            };
        }

        EchoDuelPhase phase = duelPhase != EchoDuelPhase.None
            ? duelPhase
            : contract.duelPhase != EchoDuelPhase.None
                ? contract.duelPhase
                : EchoDuelPhase.Resistance;
        bool finaleNeedsContract = phase == EchoDuelPhase.Finale
                                   && !contract.completed;
        EchoHudMode mode = ResolveHudMode(phase, finaleNeedsContract);
        EchoHudMeterKind meterKind = ResolveMeterKind(mode);
        float displayed = meterKind == EchoHudMeterKind.Phase
            ? phaseProgress
            : meterKind == EchoHudMeterKind.Stability ? stability : 0f;
        bool showPrediction = phase == EchoDuelPhase.Reveal
                              || phase == EchoDuelPhase.Resistance
                              || phase == EchoDuelPhase.Counterattack;
        float leadRange = phase == EchoDuelPhase.Finale ? 20f : 12f;
        float leadPosition = 0.5f + 0.5f * (float)System.Math.Tanh(
            playerLead / leadRange);

        return new EchoHudViewData
        {
            mode = mode,
            meterKind = meterKind,
            phaseIndex = PhaseIndex(phase),
            calibrationProgress01 = calibration,
            phaseProgress01 = phaseProgress,
            contractStability01 = stability,
            displayedMeter01 = displayed,
            announcement = AnnouncementFor(mode),
            directiveShort = DirectiveFor(mode, contract),
            predictionShort = showPrediction
                ? BuildShortPrediction(contract) : "",
            leadMeters = playerLead,
            leadPosition01 = Mathf.Clamp01(leadPosition),
            syncRemaining = Mathf.Max(0, syncRemaining),
            recoveryProgress01 = RecoveryProgress(recoverySeconds,
                recoveryDuration),
            recoverySeconds = Mathf.Max(0f, recoverySeconds),
            remainingDistance = Mathf.Max(0f, remainingDistance),
            contractMarkerCount = Mathf.Max(0, contractMarkerCount),
            showContractMarkers = contract.type
                                  == EchoContractType.BreakLaneHabit,
            showPrediction = showPrediction,
            showBuff = showBuff,
            buffText = buffText ?? ""
        };
    }

    public static string BuildShortDirective(EchoContractData contract)
    {
        if (contract == null) return "";
        switch (contract.type)
        {
            case EchoContractType.BreakLaneHabit:
                return "改走" + EchoContractPolicy.LaneName(contract.targetLane);
            case EchoContractType.ChangeVerticalHabit:
                return EchoContractPolicy.LaneName(contract.targetLane)
                       + EchoContractPolicy.ActionName(contract.targetAction);
            case EchoContractType.DisruptRhythm:
                return "下一次" + EchoContractPolicy.ActionName(
                    contract.targetAction);
            default:
                return "";
        }
    }

    public static string BuildShortPrediction(EchoContractData contract)
    {
        if (contract == null) return "";
        if (contract.type == EchoContractType.BreakLaneHabit)
        {
            int lane = contract.predictionLane >= 0
                ? contract.predictionLane : contract.learnedLane;
            return "预判" + EchoContractPolicy.LaneName(lane);
        }
        ShadowAction action = contract.predictionAction != ShadowAction.Keep
            ? contract.predictionAction : contract.learnedAction;
        return "预判" + EchoContractPolicy.ActionName(action);
    }

    public static EchoDuelViewData BuildDuel(bool hasOpponent,
        EchoContractData contract, float playerLead,
        int minimumJumpSamples, int minimumSlideSamples,
        int jumpSamples = 0, int slideSamples = 0,
        float calibrationProgress01 = 0f,
        EchoDuelPhase duelPhase = EchoDuelPhase.None,
        float phaseProgress01 = 0f, string publicPrediction = "")
    {
        if (!hasOpponent || contract == null
            || contract.type == EchoContractType.None)
        {
            return new EchoDuelViewData
            {
                phase = "校准",
                contract = "正在校准你的回声",
                progress = "跳跃 " + Mathf.Min(jumpSamples, minimumJumpSamples)
                           + "/" + minimumJumpSamples + " · 滑铲 "
                           + Mathf.Min(slideSamples, minimumSlideSamples)
                           + "/" + minimumSlideSamples,
                progress01 = Mathf.Clamp01(calibrationProgress01),
                lead = "记录路线、动作与节奏",
                leadState = EchoLeadState.Calibrating,
                feedback = ""
            };
        }

        EchoDuelPhase phase = duelPhase != EchoDuelPhase.None
            ? duelPhase
            : contract.duelPhase != EchoDuelPhase.None
                ? contract.duelPhase : EchoDuelPhase.Resistance;

        EchoHudViewData hud = BuildHud(true, contract, playerLead,
            minimumJumpSamples, minimumSlideSamples, jumpSamples, slideSamples,
            calibrationProgress01, phase, phaseProgress01, publicPrediction);

        EchoLeadState state;
        string lead;
        if (playerLead > 0.05f)
        {
            state = EchoLeadState.Leading;
            lead = "领先 +" + playerLead.ToString("0.0") + "m";
        }
        else if (playerLead < -0.05f)
        {
            state = EchoLeadState.Trailing;
            lead = "落后 -" + Mathf.Abs(playerLead).ToString("0.0") + "m";
        }
        else
        {
            state = EchoLeadState.Tied;
            lead = "并驾齐驱";
        }

        return new EchoDuelViewData
        {
            phase = EchoDuelFlow.PhaseName(phase),
            contract = BuildContractAction(contract),
            progress = BuildProgressText(contract, phase, phaseProgress01),
            progress01 = hud.displayedMeter01,
            lead = lead,
            leadState = state,
            feedback = BuildFeedback(contract.lastFeedback),
            prediction = ShouldShowPrediction(phase)
                ? (string.IsNullOrEmpty(publicPrediction)
                    ? BuildPublicPrediction(contract) : publicPrediction)
                : "",
            feedbackSequence = contract.feedbackSequence
        };
    }

    private static EchoHudMode ResolveHudMode(EchoDuelPhase phase,
        bool finaleNeedsContract)
    {
        switch (phase)
        {
            case EchoDuelPhase.Detection: return EchoHudMode.Detection;
            case EchoDuelPhase.Reveal: return EchoHudMode.Reveal;
            case EchoDuelPhase.Counterattack: return EchoHudMode.Counterattack;
            case EchoDuelPhase.Rewrite: return EchoHudMode.Rewrite;
            case EchoDuelPhase.Finale:
                return finaleNeedsContract
                    ? EchoHudMode.FinaleContract
                    : EchoHudMode.FinaleClean;
            default: return EchoHudMode.Resistance;
        }
    }

    private static EchoHudMeterKind ResolveMeterKind(EchoHudMode mode)
    {
        switch (mode)
        {
            case EchoHudMode.Calibration:
                return EchoHudMeterKind.Calibration;
            case EchoHudMode.Detection:
            case EchoHudMode.Rewrite:
                return EchoHudMeterKind.Phase;
            case EchoHudMode.Resistance:
            case EchoHudMode.Counterattack:
            case EchoHudMode.FinaleContract:
                return EchoHudMeterKind.Stability;
            default:
                return EchoHudMeterKind.None;
        }
    }

    private static int PhaseIndex(EchoDuelPhase phase)
    {
        switch (phase)
        {
            case EchoDuelPhase.Detection: return 0;
            case EchoDuelPhase.Reveal: return 1;
            case EchoDuelPhase.Resistance: return 2;
            case EchoDuelPhase.Counterattack: return 3;
            case EchoDuelPhase.Rewrite: return 4;
            case EchoDuelPhase.Finale: return 5;
            default: return -1;
        }
    }

    private static string AnnouncementFor(EchoHudMode mode)
    {
        switch (mode)
        {
            case EchoHudMode.Detection: return "回声侦测";
            case EchoHudMode.Reveal: return "回声暴露";
            case EchoHudMode.Resistance: return "回声反抗";
            case EchoHudMode.Counterattack: return "回声反扑";
            case EchoHudMode.Rewrite: return "回声重写";
            case EchoHudMode.FinaleClean:
            case EchoHudMode.FinaleContract: return "回声决胜";
            default: return "回声校准";
        }
    }

    private static string DirectiveFor(EchoHudMode mode,
        EchoContractData contract)
    {
        switch (mode)
        {
            case EchoHudMode.Detection: return "复现中";
            case EchoHudMode.Reveal: return BuildShortPrediction(contract);
            case EchoHudMode.Rewrite: return "记录中";
            case EchoHudMode.FinaleClean: return "";
            case EchoHudMode.FinaleContract: return "最后机会";
            default: return BuildShortDirective(contract);
        }
    }

    private static float RecoveryProgress(float remaining, float duration)
    {
        if (remaining <= 0f || duration <= 0f) return 0f;
        return 1f - Mathf.Clamp01(remaining / duration);
    }

    private static string BuildProgressText(EchoContractData contract,
        EchoDuelPhase phase, float phaseProgress01)
    {
        if (phase == EchoDuelPhase.Detection)
            return "复现 " + Mathf.RoundToInt(phaseProgress01 * 100f) + "%";
        if (phase == EchoDuelPhase.Reveal)
            return "习惯暴露";
        if (phase == EchoDuelPhase.Rewrite)
            return "重写 " + Mathf.RoundToInt(phaseProgress01 * 100f) + "%";
        if (contract.completed) return "已重写";
        return "稳定度 " + Mathf.RoundToInt(contract.Progress01 * 100f) + "%";
    }

    private static bool ShouldShowPrediction(EchoDuelPhase phase)
    {
        return phase == EchoDuelPhase.Reveal
               || phase == EchoDuelPhase.Resistance
               || phase == EchoDuelPhase.Counterattack
               || phase == EchoDuelPhase.Finale;
    }

    private static string BuildPublicPrediction(EchoContractData contract)
    {
        if (contract.type == EchoContractType.BreakLaneHabit)
        {
            int lane = contract.predictionLane >= 0
                ? contract.predictionLane : contract.learnedLane;
            return "预判：依赖" + EchoContractPolicy.LaneName(lane);
        }
        ShadowAction action = contract.predictionAction != ShadowAction.Keep
            ? contract.predictionAction : contract.learnedAction;
        return "预判：继续" + EchoContractPolicy.ActionName(action);
    }

    public static string BuildContractAction(EchoContractData contract)
    {
        if (contract == null) return "回声契约";
        switch (contract.type)
        {
            case EchoContractType.BreakLaneHabit:
                return EchoContractPolicy.LaneName(contract.targetLane)
                       + " · 收集引导金币";
            case EchoContractType.ChangeVerticalHabit:
                return EchoContractPolicy.LaneName(contract.targetLane) + " · "
                       + EchoContractPolicy.ActionName(contract.targetAction)
                       + "躲避";
            case EchoContractType.DisruptRhythm:
                return EchoContractPolicy.LaneName(contract.targetLane)
                       + " · 下一次："
                       + EchoContractPolicy.ActionName(contract.targetAction);
            default:
                return TrimPrefix(contract.title, "回声契约：");
        }
    }

    private static string BuildFeedback(string feedback)
    {
        if (string.IsNullOrEmpty(feedback)) return "";
        if (feedback.StartsWith("反制生效："))
            return "反制成功 · " + TrimPrefix(feedback, "反制生效：");
        if (feedback.StartsWith("AI施压："))
            return "回声施压 · " + TrimPrefix(feedback, "AI施压：");
        if (feedback.StartsWith("回声施压："))
            return "回声施压 · " + TrimPrefix(feedback, "回声施压：");
        if (feedback.StartsWith("预测失效："))
            return "预测失效 · " + TrimPrefix(feedback, "预测失效：");
        return feedback;
    }

    private static string TrimPrefix(string value, string prefix)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.StartsWith(prefix) ? value.Substring(prefix.Length) : value;
    }
}
