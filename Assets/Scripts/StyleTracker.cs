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
    private static float _rhythmProximityMean;
    private static float _rhythmProximityM2;
    private static int _rhythmSamples;
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
        _rhythmProximityMean = 0f;
        _rhythmProximityM2 = 0f;
        _rhythmSamples = 0;
        _recoveryTimeRemaining = 0f;
        _recoveryActions = 0;
        _recoveryRiskTotal = 0f;
    }

    public static void TickLane(int lane, float deltaTime)
    {
        TickLane(lane, deltaTime, 1f);
    }

    public static void TickLane(int lane, float deltaTime,
        float offeredLaneCenter)
    {
        if (!_runActive) return;
        float elapsed = Mathf.Max(0f, deltaTime);
        _laneSampleTimer += elapsed;

        while (_laneSampleTimer >= LaneSampleInterval)
        {
            _laneSampleTimer -= LaneSampleInterval;
            _profile.ObserveLaneChoice(lane, offeredLaneCenter);
        }

        if (_recoveryTimeRemaining <= 0f) return;
        _recoveryTimeRemaining -= elapsed;
        if (_recoveryTimeRemaining <= 0f)
            CommitRecoveryObservation();
    }

    public static void RecordAction(ShadowAction action, float threatProximity,
        float jumpTimingOffset, bool airLaneChange = false,
        bool matchedActionObstacle = false)
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
        if (ShouldObserveVerticalAction(action, matchedActionObstacle))
            _profile.ObserveVerticalAction(action);

        if ((action == ShadowAction.Jump || action == ShadowAction.Slide)
            && proximity > 0.05f)
        {
            _rhythmSamples++;
            float delta = proximity - _rhythmProximityMean;
            _rhythmProximityMean += delta / _rhythmSamples;
            _rhythmProximityM2 += delta
                                  * (proximity - _rhythmProximityMean);
            if (_rhythmSamples >= 2)
            {
                float deviation = Mathf.Sqrt(
                    _rhythmProximityM2 / Mathf.Max(1, _rhythmSamples - 1));
                _profile.ObserveRhythm(
                    1f - Mathf.Clamp01(deviation / 0.25f));
            }
        }

        if (_recoveryTimeRemaining > 0f)
        {
            _recoveryActions++;
            _recoveryRiskTotal += proximity;
        }
    }

    public static bool ShouldObserveVerticalAction(ShadowAction action,
        bool matchedActionObstacle)
    {
        return matchedActionObstacle
               && (action == ShadowAction.Jump || action == ShadowAction.Slide);
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
