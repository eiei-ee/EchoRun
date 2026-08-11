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
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public EchoContractType type;
    public int generation;
    public int learnedLane = -1;
    public int targetLane = -1;
    public ShadowAction learnedAction = ShadowAction.Keep;
    public ShadowAction targetAction = ShadowAction.Keep;
    public float targetProgress = 1f;
    public float progress;
    public float playerProgressBonus;
    public float shadowProgressBonus;
    public bool completed;
    public bool won;
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
        version = CurrentVersion;
        if (!Enum.IsDefined(typeof(EchoContractType), type))
            type = EchoContractType.None;
        generation = Mathf.Max(0, generation);
        learnedLane = Mathf.Clamp(learnedLane, -1, 2);
        targetLane = Mathf.Clamp(targetLane, -1, 2);
        targetProgress = Mathf.Max(0.01f, targetProgress);
        progress = Mathf.Clamp(progress, 0f, targetProgress);
        playerProgressBonus = Mathf.Max(0f, playerProgressBonus);
        shadowProgressBonus = Mathf.Max(0f, shadowProgressBonus);
        completed = progress >= targetProgress || completed;
        title = title ?? "";
        learnedTrait = learnedTrait ?? "";
        ruleDescription = ruleDescription ?? "";
        objective = objective ?? "";
        lastFeedback = lastFeedback ?? "";
    }
}

public static class EchoContractPolicy
{
    public static EchoContractData Create(PlayerStyleData source, int generation)
    {
        PlayerStyleData style = source != null
            ? source.Clone()
            : new PlayerStyleData();
        style.Normalize();

        float laneEvidence = Mathf.Clamp01(style.laneSamples / 12f);
        float verticalEvidence = Mathf.Clamp01(style.slideOpportunitySamples / 5f);
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
        if (generation % 3 == 1)
            return CreateVerticalContract(style, generation);
        if (generation % 3 == 2)
            return CreateRhythmContract(style, generation);
        return CreateLaneContract(style, generation);
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
            targetProgress = 5f,
            title = "回声契约：逆向改道",
            learnedTrait = "AI识别：你经常依赖" + learnedName,
            ruleDescription = "本代把高价值安全路线迁移到" + targetName
                              + "；停留在旧路线会让回声加速。",
            objective = "累计在" + targetName + "保持 5 秒，并在终点领先回声。"
        };
    }

    private static EchoContractData CreateVerticalContract(
        PlayerStyleData style, int generation)
    {
        bool prefersSlide = style.slideFrequency >= 0.5f;
        ShadowAction learned = prefersSlide ? ShadowAction.Slide : ShadowAction.Jump;
        ShadowAction target = prefersSlide ? ShadowAction.Jump : ShadowAction.Slide;
        string learnedName = ActionName(learned);
        string targetName = ActionName(target);
        return new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            generation = Mathf.Max(1, generation),
            learnedAction = learned,
            targetAction = target,
            targetProgress = 3f,
            title = "回声契约：动作反转",
            learnedTrait = "AI识别：你更依赖" + learnedName,
            ruleDescription = "本代提高需要" + targetName
                              + "破解的组合；重复旧动作会给回声推进优势。",
            objective = "用" + targetName + "正确躲避 3 次，并在终点领先回声。"
        };
    }

    private static EchoContractData CreateRhythmContract(
        PlayerStyleData style, int generation)
    {
        string rhythm = style.rhythmStability >= 0.65f
            ? "你的跳跃/滑铲节奏高度固定"
            : "你的动作序列已被AI归纳";
        return new EchoContractData
        {
            type = EchoContractType.DisruptRhythm,
            generation = Mathf.Max(1, generation),
            targetProgress = 4f,
            title = "回声契约：打乱节拍",
            learnedTrait = "AI识别：" + rhythm,
            ruleDescription = "本代交替生成高低障碍；连续复用同一种动作会让回声加速。",
            objective = "交替用跳跃和滑铲正确躲避 4 次，并在终点领先回声。"
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
        string vertical = Mathf.Abs(style.slideFrequency - 0.5f) < 0.12f
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

    private float _targetLaneAwardedSeconds;
    private float _learnedLaneAwardedSeconds;
    private ShadowAction _lastRhythmAction = ShadowAction.Keep;

    public EchoContractEvaluator(EchoContractData contract)
    {
        Contract = contract != null ? contract.Clone() : new EchoContractData();
        Contract.Normalize();
    }

    public void TickLane(int lane, float deltaTime)
    {
        if (Contract.type != EchoContractType.BreakLaneHabit
            || Contract.completed || deltaTime <= 0f)
            return;

        if (lane == Contract.targetLane)
        {
            float before = _targetLaneAwardedSeconds;
            _targetLaneAwardedSeconds += deltaTime;
            Contract.progress = Mathf.Min(
                Contract.targetProgress, Contract.progress + deltaTime);
            int awards = Mathf.FloorToInt(_targetLaneAwardedSeconds)
                         - Mathf.FloorToInt(before);
            if (awards > 0)
            {
                Contract.playerProgressBonus += awards * 1.5f;
                Contract.lastFeedback = "反制生效：新路线为你提供推进";
            }
            CompleteIfReady();
        }
        else if (lane == Contract.learnedLane)
        {
            float before = _learnedLaneAwardedSeconds;
            _learnedLaneAwardedSeconds += deltaTime;
            int awards = Mathf.FloorToInt(_learnedLaneAwardedSeconds)
                         - Mathf.FloorToInt(before);
            if (awards > 0)
            {
                Contract.shadowProgressBonus += awards * 0.75f;
                Contract.lastFeedback = "AI施压：旧路线习惯正在强化回声";
            }
        }
    }

    public void RecordDodge(ObstacleType obstacleType)
    {
        ShadowAction action = obstacleType == ObstacleType.High
            ? ShadowAction.Jump
            : obstacleType == ObstacleType.Low
                ? ShadowAction.Slide
                : ShadowAction.Keep;
        if (action == ShadowAction.Keep || Contract.completed) return;

        if (Contract.type == EchoContractType.ChangeVerticalHabit)
        {
            if (action == Contract.targetAction)
            {
                Contract.progress = Mathf.Min(
                    Contract.targetProgress, Contract.progress + 1f);
                Contract.playerProgressBonus += 3.5f;
                Contract.lastFeedback = "反制生效：动作反转骗过了回声";
                CompleteIfReady();
            }
            else if (action == Contract.learnedAction)
            {
                Contract.shadowProgressBonus += 2f;
                Contract.lastFeedback = "AI施压：你重复了被预测的动作";
            }
            return;
        }

        if (Contract.type != EchoContractType.DisruptRhythm) return;
        if (_lastRhythmAction == ShadowAction.Keep
            || action != _lastRhythmAction)
        {
            Contract.progress = Mathf.Min(
                Contract.targetProgress, Contract.progress + 1f);
            Contract.playerProgressBonus += 2.75f;
            Contract.lastFeedback = "反制生效：动作节拍已改变";
            CompleteIfReady();
        }
        else
        {
            Contract.shadowProgressBonus += 1.75f;
            Contract.lastFeedback = "AI施压：重复节拍被回声预测";
        }
        _lastRhythmAction = action;
    }

    public void RecordHit()
    {
        if (Contract.type == EchoContractType.None || Contract.completed) return;
        Contract.shadowProgressBonus += 2f;
        Contract.lastFeedback = "AI施压：失误让回声扩大优势";
    }

    public string BuildHudText()
    {
        if (Contract.type == EchoContractType.None) return "";
        string progress = Contract.progress.ToString("0.#") + "/"
                          + Contract.targetProgress.ToString("0.#");
        string state = Contract.completed ? "契约已破解" : "反制 " + progress;
        string feedback = string.IsNullOrEmpty(Contract.lastFeedback)
            ? ""
            : " · " + Contract.lastFeedback;
        return Contract.title + " · " + state + feedback;
    }

    private void CompleteIfReady()
    {
        if (Contract.progress < Contract.targetProgress) return;
        Contract.completed = true;
        Contract.lastFeedback = "契约已破解：保持领先才能击败本代回声";
    }
}
