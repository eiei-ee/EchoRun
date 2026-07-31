using System;
using System.Collections.Generic;
using UnityEngine;

public enum AIDirectorIntent
{
    Observe,
    Recovery,
    Flow,
    Pressure,
    RecordPush
}

public struct AITrackPlan
{
    public AIDirectorIntent intent;
    public float difficulty;
    public float obstacleChance;
    public float coinChance;
    public int minCoinCount;
    public int maxCoinCount;
    public int maxBlockedLanes;
    public int safeLane;
    public bool shouldTurn;
}

// Online linear contextual bandit. Its weights are the runtime model and are
// updated from each completed stretch of play rather than from fixed rules.
public sealed class AITrackPolicy
{
    public const int FeatureCount = 5;
    public const int ActionCount = 4;

    private readonly float[,] _weights =
    {
        { 0.20f, -0.70f,  1.10f, 0.00f, 0.15f }, // Recovery
        { 0.45f,  0.15f,  0.20f, 0.10f, 0.30f }, // Flow
        {-0.10f,  0.95f, -0.60f, 0.45f, 0.35f }, // Pressure
        {-0.65f,  1.00f, -0.80f, 1.20f, 0.20f }  // Record push
    };

    private System.Random _random;

    public AITrackPolicy(int seed = 1337, float[] savedWeights = null)
    {
        _random = new System.Random(seed);
        if (savedWeights == null || savedWeights.Length != ActionCount * FeatureCount)
            return;

        int index = 0;
        for (int action = 0; action < ActionCount; action++)
            for (int feature = 0; feature < FeatureCount; feature++)
                _weights[action, feature] = savedWeights[index++];
    }

    public int Select(float[] context, bool explore, float explorationRate)
    {
        ValidateContext(context);

        if (explore && _random.NextDouble() < Mathf.Clamp01(explorationRate))
            return _random.Next(0, ActionCount);

        int bestAction = 0;
        float bestScore = Score(0, context);
        for (int action = 1; action < ActionCount; action++)
        {
            float score = Score(action, context);
            if (score > bestScore)
            {
                bestAction = action;
                bestScore = score;
            }
        }
        return bestAction;
    }

    public void ResetRandom(int seed)
    {
        _random = new System.Random(seed);
    }

    public void Update(int action, float[] context, float reward, float learningRate)
    {
        ValidateContext(context);
        if (action < 0 || action >= ActionCount)
            throw new ArgumentOutOfRangeException(nameof(action));

        float predictionError = Mathf.Clamp(reward, -1f, 1f) - Score(action, context);
        float rate = Mathf.Clamp(learningRate, 0.001f, 0.5f);
        for (int feature = 0; feature < FeatureCount; feature++)
        {
            float updated = _weights[action, feature]
                            + rate * predictionError * context[feature];
            _weights[action, feature] = Mathf.Clamp(updated, -3f, 3f);
        }
    }

    public float Score(int action, float[] context)
    {
        ValidateContext(context);
        if (action < 0 || action >= ActionCount)
            throw new ArgumentOutOfRangeException(nameof(action));

        float score = 0f;
        for (int feature = 0; feature < FeatureCount; feature++)
            score += _weights[action, feature] * context[feature];
        return score;
    }

    public float[] ExportWeights()
    {
        float[] result = new float[ActionCount * FeatureCount];
        int index = 0;
        for (int action = 0; action < ActionCount; action++)
            for (int feature = 0; feature < FeatureCount; feature++)
                result[index++] = _weights[action, feature];
        return result;
    }

    private static void ValidateContext(float[] context)
    {
        if (context == null || context.Length != FeatureCount)
            throw new ArgumentException("AI track context must contain five features.", nameof(context));
    }
}

public class AITrackDirector : MonoBehaviour
{
    public static AITrackDirector Instance { get; private set; }

    [Header("Runtime AI")]
    public bool useAI = true;
    [Range(0.001f, 0.5f)] public float learningRate = 0.08f;
    [Range(0f, 1f)] public float explorationRate = 0.35f;
    public int observationSegments = 2;

    public AITrackPlan CurrentPlan { get; private set; }
    public int ModelUpdateCount { get; private set; }
    public float LastPolicyMean { get; private set; }
    public float LastPolicyUncertainty { get; private set; }
    public bool LastDecisionSafetyAdjusted { get; private set; }
    public string CurrentStatus { get; private set; } = "AI导演 · 等待开局";

    private static AILinUcbPolicy _sessionPolicy;
    private GameManager _gameManager;
    private int _decisionCount;
    private int _evaluatedCoins;
    private int _evaluatedDodges;
    private int _evaluatedHits;
    private float _evaluatedDistance;
    private int _laneChanges;
    private int _jumps;
    private int _slides;
    private int _coins;
    private int _dodges;
    private int _hits;
    private readonly int[] _laneVisits = { 0, 1, 0 };
    private readonly Queue<PendingDecision> _pendingDecisions =
        new Queue<PendingDecision>();
    private sealed class PendingDecision
    {
        public int action;
        public float[] context;
        public float segmentEndDistance;
        public int telemetryDecisionId;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EchoRunSaveSystem.EnsureInitialized();
        explorationRate = GameBalanceConfig.Current.ai.directorExploration;
        if (_sessionPolicy == null)
        {
            _sessionPolicy = new AILinUcbPolicy(
                EchoRunSaveSystem.GetDirectorWeights(),
                EchoRunSaveSystem.GetDirectorPolicyJson());
        }
        ModelUpdateCount = EchoRunSaveSystem.DirectorModelUpdateCount;
    }

    void Start()
    {
        _gameManager = GameManager.Instance;
        if (_gameManager != null)
            _gameManager.OnStateChanged.AddListener(OnGameStateChanged);
    }

    public AITrackPlan CreatePlan(float baseDifficulty, float baseObstacleChance,
        float baseCoinChance, float baseTurnChance, int previousSafeLane, bool canTurn,
        float segmentEndDistance)
    {
        if (_sessionPolicy == null)
        {
            _sessionPolicy = new AILinUcbPolicy(
                EchoRunSaveSystem.GetDirectorWeights(),
                EchoRunSaveSystem.GetDirectorPolicyJson());
        }

        _decisionCount++;
        TrainCompletedPlans(false);

        float[] context = BuildContext();
        AIDirectorIntent intent;
        int proposedAction = -1;
        LastPolicyMean = 0f;
        LastPolicyUncertainty = 0f;
        LastDecisionSafetyAdjusted = false;
        PendingDecision pendingDecision = null;
        if (!useAI || _decisionCount <= Mathf.Max(1, observationSegments))
        {
            intent = AIDirectorIntent.Observe;
        }
        else
        {
            proposedAction = _sessionPolicy.Select(
                context, explorationRate);
            LastPolicyMean = _sessionPolicy.LastSelectedMean;
            LastPolicyUncertainty =
                _sessionPolicy.LastSelectedUncertainty;
            int action = ApplySafetyConstraints(
                proposedAction, context);
            LastDecisionSafetyAdjusted = action != proposedAction;
            intent = (AIDirectorIntent)(action + 1);
            pendingDecision = new PendingDecision
            {
                action = action,
                context = (float[])context.Clone(),
                segmentEndDistance = segmentEndDistance
            };
        }

        CurrentPlan = BuildPlan(intent, baseDifficulty, baseObstacleChance,
            baseCoinChance, baseTurnChance, previousSafeLane, canTurn);
        int telemetryDecisionId =
            AIRunTelemetry.RecordDirectorDecision(
                context, CurrentPlan, proposedAction,
                LastPolicyMean, LastPolicyUncertainty,
                LastDecisionSafetyAdjusted);
        if (pendingDecision != null)
        {
            pendingDecision.telemetryDecisionId = telemetryDecisionId;
            _pendingDecisions.Enqueue(pendingDecision);
        }
        CurrentStatus = BuildStatus(CurrentPlan);
        return CurrentPlan;
    }

    public void RecordLaneChange(int lane)
    {
        _laneChanges++;
        if (lane >= 0 && lane < _laneVisits.Length) _laneVisits[lane]++;
    }

    public void RecordJump() => _jumps++;
    public void RecordSlide() => _slides++;
    public void RecordCoin() => _coins++;
    public void RecordDodge() => _dodges++;
    public void RecordObstacleHit() => _hits++;

    public float[] GetModelWeightsSnapshot()
    {
        return _sessionPolicy != null
            ? _sessionPolicy.ExportWeights()
            : EchoRunSaveSystem.GetDirectorWeights();
    }

    public string GetPolicyStateSnapshot()
    {
        return _sessionPolicy != null
            ? _sessionPolicy.ExportStateJson()
            : EchoRunSaveSystem.GetDirectorPolicyJson();
    }

    public void ResetTraining()
    {
        _sessionPolicy = new AILinUcbPolicy();
        ModelUpdateCount = 0;
        LastPolicyMean = 0f;
        LastPolicyUncertainty = 0f;
        LastDecisionSafetyAdjusted = false;
        CurrentPlan = default;
        CurrentStatus = "AI导演 · 训练已重置";
        _pendingDecisions.Clear();
        _decisionCount = 0;
        _evaluatedCoins = _evaluatedDodges = _evaluatedHits = 0;
        _evaluatedDistance = 0f;
        _laneChanges = _jumps = _slides = _coins = _dodges = _hits = 0;
        _laneVisits[0] = 0;
        _laneVisits[1] = 1;
        _laneVisits[2] = 0;
        EchoRunSaveSystem.SaveDirector(null, 0, "");
    }

    private int ApplySafetyConstraints(int proposedAction, float[] context)
    {
        return ConstrainAction(proposedAction, context[2],
            AIPlayerSkillEstimator.Uncertainty,
            _hits > _evaluatedHits);
    }

    public static int ConstrainAction(int proposedAction, float strain,
        float skillUncertainty, bool recentHit)
    {
        int action = Mathf.Clamp(
            proposedAction, 0, AITrackPolicy.ActionCount - 1);
        if (recentHit) return 0;
        if (strain > 0.72f && action > 1) return 0;
        if (skillUncertainty > 0.72f && action > 1) return 1;
        return action;
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.Playing && _decisionCount == 0)
            CurrentStatus = "AI导演 · 正在观察";
        else if (state == GameState.GameOver)
        {
            TrainCompletedPlans(true);
            SaveDirectorModel();
        }
    }

    private float[] BuildContext()
    {
        float distance = _gameManager != null ? _gameManager.Distance : 0f;
        float score = _gameManager != null ? _gameManager.Score : 0f;
        float highScore = _gameManager != null ? _gameManager.HighScore : 0f;
        float actionCount = _laneChanges + _jumps + _slides;

        float liveMastery = Mathf.Clamp01(
            distance / 220f + _dodges * 0.08f);
        float skillConfidence = AIPlayerSkillEstimator.Confidence;
        float mastery = Mathf.Lerp(
            liveMastery, AIPlayerSkillEstimator.Skill, skillConfidence);
        float strain = Mathf.Clamp01(_hits * 0.8f
                       + Mathf.Max(0f, actionCount - distance / 8f) / 20f
                       + AIPlayerSkillEstimator.Uncertainty * 0.18f);
        float recordPressure = highScore > 0f
            ? Mathf.Clamp01(score / Mathf.Max(1f, highScore))
            : 0f;
        float engagement = Mathf.Clamp01((actionCount + _coins * 0.5f)
                                          / Mathf.Max(4f, distance / 8f));

        AIShadowRunner shadow = AIShadowRunner.Instance;
        if (shadow != null && shadow.HasActiveOpponent)
        {
            float normalizedLead = Mathf.Clamp(shadow.PlayerLead / 14f, -1f, 1f);
            mastery = Mathf.Clamp01(mastery + Mathf.Max(0f, normalizedLead) * 0.3f);
            strain = Mathf.Clamp01(strain + Mathf.Max(0f, -normalizedLead) * 0.4f);
            recordPressure = Mathf.Max(recordPressure, shadow.DuelPressure);
            engagement = Mathf.Clamp01(engagement + 0.2f);
        }

        return new[] { 1f, mastery, strain, recordPressure, engagement };
    }

    private void TrainCompletedPlans(bool includeFailedSegment)
    {
        if (_sessionPolicy == null || _pendingDecisions.Count == 0) return;

        float distance = _gameManager != null ? _gameManager.Distance : 0f;
        while (_pendingDecisions.Count > 0
               && _pendingDecisions.Peek().segmentEndDistance <= distance + 0.5f)
        {
            TrainDecision(_pendingDecisions.Dequeue(), distance);
        }

        // A collision happens inside a segment, before its end marker is reached.
        if (includeFailedSegment && _pendingDecisions.Count > 0 && _hits > _evaluatedHits)
            TrainDecision(_pendingDecisions.Dequeue(), distance);
    }

    private void TrainDecision(PendingDecision decision, float distance)
    {
        int coinGain = _coins - _evaluatedCoins;
        int dodgeGain = _dodges - _evaluatedDodges;
        int hitGain = _hits - _evaluatedHits;
        float distanceGain = Mathf.Max(0f, distance - _evaluatedDistance);

        float reward = 0.15f
                       + Mathf.Clamp01(distanceGain / 25f) * 0.35f
                       + Mathf.Clamp(coinGain * 0.08f, 0f, 0.32f)
                       + Mathf.Clamp(dodgeGain * 0.12f, 0f, 0.24f)
                       - hitGain * 1.25f;

        float clampedReward = Mathf.Clamp(reward, -1f, 1f);
        AIPlayerSkillEstimator.RecordSegmentOutcome(
            hitGain == 0, distanceGain);
        _sessionPolicy.Update(decision.action, decision.context,
            clampedReward, learningRate * 12.5f);
        ModelUpdateCount++;
        AIRunTelemetry.RecordDirectorOutcome(
            decision.telemetryDecisionId, clampedReward, ModelUpdateCount);
        SaveDirectorModel();
        _evaluatedDistance = distance;
        _evaluatedCoins = _coins;
        _evaluatedDodges = _dodges;
        _evaluatedHits = _hits;
    }

    private void SaveDirectorModel()
    {
        if (_sessionPolicy == null) return;
        EchoRunSaveSystem.SaveDirector(
            _sessionPolicy.ExportWeights(), ModelUpdateCount,
            _sessionPolicy.ExportStateJson());
    }

    private AITrackPlan BuildPlan(AIDirectorIntent intent, float baseDifficulty,
        float baseObstacleChance, float baseCoinChance, float baseTurnChance,
        int previousSafeLane, bool canTurn)
    {
        AITrackPlan plan = new AITrackPlan
        {
            intent = intent,
            difficulty = Mathf.Clamp01(baseDifficulty),
            obstacleChance = Mathf.Clamp01(baseObstacleChance),
            coinChance = Mathf.Clamp01(baseCoinChance),
            minCoinCount = 5,
            maxCoinCount = 8,
            maxBlockedLanes = baseDifficulty > 0.5f ? 2 : 1,
            safeLane = previousSafeLane,
            shouldTurn = false
        };

        float turnMultiplier = 1f;
        switch (intent)
        {
            case AIDirectorIntent.Observe:
                plan.difficulty = 0f;
                plan.obstacleChance = 0f;
                plan.coinChance = 0.8f;
                plan.minCoinCount = 5;
                plan.maxCoinCount = 7;
                plan.maxBlockedLanes = 1;
                turnMultiplier = 0f;
                break;
            case AIDirectorIntent.Recovery:
                plan.difficulty = Mathf.Min(plan.difficulty, 0.35f);
                plan.obstacleChance = Mathf.Min(plan.obstacleChance, 0.30f);
                plan.coinChance = Mathf.Max(plan.coinChance, 0.9f);
                plan.minCoinCount = 6;
                plan.maxCoinCount = 9;
                plan.maxBlockedLanes = 1;
                turnMultiplier = 0.45f;
                break;
            case AIDirectorIntent.Flow:
                plan.difficulty = Mathf.Clamp(plan.difficulty, 0.3f, 0.6f);
                plan.obstacleChance = Mathf.Clamp(plan.obstacleChance, 0.4f, 0.62f);
                plan.coinChance = Mathf.Max(plan.coinChance, 0.65f);
                turnMultiplier = 0.85f;
                break;
            case AIDirectorIntent.Pressure:
                plan.difficulty = Mathf.Max(plan.difficulty, 0.68f);
                plan.obstacleChance = Mathf.Max(plan.obstacleChance, 0.74f);
                plan.coinChance = Mathf.Min(plan.coinChance, 0.55f);
                plan.minCoinCount = 4;
                plan.maxCoinCount = 6;
                plan.maxBlockedLanes = 2;
                turnMultiplier = 1.35f;
                break;
            case AIDirectorIntent.RecordPush:
                plan.difficulty = Mathf.Max(plan.difficulty, 0.82f);
                plan.obstacleChance = Mathf.Max(plan.obstacleChance, 0.86f);
                plan.coinChance = Mathf.Min(plan.coinChance, 0.5f);
                plan.minCoinCount = 3;
                plan.maxCoinCount = 5;
                plan.maxBlockedLanes = 2;
                turnMultiplier = 1.75f;
                break;
        }

        plan.safeLane = ChooseSafeLane(intent, previousSafeLane);
        plan.shouldTurn = canTurn
                          && AIRunRandom.Value
                          < Mathf.Clamp01(baseTurnChance * turnMultiplier);
        return plan;
    }

    private int ChooseSafeLane(AIDirectorIntent intent, int previousSafeLane)
    {
        int targetLane = previousSafeLane;
        if (intent == AIDirectorIntent.Recovery)
            targetLane = IndexOfMostVisitedLane();
        else if (intent == AIDirectorIntent.Pressure || intent == AIDirectorIntent.RecordPush)
            targetLane = IndexOfLeastVisitedLane();
        else if (intent == AIDirectorIntent.Flow)
            targetLane = (_decisionCount / 2) % 3;

        return Mathf.Clamp(targetLane,
            Mathf.Max(0, previousSafeLane - 1),
            Mathf.Min(2, previousSafeLane + 1));
    }

    private int IndexOfMostVisitedLane()
    {
        int result = 0;
        for (int i = 1; i < _laneVisits.Length; i++)
            if (_laneVisits[i] > _laneVisits[result]) result = i;
        return result;
    }

    private int IndexOfLeastVisitedLane()
    {
        int result = 0;
        for (int i = 1; i < _laneVisits.Length; i++)
            if (_laneVisits[i] < _laneVisits[result]) result = i;
        return result;
    }

    private string BuildStatus(AITrackPlan plan)
    {
        string label;
        switch (plan.intent)
        {
            case AIDirectorIntent.Recovery: label = "恢复节奏"; break;
            case AIDirectorIntent.Flow: label = "保持流动"; break;
            case AIDirectorIntent.Pressure: label = "施加压力"; break;
            case AIDirectorIntent.RecordPush: label = "纪录冲刺"; break;
            default: label = "观察中"; break;
        }
        return string.Format("AI导演 · {0} {1:0}%", label, plan.difficulty * 100f);
    }

    void OnDestroy()
    {
        if (_gameManager != null)
            _gameManager.OnStateChanged.RemoveListener(OnGameStateChanged);
        if (Instance == this) Instance = null;
    }
}
