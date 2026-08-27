using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class EchoIdentityStyleSnapshot
{
    public int version = PlayerStyleData.CurrentVersion;
    public float aggressiveness = 0.5f;
    public float jumpTiming;
    public float slideFrequency = 0.5f;
    public float slideOpportunitySuccess = 0.5f;
    public float lanePreference;
    public float rhythmStability = 0.5f;
    public float recoveryStyle = 0.5f;
    public int aggressivenessSamples;
    public int jumpTimingSamples;
    public int verticalActionSamples;
    public int jumpActionSamples;
    public int slideActionSamples;
    public int slideOpportunitySamples;
    public int laneSamples;
    public int rhythmSamples;
    public int recoverySamples;

    public static EchoIdentityStyleSnapshot FromPlayerStyle(PlayerStyleData source)
    {
        PlayerStyleData style = source != null ? source.Clone() : new PlayerStyleData();
        style.Normalize();
        return new EchoIdentityStyleSnapshot
        {
            version = style.version,
            aggressiveness = style.aggressiveness,
            jumpTiming = style.jumpTiming,
            slideFrequency = style.slideFrequency,
            slideOpportunitySuccess = style.slideOpportunitySuccess,
            lanePreference = style.lanePreference,
            rhythmStability = style.rhythmStability,
            recoveryStyle = style.recoveryStyle,
            aggressivenessSamples = style.aggressivenessSamples,
            jumpTimingSamples = style.jumpTimingSamples,
            verticalActionSamples = style.verticalActionSamples,
            jumpActionSamples = style.jumpActionSamples,
            slideActionSamples = style.slideActionSamples,
            slideOpportunitySamples = style.slideOpportunitySamples,
            laneSamples = style.laneSamples,
            rhythmSamples = style.rhythmSamples,
            recoverySamples = style.recoverySamples
        };
    }

    public PlayerStyleData ToPlayerStyle()
    {
        var style = new PlayerStyleData
        {
            version = version,
            aggressiveness = aggressiveness,
            jumpTiming = jumpTiming,
            slideFrequency = slideFrequency,
            slideOpportunitySuccess = slideOpportunitySuccess,
            lanePreference = lanePreference,
            rhythmStability = rhythmStability,
            recoveryStyle = recoveryStyle,
            aggressivenessSamples = aggressivenessSamples,
            jumpTimingSamples = jumpTimingSamples,
            verticalActionSamples = verticalActionSamples,
            jumpActionSamples = jumpActionSamples,
            slideActionSamples = slideActionSamples,
            slideOpportunitySamples = slideOpportunitySamples,
            laneSamples = laneSamples,
            rhythmSamples = rhythmSamples,
            recoverySamples = recoverySamples
        };
        style.Normalize();
        return style;
    }

    public EchoIdentityStyleSnapshot Clone()
    {
        return FromPlayerStyle(ToPlayerStyle());
    }
}

[Serializable]
public sealed class EchoMemoryContract
{
    public const int CurrentVersion = 1;
    public const float PreciseDescriptionConfidence = 0.6f;

    public int version = CurrentVersion;
    public string contractId = "";
    public string identityId = "";
    public int preferredLane = 1;
    public float confidence;
    public int evidenceCount;

    public bool HasPreciseRouteMemory => evidenceCount >= 3
                                         && confidence >= PreciseDescriptionConfidence;

    public EchoMemoryContract Clone()
    {
        return JsonUtility.FromJson<EchoMemoryContract>(JsonUtility.ToJson(this));
    }

    public void Normalize()
    {
        version = CurrentVersion;
        contractId = contractId ?? "";
        identityId = identityId ?? "";
        preferredLane = Mathf.Clamp(preferredLane, 0, 2);
        confidence = IsFinite(confidence) ? Mathf.Clamp01(confidence) : 0f;
        evidenceCount = Mathf.Max(0, evidenceCount);
    }

    public string BuildMemoryText()
    {
        if (!HasPreciseRouteMemory)
            return "回声记忆模糊\n你的选择尚未形成稳定模式";

        string lane = preferredLane == 0 ? "左侧"
            : preferredLane == 2 ? "右侧" : "中间";
        return "压力出现时，你偏向" + lane;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

[Serializable]
public sealed class ActiveEchoIdentity
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int generation;
    public string identityId = "";
    public string parentIdentityId = "";
    public int sourceRunSequence;
    public float[] policyWeights;
    public float[] sequenceTransitions;
    public int sequencePairCount;
    public EchoIdentityStyleSnapshot style = new EchoIdentityStyleSnapshot();
    public float pace;
    public float sourceCourseDuration;
    public float clarity = 1f;
    public EchoMemoryContract memoryContract;

    public bool RequiresCompatibilityCalibration => memoryContract == null;
    public bool RequiresRouteCalibration => memoryContract == null
                                            || !memoryContract
                                                .HasPreciseRouteMemory;

    public ActiveEchoIdentity Clone()
    {
        return FromJson(ToJson());
    }

    public string ToJson()
    {
        Normalize();
        return JsonUtility.ToJson(this);
    }

    public string ComputeHash()
    {
        return StableHash.ComputeHex(ToJson());
    }

    public PlayerStyleData GetPlayerStyle()
    {
        return (style ?? new EchoIdentityStyleSnapshot()).ToPlayerStyle();
    }

    public void Normalize()
    {
        version = CurrentVersion;
        generation = Mathf.Max(0, generation);
        identityId = identityId ?? "";
        parentIdentityId = parentIdentityId ?? "";
        sourceRunSequence = Mathf.Max(0, sourceRunSequence);
        policyWeights = CloneArray(policyWeights);
        sequenceTransitions = CloneArray(sequenceTransitions);
        sequencePairCount = Mathf.Max(0, sequencePairCount);
        style = (style ?? new EchoIdentityStyleSnapshot()).Clone();
        pace = IsFinite(pace) ? Mathf.Max(0f, pace) : 0f;
        sourceCourseDuration = IsFinite(sourceCourseDuration)
            ? Mathf.Max(0f, sourceCourseDuration) : 0f;
        clarity = IsFinite(clarity) ? Mathf.Clamp01(clarity) : 0f;
        if (memoryContract != null)
        {
            memoryContract = memoryContract.Clone();
            memoryContract.Normalize();
            if (string.IsNullOrEmpty(memoryContract.contractId)
                && string.IsNullOrEmpty(memoryContract.identityId)
                && memoryContract.evidenceCount == 0
                && memoryContract.confidence == 0f)
                memoryContract = null;
        }
    }

    public bool IsSemanticallyValid()
    {
        Normalize();
        if (generation <= 0 || string.IsNullOrEmpty(identityId)
            || pace <= 0f || style == null)
            return false;
        if (generation == 1 && !string.IsNullOrEmpty(parentIdentityId))
            return false;
        if (generation > 1 && string.IsNullOrEmpty(parentIdentityId))
            return false;
        return memoryContract == null
               || !string.IsNullOrEmpty(memoryContract.contractId)
               && string.Equals(memoryContract.identityId, identityId,
                   StringComparison.Ordinal);
    }

    public static ActiveEchoIdentity FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            ActiveEchoIdentity identity = JsonUtility.FromJson<ActiveEchoIdentity>(json);
            if (identity == null) return null;
            identity.Normalize();
            return identity;
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static ActiveEchoIdentity FromLegacySnapshot(
        EchoGenerationSnapshot snapshot, int sourceRunSequence)
    {
        if (snapshot == null || snapshot.generation <= 0 || snapshot.pace <= 0f)
            return null;
        EchoGenerationSnapshot frozen = snapshot.Clone();
        var identity = new ActiveEchoIdentity
        {
            generation = frozen.generation,
            parentIdentityId = frozen.generation > 1 ? "legacy-unknown" : "",
            sourceRunSequence = Mathf.Max(0, sourceRunSequence),
            policyWeights = frozen.policyWeights,
            sequenceTransitions = frozen.sequenceTransitions,
            sequencePairCount = frozen.sequencePairCount,
            style = EchoIdentityStyleSnapshot.FromPlayerStyle(frozen.GetStyle()),
            pace = frozen.pace,
            clarity = frozen.clarity,
            memoryContract = null
        };
        identity.identityId = "legacy-" + StableHash.ComputeHex(frozen.ToJson()).ToLowerInvariant();
        identity.Normalize();
        return identity;
    }

    public static string CreateIdentityId(ActiveEchoIdentity identity)
    {
        if (identity == null) return "";
        ActiveEchoIdentity seed = identity.CloneWithoutIdentityOwnership();
        return "echo-" + StableHash.ComputeHex(JsonUtility.ToJson(seed)).ToLowerInvariant();
    }

    private ActiveEchoIdentity CloneWithoutIdentityOwnership()
    {
        ActiveEchoIdentity clone = FromJson(JsonUtility.ToJson(this))
                                   ?? new ActiveEchoIdentity();
        clone.identityId = "";
        if (clone.memoryContract != null)
            clone.memoryContract.identityId = "";
        return clone;
    }

    private static float[] CloneArray(float[] source)
    {
        return source == null ? null : (float[])source.Clone();
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

public static class SingleContractValidationIdentity
{
    public const int Generation = 1;
    public const int PreferredLane = 2;

    public static bool IsEnabled(SingleContractValidationConfig validation)
    {
        return validation != null
               && validation.enabled
               && validation.useFixedIdentity;
    }

    public static ActiveEchoIdentity Create()
    {
        AIShadowSequenceState sequence =
            new AIShadowSequencePolicy().ExportState();
        var style = new PlayerStyleData
        {
            lanePreference = 1f,
            laneSamples = 5
        };
        style.Normalize();
        float duration = SingleContractFlow.CalibrationDurationSeconds;
        var identity = new ActiveEchoIdentity
        {
            generation = Generation,
            sourceRunSequence = 0,
            policyWeights = new AIShadowPolicy().ExportWeights(),
            sequenceTransitions = sequence.transitions,
            sequencePairCount = sequence.pairCount,
            style = EchoIdentityStyleSnapshot.FromPlayerStyle(style),
            pace = EchoTimeRules.DistanceForAcceleratingRun(
                       10f, 40f, 0.5f, duration) / duration,
            sourceCourseDuration = duration,
            clarity = 1f,
            memoryContract = new EchoMemoryContract
            {
                contractId = "route-validation-fixed-v1",
                preferredLane = PreferredLane,
                confidence = 1f,
                evidenceCount = 5
            }
        };
        identity.identityId = ActiveEchoIdentity.CreateIdentityId(identity);
        identity.memoryContract.identityId = identity.identityId;
        identity.Normalize();
        return identity;
    }
}

public sealed class EchoIdentityStyleAccumulator
{
    private const float RecoveryWindowDuration = 10f;

    private readonly PlayerStyleData _style;
    private float _rhythmProximityMean;
    private float _rhythmProximityM2;
    private int _rhythmActionSamples;
    private float _recoveryTimeRemaining;
    private int _recoveryActions;
    private float _recoveryRiskTotal;

    public EchoIdentityStyleAccumulator(PlayerStyleData initialStyle = null)
    {
        _style = initialStyle != null ? initialStyle.Clone() : new PlayerStyleData();
        _style.Normalize();
    }

    public PlayerStyleData Snapshot()
    {
        return _style.Clone();
    }

    public void RecordAction(ShadowAction action, int physicalLane)
    {
        RecordAction(action, physicalLane, 0f, 0f, false,
            action == ShadowAction.Jump || action == ShadowAction.Slide);
    }

    public void RecordAction(ShadowAction action, int physicalLane,
        float threatProximity, float jumpTimingOffset,
        bool airLaneChange, bool matchedActionObstacle)
    {
        _style.ObserveLane(Mathf.Clamp(physicalLane, 0, 2));
        if (action == ShadowAction.Keep) return;

        float proximity = Mathf.Clamp01(threatProximity);
        if (proximity > 0.05f || airLaneChange)
        {
            _style.ObserveAggressiveness(airLaneChange
                ? 1f
                : Mathf.InverseLerp(0.35f, 0.95f, proximity));
        }
        if (action == ShadowAction.Jump && proximity > 0.05f)
            _style.ObserveJumpTiming(jumpTimingOffset);
        if (matchedActionObstacle
            && (action == ShadowAction.Jump
                || action == ShadowAction.Slide))
            _style.ObserveVerticalAction(action);

        if ((action == ShadowAction.Jump || action == ShadowAction.Slide)
            && proximity > 0.05f)
        {
            _rhythmActionSamples++;
            float delta = proximity - _rhythmProximityMean;
            _rhythmProximityMean += delta / _rhythmActionSamples;
            _rhythmProximityM2 += delta
                                  * (proximity - _rhythmProximityMean);
            if (_rhythmActionSamples >= 2)
            {
                float deviation = Mathf.Sqrt(_rhythmProximityM2
                    / Mathf.Max(1, _rhythmActionSamples - 1));
                _style.ObserveRhythm(
                    1f - Mathf.Clamp01(deviation / 0.25f));
            }
        }

        if (_recoveryTimeRemaining > 0f)
        {
            _recoveryActions++;
            _recoveryRiskTotal += proximity;
        }
    }

    public void RecordObstacleOpportunity(ObstacleType type,
        bool usedRequiredAction)
    {
        if (type == ObstacleType.Low)
            _style.ObserveSlideOpportunity(usedRequiredAction);
    }

    public void RecordMistake()
    {
        _recoveryTimeRemaining = RecoveryWindowDuration;
        _recoveryActions = 0;
        _recoveryRiskTotal = 0f;
    }

    public void Tick(float deltaTime)
    {
        if (_recoveryTimeRemaining <= 0f) return;
        _recoveryTimeRemaining -= Mathf.Max(0f, deltaTime);
        if (_recoveryTimeRemaining <= 0f)
            CommitRecoveryObservation();
    }

    public void FinalizeRun()
    {
        if (_recoveryTimeRemaining > 0f && _recoveryActions > 0)
            CommitRecoveryObservation();
    }

    private void CommitRecoveryObservation()
    {
        float actionUrgency = Mathf.Clamp01(_recoveryActions / 6f);
        float averageRisk = _recoveryActions > 0
            ? _recoveryRiskTotal / _recoveryActions : 0f;
        _style.ObserveRecovery(actionUrgency * 0.65f
                               + averageRisk * 0.35f);
        _recoveryTimeRemaining = 0f;
        _recoveryActions = 0;
        _recoveryRiskTotal = 0f;
    }
}

public sealed class GateChoiceAccumulator
{
    private readonly HashSet<int> _recordedGateIds = new HashSet<int>();
    private readonly int[] _choiceCounts = new int[3];
    private readonly int[] _successfulCounts = new int[3];

    public int FormalChoiceCount { get; private set; }
    public int SuccessfulExecutionCount { get; private set; }

    public bool Record(int gateId, int lane, bool executionSucceeded)
    {
        if (gateId <= 0 || lane < 0 || lane >= _choiceCounts.Length
            || !_recordedGateIds.Add(gateId))
            return false;
        _choiceCounts[lane]++;
        FormalChoiceCount++;
        if (executionSucceeded)
        {
            _successfulCounts[lane]++;
            SuccessfulExecutionCount++;
        }
        return true;
    }

    public int ChoiceCountForLane(int lane)
    {
        return _choiceCounts[Mathf.Clamp(lane, 0, 2)];
    }

    public bool TryBuildMemoryContract(out EchoMemoryContract contract)
    {
        return TryBuildMemoryContract(true, out contract);
    }

    public bool TryBuildMemoryContract(bool requirePreciseMemory,
        out EchoMemoryContract contract)
    {
        contract = null;
        if (FormalChoiceCount < 5
            || requirePreciseMemory && SuccessfulExecutionCount < 3)
            return false;

        int preferredLane = 0;
        for (int lane = 1; lane < _choiceCounts.Length; lane++)
        {
            if (_choiceCounts[lane] > _choiceCounts[preferredLane])
                preferredLane = lane;
        }
        int evidence = _choiceCounts[preferredLane];
        if (requirePreciseMemory && evidence < 3) return false;

        contract = new EchoMemoryContract
        {
            preferredLane = preferredLane,
            evidenceCount = evidence,
            confidence = evidence / (float)Mathf.Max(1, FormalChoiceCount)
        };
        contract.contractId = "route-" + StableHash.ComputeHex(
            _choiceCounts[0] + ":" + _choiceCounts[1] + ":"
            + _choiceCounts[2] + ":" + SuccessfulExecutionCount + ":"
            + FormalChoiceCount)
            .ToLowerInvariant();
        contract.Normalize();
        return !requirePreciseMemory || contract.HasPreciseRouteMemory;
    }
}

public sealed class RunIdentityDraft
{
    public string baseIdentityId = "";
    public int baseGeneration;
    public int runSequence;
    public AIShadowPolicy policy;
    public AIShadowSequencePolicy sequence;
    public EchoIdentityStyleAccumulator style;
    public GateChoiceAccumulator gateChoices;
    public float physicalPace;
    public float sourceCourseDuration;
    public int effectiveSamples;
    public int sampleCount;
    public int activeSampleCount;
    public int[] actionCounts = new int[AIShadowPolicy.ActionCount];
    public bool IsDiscarded => _discarded;

    private ActiveEchoIdentity _frozenBaseIdentity;
    private bool _discarded;

    public static RunIdentityDraft Create(ActiveEchoIdentity baseIdentity,
        int runSequence)
    {
        ActiveEchoIdentity frozenBase = baseIdentity != null
            ? ActiveEchoIdentity.FromJson(JsonUtility.ToJson(baseIdentity))
            : null;
        return new RunIdentityDraft
        {
            baseIdentityId = frozenBase != null ? frozenBase.identityId : "",
            baseGeneration = frozenBase != null ? frozenBase.generation : 0,
            runSequence = Mathf.Max(0, runSequence),
            policy = new AIShadowPolicy(frozenBase != null
                ? frozenBase.policyWeights : null),
            sequence = new AIShadowSequencePolicy(frozenBase != null
                    ? frozenBase.sequenceTransitions : null,
                frozenBase != null ? frozenBase.sequencePairCount : 0),
            style = new EchoIdentityStyleAccumulator(frozenBase != null
                ? frozenBase.GetPlayerStyle() : null),
            gateChoices = new GateChoiceAccumulator(),
            physicalPace = frozenBase != null ? frozenBase.pace : 0f,
            sourceCourseDuration = frozenBase != null
                ? frozenBase.sourceCourseDuration : 0f,
            actionCounts = new int[AIShadowPolicy.ActionCount],
            _frozenBaseIdentity = frozenBase
        };
    }

    public void RecordSample(ShadowAction action)
    {
        RecordSample(action, 1);
    }

    public void RecordSample(ShadowAction action, int physicalLane)
    {
        RecordSample(action, physicalLane, 0f, 0f, false,
            action == ShadowAction.Jump || action == ShadowAction.Slide);
    }

    public void RecordSample(ShadowAction action, int physicalLane,
        float threatProximity, float jumpTimingOffset,
        bool airLaneChange, bool matchedActionObstacle)
    {
        if (_discarded) return;
        sampleCount++;
        int actionIndex = Mathf.Clamp((int)action, 0,
            AIShadowPolicy.ActionCount - 1);
        if (actionCounts == null
            || actionCounts.Length != AIShadowPolicy.ActionCount)
            actionCounts = new int[AIShadowPolicy.ActionCount];
        actionCounts[actionIndex]++;
        style?.RecordAction(action, physicalLane, threatProximity,
            jumpTimingOffset, airLaneChange, matchedActionObstacle);
        if (action == ShadowAction.Keep) return;
        activeSampleCount++;
        effectiveSamples++;
    }

    public void TickStyle(float deltaTime)
    {
        if (!_discarded) style?.Tick(deltaTime);
    }

    public void RecordStyleObstacleOpportunity(ObstacleType type,
        bool usedRequiredAction)
    {
        if (!_discarded)
            style?.RecordObstacleOpportunity(type, usedRequiredAction);
    }

    public void RecordStyleMistake()
    {
        if (!_discarded) style?.RecordMistake();
    }

    public void FinalizeStyle()
    {
        if (!_discarded) style?.FinalizeRun();
    }

    public bool RecordFormalGateChoice(int gateId, int physicalLane,
        bool executionSucceeded)
    {
        return !_discarded && gateChoices != null
               && gateChoices.Record(
                   gateId, physicalLane, executionSucceeded);
    }

    public bool IsCalibrationPromotionReady(int minimumTotalSamples,
        int minimumActiveSamples, int minimumActionCategories,
        int minimumJumpSamples, int minimumSlideSamples)
    {
        return _frozenBaseIdentity == null
               && HasCalibrationPromotionEvidence(
                   minimumTotalSamples, minimumActiveSamples,
                   minimumActionCategories, minimumJumpSamples,
                   minimumSlideSamples);
    }

    public bool TryBuildCalibrationPromotion(bool runCompleted, float clarity,
        int minimumTotalSamples, int minimumActiveSamples,
        int minimumActionCategories, int minimumJumpSamples,
        int minimumSlideSamples, out ActiveEchoIdentity identity)
    {
        identity = null;
        if (!runCompleted || !IsCalibrationPromotionReady(
                minimumTotalSamples, minimumActiveSamples,
                minimumActionCategories, minimumJumpSamples,
                minimumSlideSamples))
            return false;
        return TryBuildIdentity(null, clarity, true, out identity);
    }

    public bool TryBuildCompatibilityCalibrationPromotion(bool runCompleted,
        float clarity, int minimumTotalSamples, int minimumActiveSamples,
        int minimumActionCategories, int minimumJumpSamples,
        int minimumSlideSamples, out ActiveEchoIdentity identity)
    {
        identity = null;
        if (!runCompleted || _frozenBaseIdentity == null
            || !_frozenBaseIdentity.RequiresRouteCalibration
            || !HasCalibrationPromotionEvidence(
                minimumTotalSamples, minimumActiveSamples,
                minimumActionCategories, minimumJumpSamples,
                minimumSlideSamples))
            return false;
        return TryBuildIdentity(
            _frozenBaseIdentity, clarity, true, out identity);
    }

    public bool TryBuildChallengePromotion(bool playerWon, float clarity,
        out ActiveEchoIdentity identity)
    {
        identity = null;
        if (!playerWon || _discarded || _frozenBaseIdentity == null
            || _frozenBaseIdentity.RequiresRouteCalibration
            || !string.Equals(baseIdentityId,
                _frozenBaseIdentity.identityId, StringComparison.Ordinal)
            || baseGeneration != _frozenBaseIdentity.generation)
            return false;
        return TryBuildIdentity(
            _frozenBaseIdentity, clarity, false, out identity);
    }

    public void Discard()
    {
        _discarded = true;
        policy = null;
        sequence = null;
        style = null;
        gateChoices = null;
        actionCounts = null;
        physicalPace = 0f;
        sourceCourseDuration = 0f;
        effectiveSamples = 0;
        sampleCount = 0;
        activeSampleCount = 0;
    }

    private bool HasCalibrationPromotionEvidence(int minimumTotalSamples,
        int minimumActiveSamples, int minimumActionCategories,
        int minimumJumpSamples, int minimumSlideSamples)
    {
        return !_discarded && runSequence > 0 && physicalPace > 0f
               && policy != null && sequence != null && style != null
               && gateChoices != null
               && AIShadowRules.HasCalibrationSamples(
                   sampleCount, activeSampleCount, actionCounts,
                   minimumTotalSamples, minimumActiveSamples,
                   minimumActionCategories, minimumJumpSamples,
                   minimumSlideSamples)
               && gateChoices.TryBuildMemoryContract(out _);
    }

    private bool TryBuildIdentity(ActiveEchoIdentity frozenBase,
        float clarity, bool requirePreciseMemory,
        out ActiveEchoIdentity identity)
    {
        identity = null;
        if (_discarded || runSequence <= 0 || policy == null
            || sequence == null || style == null || gateChoices == null
            || physicalPace <= 0f
            || !gateChoices.TryBuildMemoryContract(
                requirePreciseMemory, out EchoMemoryContract contract))
            return false;

        AIShadowSequenceState sequenceState = sequence.ExportState();
        identity = frozenBase != null
            ? ActiveEchoIdentity.FromJson(JsonUtility.ToJson(frozenBase))
            : new ActiveEchoIdentity();
        if (identity == null) return false;
        identity.generation = frozenBase != null
            ? frozenBase.generation + 1 : 1;
        identity.parentIdentityId = frozenBase != null
            ? frozenBase.identityId : "";
        identity.sourceRunSequence = runSequence;
        identity.policyWeights = policy.ExportWeights();
        identity.sequenceTransitions = sequenceState.transitions;
        identity.sequencePairCount = sequenceState.pairCount;
        identity.style = EchoIdentityStyleSnapshot.FromPlayerStyle(
            style.Snapshot());
        identity.pace = physicalPace;
        identity.sourceCourseDuration = sourceCourseDuration;
        identity.clarity = Mathf.Clamp01(clarity);
        identity.memoryContract = contract;
        identity.identityId = ActiveEchoIdentity.CreateIdentityId(identity);
        identity.memoryContract.identityId = identity.identityId;
        identity.Normalize();
        if (identity.IsSemanticallyValid()) return true;
        identity = null;
        return false;
    }
}

public sealed class RunAdaptationState
{
    public string contractId = "";
    public bool relearnUsed;
    public int hypothesisVersion;
    public int predictedStrategy;
    public int consecutiveSuccessfulCounters;
    public int resolvedGateCount;
}

[Serializable]
public sealed class EchoRetryState
{
    public string identityId = "";
    public string contractId = "";
    public int attemptCount;

    public EchoRetryState Clone()
    {
        return JsonUtility.FromJson<EchoRetryState>(JsonUtility.ToJson(this));
    }

    public void Normalize()
    {
        identityId = identityId ?? "";
        contractId = contractId ?? "";
        attemptCount = Mathf.Max(0, attemptCount);
        if (string.IsNullOrEmpty(identityId) || string.IsNullOrEmpty(contractId))
        {
            identityId = "";
            contractId = "";
            attemptCount = 0;
        }
    }
}
