using UnityEngine;

[System.Serializable]
public sealed class EchoCalibrationStatus
{
    public int totalSamples;
    public int minimumTotal;
    public int activeSamples;
    public int minimumActive;
    public int categories;
    public int minimumCategories;
    public int jumpSamples;
    public int minimumJump;
    public int slideSamples;
    public int minimumSlide;
    public bool requirementsMet;
    public string nextRequirement = "";
}

public static class AIShadowRules
{
    public static bool CanAvoidObstacle(ObstacleType obstacleType,
        bool isJumping, bool isSliding)
    {
        if (obstacleType == ObstacleType.Low) return isSliding;
        if (obstacleType == ObstacleType.High) return isJumping;
        return false;
    }

    public static ShadowAction RequiredActionForObstacle(ObstacleType obstacleType)
    {
        if (obstacleType == ObstacleType.Low) return ShadowAction.Slide;
        if (obstacleType == ObstacleType.High) return ShadowAction.Jump;
        return ShadowAction.Keep;
    }

    public static bool CanStartVerticalAction(ShadowAction action,
        bool isJumping, bool isSliding, bool isStumbling)
    {
        if (isJumping || isSliding || isStumbling) return false;
        return action == ShadowAction.Jump || action == ShadowAction.Slide;
    }

    public static float CalculateReactionDistance(float speed, float actionDuration)
    {
        return Mathf.Clamp(Mathf.Max(0f, speed) * Mathf.Max(0.1f, actionDuration)
                           * 0.48f, 3.5f, 8f);
    }

    public static float EvaluateJumpArc(float normalizedProgress)
    {
        float sine = Mathf.Sin(Mathf.Clamp01(normalizedProgress) * Mathf.PI);
        return sine * sine;
    }

    public static float EvaluateSlideAmount(float remainingTime, float duration)
    {
        if (remainingTime <= 0f || duration <= 0f) return 0f;
        float elapsed = duration - Mathf.Clamp(remainingTime, 0f, duration);
        const float blendTime = 0.08f;
        float enter = Mathf.Clamp01(elapsed / blendTime);
        float exit = Mathf.Clamp01(remainingTime / blendTime);
        return Mathf.SmoothStep(0f, 1f, Mathf.Min(enter, exit));
    }

    public static bool HasCalibrationSamples(int totalSamples, int activeSamples,
        int[] actionCounts, int minimumTotal, int minimumActive,
        int minimumCategories, int minimumJumpSamples = 0,
        int minimumSlideSamples = 0)
    {
        return totalSamples >= Mathf.Max(1, minimumTotal)
               && activeSamples >= Mathf.Max(1, minimumActive)
               && CountTrainedActionCategories(actionCounts)
               >= Mathf.Max(1, minimumCategories)
               && GetActionCount(actionCounts, ShadowAction.Jump)
               >= Mathf.Max(0, minimumJumpSamples)
               && GetActionCount(actionCounts, ShadowAction.Slide)
               >= Mathf.Max(0, minimumSlideSamples);
    }

    public static float CalculateCalibrationProgress(int totalSamples,
        int activeSamples, int[] actionCounts, int minimumTotal,
        int minimumActive, int minimumCategories, int minimumJumpSamples = 0,
        int minimumSlideSamples = 0)
    {
        float totalProgress = Mathf.Clamp01(
            (float)Mathf.Max(0, totalSamples) / Mathf.Max(1, minimumTotal));
        float activeProgress = Mathf.Clamp01(
            (float)Mathf.Max(0, activeSamples) / Mathf.Max(1, minimumActive));
        float categoryProgress = Mathf.Clamp01(
            (float)CountTrainedActionCategories(actionCounts)
            / Mathf.Max(1, minimumCategories));
        float jumpProgress = minimumJumpSamples > 0
            ? Mathf.Clamp01((float)GetActionCount(
                actionCounts, ShadowAction.Jump) / minimumJumpSamples)
            : 1f;
        float slideProgress = minimumSlideSamples > 0
            ? Mathf.Clamp01((float)GetActionCount(
                actionCounts, ShadowAction.Slide) / minimumSlideSamples)
            : 1f;
        return Mathf.Min(totalProgress, activeProgress, categoryProgress,
            jumpProgress, slideProgress);
    }

    public static EchoCalibrationStatus BuildCalibrationStatus(
        int totalSamples, int activeSamples, int[] actionCounts,
        int minimumTotal, int minimumActive, int minimumCategories,
        int minimumJumpSamples, int minimumSlideSamples)
    {
        var status = new EchoCalibrationStatus
        {
            totalSamples = Mathf.Max(0, totalSamples),
            minimumTotal = Mathf.Max(1, minimumTotal),
            activeSamples = Mathf.Max(0, activeSamples),
            minimumActive = Mathf.Max(1, minimumActive),
            categories = CountTrainedActionCategories(actionCounts),
            minimumCategories = Mathf.Max(1, minimumCategories),
            jumpSamples = GetActionCount(actionCounts, ShadowAction.Jump),
            minimumJump = Mathf.Max(0, minimumJumpSamples),
            slideSamples = GetActionCount(actionCounts, ShadowAction.Slide),
            minimumSlide = Mathf.Max(0, minimumSlideSamples)
        };
        status.requirementsMet = HasCalibrationSamples(status.totalSamples,
            status.activeSamples, actionCounts, status.minimumTotal,
            status.minimumActive, status.minimumCategories,
            status.minimumJump, status.minimumSlide);
        if (status.totalSamples < status.minimumTotal)
            status.nextRequirement = "继续跑动，补足总样本";
        else if (status.activeSamples < status.minimumActive)
            status.nextRequirement = "完成换道、跳跃或滑铲";
        else if (status.categories < status.minimumCategories)
            status.nextRequirement = "再完成一种不同动作";
        else if (status.jumpSamples < status.minimumJump)
            status.nextRequirement = "再完成跳跃";
        else if (status.slideSamples < status.minimumSlide)
            status.nextRequirement = "再完成滑铲";
        else
            status.nextRequirement = "数据已达标，跑到终点生成回声";
        return status;
    }

    public static int CountTrainedActionCategories(int[] actionCounts)
    {
        if (actionCounts == null || actionCounts.Length < 5) return 0;
        int categories = actionCounts[(int)ShadowAction.Left]
                         + actionCounts[(int)ShadowAction.Right] > 0 ? 1 : 0;
        if (actionCounts[(int)ShadowAction.Jump] > 0) categories++;
        if (actionCounts[(int)ShadowAction.Slide] > 0) categories++;
        return categories;
    }

    private static int GetActionCount(int[] actionCounts, ShadowAction action)
    {
        int index = (int)action;
        return actionCounts != null && index >= 0 && index < actionCounts.Length
            ? Mathf.Max(0, actionCounts[index])
            : 0;
    }
}
