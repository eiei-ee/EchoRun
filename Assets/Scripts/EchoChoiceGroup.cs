using System;
using System.Collections.Generic;
using UnityEngine;

public enum EchoResponseKind
{
    None,
    Jump,
    Slide,
    RouteAvoid,
    ClearRoute,
    Hit,
    NoAction,
    Cancelled
}

public enum EchoChoiceGroupKind
{
    RegularObstacleRow,
    DetectionProbe,
    ResistanceChoice,
    CounterattackChoice,
    FinaleObstacle
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
    public EchoChoiceGroupKind groupKind;
    public float routeDistance;
    public float settleRouteDistance;
    public int clearLane = -1;
    public EchoPredictionSnapshot prediction;
    public ObstacleOpportunity[] options = Array.Empty<ObstacleOpportunity>();
}

public struct ObstacleOpportunityResolution
{
    public int opportunityId;
    public int groupId;
    public int phaseSequence;
    public int planVersion;
    public EchoChoiceGroupKind groupKind;
    public int lane;
    public int entryLane;
    public int finalLane;
    public bool laneChanged;
    public ObstacleType obstacleType;
    public EchoResponseKind predictedResponse;
    public float predictionConfidence;
    public EchoResponseKind response;
    public bool physicallySucceeded;
    public bool passedInLane;
}

/// <summary>
/// Resolves an entire obstacle row once, after the player has passed every
/// option in that row. Lane changes update the eventual choice but never close
/// the row early, so the action performed in the final lane remains observable.
/// </summary>
public sealed class ObstacleOpportunityTracker
{
    private const int MaxRememberedGroups = 256;
    private readonly HashSet<int> _resolvedGroups = new HashSet<int>();
    private readonly Queue<int> _resolvedOrder = new Queue<int>();
    private readonly bool[] _usedJumpByLane = new bool[3];
    private readonly bool[] _usedSlideByLane = new bool[3];
    private EchoChoiceGroup _pending;
    private int _entryLane;
    private int _currentLane;

    public bool HasPending => _pending != null;
    public int PendingOpportunityId => HasPending
        ? OpportunityForLane(_pending, _currentLane)?.opportunityId ?? 0
        : 0;
    public ISet<int> ResolvedOpportunityIds { get; } = new HashSet<int>();

    public void Reset()
    {
        _pending = null;
        _entryLane = 0;
        _currentLane = 0;
        Array.Clear(_usedJumpByLane, 0, _usedJumpByLane.Length);
        Array.Clear(_usedSlideByLane, 0, _usedSlideByLane.Length);
        _resolvedGroups.Clear();
        _resolvedOrder.Clear();
        ResolvedOpportunityIds.Clear();
    }

    public void MarkAction(ShadowAction action, int playerLane)
    {
        if (!HasPending) return;
        int lane = Mathf.Clamp(playerLane, 0, 2);
        if (action == ShadowAction.Jump) _usedJumpByLane[lane] = true;
        if (action == ShadowAction.Slide) _usedSlideByLane[lane] = true;
    }

    public bool UpdateGroup(EchoChoiceGroup group, int playerLane,
        float playerRouteDistance, bool isJumping, bool isSliding,
        float detectionDistance,
        out ObstacleOpportunityResolution resolution)
    {
        resolution = default;
        int lane = Mathf.Clamp(playerLane, 0, 2);
        if (!HasPending)
        {
            if (group == null || group.groupId == 0
                || _resolvedGroups.Contains(group.groupId)
                || group.routeDistance - playerRouteDistance
                > Mathf.Max(0f, detectionDistance))
                return false;
            Arm(group, lane);
        }

        _currentLane = lane;
        _usedJumpByLane[lane] |= isJumping;
        _usedSlideByLane[lane] |= isSliding;
        float settleDistance = _pending.settleRouteDistance > 0f
            ? _pending.settleRouteDistance
            : _pending.routeDistance + 1f;
        if (playerRouteDistance < settleDistance) return false;

        ObstacleOpportunity option = OpportunityForLane(_pending, lane);
        if (option == null)
            return Resolve(null, EchoResponseKind.ClearRoute, true, false,
                out resolution);
        EchoResponseKind response = ResponseForLane(lane);
        bool succeeded = RequiredActionFor(option.obstacleType) == response;
        return Resolve(option, response, succeeded, true, out resolution);
    }

    public bool ResolveContact(int opportunityId, bool passed,
        out ObstacleOpportunityResolution resolution)
    {
        resolution = default;
        if (!HasPending || opportunityId == 0) return false;
        ObstacleOpportunity option = FindOpportunity(_pending, opportunityId);
        if (option == null) return false;
        _currentLane = Mathf.Clamp(option.lane, 0, 2);
        EchoResponseKind response = passed
            ? RequiredActionFor(option.obstacleType)
            : EchoResponseKind.Hit;
        return Resolve(option, response, passed, passed, out resolution);
    }

    public bool Cancel(out ObstacleOpportunityResolution resolution)
    {
        return Resolve(null, EchoResponseKind.Cancelled, false, false,
            out resolution);
    }

    private void Arm(EchoChoiceGroup group, int lane)
    {
        _pending = group;
        _entryLane = lane;
        _currentLane = lane;
        Array.Clear(_usedJumpByLane, 0, _usedJumpByLane.Length);
        Array.Clear(_usedSlideByLane, 0, _usedSlideByLane.Length);
    }

    private EchoResponseKind ResponseForLane(int lane)
    {
        if (_usedJumpByLane[lane]) return EchoResponseKind.Jump;
        if (_usedSlideByLane[lane]) return EchoResponseKind.Slide;
        return EchoResponseKind.NoAction;
    }

    private bool Resolve(ObstacleOpportunity option,
        EchoResponseKind response, bool succeeded, bool passedInLane,
        out ObstacleOpportunityResolution resolution)
    {
        resolution = default;
        if (!HasPending) return false;
        int finalLane = Mathf.Clamp(_currentLane, 0, 2);
        resolution = new ObstacleOpportunityResolution
        {
            opportunityId = option != null ? option.opportunityId : 0,
            groupId = _pending.groupId,
            phaseSequence = _pending.phaseSequence,
            planVersion = _pending.planVersion,
            groupKind = _pending.groupKind,
            lane = finalLane,
            entryLane = _entryLane,
            finalLane = finalLane,
            laneChanged = _entryLane != finalLane,
            obstacleType = option != null
                ? option.obstacleType : ObstacleType.Barrier,
            predictedResponse = _pending.prediction != null
                ? _pending.prediction.predictedResponse
                : EchoResponseKind.None,
            predictionConfidence = _pending.prediction != null
                ? _pending.prediction.confidence : 0f,
            response = response,
            physicallySucceeded = succeeded,
            passedInLane = passedInLane
        };
        if (_pending.options != null)
        {
            for (int i = 0; i < _pending.options.Length; i++)
                if (_pending.options[i] != null
                    && _pending.options[i].opportunityId != 0)
                    ResolvedOpportunityIds.Add(
                        _pending.options[i].opportunityId);
        }
        RememberResolvedGroup(_pending.groupId);
        _pending = null;
        Array.Clear(_usedJumpByLane, 0, _usedJumpByLane.Length);
        Array.Clear(_usedSlideByLane, 0, _usedSlideByLane.Length);
        return true;
    }

    private void RememberResolvedGroup(int groupId)
    {
        if (!_resolvedGroups.Add(groupId)) return;
        _resolvedOrder.Enqueue(groupId);
        while (_resolvedOrder.Count > MaxRememberedGroups)
            _resolvedGroups.Remove(_resolvedOrder.Dequeue());
    }

    private static ObstacleOpportunity OpportunityForLane(
        EchoChoiceGroup group, int lane)
    {
        if (group?.options == null) return null;
        for (int i = 0; i < group.options.Length; i++)
            if (group.options[i] != null && group.options[i].lane == lane)
                return group.options[i];
        return null;
    }

    private static ObstacleOpportunity FindOpportunity(
        EchoChoiceGroup group, int opportunityId)
    {
        if (group?.options == null) return null;
        for (int i = 0; i < group.options.Length; i++)
            if (group.options[i] != null
                && group.options[i].opportunityId == opportunityId)
                return group.options[i];
        return null;
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
