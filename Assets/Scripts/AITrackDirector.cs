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

    private readonly System.Random _random;

    public AITrackPolicy(int seed = 1337)
    {
        _random = new System.Random(seed);
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
    [Range(0f, 0.5f)] public float explorationRate = 0.10f;
    public int observationSegments = 2;

    public AITrackPlan CurrentPlan { get; private set; }
    public int ModelUpdateCount { get; private set; }
    public string CurrentStatus { get; private set; } = "AI导演 · 等待开局";

    private static AITrackPolicy _sessionPolicy;
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
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        if (_sessionPolicy == null)
            _sessionPolicy = new AITrackPolicy(Environment.TickCount);
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
            _sessionPolicy = new AITrackPolicy(Environment.TickCount);

        _decisionCount++;
        TrainCompletedPlans(false);

        float[] context = BuildContext();
        AIDirectorIntent intent;
        if (!useAI || _decisionCount <= Mathf.Max(1, observationSegments))
        {
            intent = AIDirectorIntent.Observe;
        }
        else
        {
            int action = _sessionPolicy.Select(context, true, explorationRate);
            intent = (AIDirectorIntent)(action + 1);
            _pendingDecisions.Enqueue(new PendingDecision
            {
                action = action,
                context = (float[])context.Clone(),
                segmentEndDistance = segmentEndDistance
            });
        }

        CurrentPlan = BuildPlan(intent, baseDifficulty, baseObstacleChance,
            baseCoinChance, baseTurnChance, previousSafeLane, canTurn);
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

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.Playing && _decisionCount == 0)
            CurrentStatus = "AI导演 · 正在观察";
        else if (state == GameState.GameOver)
            TrainCompletedPlans(true);
    }

    private float[] BuildContext()
    {
        float distance = _gameManager != null ? _gameManager.Distance : 0f;
        float score = _gameManager != null ? _gameManager.Score : 0f;
        float highScore = _gameManager != null ? _gameManager.HighScore : 0f;
        float actionCount = _laneChanges + _jumps + _slides;

        float mastery = Mathf.Clamp01(distance / 220f + _dodges * 0.08f);
        float strain = Mathf.Clamp01(_hits * 0.8f
                       + Mathf.Max(0f, actionCount - distance / 8f) / 20f);
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

        _sessionPolicy.Update(decision.action, decision.context,
            Mathf.Clamp(reward, -1f, 1f), learningRate);
        ModelUpdateCount++;
        _evaluatedDistance = distance;
        _evaluatedCoins = _coins;
        _evaluatedDodges = _dodges;
        _evaluatedHits = _hits;
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
                          && UnityEngine.Random.value
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
