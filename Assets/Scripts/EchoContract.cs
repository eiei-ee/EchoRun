using System;
using UnityEngine;

public enum EchoContractType
{
    None,
    BreakLaneHabit,
    ChangeVerticalHabit,
    DisruptRhythm
}

[Serializable]
public sealed class EchoContractData
{
    public const int CurrentVersion = 4;

    public int version = CurrentVersion;
    public EchoContractType type;
    public int generation;
    public int learnedLane = -1;
    public int targetLane = -1;
    public ShadowAction learnedAction = ShadowAction.Keep;
    public ShadowAction startingAction = ShadowAction.Keep;
    public ShadowAction targetAction = ShadowAction.Keep;
    public float targetProgress = 1f;
    public float progress;
    public float playerProgressBonus;
    public float shadowProgressBonus;
    public EchoDuelPhase duelPhase;
    public bool initialBreakCompleted;
    public bool counterattackActive;
    public int predictionLane = -1;
    public ShadowAction predictionAction = ShadowAction.Keep;
    public bool completed;
    public bool won;
    public bool duelFailed;
    public EchoDuelPhase failurePhase;
    public bool exploratory;
    public int feedbackSequence;
    public string title = "";
    public string learnedTrait = "";
    public string ruleDescription = "";
    public string objective = "";
    public string lastFeedback = "";

    public float Progress01 => Mathf.Clamp01(
        progress / Mathf.Max(0.01f, targetProgress));

    public EchoContractData Clone()
    {
        return JsonUtility.FromJson<EchoContractData>(JsonUtility.ToJson(this));
    }

    public void Normalize()
    {
        int sourceVersion = version;
        version = CurrentVersion;
        if (!Enum.IsDefined(typeof(EchoContractType), type))
            type = EchoContractType.None;
        generation = Mathf.Max(0, generation);
        learnedLane = Mathf.Clamp(learnedLane, -1, 2);
        targetLane = Mathf.Clamp(targetLane, -1, 2);
        if (!Enum.IsDefined(typeof(ShadowAction), learnedAction))
            learnedAction = ShadowAction.Keep;
        if (!Enum.IsDefined(typeof(ShadowAction), targetAction))
            targetAction = ShadowAction.Keep;
        if (!Enum.IsDefined(typeof(ShadowAction), startingAction))
            startingAction = ShadowAction.Keep;
        if ((type == EchoContractType.ChangeVerticalHabit
             || type == EchoContractType.DisruptRhythm)
            && learnedAction != ShadowAction.Jump
            && learnedAction != ShadowAction.Slide
            && (targetAction == ShadowAction.Jump
                || targetAction == ShadowAction.Slide))
            learnedAction = targetAction == ShadowAction.Jump
                ? ShadowAction.Slide : ShadowAction.Jump;
        if (startingAction == ShadowAction.Keep
            && (type == EchoContractType.ChangeVerticalHabit
                || type == EchoContractType.DisruptRhythm))
            startingAction = targetAction != ShadowAction.Keep
                ? targetAction
                : generation % 2 == 0 ? ShadowAction.Jump : ShadowAction.Slide;
        if (type == EchoContractType.DisruptRhythm
            && targetAction == ShadowAction.Keep)
            targetAction = startingAction;
        if ((type == EchoContractType.ChangeVerticalHabit
             || type == EchoContractType.DisruptRhythm)
            && learnedAction != ShadowAction.Jump
            && learnedAction != ShadowAction.Slide)
            learnedAction = targetAction == ShadowAction.Jump
                ? ShadowAction.Slide : ShadowAction.Jump;
        if (sourceVersion < 3 && targetProgress <= 10f)
        {
            float legacyTarget = Mathf.Max(0.01f, targetProgress);
            progress = Mathf.Clamp01(progress / legacyTarget) * 100f;
            targetProgress = 100f;
            if (completed && !won)
            {
                initialBreakCompleted = true;
                counterattackActive = true;
                progress = 55f;
                completed = false;
            }
        }
        targetProgress = Mathf.Max(1f, targetProgress);
        progress = Mathf.Clamp(progress, 0f, targetProgress);
        playerProgressBonus = Mathf.Max(0f, playerProgressBonus);
        shadowProgressBonus = Mathf.Max(0f, shadowProgressBonus);
        completed = progress >= targetProgress || completed;
        if (!Enum.IsDefined(typeof(EchoDuelPhase), duelPhase))
            duelPhase = EchoDuelPhase.None;
        if (!Enum.IsDefined(typeof(EchoDuelPhase), failurePhase))
            failurePhase = EchoDuelPhase.None;
        if (!duelFailed) failurePhase = EchoDuelPhase.None;
        if (duelPhase == EchoDuelPhase.None && type != EchoContractType.None)
            duelPhase = EchoDuelPhase.Detection;
        predictionLane = Mathf.Clamp(predictionLane, -1, 2);
        if (!Enum.IsDefined(typeof(ShadowAction), predictionAction))
            predictionAction = ShadowAction.Keep;
        if (predictionLane < 0 && type == EchoContractType.BreakLaneHabit)
            predictionLane = learnedLane;
        if (predictionAction == ShadowAction.Keep
            && type != EchoContractType.BreakLaneHabit)
            predictionAction = learnedAction;
        feedbackSequence = Mathf.Max(0, feedbackSequence);
        title = title ?? "";
        learnedTrait = learnedTrait ?? "";
        ruleDescription = ruleDescription ?? "";
        objective = objective ?? "";
        lastFeedback = lastFeedback ?? "";
    }

    public EchoContractData ResetForRun()
    {
        EchoContractData reset = Clone();
        reset.Normalize();
        reset.progress = 0f;
        reset.playerProgressBonus = 0f;
        reset.shadowProgressBonus = 0f;
        reset.duelPhase = EchoDuelPhase.Detection;
        reset.initialBreakCompleted = false;
        reset.counterattackActive = false;
        reset.predictionLane = reset.learnedLane;
        reset.predictionAction = reset.learnedAction;
        reset.completed = false;
        reset.won = false;
        reset.duelFailed = false;
        reset.failurePhase = EchoDuelPhase.None;
        reset.feedbackSequence = 0;
        reset.lastFeedback = "";
        if (reset.startingAction != ShadowAction.Keep)
            reset.targetAction = reset.startingAction;
        return reset;
    }
}

public static class EchoContractPolicy
{
    public static EchoContractData CreateForRun(PlayerStyleData source,
        int generation, string retryJson)
    {
        if (!string.IsNullOrEmpty(retryJson))
        {
            try
            {
                EchoContractData retry = JsonUtility.FromJson<EchoContractData>(
                    retryJson);
                if (retry != null)
                {
                    retry.Normalize();
                    if (retry.type != EchoContractType.None && !retry.won
                        && retry.generation == Mathf.Max(1, generation))
                        return retry.ResetForRun();
                }
            }
            catch (Exception)
            {
                // A damaged retry snapshot must not block a playable run.
            }
        }
        return Create(source, generation);
    }

    public static EchoContractData Create(PlayerStyleData source, int generation)
    {
        PlayerStyleData style = source != null
            ? source.Clone()
            : new PlayerStyleData();
        style.Normalize();

        float laneEvidence = Mathf.Clamp01(style.laneSamples / 12f);
        float verticalEvidence = Mathf.Clamp01(style.verticalActionSamples / 5f);
        float rhythmEvidence = Mathf.Clamp01(style.rhythmSamples / 6f);
        float laneScore = Mathf.Abs(style.lanePreference) * laneEvidence;
        float verticalScore = Mathf.Abs(style.slideFrequency - 0.5f)
                              * 2f * verticalEvidence;
        float rhythmScore = Mathf.Clamp01((style.rhythmStability - 0.45f) / 0.55f)
                            * rhythmEvidence;

        // Rotate close calls so generations create different rules while still
        // prioritizing the strongest learned habit.
        float rotation = Mathf.Abs(generation) % 3 * 0.015f;
        laneScore += generation % 3 == 0 ? rotation : 0f;
        verticalScore += generation % 3 == 1 ? rotation : 0f;
        rhythmScore += generation % 3 == 2 ? rotation : 0f;

        if (laneScore >= verticalScore && laneScore >= rhythmScore
            && laneScore >= 0.12f)
            return CreateLaneContract(style, generation);
        if (verticalScore >= rhythmScore && verticalScore >= 0.12f)
            return CreateVerticalContract(style, generation);
        if (rhythmScore >= 0.12f)
            return CreateRhythmContract(style, generation);

        // Sparse early data still yields a playable contract. The choice is
        // deterministic and comes from the frozen style snapshot.
        EchoContractData exploration;
        if (generation % 3 == 1)
            exploration = CreateVerticalContract(style, generation);
        else if (generation % 3 == 2)
            exploration = CreateRhythmContract(style, generation);
        else
            exploration = CreateLaneContract(style, generation);
        exploration.exploratory = true;
        exploration.learnedTrait = "AI探测：有效习惯样本不足";
        exploration.ruleDescription = "本代用于验证你的反制选择；完成目标后才会形成明确画像。";
        return exploration;
    }

    private static EchoContractData CreateLaneContract(
        PlayerStyleData style, int generation)
    {
        int learnedLane = style.lanePreference < 0f ? 0 : 2;
        int targetLane = learnedLane == 0 ? 2 : 0;
        string learnedName = LaneName(learnedLane);
        string targetName = LaneName(targetLane);
        return new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            generation = Mathf.Max(1, generation),
            learnedLane = learnedLane,
            targetLane = targetLane,
            targetProgress = 100f,
            title = "回声契约：逆向改道",
            learnedTrait = "AI识别：你经常依赖" + learnedName,
            ruleDescription = "本代把高价值安全路线迁移到" + targetName
                              + "；停留在旧路线会让回声加速。",
            objective = "持续改变路线使契约稳定度达到 100%，应对反扑后再进入决胜。"
        };
    }

    private static EchoContractData CreateVerticalContract(
        PlayerStyleData style, int generation)
    {
        bool prefersSlide = style.slideFrequency >= 0.5f;
        ShadowAction learned = prefersSlide ? ShadowAction.Slide : ShadowAction.Jump;
        ShadowAction target = prefersSlide ? ShadowAction.Jump : ShadowAction.Slide;
        int challengeLane = Mathf.Abs(generation) % 3;
        string learnedName = ActionName(learned);
        string targetName = ActionName(target);
        return new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            generation = Mathf.Max(1, generation),
            learnedAction = learned,
            targetLane = challengeLane,
            startingAction = target,
            targetAction = target,
            targetProgress = 100f,
            title = "回声契约：动作反转",
            learnedTrait = "AI识别：你上一代更常执行" + learnedName,
            ruleDescription = "本代提高需要" + targetName
                              + "破解的组合；重复旧动作会给回声推进优势。",
            objective = "用有效的" + targetName
                        + "机会破坏旧预测，并在反扑组合中稳定新策略。"
        };
    }

    private static EchoContractData CreateRhythmContract(
        PlayerStyleData style, int generation)
    {
        ShadowAction learned = style.slideFrequency >= 0.5f
            ? ShadowAction.Slide : ShadowAction.Jump;
        ShadowAction first = learned == ShadowAction.Jump
            ? ShadowAction.Slide : ShadowAction.Jump;
        int challengeLane = Mathf.Abs(generation) % 3;
        string rhythm = style.rhythmStability >= 0.65f
            ? "你的跳跃/滑铲节奏高度固定"
            : "你的动作序列已被AI归纳";
        return new EchoContractData
        {
            type = EchoContractType.DisruptRhythm,
            generation = Mathf.Max(1, generation),
            targetLane = challengeLane,
            learnedAction = learned,
            predictionAction = learned,
            startingAction = first,
            targetAction = first,
            targetProgress = 100f,
            title = "回声契约：打乱节拍",
            learnedTrait = "AI识别：" + rhythm,
            ruleDescription = "本代交替生成高低障碍；连续复用同一种动作会让回声加速。",
            objective = "适应变化节拍，将契约稳定度推至 100%并通过反扑。"
        };
    }

    public static string LaneName(int lane)
    {
        if (lane <= 0) return "左侧路线";
        if (lane >= 2) return "右侧路线";
        return "中间路线";
    }

    public static string ActionName(ShadowAction action)
    {
        if (action == ShadowAction.Jump) return "跳跃";
        if (action == ShadowAction.Slide) return "滑铲";
        if (action == ShadowAction.Left) return "左移";
        if (action == ShadowAction.Right) return "右移";
        return "保持路线";
    }

    public static string BuildStyleSummary(PlayerStyleData source)
    {
        PlayerStyleData style = source != null
            ? source.Clone()
            : new PlayerStyleData();
        style.Normalize();
        string lane = Mathf.Abs(style.lanePreference) < 0.2f
            ? "路线较均衡"
            : style.lanePreference < 0f ? "偏爱左路" : "偏爱右路";
        string vertical = style.verticalActionSamples < 3
            ? "跳滑样本不足"
            : Mathf.Abs(style.slideFrequency - 0.5f) < 0.12f
                ? "跳滑均衡"
                : style.slideFrequency > 0.5f ? "常用滑铲" : "常用跳跃";
        string rhythm = style.rhythmStability >= 0.65f
            ? "节奏固定"
            : style.rhythmStability <= 0.35f ? "节奏多变" : "节奏中性";
        return lane + " · " + vertical + " · " + rhythm;
    }
}

public sealed class EchoContractEvaluator
{
    public EchoContractData Contract { get; }
    public bool ScoringSuspended { get; private set; }

    private float _learnedLaneFeedbackTimer;
    private float _lastLaneMarkerDistance = float.NegativeInfinity;
    private float _lastFinaleMarkerDistance = float.NegativeInfinity;
    private const float LaneMarkerSpacingSeconds = 4.5f;
    private const float CorrectLeadSeconds = 0.35f;
    private const float MistakeLeadSeconds = 0.2f;

    public EchoContractEvaluator(EchoContractData contract)
    {
        Contract = contract != null ? contract.Clone() : new EchoContractData();
        Contract.Normalize();
    }

    public void SetPhase(EchoDuelPhase phase)
    {
        if (Contract.type == EchoContractType.None || phase == EchoDuelPhase.None)
            return;
        if (Contract.duelPhase == phase) return;

        Contract.duelPhase = phase;
        switch (phase)
        {
            case EchoDuelPhase.Detection:
                SetFeedback("回声正在复现你的旧习惯");
                break;
            case EchoDuelPhase.Reveal:
                SetFeedback(BuildPredictionText(false));
                break;
            case EchoDuelPhase.Resistance:
                Contract.predictionLane = Contract.learnedLane;
                Contract.predictionAction = Contract.learnedAction;
                SetFeedback("开始反抗：让回声的预测失效");
                break;
            case EchoDuelPhase.Counterattack:
                BeginCounterattack();
                break;
            case EchoDuelPhase.Rewrite:
                Contract.completed = true;
                SetFeedback("契约已重写：本阶段行为将更强地塑造下一代回声");
                break;
            case EchoDuelPhase.Finale:
                SetFeedback(Contract.duelFailed
                    ? "契约锁定：反抗未完成，进入回声决胜追逐"
                    : Contract.completed
                        ? "进入决胜：守住领先并完成终局组合"
                        : "最终契约机会：在冲线前稳定新策略");
                break;
        }
    }

    public void SetScoringSuspended(bool suspended)
    {
        ScoringSuspended = suspended;
    }

    public void LockForFinale(EchoDuelPhase failurePhase)
    {
        if (Contract.type == EchoContractType.None) return;
        Contract.duelFailed = true;
        Contract.failurePhase = failurePhase;
        Contract.completed = false;
        Contract.counterattackActive = false;
        ScoringSuspended = false;
        SetFeedback(failurePhase == EchoDuelPhase.Counterattack
            ? "反扑失败：契约锁定，进入决胜追逐"
            : "反抗失败：契约锁定，进入决胜追逐");
    }

    public string BuildPredictionText(bool counterattack = true)
    {
        string prefix = counterattack && Contract.initialBreakCompleted
            ? "新预判：" : "回声预判：";
        if (Contract.type == EchoContractType.BreakLaneHabit)
        {
            int lane = Contract.predictionLane >= 0
                ? Contract.predictionLane : Contract.learnedLane;
            return prefix + "你会继续依赖" + EchoContractPolicy.LaneName(lane);
        }

        ShadowAction action = Contract.predictionAction != ShadowAction.Keep
            ? Contract.predictionAction : Contract.learnedAction;
        return prefix + "你会继续使用" + EchoContractPolicy.ActionName(action);
    }

    public void TickLane(int lane, float deltaTime, float currentSpeed = 10f)
    {
        if (Contract.type != EchoContractType.BreakLaneHabit
            || !CanChangeStability() || deltaTime <= 0f)
            return;

        int predictedLane = Contract.initialBreakCompleted
            ? Contract.predictionLane : Contract.learnedLane;
        if (lane == predictedLane)
        {
            ReduceStability(5f * deltaTime);
            Contract.shadowProgressBonus += EchoTimeRules.SecondsToDistance(
                0.1f * deltaTime, currentSpeed);
            _learnedLaneFeedbackTimer += deltaTime;
            if (_learnedLaneFeedbackTimer >= 1.5f)
            {
                _learnedLaneFeedbackTimer = 0f;
                SetFeedback(Contract.initialBreakCompleted
                    ? "回声施压：新路线正在变成另一个固定习惯"
                    : "回声施压：旧路线正在修复契约");
            }
        }
        else _learnedLaneFeedbackTimer = 0f;
    }

    public void RecordLaneMarker(int lane, float routeDistance,
        float currentSpeed = 10f)
    {
        if (Contract.type != EchoContractType.BreakLaneHabit
            || Contract.completed || Contract.duelFailed || ScoringSuspended)
            return;
        bool revealChoice = Contract.duelPhase == EchoDuelPhase.Reveal;
        if (!revealChoice && !CanChangeStability()) return;

        int predictedLane = Contract.predictionLane >= 0
            ? Contract.predictionLane : Contract.learnedLane;
        predictedLane = Mathf.Clamp(predictedLane, 0, 2);
        bool counterattack = Contract.initialBreakCompleted;
        float spacing = EchoTimeRules.MinimumSpacingDistance(
            LaneMarkerSpacingSeconds, currentSpeed);
        if (!float.IsNegativeInfinity(_lastLaneMarkerDistance)
            && routeDistance - _lastLaneMarkerDistance < spacing)
            return;

        _lastLaneMarkerDistance = routeDistance;
        if (revealChoice)
        {
            if (lane == predictedLane)
            {
                AddShadowLead(0.35f, currentSpeed);
                SetFeedback("公开预判命中：回声获得推进");
            }
            else
            {
                AddPlayerLead(0.2f, currentSpeed);
                SetFeedback("公开预判失效：你获得反超窗口");
            }
            return;
        }

        bool validLane = lane != predictedLane;
        if (!validLane)
        {
            ReduceStability(14f);
            AddShadowLead(MistakeLeadSeconds, currentSpeed);
            SetFeedback(counterattack
                ? "回声施压：你重复了刚被识别的反制路线"
                : "回声施压：你重复了被公开的预测路线");
            return;
        }

        AddStability(counterattack ? 24f : 34f,
            CorrectLeadSeconds, currentSpeed);
        if (counterattack)
        {
            Contract.predictionLane = lane;
            SetFeedback("预测失效：回声已把你的新路线列为下一次预判");
        }
        else SetFeedback("预测失效：你主动选择了非习惯路线");
        CompleteIfReady();
    }

    public void RecordFinaleRouteChoice(int lane, int predictedLane,
        int safeLane, int riskLane, float routeDistance,
        float currentSpeed = 10f)
    {
        if (Contract.type == EchoContractType.None
            || Contract.duelPhase != EchoDuelPhase.Finale
            || !Contract.completed || Contract.duelFailed
            || ScoringSuspended)
            return;

        int selected = Mathf.Clamp(lane, 0, 2);
        int predicted = Mathf.Clamp(predictedLane, 0, 2);
        int safe = Mathf.Clamp(safeLane, 0, 2);
        int risk = Mathf.Clamp(riskLane, 0, 2);
        if (selected != predicted && selected != safe && selected != risk)
            return;

        float spacing = EchoTimeRules.MinimumSpacingDistance(
            LaneMarkerSpacingSeconds, currentSpeed);
        if (!float.IsNegativeInfinity(_lastFinaleMarkerDistance)
            && routeDistance - _lastFinaleMarkerDistance < spacing)
            return;
        _lastFinaleMarkerDistance = routeDistance;

        if (selected == predicted)
        {
            AddShadowLead(0.3f, currentSpeed);
            SetFeedback("旧预测命中：高价值路线让回声获得推进");
        }
        else if (selected == risk)
        {
            AddPlayerLead(0.4f, currentSpeed);
            SetFeedback("激进决胜：你用高风险路线抢回距离");
        }
        else
        {
            SetFeedback("安全决胜：你放弃追赶收益并守住当前距离");
        }
    }

    public void RecordDodge(ObstacleType obstacleType, int playerLane = -1,
        float currentSpeed = 10f)
    {
        ShadowAction action = obstacleType == ObstacleType.High
            ? ShadowAction.Jump
            : obstacleType == ObstacleType.Low
                ? ShadowAction.Slide
                : ShadowAction.Keep;
        if (action == ShadowAction.Keep) return;

        if (Contract.duelPhase == EchoDuelPhase.Reveal
            && !Contract.completed && !Contract.duelFailed
            && !ScoringSuspended
            && (Contract.type == EchoContractType.ChangeVerticalHabit
                || Contract.type == EchoContractType.DisruptRhythm))
        {
            ShadowAction predicted = Contract.predictionAction;
            if (predicted != ShadowAction.Jump
                && predicted != ShadowAction.Slide)
                predicted = Contract.targetAction == ShadowAction.Jump
                    ? ShadowAction.Slide : ShadowAction.Jump;
            if (action == predicted)
            {
                AddShadowLead(0.35f, currentSpeed);
                SetFeedback("公开预判命中：回声复制了你的旧动作");
            }
            else
            {
                AddPlayerLead(0.2f, currentSpeed);
                SetFeedback("公开预判失效：你的动作选择骗过了回声");
            }
            return;
        }

        if (!CanChangeStability()) return;

        if (Contract.type == EchoContractType.ChangeVerticalHabit)
        {
            if (!Contract.initialBreakCompleted && Contract.targetLane >= 0
                && playerLane != Contract.targetLane)
                return;
            if (action == Contract.targetAction)
            {
                AddStability(Contract.initialBreakCompleted ? 24f : 34f,
                    CorrectLeadSeconds, currentSpeed);
                SetFeedback("预测失效：有效动作骗过了回声");
                if (Contract.initialBreakCompleted)
                {
                    Contract.predictionAction = action;
                    Contract.targetAction = OppositeVertical(action);
                }
                CompleteIfReady();
            }
            else if (action == Contract.predictionAction
                     || action == Contract.learnedAction)
            {
                ReduceStability(14f);
                AddShadowLead(MistakeLeadSeconds, currentSpeed);
                SetFeedback("回声施压：你重复了被公开的预测");
            }
            return;
        }

        if (Contract.type != EchoContractType.DisruptRhythm) return;
        if (!Contract.initialBreakCompleted && Contract.targetLane >= 0
            && playerLane != Contract.targetLane)
            return;
        if (action == Contract.targetAction)
        {
            AddStability(Contract.initialBreakCompleted ? 22f : 27f,
                CorrectLeadSeconds, currentSpeed);
            SetFeedback("预测失效：动作节拍已改变");
            Contract.targetAction = action == ShadowAction.Jump
                ? ShadowAction.Slide : ShadowAction.Jump;
            CompleteIfReady();
        }
        else
        {
            ReduceStability(14f);
            AddShadowLead(MistakeLeadSeconds, currentSpeed);
            SetFeedback("回声施压：固定节拍正在修复契约");
        }
    }

    public void SetRhythmTarget(ObstacleType obstacleType)
    {
        if (Contract.type != EchoContractType.DisruptRhythm
            || !CanChangeStability())
            return;
        if (obstacleType == ObstacleType.High)
            Contract.targetAction = ShadowAction.Jump;
        else if (obstacleType == ObstacleType.Low)
            Contract.targetAction = ShadowAction.Slide;
    }

    public void RecordHit(float currentSpeed = 10f)
    {
        if (Contract.type == EchoContractType.None || ScoringSuspended
            || Contract.duelFailed)
            return;
        ReduceStability(18f);
        AddShadowLead(0.4f, currentSpeed);
        SetFeedback("回声施压：碰撞让契约重新收紧");
    }

    public string BuildHudText()
    {
        if (Contract.type == EchoContractType.None) return "";
        string progress = Contract.Progress01.ToString("P0");
        string state = Contract.completed
            ? "契约已重写"
            : Contract.initialBreakCompleted
                ? "回声反扑 · 稳定度 " + progress
                : "契约稳定度 " + progress;
        string feedback = string.IsNullOrEmpty(Contract.lastFeedback)
            ? ""
            : " · " + Contract.lastFeedback;
        return Contract.title + " · " + state + feedback;
    }

    private void CompleteIfReady()
    {
        if (Contract.progress < Contract.targetProgress) return;
        if (!Contract.initialBreakCompleted)
        {
            Contract.initialBreakCompleted = true;
            Contract.counterattackActive = true;
            Contract.progress = Contract.targetProgress * 0.55f;
            Contract.predictionLane = Contract.targetLane;
            Contract.predictionAction = Contract.targetAction;
            SetFeedback("契约初裂：回声正在根据你的反抗重新预测");
            return;
        }

        Contract.completed = true;
        Contract.counterattackActive = false;
        SetFeedback("契约已重写：下一代回声正在记录新的你");
    }

    private void BeginCounterattack()
    {
        Contract.counterattackActive = true;
        Contract.progress = Mathf.Clamp(Contract.progress,
            Contract.targetProgress * 0.5f,
            Contract.targetProgress * 0.7f);
        if (Contract.type == EchoContractType.BreakLaneHabit)
        {
            Contract.predictionLane = Contract.targetLane;
        }
        else
        {
            Contract.predictionAction = Contract.targetAction;
            Contract.targetAction = OppositeVertical(Contract.targetAction);
        }
        SetFeedback(BuildPredictionText(true));
    }

    private bool CanChangeStability()
    {
        if (Contract.completed || Contract.duelFailed || ScoringSuspended)
            return false;
        return Contract.duelPhase == EchoDuelPhase.Resistance
               || Contract.duelPhase == EchoDuelPhase.Counterattack
               || Contract.duelPhase == EchoDuelPhase.Finale;
    }

    private void AddStability(float amount, float leadSeconds,
        float currentSpeed)
    {
        Contract.progress = Mathf.Min(Contract.targetProgress,
            Contract.progress + Mathf.Max(0f, amount));
        Contract.playerProgressBonus += EchoTimeRules.SecondsToDistance(
            leadSeconds, currentSpeed);
    }

    private void ReduceStability(float amount)
    {
        Contract.progress = Mathf.Max(0f,
            Contract.progress - Mathf.Max(0f, amount));
        Contract.completed = false;
    }

    private void AddShadowLead(float seconds, float currentSpeed)
    {
        Contract.shadowProgressBonus += EchoTimeRules.SecondsToDistance(
            seconds, currentSpeed);
    }

    private void AddPlayerLead(float seconds, float currentSpeed)
    {
        Contract.playerProgressBonus += EchoTimeRules.SecondsToDistance(
            seconds, currentSpeed);
    }

    private static ShadowAction OppositeVertical(ShadowAction action)
    {
        return action == ShadowAction.Jump
            ? ShadowAction.Slide : ShadowAction.Jump;
    }

    private void SetFeedback(string feedback)
    {
        Contract.lastFeedback = feedback;
        Contract.feedbackSequence++;
    }
}
