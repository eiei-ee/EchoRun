using UnityEngine;

public static class TrackGeometryStandards
{
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
}
