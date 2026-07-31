using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class AIRunStateSample
{
    public float time;
    public float distance;
    public float speed;
    public int score;
    public int coins;
    public int lane;
    public bool jumping;
    public bool sliding;
    public float playerLead;
}

[Serializable]
public sealed class AIRunEventSample
{
    public float time;
    public float distance;
    public string type;
    public int action;
    public int lane;
    public float value;
    public float value2;
}

[Serializable]
public sealed class AIDirectorDecisionSample
{
    public int id;
    public float time;
    public float distance;
    public int intent;
    public int proposedIntent;
    public float[] context;
    public float policyMean;
    public float policyUncertainty;
    public bool safetyAdjusted;
    public float difficulty;
    public float obstacleChance;
    public float coinChance;
    public int safeLane;
    public int maxBlockedLanes;
    public bool shouldTurn;
    public bool trained;
    public float reward;
    public int modelUpdateCount;
}

[Serializable]
public sealed class AIShadowTrainingSample
{
    public float time;
    public float distance;
    public int action;
    public int lane;
    public bool opponentDecision;
    public float confidence;
    public float[] features;
    public int baseAction;
    public float sequenceConfidence;
    public float sequenceInfluence;
}

[Serializable]
public sealed class AIRunTelemetryData
{
    public int schemaVersion = AIRunTelemetry.SchemaVersion;
    public string runId;
    public int seed;
    public long startedUtcTicks;
    public long endedUtcTicks;
    public string buildVersion;
    public string platform;
    public string finishReason;
    public bool completed;
    public int highScoreBeforeRun;
    public int shadowGenerationAtStart;
    public int directorUpdatesAtStart;
    public float playerSkillAtStart;
    public float skillConfidenceAtStart;
    public float[] shadowWeightsAtStart;
    public string shadowSequenceStateAtStart;
    public float[] directorWeightsAtStart;
    public string directorPolicyStateAtStart;
    public float duration;
    public float distance;
    public int score;
    public int coins;
    public int shadowGenerationAtEnd;
    public int directorUpdatesAtEnd;
    public float playerSkillAtEnd;
    public float skillConfidenceAtEnd;
    public float[] shadowWeightsAtEnd;
    public string shadowSequenceStateAtEnd;
    public float[] directorWeightsAtEnd;
    public string directorPolicyStateAtEnd;
    public List<AIRunStateSample> states = new List<AIRunStateSample>();
    public List<AIRunEventSample> events = new List<AIRunEventSample>();
    public List<AIDirectorDecisionSample> directorDecisions =
        new List<AIDirectorDecisionSample>();
    public List<AIShadowTrainingSample> shadowSamples =
        new List<AIShadowTrainingSample>();
}

public static class AIRunTelemetry
{
    public const int SchemaVersion = 3;
    public const float StateSampleInterval = 0.25f;

    private const int MaxStateSamples = 7200;
    private const int MaxEventSamples = 4096;
    private const int MaxShadowSamples = 8192;

    private static AIRunTelemetryData _active;
    private static float _nextStateSampleTime;
    private static float _runStartTime;
    private static int _nextDecisionId;

    public static AIRunTelemetryData ActiveRun => _active;
    public static bool IsRecording => _active != null && !_active.completed;

    public static void BeginRun(int seed, int sequence, int highScore,
        int shadowGeneration, int directorUpdates, float[] shadowWeights,
        float[] directorWeights, string directorPolicyState,
        string shadowSequenceState = "")
    {
        long now = DateTime.UtcNow.Ticks;
        _active = new AIRunTelemetryData
        {
            runId = seed.ToString("X8") + "-" + sequence.ToString("D6"),
            seed = seed,
            startedUtcTicks = now,
            buildVersion = Application.version,
            platform = Application.platform.ToString(),
            highScoreBeforeRun = Mathf.Max(0, highScore),
            shadowGenerationAtStart = Mathf.Max(0, shadowGeneration),
            directorUpdatesAtStart = Mathf.Max(0, directorUpdates),
            playerSkillAtStart = AIPlayerSkillEstimator.Skill,
            skillConfidenceAtStart = AIPlayerSkillEstimator.Confidence,
            shadowWeightsAtStart = Clone(shadowWeights),
            shadowSequenceStateAtStart = shadowSequenceState ?? "",
            directorWeightsAtStart = Clone(directorWeights),
            directorPolicyStateAtStart = directorPolicyState ?? ""
        };
        _runStartTime = Time.time;
        _nextStateSampleTime = 0f;
        _nextDecisionId = 1;
        RecordEvent("run_start", 0, 1, seed, sequence);
    }

    public static void Tick(GameManager gameManager, PlayerController player)
    {
        if (!IsRecording || gameManager == null) return;
        float elapsed = ElapsedTime();
        if (elapsed + 0.0001f < _nextStateSampleTime) return;
        _nextStateSampleTime = elapsed + StateSampleInterval;
        if (_active.states.Count >= MaxStateSamples) return;

        AIShadowRunner shadow = AIShadowRunner.Instance;
        _active.states.Add(new AIRunStateSample
        {
            time = elapsed,
            distance = gameManager.Distance,
            speed = gameManager.CurrentSpeed,
            score = gameManager.Score,
            coins = gameManager.Coins,
            lane = player != null ? player.CurrentLane : 1,
            jumping = player != null && player.IsJumping,
            sliding = player != null && player.IsSliding,
            playerLead = shadow != null && shadow.HasActiveOpponent
                ? shadow.PlayerLead
                : 0f
        });
    }

    public static void RecordEvent(string type, int action = 0, int lane = -1,
        float value = 0f, float value2 = 0f)
    {
        if (!IsRecording || _active.events.Count >= MaxEventSamples) return;
        _active.events.Add(new AIRunEventSample
        {
            time = ElapsedTime(),
            distance = CurrentDistance(),
            type = type ?? "",
            action = action,
            lane = lane,
            value = value,
            value2 = value2
        });
    }

    public static int RecordDirectorDecision(float[] context, AITrackPlan plan)
    {
        int proposedAction = plan.intent == AIDirectorIntent.Observe
            ? -1
            : (int)plan.intent - 1;
        return RecordDirectorDecision(
            context, plan, proposedAction, 0f, 0f, false);
    }

    public static int RecordDirectorDecision(float[] context,
        AITrackPlan plan, int proposedAction, float policyMean,
        float policyUncertainty, bool safetyAdjusted)
    {
        if (!IsRecording) return 0;
        int id = _nextDecisionId++;
        _active.directorDecisions.Add(new AIDirectorDecisionSample
        {
            id = id,
            time = ElapsedTime(),
            distance = CurrentDistance(),
            intent = (int)plan.intent,
            proposedIntent = proposedAction >= 0
                ? proposedAction + 1
                : (int)AIDirectorIntent.Observe,
            context = Clone(context),
            policyMean = policyMean,
            policyUncertainty = Mathf.Max(0f, policyUncertainty),
            safetyAdjusted = safetyAdjusted,
            difficulty = plan.difficulty,
            obstacleChance = plan.obstacleChance,
            coinChance = plan.coinChance,
            safeLane = plan.safeLane,
            maxBlockedLanes = plan.maxBlockedLanes,
            shouldTurn = plan.shouldTurn
        });
        return id;
    }

    public static void RecordDirectorOutcome(int decisionId, float reward,
        int modelUpdateCount)
    {
        if (!IsRecording || decisionId <= 0) return;
        for (int i = _active.directorDecisions.Count - 1; i >= 0; i--)
        {
            AIDirectorDecisionSample sample = _active.directorDecisions[i];
            if (sample.id != decisionId) continue;
            sample.trained = true;
            sample.reward = reward;
            sample.modelUpdateCount = modelUpdateCount;
            return;
        }
    }

    public static void RecordShadowSample(ShadowAction action, int lane,
        float[] features, bool opponentDecision, float confidence)
    {
        RecordShadowSample(action, lane, features, opponentDecision, confidence,
            (int)action, 0f, 0f);
    }

    public static void RecordShadowSample(ShadowAction action, int lane,
        float[] features, bool opponentDecision, float confidence, int baseAction,
        float sequenceConfidence, float sequenceInfluence)
    {
        if (!IsRecording || _active.shadowSamples.Count >= MaxShadowSamples) return;
        _active.shadowSamples.Add(new AIShadowTrainingSample
        {
            time = ElapsedTime(),
            distance = CurrentDistance(),
            action = (int)action,
            lane = Mathf.Clamp(lane, 0, 2),
            opponentDecision = opponentDecision,
            confidence = Mathf.Clamp01(confidence),
            features = Clone(features),
            baseAction = Mathf.Clamp(baseAction, 0, AIShadowPolicy.ActionCount - 1),
            sequenceConfidence = Mathf.Clamp01(sequenceConfidence),
            sequenceInfluence = Mathf.Clamp01(sequenceInfluence)
        });
    }

    public static string FinishRun(GameManager gameManager, string reason,
        int shadowGeneration, int directorUpdates, float[] shadowWeights,
        float[] directorWeights, string directorPolicyState,
        string shadowSequenceState = "")
    {
        if (!IsRecording) return GetLatestRunJson();

        _active.completed = true;
        _active.endedUtcTicks = DateTime.UtcNow.Ticks;
        _active.finishReason = reason ?? "";
        _active.duration = ElapsedTime();
        if (gameManager != null)
        {
            _active.distance = gameManager.Distance;
            _active.score = gameManager.Score;
            _active.coins = gameManager.Coins;
        }
        _active.shadowGenerationAtEnd = Mathf.Max(0, shadowGeneration);
        _active.directorUpdatesAtEnd = Mathf.Max(0, directorUpdates);
        _active.playerSkillAtEnd = AIPlayerSkillEstimator.Skill;
        _active.skillConfidenceAtEnd = AIPlayerSkillEstimator.Confidence;
        _active.shadowWeightsAtEnd = Clone(shadowWeights);
        _active.shadowSequenceStateAtEnd = shadowSequenceState ?? "";
        _active.directorWeightsAtEnd = Clone(directorWeights);
        _active.directorPolicyStateAtEnd =
            directorPolicyState ?? "";

        string json = JsonUtility.ToJson(_active);
        EchoRunSaveSystem.SaveLastRunTelemetry(json);
        return json;
    }

    public static string GetLatestRunJson()
    {
        if (_active != null) return JsonUtility.ToJson(_active);
        return EchoRunSaveSystem.GetLastRunTelemetryJson();
    }

    public static AIRunTelemetryData FromJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        AIRunTelemetryData data = JsonUtility.FromJson<AIRunTelemetryData>(json);
        if (data == null || data.schemaVersion <= 0) return null;
        data.states = data.states ?? new List<AIRunStateSample>();
        data.events = data.events ?? new List<AIRunEventSample>();
        data.directorDecisions = data.directorDecisions
                                 ?? new List<AIDirectorDecisionSample>();
        data.shadowSamples = data.shadowSamples ?? new List<AIShadowTrainingSample>();
        return data;
    }

    public static string ExportLatestRun(string directory = null)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return "";
#else
        string json = GetLatestRunJson();
        if (string.IsNullOrEmpty(json)) return "";
        AIRunTelemetryData data = FromJson(json);
        string runId = data != null && !string.IsNullOrEmpty(data.runId)
            ? data.runId
            : "latest";
        string targetDirectory = string.IsNullOrEmpty(directory)
            ? Path.Combine(Application.persistentDataPath, "TrainingData")
            : directory;
        Directory.CreateDirectory(targetDirectory);
        string path = Path.Combine(targetDirectory,
            "echo-run-" + runId + ".json");
        File.WriteAllText(path, json);
        return path;
#endif
    }

    private static float CurrentDistance()
    {
        return GameManager.Instance != null ? GameManager.Instance.Distance : 0f;
    }

    private static float ElapsedTime()
    {
        return Mathf.Max(0f, Time.time - _runStartTime);
    }

    private static float[] Clone(float[] values)
    {
        return values == null ? null : (float[])values.Clone();
    }
}
