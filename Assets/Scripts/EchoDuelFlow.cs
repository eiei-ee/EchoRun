using System;
using UnityEngine;

public enum EchoDuelPhase
{
    None,
    Calibration,
    Detection,
    Reveal,
    Resistance,
    Counterattack,
    Rewrite,
    Finale,
    Finished
}

/// <summary>
/// Runtime pacing for one echo duel. The contract owns what the player must
/// change; this class only owns when each dramatic beat is active.
/// </summary>
public sealed class EchoDuelFlow
{
    public const float DefaultDetectionDuration = 25f;
    public const float DefaultRevealDuration = 6f;
    public const float DefaultResistanceDuration = 30f;
    public const float DefaultCounterattackDuration = 40f;
    public const float DefaultRewriteDuration = 32f;
    public const float DefaultFinaleDuration = 25f;

    public EchoDuelPhase Phase { get; private set; }
    public float PhaseElapsed { get; private set; }
    public float RunElapsed { get; private set; }
    public int PhaseSequence { get; private set; }

    public float DetectionDuration { get; }
    public float RevealDuration { get; }
    public float ResistanceDuration { get; }
    public float CounterattackDuration { get; }
    public float RewriteDuration { get; }
    public float FinaleDuration { get; }

    public bool IsRewriteLearningWindow => Phase == EchoDuelPhase.Rewrite
                                           && PhaseElapsed <= RewriteDuration;

    public EchoDuelFlow(bool hasOpponent,
        float detectionDuration = DefaultDetectionDuration,
        float revealDuration = DefaultRevealDuration,
        float rewriteDuration = DefaultRewriteDuration,
        float finaleDuration = DefaultFinaleDuration,
        float resistanceDuration = DefaultResistanceDuration,
        float counterattackDuration = DefaultCounterattackDuration)
    {
        DetectionDuration = Mathf.Max(1f, detectionDuration);
        RevealDuration = Mathf.Max(1f, revealDuration);
        RewriteDuration = Mathf.Max(1f, rewriteDuration);
        FinaleDuration = Mathf.Max(5f, finaleDuration);
        ResistanceDuration = Mathf.Max(1f, resistanceDuration);
        CounterattackDuration = Mathf.Max(1f, counterattackDuration);
        Phase = hasOpponent
            ? EchoDuelPhase.Detection
            : EchoDuelPhase.Calibration;
    }

    public bool Tick(float deltaTime, float estimatedRemainingSeconds,
        EchoContractData contract)
    {
        float dt = Mathf.Max(0f, deltaTime);
        RunElapsed += dt;
        PhaseElapsed += dt;

        EchoDuelPhase next = Phase;
        switch (Phase)
        {
            case EchoDuelPhase.Detection:
                if (PhaseElapsed >= DetectionDuration
                    || (contract != null
                        && contract.detectionGroupsResolved >= 6))
                    next = EchoDuelPhase.Reveal;
                break;
            case EchoDuelPhase.Reveal:
                if (PhaseElapsed >= RevealDuration)
                    next = EchoDuelPhase.Resistance;
                break;
            case EchoDuelPhase.Resistance:
                if (PhaseElapsed >= ResistanceDuration
                    || (contract != null
                        && contract.resistanceGroupsResolved >= 3))
                    next = EchoDuelPhase.Counterattack;
                break;
            case EchoDuelPhase.Counterattack:
                if (PhaseElapsed >= CounterattackDuration
                    || (contract != null
                        && contract.counterattackGroupsResolved >= 4))
                    next = EchoDuelPhase.Rewrite;
                break;
            case EchoDuelPhase.Rewrite:
                // Rewrite remains the long pursuit phase. Only its opening
                // window receives boosted learning; the finale is reserved for
                // the configured final seconds instead of consuming half a run.
                if (ShouldEnterFinale(estimatedRemainingSeconds))
                    next = EchoDuelPhase.Finale;
                break;
        }

        return TransitionTo(next);
    }

    public bool TransitionTo(EchoDuelPhase next)
    {
        if (next == EchoDuelPhase.None || next == Phase) return false;
        Phase = next;
        PhaseElapsed = 0f;
        PhaseSequence++;
        return true;
    }

    public float PhaseProgress01
    {
        get
        {
            switch (Phase)
            {
                case EchoDuelPhase.Detection:
                    return Mathf.Clamp01(PhaseElapsed / DetectionDuration);
                case EchoDuelPhase.Reveal:
                    return Mathf.Clamp01(PhaseElapsed / RevealDuration);
                case EchoDuelPhase.Resistance:
                    return Mathf.Clamp01(PhaseElapsed / ResistanceDuration);
                case EchoDuelPhase.Counterattack:
                    return Mathf.Clamp01(PhaseElapsed / CounterattackDuration);
                case EchoDuelPhase.Rewrite:
                    return Mathf.Clamp01(PhaseElapsed / RewriteDuration);
                default:
                    return 0f;
            }
        }
    }

    private bool ShouldEnterFinale(float estimatedRemainingSeconds)
    {
        return estimatedRemainingSeconds >= 0f
               && estimatedRemainingSeconds <= FinaleDuration;
    }

    public static string PhaseName(EchoDuelPhase phase)
    {
        switch (phase)
        {
            case EchoDuelPhase.Calibration: return "校准";
            case EchoDuelPhase.Detection: return "侦测";
            case EchoDuelPhase.Reveal: return "暴露";
            case EchoDuelPhase.Resistance: return "反抗";
            case EchoDuelPhase.Counterattack: return "反扑";
            case EchoDuelPhase.Rewrite: return "重写";
            case EchoDuelPhase.Finale: return "决胜";
            case EchoDuelPhase.Finished: return "结算";
            default: return "回声决斗";
        }
    }
}

public static class EchoTimeRules
{
    public static float SecondsToDistance(float seconds, float speed)
    {
        return Mathf.Max(0f, seconds) * Mathf.Max(1f, speed);
    }

    public static float MinimumSpacingDistance(float seconds, float speed)
    {
        return SecondsToDistance(Mathf.Max(0.1f, seconds), speed);
    }

    public static float EstimateRemainingSeconds(float remainingDistance,
        float currentSpeed)
    {
        return Mathf.Max(0f, remainingDistance) / Mathf.Max(1f, currentSpeed);
    }

    public static float DistanceForAcceleratingRun(float initialSpeed,
        float maximumSpeed, float acceleration, float duration)
    {
        float start = Mathf.Max(0f, initialSpeed);
        float maximum = Mathf.Max(start, maximumSpeed);
        float rate = Mathf.Max(0f, acceleration);
        float time = Mathf.Max(0f, duration);
        if (time <= 0f) return 0f;
        if (rate <= 0.0001f || maximum <= start)
            return start * time;

        float accelerationTime = (maximum - start) / rate;
        float rampTime = Mathf.Min(time, accelerationTime);
        float distance = start * rampTime
                         + 0.5f * rate * rampTime * rampTime;
        if (time > rampTime)
            distance += maximum * (time - rampTime);
        return distance;
    }
}
