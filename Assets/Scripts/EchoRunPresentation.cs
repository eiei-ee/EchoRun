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
    FinaleContract,
    FinaleFailed
}

public enum EchoHudMeterKind
{
    None,
    Calibration,
    Phase,
    EchoLock,
    Stability = EchoLock
}

public enum SingleContractVisualState
{
    Calibration,
    Challenge,
    RelearnPulse,
    Finale
}

public enum SingleContractLeadState
{
    Tied,
    PlayerLeading,
    EchoLeading
}

public enum SingleContractInstantFeedback
{
    None,
    PredictionHit,
    RewriteSucceeded,
    SafePass,
    CounterFailed,
    EchoRelearned
}

[System.Serializable]
public struct SingleContractHudInput
{
    public SingleContractVisualState visualState;
    public bool openingMemory;
    public bool openingReplay;
    public ShadowAction openingReplayAction;
    public int openingReplayCount;
    public int generation;
    public string memory;
    public bool showPrediction;
    public int predictedLane;
    public int predictionGateNumber;
    public int predictionGateCount;
    public bool predictionGateActive;
    public float leadMeters;
    public int injuries;
    public float finishRemaining;
    public string powerUp;
    public SingleContractInstantFeedback instantFeedback;
    public float feedbackLeadDeltaMeters;
    public int feedbackSequence;
    public SingleContractCalibrationProgress calibrationProgress;
    public string result;
}

[System.Serializable]
public struct SingleContractHudData
{
    public SingleContractVisualState visualState;
    public bool openingMemory;
    public bool openingReplay;
    public string openingTitle;
    public int generation;
    public bool showMemory;
    public string memory;
    public string prediction;
    public int predictionGateNumber;
    public int predictionGateCount;
    public bool predictionGateActive;
    public float leadMeters;
    public SingleContractLeadState leadState;
    public string lead;
    public int injuries;
    public string injuriesText;
    public float finishRemaining;
    public string finishRemainingText;
    public bool showPowerUp;
    public string powerUp;
    public SingleContractInstantFeedback instantFeedbackKind;
    public float feedbackLeadDeltaMeters;
    public string instantFeedback;
    public int feedbackSequence;
    public bool showCalibrationProgress;
    public float calibrationProgress01;
    public string calibrationMeterText;
    public string calibrationActionProgress;
    public string calibrationRouteProgress;
    public string result;
}

public struct EchoHudViewData
{
    public EchoHudMode mode;
    public EchoHudMeterKind meterKind;

    public int phaseIndex;
    public float calibrationProgress01;
    public float phaseProgress01;
    public float contractStability01;
    public float echoLock01;
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
    public bool phaseTransitionPending;
    public EchoDuelPhase pendingPhase;
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
    public const float SingleContractFeedbackDurationSeconds = 3.2f;
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

    public static EchoMenuViewData BuildSingleContractMenu(
        ActiveEchoIdentity identity, int minimumJumpSamples = 2,
        int minimumSlideSamples = 2, int minimumTotalSamples = 24,
        int minimumActiveSamples = 6,
        int minimumActionCategories = 2)
    {
        if (identity == null)
        {
            return new EchoMenuViewData
            {
                generation = "你的操作，会变成下一局的对手",
                learned = "本机 AI 会实时观察你的选路、跳跃和滑铲",
                rule = "多做不同动作和选路，让学习条变亮",
                objective = "学习条亮后跑到终点，形成下一局的回声",
                primaryAction = "开始第一局"
            };
        }

        int generation = Mathf.Max(1, identity.generation);
        string generationText = "第" + generation + "代回声";
        EchoMemoryContract memory = identity.memoryContract != null
            ? identity.memoryContract.Clone()
            : null;
        if (memory == null)
        {
            return new EchoMenuViewData
            {
                generation = generationText + "还在",
                learned = "AI 还没看清你的路线习惯",
                rule = "继续做不同动作和选路，让学习条变亮",
                objective = "学习条亮后跑到终点；旧回声不会丢",
                primaryAction = "让它再观察一局"
            };
        }

        memory.Normalize();
        if (!memory.HasPreciseRouteMemory)
        {
            return new EchoMenuViewData
            {
                generation = generationText + "还在",
                learned = "AI 还没看清你的路线习惯",
                rule = "继续做不同动作和选路，让学习条变亮",
                objective = "学习条亮后跑到终点；旧回声不会丢",
                primaryAction = "让它再观察一局"
            };
        }

        return new EchoMenuViewData
        {
            generation = generationText,
            learned = "它记住了：" + memory.BuildMemoryText(),
            rule = "它猜中会抢先；连续两次骗过它，它会改猜",
            objective = "先到终点，并把回声留在身后",
            primaryAction = "挑战第" + generation + "代回声"
        };
    }

    public static string BuildSingleContractCognitionSummary(
        EchoCognitionAssessment assessment)
    {
        if (!assessment.IsAvailable) return "";

        string previousLane = SingleContractCognitionLaneName(
            assessment.PreviousLane);
        string nextLane = SingleContractCognitionLaneName(
            assessment.NextLane);
        string runEvidence = "这局你骗过它 "
                             + assessment.SuccessfulCounterCount + "/"
                             + assessment.TotalGateCount + " 次 · "
                             + (assessment.RelearnStartGateNumber > 0
                                 ? "从第" + assessment.RelearnStartGateNumber
                                   + "次选路起，它改猜了"
                                 : "它没有改猜");

        string nextCognition;
        switch (assessment.ChangeKind)
        {
            case EchoCognitionChangeKind.Consolidated:
                nextCognition = "下一代更确定：压力时你偏向" + nextLane;
                break;
            case EchoCognitionChangeKind.Shaken:
                nextCognition = assessment.NextMemoryPrecise
                    ? "下一代不再确定：你仍可能偏向" + nextLane
                    : "下一代还没看清你的路线";
                break;
            case EchoCognitionChangeKind.Shifted:
                nextCognition = "下一代开始改猜：压力时你偏向" + nextLane;
                break;
            case EchoCognitionChangeKind.Reversed:
                nextCognition = "下一代已经改猜：压力时你偏向" + nextLane;
                break;
            default:
                nextCognition = "下一代没有改猜：仍偏向" + nextLane;
                break;
        }

        return "它原本认为：压力时你偏向" + previousLane + "\n"
               + runEvidence + "\n" + nextCognition;
    }

    public static SingleContractHudData BuildSingleContractHud(
        SingleContractHudInput input)
    {
        bool showCalibrationProgress = input.visualState
                                       == SingleContractVisualState.Calibration
                                       && input.calibrationProgress.HasTargets;
        float calibrationProgress01 = showCalibrationProgress
            ? input.calibrationProgress.Progress01 : 0f;
        bool openingMemory = input.openingMemory
                             && input.generation > 0
                             && input.visualState
                             != SingleContractVisualState.Calibration;
        bool openingReplay = openingMemory && input.openingReplay
                             && input.openingReplayCount
                             >= EchoSignatureActionParser
                                 .MinimumSignatureActionCount
                             && IsOpeningReplayAction(
                                 input.openingReplayAction);
        int generation = Mathf.Max(0, input.generation);
        string memory = openingMemory
            ? openingReplay
                ? BuildOpeningReplayDetail(input.openingReplayAction,
                    input.openingReplayCount, input.memory)
                : BuildOpeningMemoryDetail(input.memory)
            : showCalibrationProgress
                ? BuildCalibrationLearningLine(input.calibrationProgress,
                    compact: true)
                : NormalizeSingleContractMemory(input.memory);
        bool showPrediction = !openingMemory && input.showPrediction
                              && input.visualState
                              != SingleContractVisualState.Calibration
                              && input.predictedLane >= 0
                               && input.predictedLane <= 2;
        int predictionGateCount = Mathf.Max(0, input.predictionGateCount);
        int predictionGateNumber = predictionGateCount > 0
            ? Mathf.Clamp(input.predictionGateNumber, 1,
                predictionGateCount)
            : Mathf.Max(0, input.predictionGateNumber);
        float leadMeters = input.leadMeters;
        SingleContractLeadState leadState;
        string lead;
        if (leadMeters > 0.05f)
        {
            leadState = SingleContractLeadState.PlayerLeading;
            lead = "玩家领先：" + leadMeters.ToString("0.0") + "米";
        }
        else if (leadMeters < -0.05f)
        {
            leadState = SingleContractLeadState.EchoLeading;
            lead = "回声领先：" + Mathf.Abs(leadMeters).ToString("0.0")
                   + "米";
        }
        else
        {
            leadState = SingleContractLeadState.Tied;
            lead = "并驾齐驱：0.0米";
        }

        int injuries = Mathf.Max(0, input.injuries);
        float finishRemaining = Mathf.Max(0f, input.finishRemaining);
        string powerUp = (input.powerUp ?? "").Trim();
        string result = (input.result ?? "").Trim();
        return new SingleContractHudData
        {
            visualState = input.visualState,
            openingMemory = openingMemory,
            openingReplay = openingReplay,
            openingTitle = openingMemory
                ? "第" + Mathf.Max(1, generation) + "代回声现身"
                : "",
            generation = generation,
            showMemory = openingMemory
                         || input.visualState
                         == SingleContractVisualState.Calibration,
            memory = memory,
            prediction = showPrediction
                ? BuildSingleContractPrediction(
                    input.predictedLane, predictionGateNumber,
                    predictionGateCount, input.predictionGateActive)
                : "",
            predictionGateNumber = predictionGateNumber,
            predictionGateCount = predictionGateCount,
            predictionGateActive = input.predictionGateActive,
            leadMeters = leadMeters,
            leadState = leadState,
            lead = lead,
            injuries = injuries,
            injuriesText = "受伤次数：" + injuries,
            finishRemaining = finishRemaining,
            finishRemainingText = finishRemaining > 0.01f
                ? "终点距离：" + Mathf.CeilToInt(finishRemaining) + "米"
                : "终点已到达",
            showPowerUp = !openingMemory && !string.IsNullOrEmpty(powerUp),
            powerUp = string.IsNullOrEmpty(powerUp)
                ? "当前补给：无"
                : NormalizeSingleContractLabel(powerUp, "当前补给：", "无"),
            instantFeedbackKind = openingMemory
                ? SingleContractInstantFeedback.None
                : input.instantFeedback,
            feedbackLeadDeltaMeters = input.feedbackLeadDeltaMeters,
            instantFeedback = SingleContractFeedbackFor(
                openingMemory
                    ? SingleContractInstantFeedback.None
                    : input.instantFeedback,
                input.feedbackLeadDeltaMeters),
            feedbackSequence = Mathf.Max(0, input.feedbackSequence),
            showCalibrationProgress = showCalibrationProgress,
            calibrationProgress01 = calibrationProgress01,
            calibrationMeterText = showCalibrationProgress
                ? input.calibrationProgress.PlayerEvidenceReady
                    ? "学够了 · 去终点"
                    : "学习 " + Mathf.RoundToInt(
                        calibrationProgress01 * 100f) + "%"
                : "",
            calibrationActionProgress = showCalibrationProgress
                ? BuildCalibrationActionLine(
                    input.calibrationProgress, injuries, compact: true)
                : "",
            calibrationRouteProgress = showCalibrationProgress
                ? BuildCalibrationRouteLine(
                    input.calibrationProgress, compact: true)
                : "",
            result = result
        };
    }

    public static string BuildSingleContractCalibrationResult(
        SingleContractCalibrationProgress progress)
    {
        if (!progress.HasTargets)
        {
            return "AI 看到了你的跑法\n"
                   + "这局观察不会带到下一局；再跑一局，它会重新观察";
        }

        bool generationProblem = progress.finishReached
                                 && progress.evidenceReady
                                 && !progress.promotionReady;
        string title = generationProblem
            ? "回声形成遇到问题" : "AI 看到了你的跑法";
        return title + "\n"
               + BuildSingleContractCalibrationEvidence(progress)
               + " · " + (progress.finishReached
                   ? "已经到终点" : "还没到终点")
               + (generationProblem
                   ? "\n学习条已经全亮，但回声没有形成；请再跑一局"
                   : "\n这局观察不会带到下一局；再跑一局，它会重新观察");
    }

    public static string BuildSingleContractCalibrationEvidence(
        SingleContractCalibrationProgress progress)
    {
        if (!progress.HasTargets) return "";
        return BuildCalibrationLearningLine(progress, compact: false)
               + " · " + BuildCalibrationActionLine(
                   progress, 0, compact: false)
               + "\n" + BuildCalibrationRouteLine(
                   progress, compact: true);
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
        string buffText = "", bool phaseTransitionPending = false,
        EchoDuelPhase pendingPhase = EchoDuelPhase.None,
        string rewriteStyleSummary = "",
        string finaleSegmentSummary = "",
        EchoChallengeStep challengeStep = default)
    {
        bool calibrating = !hasOpponent || contract == null
                           || contract.type == EchoContractType.None;
        float calibration = Mathf.Clamp01(calibrationProgress01);
        float phaseProgress = Mathf.Clamp01(phaseProgress01);
        float echoLock = contract != null ? contract.EchoLock01 : 0f;
        if (calibrating)
        {
            return new EchoHudViewData
            {
                mode = EchoHudMode.Calibration,
                meterKind = EchoHudMeterKind.Calibration,
                phaseIndex = -1,
                calibrationProgress01 = calibration,
                phaseProgress01 = phaseProgress,
                contractStability01 = echoLock,
                echoLock01 = echoLock,
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
        bool finaleFailed = phase == EchoDuelPhase.Finale
                            && (contract.duelFailed || !contract.completed);
        bool finaleNeedsContract = false;
        EchoHudMode mode = ResolveHudMode(phase, finaleNeedsContract,
            finaleFailed);
        EchoHudMeterKind meterKind = ResolveMeterKind(mode);
        float displayed = meterKind == EchoHudMeterKind.Phase
            ? phaseProgress
            : meterKind == EchoHudMeterKind.EchoLock ? echoLock : 0f;
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
            contractStability01 = echoLock,
            echoLock01 = echoLock,
            displayedMeter01 = displayed,
            announcement = AnnouncementFor(mode),
            directiveShort = phaseTransitionPending
                && pendingPhase != EchoDuelPhase.None
                ? "前方同步：" + EchoDuelFlow.PhaseName(pendingPhase)
                : DirectiveFor(mode, contract, rewriteStyleSummary,
                    finaleSegmentSummary, challengeStep),
            predictionShort = showPrediction
                ? BuildShortPrediction(contract, challengeStep) : "",
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
            phaseTransitionPending = phaseTransitionPending,
            pendingPhase = pendingPhase,
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
        return BuildShortPrediction(contract, default);
    }

    public static string BuildShortPrediction(EchoContractData contract,
        EchoChallengeStep challengeStep)
    {
        if (contract == null) return "";
        bool currentHypothesis = challengeStep.stepId > 0
                                 && challengeStep.prediction.hypothesisVersion
                                 == contract.hypothesisVersion;
        if (contract.type == EchoContractType.BreakLaneHabit)
        {
            int lane = currentHypothesis
                       && challengeStep.predictedLane >= 0
                ? challengeStep.predictedLane
                : contract.predictionLane >= 0
                ? contract.predictionLane : contract.learnedLane;
            return "预判" + EchoContractPolicy.LaneName(lane);
        }
        ShadowAction action = currentHypothesis
                              && challengeStep.predictedAction
                              != ShadowAction.Keep
            ? challengeStep.predictedAction
            : contract.predictionAction != ShadowAction.Keep
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
            contract = ContractStatusFor(contract, phase),
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
        bool finaleNeedsContract, bool finaleFailed)
    {
        switch (phase)
        {
            case EchoDuelPhase.Detection: return EchoHudMode.Detection;
            case EchoDuelPhase.Reveal: return EchoHudMode.Reveal;
            case EchoDuelPhase.Counterattack: return EchoHudMode.Counterattack;
            case EchoDuelPhase.Rewrite: return EchoHudMode.Rewrite;
            case EchoDuelPhase.Finale:
                if (finaleFailed) return EchoHudMode.FinaleFailed;
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
                return EchoHudMeterKind.EchoLock;
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
            case EchoHudMode.FinaleFailed: return "契约锁定 · 回声决胜";
            default: return "回声校准";
        }
    }

    private static string DirectiveFor(EchoHudMode mode,
        EchoContractData contract, string rewriteStyleSummary,
        string finaleSegmentSummary, EchoChallengeStep challengeStep)
    {
        switch (mode)
        {
            case EchoHudMode.Detection: return "复现";
            case EchoHudMode.Reveal: return "下注";
            case EchoHudMode.Resistance: return "裂解";
            case EchoHudMode.Counterattack:
                return BuildCounterattackDirective(contract, challengeStep);
            case EchoHudMode.Rewrite:
                return "新影成形";
            case EchoHudMode.FinaleClean:
                return "冲刺";
            case EchoHudMode.FinaleContract:
                return string.IsNullOrEmpty(finaleSegmentSummary)
                    ? "最后机会"
                    : finaleSegmentSummary + " · 最后机会";
            case EchoHudMode.FinaleFailed: return "契约锁定 · 完成追逐";
            default: return "";
        }
    }

    public static string BuildCounterattackDirective(
        EchoContractData contract, EchoChallengeStep challengeStep)
    {
        if (contract == null) return "等待交锋";
        if (contract.completed) return "锁定碎裂";

        EchoEncounterResult result = contract.lastEncounterResult;
        if (!result.IsResolved
            || result.phase != EchoDuelPhase.Counterattack)
            return "等待交锋";

        int fracturePercent = Mathf.RoundToInt(contract.Progress01 * 100f);
        bool relockedAfterResult = contract.hypothesisVersion
                                   > result.hypothesisVersion;
        switch (result.outcome)
        {
            case EchoEncounterOutcome.PredictionBroken:
            case EchoEncounterOutcome.SafeChoice:
                return relockedAfterResult
                    ? "本次成功 · 回声改判"
                    : "本次成功 · 裂解 " + fracturePercent + "%";
            case EchoEncounterOutcome.PredictionHit:
                return "预判命中 · 裂解 " + fracturePercent + "%";
            case EchoEncounterOutcome.Collision:
                return "碰撞失手 · 裂解 " + fracturePercent + "%";
            case EchoEncounterOutcome.Cancelled:
                return "未交锋 · 裂解 " + fracturePercent + "%";
            default:
                return "等待交锋";
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
        if (contract.duelFailed) return "锁定未破";
        if (phase == EchoDuelPhase.Detection)
            return "复现 " + Mathf.RoundToInt(phaseProgress01 * 100f) + "%";
        if (phase == EchoDuelPhase.Reveal)
            return "习惯暴露";
        if (phase == EchoDuelPhase.Rewrite)
            return "重写 " + Mathf.RoundToInt(phaseProgress01 * 100f) + "%";
        if (contract.completed) return "锁定碎裂";
        if (phase == EchoDuelPhase.Counterattack)
            return "裂解 " + Mathf.RoundToInt(contract.Progress01 * 100f)
                   + "%";
        float echoLock = contract.EchoLock01;
        if (echoLock >= 0.98f) return "完整锁定";
        if (echoLock >= 0.55f) return "锁定开裂";
        if (echoLock > 0.01f) return "深度裂解";
        return "锁定碎裂";
    }

    private static bool ShouldShowPrediction(EchoDuelPhase phase)
    {
        return phase == EchoDuelPhase.Reveal
               || phase == EchoDuelPhase.Resistance
               || phase == EchoDuelPhase.Counterattack;
    }

    private static string ContractStatusFor(EchoContractData contract,
        EchoDuelPhase phase)
    {
        if (contract.duelFailed) return "锁定未破 · 完成追逐";
        if (phase == EchoDuelPhase.Detection) return "AI正在复现旧习惯";
        if (phase == EchoDuelPhase.Rewrite) return "AI正在记录新策略";
        if (phase == EchoDuelPhase.Finale) return "只争距离";
        if (phase == EchoDuelPhase.Counterattack)
            return "AI已学习你的反制 · 等待你的选择";
        return "AI预测已公开 · 打破旧习惯";
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

    private static string BuildCalibrationLearningLine(
        SingleContractCalibrationProgress progress, bool compact)
    {
        if (compact)
        {
            return "AI 学习 " + Count(progress.totalSamples) + "/"
                   + Count(progress.minimumTotalSamples) + " · 主动 "
                   + Count(progress.activeSamples) + "/"
                   + Count(progress.minimumActiveSamples) + " · 种类 "
                   + Count(progress.actionCategories) + "/"
                   + Count(progress.minimumActionCategories);
        }
        return "观察 " + Count(progress.totalSamples) + "/"
               + Count(progress.minimumTotalSamples) + " · 主动 "
               + Count(progress.activeSamples) + "/"
               + Count(progress.minimumActiveSamples) + " · 动作种类 "
               + Count(progress.actionCategories) + "/"
               + Count(progress.minimumActionCategories);
    }

    private static string BuildCalibrationActionLine(
        SingleContractCalibrationProgress progress, int injuries,
        bool compact)
    {
        if (compact)
        {
            return "跳 " + Count(progress.jumpSamples) + "/"
                   + Count(progress.minimumJumpSamples) + " · 滑 "
                   + Count(progress.slideSamples) + "/"
                   + Count(progress.minimumSlideSamples) + " · 受伤 "
                   + Count(injuries);
        }
        return "跳跃 " + Count(progress.jumpSamples) + "/"
               + Count(progress.minimumJumpSamples) + " · 滑铲 "
               + Count(progress.slideSamples) + "/"
               + Count(progress.minimumSlideSamples);
    }

    private static string BuildCalibrationRouteLine(
        SingleContractCalibrationProgress progress, bool compact)
    {
        if (compact)
        {
            string route = progress.preferredLaneUnique
                           && progress.preferredLane >= 0
                ? CompactSingleContractLaneName(progress.preferredLane)
                : "同路";
            string routeEvidence;
            if (progress.strongestRouteChoices
                < progress.minimumStrongestRouteChoices)
            {
                routeEvidence = route
                                + Count(progress.strongestRouteChoices)
                                + "/"
                                + Count(progress
                                    .minimumStrongestRouteChoices);
            }
            else if (progress.preferredLaneConfidence
                     < EchoMemoryContract.PreciseDescriptionConfidence)
            {
                routeEvidence = "同路"
                                + Mathf.RoundToInt(Mathf.Clamp01(
                                    progress.preferredLaneConfidence) * 100f)
                                + "%/"
                                + Mathf.RoundToInt(EchoMemoryContract
                                    .PreciseDescriptionConfidence * 100f)
                                + "%";
            }
            else
            {
                routeEvidence = route
                                + Count(progress.strongestRouteChoices)
                                + "次";
            }
            return "选路 " + Count(progress.formalChoices) + "/"
                   + Count(progress.minimumFormalChoices) + " · 通过"
                   + " " + Count(progress.successfulChoices) + "/"
                   + Count(progress.minimumSuccessfulChoices) + " · "
                   + routeEvidence;
        }

        string tendency = progress.preferredLaneUnique
                          && progress.preferredLane >= 0
            ? SingleContractCognitionLaneName(progress.preferredLane)
              + "倾向"
            : progress.strongestRouteChoices > 0
                ? "倾向形成中" : "路线倾向";
        return "路线选择 " + Count(progress.formalChoices) + "/"
               + Count(progress.minimumFormalChoices) + " · "
               + "成功通过 " + Count(progress.successfulChoices) + "/"
               + Count(progress.minimumSuccessfulChoices) + " · "
               + tendency + " " + Count(progress.strongestRouteChoices)
               + "/" + Count(progress.minimumStrongestRouteChoices);
    }

    private static string CompactSingleContractLaneName(int lane)
    {
        switch (Mathf.Clamp(lane, 0, 2))
        {
            case 0: return "左路";
            case 2: return "右路";
            default: return "中路";
        }
    }

    private static int Count(int value)
    {
        return Mathf.Max(0, value);
    }

    private static string BuildSingleContractPrediction(int lane,
        int gateNumber, int gateCount, bool gateActive)
    {
        string progress = gateNumber > 0 && gateCount > 0
            ? "第" + gateNumber + "/" + gateCount + "次选路 · " : "";
        string timing = gateActive ? "这次它猜" : "下一次它猜";
        return progress + timing + CompactSingleContractLaneName(lane)
               + "\n红=它猜  青=骗它  白=安全";
    }

    private static string SingleContractFeedbackFor(
        SingleContractInstantFeedback feedback, float leadDeltaMeters)
    {
        float meters = Mathf.Abs(leadDeltaMeters);
        switch (feedback)
        {
            case SingleContractInstantFeedback.PredictionHit:
                return "它猜中了 · 回声 +" + meters.ToString("0.0") + "米";
            case SingleContractInstantFeedback.RewriteSucceeded:
                return "你骗过它 · 玩家 +" + meters.ToString("0.0") + "米";
            case SingleContractInstantFeedback.SafePass:
                return "安全通过 · 距离不变";
            case SingleContractInstantFeedback.CounterFailed:
                return "没骗过它 · 回声 +" + meters.ToString("0.0") + "米";
            case SingleContractInstantFeedback.EchoRelearned:
                return "回声改猜了 · 后续已更新";
            default:
                return "";
        }
    }

    private static string SingleContractCognitionLaneName(int lane)
    {
        switch (lane)
        {
            case 0: return "左侧";
            case 2: return "右侧";
            default: return "中间";
        }
    }

    private static string NormalizeSingleContractLabel(
        string value, string label, string fallback)
    {
        string normalized = (value ?? "").Trim();
        if (normalized.StartsWith(label)) return normalized;
        return label + (string.IsNullOrEmpty(normalized)
            ? fallback
            : normalized);
    }

    private static string NormalizeSingleContractMemory(string value)
    {
        string normalized = (value ?? "").Trim();
        if (string.IsNullOrEmpty(normalized)
            || normalized.Contains("尚未形成稳定模式"))
            return "AI 正在观察你的跑法";
        if (normalized.StartsWith("回声记忆模糊"))
            return "AI 正在观察你的跑法";
        if (normalized.StartsWith("旧回声已保留"))
            return "旧回声还在 · AI 正在观察新跑法";
        normalized = TrimPrefix(normalized.Replace('\n', ' '), "回声记忆：");
        normalized = TrimPrefix(normalized, "它记住了：");
        return "它记住了：" + (string.IsNullOrEmpty(normalized)
            ? "AI 正在观察你的跑法" : normalized);
    }

    private static string BuildOpeningMemoryDetail(string value)
    {
        string memory = (value ?? "").Trim().Replace('\n', ' ');
        memory = TrimPrefix(memory, "回声记忆：");
        memory = TrimPrefix(memory, "它记住了：");
        if (string.IsNullOrEmpty(memory))
            return "它还没看清你的路线";
        return "它记住了：" + memory;
    }

    private static bool IsOpeningReplayAction(ShadowAction action)
    {
        return action == ShadowAction.Jump || action == ShadowAction.Slide
               || action == ShadowAction.Left || action == ShadowAction.Right;
    }

    private static string BuildOpeningReplayDetail(ShadowAction action,
        int count, string memory)
    {
        string actionText;
        switch (action)
        {
            case ShadowAction.Jump:
                actionText = "跳跃";
                break;
            case ShadowAction.Slide:
                actionText = "滑铲";
                break;
            case ShadowAction.Left:
                actionText = "左移";
                break;
            case ShadowAction.Right:
                actionText = "右移";
                break;
            default:
                actionText = "动作";
                break;
        }

        string routeHint = BuildOpeningRouteHint(memory);
        return "上一局学到：" + actionText + "×" + Mathf.Max(1, count)
               + routeHint;
    }

    private static string BuildOpeningRouteHint(string memory)
    {
        string normalized = (memory ?? "").Replace('\n', ' ');
        if (normalized.Contains("左侧")) return " · 压力时偏左";
        if (normalized.Contains("右侧")) return " · 压力时偏右";
        if (normalized.Contains("中间")) return " · 压力时走中";
        return "";
    }

    private static string TrimPrefix(string value, string prefix)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.StartsWith(prefix) ? value.Substring(prefix.Length) : value;
    }
}
