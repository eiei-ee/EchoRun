using System;
using UnityEngine;

[Serializable]
public sealed class EchoRewriteSnapshot
{
    [Range(0f, 1f)] public float routeVariation01;
    [Range(0f, 1f)] public float actionMix01;
    [Range(0f, 1f)] public float rhythmNovelty01;
    [Range(0f, 1f)] public float execution01;
    [Range(1f, 2f)] public float writeStrength = 1f;
    public int effectiveRouteChoices;
    public int effectiveVerticalActions;
    public int successfulExecutions;
    public int mistakes;
    public string styleJson = "";

    public PlayerStyleData GetStyle()
    {
        if (string.IsNullOrEmpty(styleJson)) return new PlayerStyleData();
        try
        {
            PlayerStyleData style = JsonUtility.FromJson<PlayerStyleData>(
                styleJson);
            style = style ?? new PlayerStyleData();
            style.Normalize();
            return style;
        }
        catch (Exception)
        {
            return new PlayerStyleData();
        }
    }

    public EchoRewriteSnapshot Clone()
    {
        return JsonUtility.FromJson<EchoRewriteSnapshot>(
            JsonUtility.ToJson(this));
    }

    public string BuildHudSummary()
    {
        return "路线变化 " + Percent(routeVariation01)
               + " · 动作混合 " + Percent(actionMix01)
               + "\n节奏新颖 " + Percent(rhythmNovelty01)
               + " · 有效执行 " + Percent(execution01)
               + " · 写入×" + Mathf.Clamp(writeStrength, 1f, 2f)
                   .ToString("0.00");
    }

    public string BuildProfileSummary()
    {
        string route = routeVariation01 >= 0.55f
            ? "多路线" : routeVariation01 >= 0.25f
                ? "双路线" : "路线稳定";
        string action = actionMix01 >= 0.55f
            ? "混合动作" : actionMix01 >= 0.2f
                ? "动作变化" : "单一动作";
        string rhythm = rhythmNovelty01 >= 0.55f
            ? "非固定节奏" : rhythmNovelty01 >= 0.2f
                ? "节奏变化" : "节奏稳定";
        return route + " · " + action + " · " + rhythm
               + " · 写入×" + Mathf.Clamp(writeStrength, 1f, 2f)
                   .ToString("0.00");
    }

    private static string Percent(float value)
    {
        return Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }
}

/// <summary>
/// Measures deliberate, successfully executed variation during the fixed
/// rewrite window. Raw input spam is deliberately ignored: route evidence
/// comes from collected route trails and vertical evidence must match a real
/// obstacle opportunity.
/// </summary>
public sealed class EchoRewriteTracker
{
    public const float MinimumRouteSampleSpacing = 14f;
    private const int MistakeExecutionWeight = 4;

    private PlayerStyleData _baselineStyle;
    private readonly bool[] _visitedLanes = new bool[3];
    private int _visitedLaneCount;
    private int _routeSamples;
    private int _laneSwitches;
    private float _laneTotal;
    private int _lastLane = -1;
    private float _lastRouteDistance = float.NegativeInfinity;
    private int _jumpActions;
    private int _slideActions;
    private float _lastVerticalActionTime = -1f;
    private int _rhythmIntervals;
    private float _rhythmMean;
    private float _rhythmM2;
    private int _successes;
    private int _mistakes;

    public EchoRewriteTracker(PlayerStyleData baselineStyle = null)
    {
        if (baselineStyle == null) return;
        _baselineStyle = baselineStyle.Clone();
        _baselineStyle.Normalize();
    }

    public void RecordRouteChoice(int lane, float routeDistance)
    {
        int clampedLane = Mathf.Clamp(lane, 0, 2);
        float distance = Mathf.Max(0f, routeDistance);
        if (!float.IsNegativeInfinity(_lastRouteDistance)
            && distance - _lastRouteDistance < MinimumRouteSampleSpacing)
            return;

        _lastRouteDistance = distance;
        _routeSamples++;
        _laneTotal += clampedLane;
        _successes++;
        if (!_visitedLanes[clampedLane])
        {
            _visitedLanes[clampedLane] = true;
            _visitedLaneCount++;
        }
        if (_lastLane >= 0 && _lastLane != clampedLane) _laneSwitches++;
        _lastLane = clampedLane;
    }

    public void RecordVerticalAction(ShadowAction action,
        bool matchedObstacle, float eventTime)
    {
        if (!matchedObstacle
            || (action != ShadowAction.Jump
                && action != ShadowAction.Slide))
            return;

        if (action == ShadowAction.Jump) _jumpActions++;
        else _slideActions++;

        float time = Mathf.Max(0f, eventTime);
        if (_lastVerticalActionTime >= 0f)
        {
            float interval = time - _lastVerticalActionTime;
            if (interval >= 0.15f && interval <= 12f)
            {
                _rhythmIntervals++;
                float delta = interval - _rhythmMean;
                _rhythmMean += delta / _rhythmIntervals;
                _rhythmM2 += delta * (interval - _rhythmMean);
            }
        }
        _lastVerticalActionTime = time;
    }

    public void RecordSuccessfulExecution()
    {
        _successes++;
    }

    public void RecordMistake()
    {
        _mistakes++;
    }

    public EchoRewriteSnapshot BuildSnapshot(PlayerStyleData style)
    {
        float routeParticipation = Mathf.Clamp01(_routeSamples / 4f);
        float distinctRoutes = Mathf.Clamp01((_visitedLaneCount - 1f) / 2f);
        float switchRatio = _routeSamples > 1
            ? Mathf.Clamp01(_laneSwitches / (float)(_routeSamples - 1))
            : 0f;
        float routeVariation = routeParticipation
                               * (distinctRoutes * 0.65f
                                  + switchRatio * 0.35f);

        int verticalActions = _jumpActions + _slideActions;
        float actionBalance = verticalActions >= 2
            ? 1f - Mathf.Abs(_jumpActions - _slideActions)
              / (float)verticalActions
            : 0f;
        float actionMix = actionBalance
                          * Mathf.Clamp01(verticalActions / 4f);

        float rhythmNovelty = 0f;
        if (_rhythmIntervals >= 2 && _rhythmMean > 0.001f)
        {
            float deviation = Mathf.Sqrt(
                _rhythmM2 / Mathf.Max(1, _rhythmIntervals - 1));
            float coefficient = deviation / _rhythmMean;
            rhythmNovelty = Mathf.Clamp01(coefficient / 0.35f)
                            * Mathf.Clamp01(_rhythmIntervals / 3f);
        }

        int weightedMistakes = _mistakes * MistakeExecutionWeight;
        int executionSamples = _successes + weightedMistakes;
        float execution = executionSamples > 0
            ? _successes / (float)executionSamples
              * Mathf.Clamp01(executionSamples / 4f)
            : 0f;
        float effectiveVariation = routeVariation * 0.40f
                                   + actionMix * 0.35f
                                   + rhythmNovelty * 0.25f;
        float strength = Mathf.Clamp(
            1f + effectiveVariation * execution, 1f, 2f);

        if (_baselineStyle == null)
        {
            _baselineStyle = style != null
                ? style.Clone() : new PlayerStyleData();
            _baselineStyle.Normalize();
        }
        PlayerStyleData frozenStyle = _baselineStyle.Clone();
        if (_routeSamples > 0 && routeVariation > 0f)
        {
            float rewriteLanePreference = Mathf.Clamp(
                _laneTotal / _routeSamples - 1f, -1f, 1f);
            frozenStyle.lanePreference = Mathf.Lerp(
                frozenStyle.lanePreference, rewriteLanePreference,
                Mathf.Clamp01(routeVariation));
            frozenStyle.laneSamples += _routeSamples;
        }
        if (verticalActions > 0 && actionMix > 0f)
        {
            float rewriteSlideFrequency = (_slideActions + 1f)
                                          / (verticalActions + 2f);
            frozenStyle.slideFrequency = Mathf.Lerp(
                frozenStyle.slideFrequency, rewriteSlideFrequency,
                Mathf.Clamp01(actionMix));
            frozenStyle.jumpActionSamples += _jumpActions;
            frozenStyle.slideActionSamples += _slideActions;
        }
        if (_rhythmIntervals >= 2 && rhythmNovelty > 0f)
        {
            frozenStyle.rhythmStability = Mathf.Lerp(
                frozenStyle.rhythmStability, 1f - rhythmNovelty,
                Mathf.Clamp01(rhythmNovelty));
            frozenStyle.rhythmSamples += _rhythmIntervals;
        }
        frozenStyle.Normalize();
        return new EchoRewriteSnapshot
        {
            routeVariation01 = Mathf.Clamp01(routeVariation),
            actionMix01 = Mathf.Clamp01(actionMix),
            rhythmNovelty01 = Mathf.Clamp01(rhythmNovelty),
            execution01 = Mathf.Clamp01(execution),
            writeStrength = strength,
            effectiveRouteChoices = _routeSamples,
            effectiveVerticalActions = verticalActions,
            successfulExecutions = _successes,
            mistakes = _mistakes,
            styleJson = JsonUtility.ToJson(frozenStyle)
        };
    }
}
