using System;
using System.Collections.Generic;
using UnityEngine;

public enum EchoResponseKind
{
    None,
    Jump,
    Slide,
    RouteAvoid,
    Hit,
    NoAction,
    Cancelled
}

[Serializable]
public sealed class ObstacleOpportunity
{
    public int opportunityId;
    public int groupId;
    public int phaseSequence;
    public int planVersion;
    public int lane;
    public ObstacleType obstacleType;
    public float routeDistance;
}

[Serializable]
public sealed class EchoChoiceGroup
{
    public int groupId;
    public int phaseSequence;
    public int planVersion;
    public int rowId;
    public float routeDistance;
    public ObstacleOpportunity[] options = Array.Empty<ObstacleOpportunity>();
}

public struct ObstacleOpportunityResolution
{
    public int opportunityId;
    public int groupId;
    public int lane;
    public ObstacleType obstacleType;
    public EchoResponseKind response;
    public bool physicallySucceeded;
    public bool passedInLane;
}

/// <summary>
/// Resolves one obstacle row exactly once after the player chooses a lane and
/// passes its route position. It treats clean jumps and slides symmetrically.
/// </summary>
public sealed class ObstacleOpportunityTracker
{
    private const int MaxRememberedGroups = 256;
    private readonly HashSet<int> _resolvedGroups = new HashSet<int>();
    private readonly Queue<int> _resolvedOrder = new Queue<int>();
    private ObstacleOpportunity _pending;
    private bool _usedJump;
    private bool _usedSlide;

    public bool HasPending => _pending != null;
    public int PendingOpportunityId => HasPending ? _pending.opportunityId : 0;
    public ISet<int> ResolvedOpportunityIds { get; } = new HashSet<int>();

    public void Reset()
    {
        _pending = null;
        _usedJump = false;
        _usedSlide = false;
        _resolvedGroups.Clear();
        _resolvedOrder.Clear();
        ResolvedOpportunityIds.Clear();
    }

    public void MarkAction(ShadowAction action, int playerLane)
    {
        if (!HasPending || Mathf.Clamp(playerLane, 0, 2) != _pending.lane)
            return;
        if (action == ShadowAction.Jump) _usedJump = true;
        if (action == ShadowAction.Slide) _usedSlide = true;
    }

    public bool Update(int playerLane, bool isJumping, bool isSliding,
        bool hasObstacle, float obstacleDistance, ObstacleType obstacleType,
        int opportunityId, int groupId, float detectionDistance,
        out ObstacleOpportunityResolution resolution)
    {
        resolution = default;
        int lane = Mathf.Clamp(playerLane, 0, 2);
        if (HasPending)
        {
            if (lane == _pending.lane)
            {
                _usedJump |= isJumping;
                _usedSlide |= isSliding;
            }
            if (lane != _pending.lane)
                return Resolve(EchoResponseKind.RouteAvoid, true, false,
                    out resolution);
            if (!hasObstacle || opportunityId != _pending.opportunityId)
            {
                EchoResponseKind response = ResponseForPending();
                bool succeeded = RequiredActionFor(_pending.obstacleType)
                                 == response;
                return Resolve(response, succeeded, true, out resolution);
            }
            return false;
        }

        if (!hasObstacle || opportunityId == 0
            || ResolvedOpportunityIds.Contains(opportunityId)
            || (groupId != 0 && _resolvedGroups.Contains(groupId))
            || obstacleDistance > Mathf.Max(0f, detectionDistance)
            || (obstacleType != ObstacleType.High
                && obstacleType != ObstacleType.Low))
            return false;

        _pending = new ObstacleOpportunity
        {
            opportunityId = opportunityId,
            groupId = groupId != 0 ? groupId : opportunityId,
            lane = lane,
            obstacleType = obstacleType
        };
        _usedJump = isJumping;
        _usedSlide = isSliding;
        return false;
    }

    public bool ResolveContact(int opportunityId, bool passed,
        out ObstacleOpportunityResolution resolution)
    {
        resolution = default;
        if (!HasPending || opportunityId == 0
            || opportunityId != _pending.opportunityId)
            return false;
        EchoResponseKind response = passed
            ? RequiredActionFor(_pending.obstacleType)
            : EchoResponseKind.Hit;
        return Resolve(response, passed, passed, out resolution);
    }

    public bool Cancel(out ObstacleOpportunityResolution resolution)
    {
        return Resolve(EchoResponseKind.Cancelled, false, false,
            out resolution);
    }

    private EchoResponseKind ResponseForPending()
    {
        if (_usedJump) return EchoResponseKind.Jump;
        if (_usedSlide) return EchoResponseKind.Slide;
        return EchoResponseKind.NoAction;
    }

    private bool Resolve(EchoResponseKind response, bool succeeded,
        bool passedInLane, out ObstacleOpportunityResolution resolution)
    {
        resolution = default;
        if (!HasPending) return false;
        resolution = new ObstacleOpportunityResolution
        {
            opportunityId = _pending.opportunityId,
            groupId = _pending.groupId,
            lane = _pending.lane,
            obstacleType = _pending.obstacleType,
            response = response,
            physicallySucceeded = succeeded,
            passedInLane = passedInLane
        };
        ResolvedOpportunityIds.Add(_pending.opportunityId);
        if (_resolvedGroups.Add(_pending.groupId))
        {
            _resolvedOrder.Enqueue(_pending.groupId);
            while (_resolvedOrder.Count > MaxRememberedGroups)
                _resolvedGroups.Remove(_resolvedOrder.Dequeue());
        }
        _pending = null;
        _usedJump = false;
        _usedSlide = false;
        return true;
    }

    private static EchoResponseKind RequiredActionFor(ObstacleType type)
    {
        return type == ObstacleType.High
            ? EchoResponseKind.Jump
            : type == ObstacleType.Low
                ? EchoResponseKind.Slide
                : EchoResponseKind.None;
    }
}
