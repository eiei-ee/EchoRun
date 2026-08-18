using UnityEngine;

public enum ObstacleType { Low, High, Barrier }

public class Obstacle : MonoBehaviour
{
    public ObstacleType type;
    public int opportunityId;
    public int choiceGroupId;
    public int phaseSequence;
    public int planVersion;
    public int lane = -1;
    public float routeDistance;

    public void ConfigureOpportunity(int newOpportunityId, int newGroupId,
        int newPhaseSequence, int newPlanVersion, int newLane,
        float newRouteDistance)
    {
        opportunityId = newOpportunityId;
        choiceGroupId = newGroupId;
        phaseSequence = newPhaseSequence;
        planVersion = newPlanVersion;
        lane = Mathf.Clamp(newLane, 0, 2);
        routeDistance = Mathf.Max(0f, newRouteDistance);
    }
}
