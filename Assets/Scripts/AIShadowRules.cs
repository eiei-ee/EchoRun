using UnityEngine;

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
        int minimumCategories)
    {
        return totalSamples >= Mathf.Max(1, minimumTotal)
               && activeSamples >= Mathf.Max(1, minimumActive)
               && CountTrainedActionCategories(actionCounts)
               >= Mathf.Max(1, minimumCategories);
    }

    public static float CalculateCalibrationProgress(int totalSamples,
        int activeSamples, int[] actionCounts, int minimumTotal,
        int minimumActive, int minimumCategories)
    {
        float totalProgress = Mathf.Clamp01(
            (float)Mathf.Max(0, totalSamples) / Mathf.Max(1, minimumTotal));
        float activeProgress = Mathf.Clamp01(
            (float)Mathf.Max(0, activeSamples) / Mathf.Max(1, minimumActive));
        float categoryProgress = Mathf.Clamp01(
            (float)CountTrainedActionCategories(actionCounts)
            / Mathf.Max(1, minimumCategories));
        return Mathf.Min(totalProgress, activeProgress, categoryProgress);
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
}
