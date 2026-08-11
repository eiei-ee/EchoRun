using System;
using UnityEngine;

public static class StyleTracker
{
    private const float LaneSampleInterval = 0.5f;
    private const float RecoveryWindowDuration = 10f;

    private static PlayerStyleData _profile;
    private static bool _initialized;
    private static bool _runActive;
    private static float _laneSampleTimer;
    private static float _lastActionTime = -1f;
    private static float _intervalMean;
    private static float _intervalM2;
    private static int _intervalSamples;
    private static float _recoveryTimeRemaining;
    private static int _recoveryActions;
    private static float _recoveryRiskTotal;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        string json = EchoRunSaveSystem.GetPlayerStyleJson();
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _profile = JsonUtility.FromJson<PlayerStyleData>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Player style profile could not be loaded: "
                                 + exception.Message);
            }
        }

        _profile = _profile ?? new PlayerStyleData();
        _profile.Normalize();
    }

    public static void BeginRun()
    {
        EnsureInitialized();
        _runActive = true;
        _laneSampleTimer = 0f;
        _lastActionTime = -1f;
        _intervalMean = 0f;
        _intervalM2 = 0f;
        _intervalSamples = 0;
        _recoveryActions = 0;
        _recoveryRiskTotal = 0f;
    }

    public static void TickLane(int lane, float deltaTime)
    {
        if (!_runActive) return;
        float elapsed = Mathf.Max(0f, deltaTime);
        _laneSampleTimer += elapsed;

        while (_laneSampleTimer >= LaneSampleInterval)
        {
            _laneSampleTimer -= LaneSampleInterval;
            _profile.ObserveLane(lane);
        }

        if (_recoveryTimeRemaining <= 0f) return;
        _recoveryTimeRemaining -= elapsed;
        if (_recoveryTimeRemaining <= 0f)
            CommitRecoveryObservation();
    }

    public static void RecordAction(ShadowAction action, float threatProximity,
        float jumpTimingOffset, float time, bool airLaneChange = false)
    {
        if (!_runActive || action == ShadowAction.Keep) return;
        float proximity = Mathf.Clamp01(threatProximity);

        if (proximity > 0.05f || airLaneChange)
            _profile.ObserveAggressiveness(
                airLaneChange
                    ? 1f
                    : Mathf.InverseLerp(0.35f, 0.95f, proximity));
        if (action == ShadowAction.Jump && proximity > 0.05f)
            _profile.ObserveJumpTiming(jumpTimingOffset);

        if (_lastActionTime >= 0f)
        {
            float interval = Mathf.Max(0f, time - _lastActionTime);
            _intervalSamples++;
            float delta = interval - _intervalMean;
            _intervalMean += delta / _intervalSamples;
            _intervalM2 += delta * (interval - _intervalMean);
            if (_intervalSamples >= 2)
            {
                float deviation = Mathf.Sqrt(
                    _intervalM2 / Mathf.Max(1, _intervalSamples - 1));
                _profile.ObserveRhythm(1f - Mathf.Clamp01(deviation / 1.25f));
            }
        }
        _lastActionTime = time;

        if (_recoveryTimeRemaining > 0f)
        {
            _recoveryActions++;
            _recoveryRiskTotal += proximity;
        }
    }

    public static void RecordObstacleOpportunity(ObstacleType type,
        bool usedRequiredAction)
    {
        if (!_runActive) return;
        if (type == ObstacleType.Low)
            _profile.ObserveSlideOpportunity(usedRequiredAction);
    }

    public static void RecordMistake()
    {
        if (!_runActive) return;
        _recoveryTimeRemaining = RecoveryWindowDuration;
        _recoveryActions = 0;
        _recoveryRiskTotal = 0f;
    }

    public static void EndRun()
    {
        if (!_runActive) return;
        _runActive = false;
        if (_recoveryTimeRemaining > 0f && _recoveryActions > 0)
            CommitRecoveryObservation();
        _profile.Normalize();
        EchoRunSaveSystem.SavePlayerStyle(JsonUtility.ToJson(_profile));
    }

    public static PlayerStyleData GetSnapshot()
    {
        EnsureInitialized();
        return _profile.Clone();
    }

    public static void ResetTraining()
    {
        _profile = new PlayerStyleData();
        _profile.Normalize();
        _initialized = true;
        _runActive = false;
        _recoveryTimeRemaining = 0f;
        EchoRunSaveSystem.SavePlayerStyle("");
    }

    private static void CommitRecoveryObservation()
    {
        float actionUrgency = Mathf.Clamp01(_recoveryActions / 6f);
        float averageRisk = _recoveryActions > 0
            ? _recoveryRiskTotal / _recoveryActions
            : 0f;
        _profile.ObserveRecovery(actionUrgency * 0.65f
                                 + averageRisk * 0.35f);
        _recoveryTimeRemaining = 0f;
        _recoveryActions = 0;
        _recoveryRiskTotal = 0f;
    }
}

public sealed class SlideOpportunityTracker
{
    private const int MaxRememberedObstacles = 256;
    private readonly System.Collections.Generic.HashSet<int> _resolvedIds =
        new System.Collections.Generic.HashSet<int>();
    private readonly System.Collections.Generic.Queue<int> _resolvedOrder =
        new System.Collections.Generic.Queue<int>();
    private int _pendingId;
    private int _pendingLane;
    private bool _usedSlide;

    public bool HasPending => _pendingId != 0;
    public int PendingId => _pendingId;
    public System.Collections.Generic.ISet<int> ResolvedIds => _resolvedIds;

    public void Reset()
    {
        _pendingId = 0;
        _pendingLane = 1;
        _usedSlide = false;
        _resolvedIds.Clear();
        _resolvedOrder.Clear();
    }

    public bool Update(int playerLane, bool isSliding, bool hasObstacle,
        float obstacleDistance, ObstacleType obstacleType, int obstacleId,
        float detectionDistance, out bool usedSlide)
    {
        usedSlide = false;
        int lane = Mathf.Clamp(playerLane, 0, 2);
        if (HasPending)
        {
            if (lane != _pendingLane)
                return Resolve(out usedSlide);
            if (isSliding) _usedSlide = true;
            if (!hasObstacle || obstacleId != _pendingId)
                return Resolve(out usedSlide);
            return false;
        }

        if (!hasObstacle || obstacleType != ObstacleType.Low
            || obstacleId == 0 || _resolvedIds.Contains(obstacleId)
            || obstacleDistance > Mathf.Max(0f, detectionDistance))
            return false;

        _pendingId = obstacleId;
        _pendingLane = lane;
        _usedSlide = isSliding;
        return false;
    }

    public void MarkSlide(int playerLane)
    {
        if (HasPending && Mathf.Clamp(playerLane, 0, 2) == _pendingLane)
            _usedSlide = true;
    }

    public bool Resolve(out bool usedSlide)
    {
        usedSlide = _usedSlide;
        if (!HasPending) return false;
        if (_resolvedIds.Add(_pendingId))
        {
            _resolvedOrder.Enqueue(_pendingId);
            while (_resolvedOrder.Count > MaxRememberedObstacles)
                _resolvedIds.Remove(_resolvedOrder.Dequeue());
        }
        _pendingId = 0;
        _usedSlide = false;
        return true;
    }
}
