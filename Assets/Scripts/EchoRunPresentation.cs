using UnityEngine;

public enum EchoLeadState
{
    Calibrating,
    Tied,
    Leading,
    Trailing
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

public struct EchoBriefingRow
{
    public string icon;
    public string label;
    public string value;

    public EchoBriefingRow(string icon, string label, string value)
    {
        this.icon = icon;
        this.label = label;
        this.value = value;
    }
}

public struct EchoBriefingViewData
{
    public string title;
    public string subtitle;
    public EchoBriefingRow[] rows;
    public string primaryAction;
    public string footnote;
}

public sealed class EchoPhaseBannerData
{
    public readonly string title;
    public readonly string subtitle;
    public readonly Color accent;

    public EchoPhaseBannerData(string title, string subtitle, Color accent)
    {
        this.title = title;
        this.subtitle = subtitle;
        this.accent = accent;
    }
}

public struct EchoReportRow
{
    public string icon;
    public string label;
    public string value;

    public EchoReportRow(string icon, string label, string value)
    {
        this.icon = icon;
        this.label = label;
        this.value = value;
    }
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
                generation = "首次校准 · 回声尚未形成",
                learned = "路线、动作与节奏等待采样",
                rule = "完成校准后生成第 01 代回声",
                objective = "至少跳跃 " + minimumJumpSamples
                            + " 次并滑铲 " + minimumSlideSamples + " 次",
                primaryAction = "开始校准"
            };
        }

        EchoContractData contract = contractPreview != null
            ? contractPreview.ResetForRun()
            : EchoContractPolicy.Create(style, generation);
        string status = echoClarity < 0.995f
            ? " · 清晰度 "
              + Mathf.RoundToInt(Mathf.Clamp01(echoClarity) * 100f) + "%"
            : " · 已就绪";
        return new EchoMenuViewData
        {
            generation = "第 " + generation.ToString("D2") + " 代回声" + status,
            learned = TrimPrefix(contract.learnedTrait, "AI识别："),
            rule = TrimPrefix(contract.title, "回声契约："),
            objective = contract.objective,
            primaryAction = "挑战第 " + generation.ToString("D2") + " 代回声"
        };
    }

    public static EchoDuelViewData BuildDuel(bool hasOpponent,
        EchoContractData contract, float playerLead,
        int minimumJumpSamples, int minimumSlideSamples,
        int jumpSamples = 0, int slideSamples = 0,
        float calibrationProgress01 = 0f,
        EchoDuelPhase duelPhase = EchoDuelPhase.None,
        float phaseProgress01 = 0f, string publicPrediction = "",
        string publicChallenge = "", string publicEvidence = "",
        EchoCalibrationStatus calibrationStatus = null)
    {
        if (!hasOpponent || contract == null
            || contract.type == EchoContractType.None)
        {
            return new EchoDuelViewData
            {
                phase = "校准",
                contract = "正在校准你的回声",
                progress = BuildCalibrationProgress(calibrationStatus,
                    jumpSamples, slideSamples, minimumJumpSamples,
                    minimumSlideSamples),
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
            contract = BuildPhaseInstruction(
                phase, contract, publicChallenge, publicEvidence),
            progress = BuildProgressText(contract, phase, phaseProgress01),
            progress01 = UsesPhaseProgress(phase)
                ? Mathf.Clamp01(phaseProgress01) : contract.Progress01,
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

    private static bool UsesPhaseProgress(EchoDuelPhase phase)
    {
        return phase == EchoDuelPhase.Detection
               || phase == EchoDuelPhase.Reveal
               || phase == EchoDuelPhase.Rewrite;
    }

    private static string BuildProgressText(EchoContractData contract,
        EchoDuelPhase phase, float phaseProgress01)
    {
        if (phase == EchoDuelPhase.Detection)
            return "复现 " + Mathf.RoundToInt(phaseProgress01 * 100f) + "%";
        if (phase == EchoDuelPhase.Reveal)
            return "习惯暴露";
        if (phase == EchoDuelPhase.Resistance)
            return "反抗 " + contract.resistanceGroupsResolved + "/3 · 预测失效 "
                   + contract.resistancePredictionMisses + "/2 · 策略 "
                   + CountResponseKinds(contract.resistanceDistinctResponseMask)
                   + "/2";
        if (phase == EchoDuelPhase.Counterattack)
            return "反扑 " + contract.counterattackGroupsResolved
                   + "/4 · 预测失效 "
                   + contract.counterattackPredictionMisses + "/3 · 策略 "
                   + CountResponseKinds(contract.counterattackDistinctResponseMask)
                   + "/2";
        if (phase == EchoDuelPhase.Rewrite)
            return (contract.contractBroken ? "契约已破解 · " : "契约未破解 · ")
                   + (phaseProgress01 >= 0.999f
                       ? "学习窗口完成，继续追逐"
                       : "重写 " + Mathf.RoundToInt(phaseProgress01 * 100f) + "%");
        if (contract.completed) return "已破解";
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

    public static string BuildObstacleChallenge(int lane, ObstacleType obstacleType)
    {
        ShadowAction action = AIShadowRules.RequiredActionForObstacle(obstacleType);
        if (action != ShadowAction.Jump && action != ShadowAction.Slide)
            return "";
        return "破解要求：前方" + EchoContractPolicy.LaneName(lane)
               + " · " + EchoContractPolicy.ActionName(action);
    }

    public static string BuildChoiceGroupChallenge(EchoChoiceGroup group)
    {
        if (group == null || group.options == null
            || group.options.Length == 0)
            return "";
        bool hasJump = false;
        bool hasSlide = false;
        for (int i = 0; i < group.options.Length; i++)
        {
            hasJump |= group.options[i].obstacleType == ObstacleType.High;
            hasSlide |= group.options[i].obstacleType == ObstacleType.Low;
        }
        string choices = hasJump && hasSlide
            ? "跳跃 / 滑铲 / 走空路"
            : hasJump ? "跳跃 / 走空路"
                : hasSlide ? "滑铲 / 走空路" : "走空路";
        return "下一选择：" + choices;
    }

    private static string BuildCalibrationProgress(
        EchoCalibrationStatus status, int jumpSamples, int slideSamples,
        int minimumJumpSamples, int minimumSlideSamples)
    {
        if (status == null)
            return "跳跃 " + Mathf.Min(jumpSamples, minimumJumpSamples)
                   + "/" + minimumJumpSamples + " · 滑铲 "
                   + Mathf.Min(slideSamples, minimumSlideSamples)
                   + "/" + minimumSlideSamples;
        return "样本 " + status.totalSamples + "/" + status.minimumTotal
               + " · 有效 " + status.activeSamples + "/" + status.minimumActive
               + " · 类型 " + status.categories + "/" + status.minimumCategories
               + "\n跳跃 " + status.jumpSamples + "/" + status.minimumJump
               + " · 滑铲 " + status.slideSamples + "/" + status.minimumSlide
               + " · " + status.nextRequirement;
    }

    private static int CountResponseKinds(int mask)
    {
        int count = 0;
        while (mask != 0)
        {
            count += mask & 1;
            mask >>= 1;
        }
        return count;
    }

    private static string BuildPhaseInstruction(EchoDuelPhase phase,
        EchoContractData contract, string publicChallenge,
        string publicEvidence)
    {
        if (phase == EchoDuelPhase.Detection)
            return string.IsNullOrEmpty(publicEvidence)
                ? "当前阶段：收集本局证据" : publicEvidence;
        if (phase == EchoDuelPhase.Reveal)
            return "上一代画像：" + TrimPrefix(
                contract.learnedTrait, "AI识别：");
        if (phase == EchoDuelPhase.Rewrite)
            return "当前阶段：自由改写跑法";
        return string.IsNullOrEmpty(publicChallenge)
            ? "破解要求：" + BuildContractAction(contract)
            : publicChallenge;
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

    // ═══════════════════════════════════════════════════
    //  Competition surfaces (briefing / banner / report)
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// The contract briefing only earns its extra click when an echo
    /// contract actually exists (generation &gt; 0). Calibration runs and
    /// auto-start recording flows go straight to the track.
    /// </summary>
    public static bool ShouldShowContractBriefing(int generation,
        bool autoStartRequested)
    {
        return generation > 0 && !autoStartRequested;
    }

    public static EchoBriefingViewData BuildBriefing(int generation,
        PlayerStyleData style, EchoContractData contractPreview,
        float echoClarity, int minimumJumpSamples, int minimumSlideSamples)
    {
        if (generation <= 0)
        {
            return new EchoBriefingViewData
            {
                title = "首次回声校准",
                subtitle = "没有对手的这一局，正在塑造你未来的对手",
                rows = new[]
                {
                    new EchoBriefingRow("echo", "它将学习",
                        "路线选择、跳滑时机与行动节奏"),
                    new EchoBriefingRow("detection", "校准目标",
                        "至少跳跃 " + minimumJumpSamples
                        + " 次 · 滑铲 " + minimumSlideSamples + " 次"),
                    new EchoBriefingRow("generation", "完成后",
                        "生成第 1 代回声与首份回声契约"),
                },
                primaryAction = "开始校准",
                footnote = "校准约 75 秒，全程可正常游玩"
            };
        }

        EchoContractData contract = contractPreview != null
            ? contractPreview.ResetForRun()
            : EchoContractPolicy.Create(style, generation);
        int clarityPercent = Mathf.RoundToInt(
            Mathf.Clamp01(echoClarity) * 100f);
        return new EchoBriefingViewData
        {
            title = "第 " + generation + " 代回声 · 赛前简报",
            subtitle = "它按你的习惯布防——按契约跑，拆它的预判",
            rows = new[]
            {
                new EchoBriefingRow("echo", "AI 识别",
                    TrimPrefix(contract.learnedTrait, "AI识别：")),
                new EchoBriefingRow("contract", "本代规则",
                    contract.ruleDescription),
                new EchoBriefingRow("lead", "破解目标", contract.objective),
                new EchoBriefingRow("clarity", "回声清晰度",
                    clarityPercent + "%"),
            },
            primaryAction = "开跑",
            footnote = "决斗六阶段：侦测 → 暴露 → 反抗 → 反扑 → 重写 → 决胜"
        };
    }

    /// <summary>
    /// Returns null for phases that must not interrupt the run with a
    /// banner (None / Calibration / Finished).
    /// </summary>
    public static EchoPhaseBannerData BuildPhaseBanner(EchoDuelPhase phase)
    {
        switch (phase)
        {
            case EchoDuelPhase.Detection:
                return new EchoPhaseBannerData("侦测",
                    "回声正在复现你的跑法", EchoRunUITheme.PhaseDetection);
            case EchoDuelPhase.Reveal:
                return new EchoPhaseBannerData("暴露",
                    "它看穿了你的习惯", EchoRunUITheme.PhaseReveal);
            case EchoDuelPhase.Resistance:
                return new EchoPhaseBannerData("反抗",
                    "按契约行动，打破它的预判", EchoRunUITheme.PhaseResistance);
            case EchoDuelPhase.Counterattack:
                return new EchoPhaseBannerData("反扑",
                    "回声开始针对你的弱点", EchoRunUITheme.PhaseCounterattack);
            case EchoDuelPhase.Rewrite:
                return new EchoPhaseBannerData("重写",
                    "坚持住，它的模型正在崩解", EchoRunUITheme.PhaseRewrite);
            case EchoDuelPhase.Finale:
                return new EchoPhaseBannerData("决胜",
                    "最后窗口——拉开身位", EchoRunUITheme.PhaseFinale);
            default:
                return null;
        }
    }

    public static EchoReportRow[] BuildTrainingReportRows(
        AITrainingReport report)
    {
        if (report == null) return new EchoReportRow[0];

        int sampleTotal = 0;
        if (report.actionSamples != null)
            for (int i = 0; i < report.actionSamples.Length; i++)
                sampleTotal += report.actionSamples[i];

        string generationTransition = report.generationAfter
                                          > report.generationBefore
            ? "第 " + report.generationBefore + " 代 → 第 "
              + report.generationAfter + " 代"
            : "第 " + report.generationAfter + " 代 · 未晋升";
        string weightDrift = "影子 ±"
                             + Mathf.RoundToInt(report.shadowWeightDelta
                                                * 100f) + "%"
                             + " · 导演 ±"
                             + Mathf.RoundToInt(report.directorWeightDelta
                                                * 100f) + "%";
        string skillDrift = Mathf.Abs(report.skillAfter
                                      - report.skillBefore) < 0.005f
            ? "评估稳定"
            : (report.skillAfter > report.skillBefore ? "+" : "")
              + Mathf.RoundToInt((report.skillAfter - report.skillBefore)
                                 * 100f) + "%";

        return new[]
        {
            new EchoReportRow("generation", "代际", generationTransition),
            new EchoReportRow("echo", "本代学习",
                string.IsNullOrEmpty(report.learnedAction)
                    ? "待观察" : report.learnedAction),
            new EchoReportRow("rewrite", "模型更新", weightDrift),
            new EchoReportRow("pace", "技术评估", skillDrift),
            new EchoReportRow("stability", "本局样本",
                sampleTotal > 0 ? sampleTotal + " 个动作样本" : "样本不足"),
        };
    }
}
