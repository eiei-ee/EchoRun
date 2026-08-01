using System.Collections.Generic;
using UnityEngine;

public static class TrackSpawnRules
{
    public static bool NeedsSegment(float plannedRouteDistance,
        float playerRouteDistance, float segmentLength, int poolSize)
    {
        float safeLength = Mathf.Max(1f, segmentLength);
        float lookAheadDistance = safeLength * Mathf.Max(2, poolSize / 2);
        return plannedRouteDistance - playerRouteDistance < lookAheadDistance;
    }

    public static bool CanRecycleSegment(float segmentRouteDistance,
        float playerRouteDistance, float segmentLength, float recycleMultiplier)
    {
        float recycleDistance = Mathf.Max(1f, segmentLength)
                                * Mathf.Max(1f, recycleMultiplier);
        return playerRouteDistance - segmentRouteDistance > recycleDistance;
    }

    public static bool ShouldSpawnObstacleRow(int straightSegmentsSpawned,
        int obstacleFreeSegments, int warmupSegments, int maxFreeSegments,
        float chance, float chanceRoll)
    {
        if (straightSegmentsSpawned <= Mathf.Max(0, warmupSegments)) return false;
        if (obstacleFreeSegments > Mathf.Max(0, maxFreeSegments)) return true;
        return chanceRoll < Mathf.Clamp01(chance);
    }

    public static int ChooseFairSafeLane(int proposedLane, int previousSafeLane,
        int[] laneObstacleDrought)
    {
        int proposed = Mathf.Clamp(proposedLane, 0, 2);
        int previous = Mathf.Clamp(previousSafeLane, 0, 2);
        int minLane = Mathf.Max(0, previous - 1);
        int maxLane = Mathf.Min(2, previous + 1);
        int bestLane = Mathf.Clamp(proposed, minLane, maxLane);
        int bestDrought = GetLaneDrought(laneObstacleDrought, bestLane);

        for (int lane = minLane; lane <= maxLane; lane++)
        {
            int drought = GetLaneDrought(laneObstacleDrought, lane);
            if (drought < bestDrought
                || (drought == bestDrought
                    && Mathf.Abs(lane - proposed) < Mathf.Abs(bestLane - proposed)))
            {
                bestLane = lane;
                bestDrought = drought;
            }
        }
        return bestLane;
    }

    public static int[] SelectBlockedLanes(int safeLane, int blockedLaneCount,
        int[] laneObstacleDrought)
    {
        int safe = Mathf.Clamp(safeLane, 0, 2);
        var candidates = new List<int>(2);
        for (int lane = 0; lane < 3; lane++)
            if (lane != safe) candidates.Add(lane);

        candidates.Sort((left, right) =>
        {
            int droughtOrder = GetLaneDrought(laneObstacleDrought, right)
                .CompareTo(GetLaneDrought(laneObstacleDrought, left));
            return droughtOrder != 0 ? droughtOrder : left.CompareTo(right);
        });

        int count = Mathf.Clamp(blockedLaneCount, 1, 2);
        return candidates.GetRange(0, Mathf.Min(count, candidates.Count)).ToArray();
    }

    private static int GetLaneDrought(int[] laneObstacleDrought, int lane)
    {
        return laneObstacleDrought != null && lane >= 0
               && lane < laneObstacleDrought.Length
            ? Mathf.Max(0, laneObstacleDrought[lane])
            : 0;
    }
}
