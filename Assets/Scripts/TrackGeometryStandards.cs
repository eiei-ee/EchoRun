using UnityEngine;

public static class TrackGeometryStandards
{
    public const float StandardSegmentLength = 20f;
    public const float LaneSpacing = 3f;
    public const float WalkableWidth = 9f;
    public const float VisualRoadWidth = 11f;
    public const float VisualRoadHalfWidth = VisualRoadWidth * 0.5f;
    public const float EdgeRailInset = 0.15f;
    public const float EdgeRailOffset = VisualRoadHalfWidth - EdgeRailInset;

    public static float GetLaneCenter(int lane)
    {
        return (Mathf.Clamp(lane, 0, 2) - 1) * LaneSpacing;
    }

    public static float TurnEntrySurfaceLength(float segmentLength)
    {
        return Mathf.Max(0f, segmentLength * 0.5f) + VisualRoadHalfWidth;
    }

    public static float TurnEntrySurfaceCenter(float segmentLength)
    {
        return TurnEntrySurfaceLength(segmentLength) * 0.5f;
    }

    public static float TurnExitSurfaceLength(float segmentLength)
    {
        return Mathf.Max(0.01f,
            segmentLength * 0.5f - VisualRoadHalfWidth);
    }

    public static float TurnExitSurfaceCenter(float segmentLength)
    {
        return VisualRoadHalfWidth
               + TurnExitSurfaceLength(segmentLength) * 0.5f;
    }
}
