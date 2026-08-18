using System;
using UnityEngine;

public enum EchoEvidenceConclusion
{
    Insufficient,
    Balanced,
    Jump,
    Slide,
    RouteAvoid
}

[Serializable]
public sealed class EchoPredictionSnapshot
{
    public EchoEvidenceConclusion conclusion;
    public EchoResponseKind predictedResponse;
    public float confidence;
    public int opportunityCount;
    public int jumpCount;
    public int slideCount;
    public int routeAvoidCount;
    public int noActionCount;

    public bool HasSpecificPrediction =>
        conclusion == EchoEvidenceConclusion.Jump
        || conclusion == EchoEvidenceConclusion.Slide
        || conclusion == EchoEvidenceConclusion.RouteAvoid;
}

/// <summary>Evidence collected during one challenge attempt only.</summary>
public sealed class EchoDuelEvidence
{
    private const float StrongWeight = 2f;
    private const float WeakWeight = 0.5f;
    private const int MinimumOpportunities = 3;
    private const float MinimumMargin = 1.5f;

    private float _jumpWeight;
    private float _slideWeight;
    private float _routeWeight;
    private int _opportunityCount;
    private int _jumpCount;
    private int _slideCount;
    private int _routeAvoidCount;
    private int _noActionCount;

    public EchoPredictionSnapshot Prediction => BuildPrediction();

    public void Reset()
    {
        _jumpWeight = 0f;
        _slideWeight = 0f;
        _routeWeight = 0f;
        _opportunityCount = 0;
        _jumpCount = 0;
        _slideCount = 0;
        _routeAvoidCount = 0;
        _noActionCount = 0;
    }

    public void Observe(ObstacleOpportunityResolution result)
    {
        if (result.response == EchoResponseKind.Cancelled
            || result.response == EchoResponseKind.None)
            return;
        _opportunityCount++;
        switch (result.response)
        {
            case EchoResponseKind.Jump:
                _jumpCount++;
                _jumpWeight += StrongWeight;
                break;
            case EchoResponseKind.Slide:
                _slideCount++;
                _slideWeight += StrongWeight;
                break;
            case EchoResponseKind.RouteAvoid:
                _routeAvoidCount++;
                _routeWeight += StrongWeight;
                break;
            case EchoResponseKind.NoAction:
            case EchoResponseKind.Hit:
                _noActionCount++;
                break;
        }
    }

    public void ObserveFreeAction(ShadowAction action)
    {
        if (action == ShadowAction.Jump) _jumpWeight += WeakWeight;
        else if (action == ShadowAction.Slide) _slideWeight += WeakWeight;
    }

    public string BuildEvidenceText()
    {
        return "本局侦测：跳跃 " + _jumpCount + " · 滑铲 " + _slideCount
               + " · 改道 " + _routeAvoidCount + " · 未响应 "
               + _noActionCount;
    }

    public string BuildPredictionText(string prefix = "回声预判：")
    {
        return BuildPredictionText(BuildPrediction(), prefix);
    }

    public static string BuildPredictionText(EchoPredictionSnapshot snapshot,
        string prefix = "回声预判：")
    {
        if (snapshot == null)
            return prefix + "证据不足，暂不下结论";
        if (snapshot.conclusion == EchoEvidenceConclusion.Insufficient)
            return prefix + "证据不足，暂不下结论";
        if (snapshot.conclusion == EchoEvidenceConclusion.Balanced)
            return prefix + "行为均衡，暂无单一偏好";
        string action = snapshot.conclusion == EchoEvidenceConclusion.Jump
            ? "继续跳跃"
            : snapshot.conclusion == EchoEvidenceConclusion.Slide
                ? "继续滑铲" : "继续改道";
        return prefix + action + " · "
               + Mathf.RoundToInt(snapshot.confidence * 100f) + "%";
    }

    private EchoPredictionSnapshot BuildPrediction()
    {
        float top = _jumpWeight;
        float second = Mathf.Max(_slideWeight, _routeWeight);
        EchoEvidenceConclusion conclusion = EchoEvidenceConclusion.Jump;
        EchoResponseKind response = EchoResponseKind.Jump;
        if (_slideWeight > top)
        {
            second = Mathf.Max(top, _routeWeight);
            top = _slideWeight;
            conclusion = EchoEvidenceConclusion.Slide;
            response = EchoResponseKind.Slide;
        }
        if (_routeWeight > top)
        {
            second = Mathf.Max(top, _slideWeight);
            top = _routeWeight;
            conclusion = EchoEvidenceConclusion.RouteAvoid;
            response = EchoResponseKind.RouteAvoid;
        }

        float total = _jumpWeight + _slideWeight + _routeWeight;
        if (_opportunityCount < MinimumOpportunities || top <= 0f)
        {
            conclusion = EchoEvidenceConclusion.Insufficient;
            response = EchoResponseKind.None;
        }
        else if (top - second < MinimumMargin)
        {
            conclusion = EchoEvidenceConclusion.Balanced;
            response = EchoResponseKind.None;
        }
        return new EchoPredictionSnapshot
        {
            conclusion = conclusion,
            predictedResponse = response,
            confidence = total > 0f ? Mathf.Clamp01(top / total) : 0f,
            opportunityCount = _opportunityCount,
            jumpCount = _jumpCount,
            slideCount = _slideCount,
            routeAvoidCount = _routeAvoidCount,
            noActionCount = _noActionCount
        };
    }
}
