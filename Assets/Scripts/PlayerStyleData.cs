using System;
using UnityEngine;

[Serializable]
public sealed class PlayerStyleData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    [Range(0f, 1f)] public float aggressiveness = 0.5f;
    [Range(-1f, 1f)] public float jumpTiming;
    [Range(0f, 1f)] public float slideFrequency = 0.5f;
    [Range(-1f, 1f)] public float lanePreference;
    [Range(0f, 1f)] public float rhythmStability = 0.5f;
    [Range(0f, 1f)] public float recoveryStyle = 0.5f;

    public int aggressivenessSamples;
    public int jumpTimingSamples;
    public int slideOpportunitySamples;
    public int laneSamples;
    public int rhythmSamples;
    public int recoverySamples;

    public float Confidence
    {
        get
        {
            float weightedSamples = aggressivenessSamples
                                    + jumpTimingSamples
                                    + slideOpportunitySamples
                                    + laneSamples * 0.1f
                                    + rhythmSamples
                                    + recoverySamples * 2f;
            return Mathf.Clamp01(weightedSamples / 48f);
        }
    }

    public void Normalize()
    {
        version = CurrentVersion;
        aggressiveness = Mathf.Clamp01(aggressiveness);
        jumpTiming = Mathf.Clamp(jumpTiming, -1f, 1f);
        slideFrequency = Mathf.Clamp01(slideFrequency);
        lanePreference = Mathf.Clamp(lanePreference, -1f, 1f);
        rhythmStability = Mathf.Clamp01(rhythmStability);
        recoveryStyle = Mathf.Clamp01(recoveryStyle);
        aggressivenessSamples = Mathf.Max(0, aggressivenessSamples);
        jumpTimingSamples = Mathf.Max(0, jumpTimingSamples);
        slideOpportunitySamples = Mathf.Max(0, slideOpportunitySamples);
        laneSamples = Mathf.Max(0, laneSamples);
        rhythmSamples = Mathf.Max(0, rhythmSamples);
        recoverySamples = Mathf.Max(0, recoverySamples);
    }

    public PlayerStyleData Clone()
    {
        return JsonUtility.FromJson<PlayerStyleData>(JsonUtility.ToJson(this));
    }

    public void ObserveAggressiveness(float normalizedRisk)
    {
        aggressiveness = UpdateAverage(aggressiveness,
            Mathf.Clamp01(normalizedRisk), aggressivenessSamples++);
    }

    public void ObserveJumpTiming(float normalizedTimingOffset)
    {
        jumpTiming = UpdateAverage(jumpTiming,
            Mathf.Clamp(normalizedTimingOffset, -1f, 1f), jumpTimingSamples++);
    }

    public void ObserveSlideOpportunity(bool usedSlide)
    {
        slideFrequency = UpdateAverage(slideFrequency,
            usedSlide ? 1f : 0f, slideOpportunitySamples++);
    }

    public void ObserveLane(int lane)
    {
        lanePreference = UpdateAverage(lanePreference,
            Mathf.Clamp(lane, 0, 2) - 1f, laneSamples++);
    }

    public void ObserveRhythm(float normalizedStability)
    {
        rhythmStability = UpdateAverage(rhythmStability,
            Mathf.Clamp01(normalizedStability), rhythmSamples++);
    }

    public void ObserveRecovery(float normalizedUrgency)
    {
        recoveryStyle = UpdateAverage(recoveryStyle,
            Mathf.Clamp01(normalizedUrgency), recoverySamples++);
    }

    private static float UpdateAverage(float current, float observation,
        int previousSamples)
    {
        // Fast enough to adapt during a run, slow enough not to swing on one input.
        float rate = Mathf.Lerp(0.24f, 0.08f,
            Mathf.Clamp01(previousSamples / 24f));
        return Mathf.Lerp(current, observation, rate);
    }
}
