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
    public EchoContractType echoContractType;
    public int echoChallengeLane;
    public ShadowAction echoTargetAction;
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
        if (savedWeights == null) return;
        if (!AIModelWeightRules.TrySanitize(savedWeights,
                ActionCount * FeatureCount, -3f, 3f, out float[] sanitized))
        {
            Debug.LogWarning(
                "AI director weights were invalid and were reset to defaults.");
            return;
        }

        int index = 0;
        for (int action = 0; action < ActionCount; action++)
            for (int feature = 0; feature < FeatureCount; feature++)
                _weights[action, feature] = sanitized[index++];
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

public class AITrackDirector : MonoBehaviour, IShadowDirectiveSource
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
    public ShadowAIDirective CurrentShadowDirective { get; private set; }
        = ShadowAIDirective.Neutral;
    public string CurrentStatus { get; private set; } = "AI导演 · 等待开局";
    public float CurrentLaneIncentiveCenter
    {
        get
        {
            if (_activeDecision == null) return 1f;
            float safe = Mathf.Clamp(CurrentPlan.safeLane, 0, 2);
            if (CurrentPlan.echoChallengeLane < 0
                || CurrentPlan.echoChallengeLane > 2)
                return safe;
            return (safe + CurrentPlan.echoChallengeLane) * 0.5f;
        }
    }

    private static AILinUcbPolicy _sessionPolicy;
    private GameManager _gameManager;
    private int _decisionCount;
    private int _lastPlannedHitCount;
    private int _laneChanges;
    private int _jumps;
    private int _slides;
    private int _coins;
    private int _dodges;
    private int _hits;
    private float _lastHitDistance = float.NegativeInfinity;
    private readonly int[] _laneVisits = { 0, 1, 0 };
    private readonly Queue<PlannedDecision> _plannedDecisions =
        new Queue<PlannedDecision>();
    private PlannedDecision _activeDecision;

    private sealed class PlannedDecision
    {
        public int action;
        public float[] context;
        public AITrackPlan plan;
        public ShadowAIDirective directive;
        public float policyMean;
        public float policyUncertainty;
        public bool safetyAdjusted;
        public float segmentStartDistance;
        public float segmentEndDistance;
        public int telemetryDecisionId;
        public bool activated;
        public float activationDistance;
        public int coinsAtActivation;
        public int dodgesAtActivation;
        public int hitsAtActivation;
        public bool policyUpdateEligible;
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
        return CreatePlan(baseDifficulty, baseObstacleChance, baseCoinChance,
            baseTurnChance, previousSafeLane, canTurn,
            Mathf.Max(0f, segmentEndDistance - 20f), segmentEndDistance);
    }

    public AITrackPlan CreatePlan(float baseDifficulty, float baseObstacleChance,
        float baseCoinChance, float baseTurnChance, int previousSafeLane, bool canTurn,
        float segmentStartDistance, float segmentEndDistance)
    {
        if (_sessionPolicy == null)
        {
            _sessionPolicy = new AILinUcbPolicy(
                EchoRunSaveSystem.GetDirectorWeights(),
                EchoRunSaveSystem.GetDirectorPolicyJson());
        }

        _decisionCount++;

        float[] context = BuildContext();
        AIDirectorIntent intent;
        int proposedAction = -1;
        float policyMean = 0f;
        float policyUncertainty = 0f;
        bool safetyAdjusted = false;
        int selectedAction = -1;
        if (!useAI || IsObservationSegment(segmentStartDistance,
                segmentEndDistance, observationSegments))
        {
            intent = AIDirectorIntent.Observe;
        }
        else
        {
            proposedAction = _sessionPolicy.Select(
                context, explorationRate);
            policyMean = _sessionPolicy.LastSelectedMean;
            policyUncertainty =
                _sessionPolicy.LastSelectedUncertainty;
            int action = ApplySafetyConstraints(
                proposedAction, context);
            safetyAdjusted = action != proposedAction;
            selectedAction = action;
            intent = (AIDirectorIntent)(action + 1);
        }

        AITrackPlan plan = BuildPlan(intent, baseDifficulty, baseObstacleChance,
            baseCoinChance, baseTurnChance, previousSafeLane, canTurn);
        EchoContractData activeContract = AIShadowRunner.Instance != null
            ? AIShadowRunner.Instance.ActiveContract : null;
        plan = ApplyEchoContract(
            plan, activeContract,
            _decisionCount);
        ShadowAIDirective directive = BuildShadowDirective(intent);
        int telemetryDecisionId =
            AIRunTelemetry.RecordDirectorDecision(
                context, plan, proposedAction,
                policyMean, policyUncertainty,
                safetyAdjusted, segmentStartDistance, segmentEndDistance);
        _plannedDecisions.Enqueue(new PlannedDecision
        {
            action = selectedAction,
            context = (float[])context.Clone(),
            plan = plan,
            directive = directive,
            policyMean = policyMean,
            policyUncertainty = policyUncertainty,
            safetyAdjusted = safetyAdjusted,
            segmentStartDistance = Mathf.Max(0f, segmentStartDistance),
            segmentEndDistance = Mathf.Max(segmentStartDistance,
                segmentEndDistance),
            telemetryDecisionId = telemetryDecisionId,
            policyUpdateEligible = selectedAction >= 0
                                   && IsPolicyAttributionEligible(
                                       activeContract)
        });
        _lastPlannedHitCount = _hits;
        return plan;
    }

    public void ActivatePlanForDistance(float distance)
    {
        float routeDistance = Mathf.Max(0f, distance);
        while (_plannedDecisions.Count > 0
               && _plannedDecisions.Peek().segmentEndDistance
               <= routeDistance + 0.01f)
        {
            PlannedDecision completed = _plannedDecisions.Dequeue();
            if (ReferenceEquals(_activeDecision, completed))
            {
                ResolveDecision(completed, routeDistance);
                _activeDecision = null;
            }
        }

        if (_activeDecision != null || _plannedDecisions.Count == 0) return;

        PlannedDecision candidate = _plannedDecisions.Peek();
        if (candidate.segmentStartDistance > routeDistance + 0.5f
            || candidate.segmentEndDistance < routeDistance - 0.01f)
            return;

        ActivateDecision(candidate, routeDistance);
    }

    public void FinalizeActivePlanForRunEnd(float distance)
    {
        if (_activeDecision != null)
            ResolveDecision(_activeDecision, Mathf.Max(0f, distance));
        _activeDecision = null;
        _plannedDecisions.Clear();
        CurrentShadowDirective = ShadowAIDirective.Neutral;
    }

    public void RecordLaneChange(int lane)
    {
        _laneChanges++;
        if (lane >= 0 && lane < _laneVisits.Length) _laneVisits[lane]++;
    }

    public void RecordJump() => _jumps++;
    public void RecordSlide() => _slides++;
    public void RecordCoin()
    {
        ActivateAtCurrentDistance();
        _coins++;
    }

    public void RecordDodge()
    {
        ActivateAtCurrentDistance();
        _dodges++;
    }

    public void RecordObstacleHit()
    {
        ActivateAtCurrentDistance();
        _hits++;
        _lastHitDistance = _gameManager != null ? _gameManager.Distance : 0f;
    }

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
        CurrentShadowDirective = ShadowAIDirective.Neutral;
        CurrentStatus = "AI导演 · 训练已重置";
        _plannedDecisions.Clear();
        _activeDecision = null;
        _decisionCount = 0;
        _lastPlannedHitCount = 0;
        _lastHitDistance = float.NegativeInfinity;
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
            _hits > _lastPlannedHitCount);
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
            float distance = _gameManager != null ? _gameManager.Distance : 0f;
            FinalizeActivePlanForRunEnd(distance);
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
        float strain = Mathf.Clamp01(CalculateRecentHitStrain(
                           distance, _lastHitDistance)
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

    public static float CalculateRecentHitStrain(float distance,
        float lastHitDistance, float recoveryDistance = 60f)
    {
        if (float.IsNaN(lastHitDistance)
            || float.IsInfinity(lastHitDistance))
            return 0f;
        float sinceHit = Mathf.Max(0f, distance - lastHitDistance);
        return (1f - Mathf.Clamp01(
            sinceHit / Mathf.Max(1f, recoveryDistance))) * 0.8f;
    }

    private void ActivateDecision(PlannedDecision decision, float distance)
    {
        decision.activated = true;
        decision.activationDistance = Mathf.Clamp(distance,
            decision.segmentStartDistance, decision.segmentEndDistance);
        decision.coinsAtActivation = _coins;
        decision.dodgesAtActivation = _dodges;
        decision.hitsAtActivation = _hits;
        _activeDecision = decision;
        CurrentPlan = decision.plan;
        CurrentShadowDirective = decision.directive;
        LastPolicyMean = decision.policyMean;
        LastPolicyUncertainty = decision.policyUncertainty;
        LastDecisionSafetyAdjusted = decision.safetyAdjusted;
        CurrentStatus = BuildStatus(decision.plan);
        AIRunTelemetry.RecordDirectorActivation(
            decision.telemetryDecisionId, decision.activationDistance);
    }

    private void ActivateAtCurrentDistance()
    {
        ActivatePlanForDistance(
            _gameManager != null ? _gameManager.Distance : 0f);
    }

    private void ResolveDecision(PlannedDecision decision, float distance)
    {
        if (!decision.activated) return;
        int coinGain = Mathf.Max(0, _coins - decision.coinsAtActivation);
        int dodgeGain = Mathf.Max(0, _dodges - decision.dodgesAtActivation);
        int hitGain = Mathf.Max(0, _hits - decision.hitsAtActivation);
        float evaluatedDistance = Mathf.Clamp(distance,
            decision.segmentStartDistance, decision.segmentEndDistance);
        float distanceGain = Mathf.Max(0f,
            evaluatedDistance - decision.activationDistance);

        AIPlayerSkillEstimator.RecordSegmentOutcome(
            hitGain == 0, distanceGain);
        if (decision.action < 0 || _sessionPolicy == null
            || !decision.policyUpdateEligible) return;

        float reward = 0.15f
                       + Mathf.Clamp01(distanceGain / 25f) * 0.35f
                       + Mathf.Clamp(coinGain * 0.08f, 0f, 0.32f)
                       + Mathf.Clamp(dodgeGain * 0.12f, 0f, 0.24f)
                       - hitGain * 1.25f;

        float clampedReward = Mathf.Clamp(reward, -1f, 1f);
        _sessionPolicy.Update(decision.action, decision.context,
            clampedReward, learningRate * 12.5f);
        ModelUpdateCount++;
        AIRunTelemetry.RecordDirectorOutcome(
            decision.telemetryDecisionId, clampedReward, ModelUpdateCount);
        SaveDirectorModel();
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
            shouldTurn = false,
            echoContractType = EchoContractType.None,
            echoChallengeLane = -1,
            echoTargetAction = ShadowAction.Keep
        };

        float turnMultiplier = TurnMultiplierForIntent(intent);
        switch (intent)
        {
            case AIDirectorIntent.Observe:
                plan.difficulty = 0f;
                plan.obstacleChance = 0f;
                plan.coinChance = 0.8f;
                plan.minCoinCount = 5;
                plan.maxCoinCount = 7;
                plan.maxBlockedLanes = 1;
                break;
            case AIDirectorIntent.Recovery:
                plan.difficulty = Mathf.Min(plan.difficulty, 0.35f);
                plan.obstacleChance = Mathf.Min(plan.obstacleChance, 0.30f);
                plan.coinChance = Mathf.Max(plan.coinChance, 0.9f);
                plan.minCoinCount = 6;
                plan.maxCoinCount = 9;
                plan.maxBlockedLanes = 1;
                break;
            case AIDirectorIntent.Flow:
                plan.difficulty = Mathf.Clamp(plan.difficulty, 0.3f, 0.6f);
                plan.obstacleChance = Mathf.Clamp(plan.obstacleChance, 0.4f, 0.62f);
                plan.coinChance = Mathf.Max(plan.coinChance, 0.65f);
                break;
            case AIDirectorIntent.Pressure:
                plan.difficulty = Mathf.Max(plan.difficulty, 0.68f);
                plan.obstacleChance = Mathf.Max(plan.obstacleChance, 0.74f);
                plan.coinChance = Mathf.Min(plan.coinChance, 0.55f);
                plan.minCoinCount = 4;
                plan.maxCoinCount = 6;
                plan.maxBlockedLanes = 2;
                break;
            case AIDirectorIntent.RecordPush:
                plan.difficulty = Mathf.Max(plan.difficulty, 0.82f);
                plan.obstacleChance = Mathf.Max(plan.obstacleChance, 0.86f);
                plan.coinChance = Mathf.Min(plan.coinChance, 0.5f);
                plan.minCoinCount = 3;
                plan.maxCoinCount = 5;
                plan.maxBlockedLanes = 2;
                break;
        }

        plan.safeLane = ChooseSafeLane(intent, previousSafeLane);
        plan.shouldTurn = canTurn
                          && AIRunRandom.Value
                          < Mathf.Clamp01(baseTurnChance * turnMultiplier);
        return plan;
    }

    public static bool IsObservationSegment(float segmentStartDistance,
        float segmentEndDistance, int observationSegments)
    {
        int count = Mathf.Max(0, observationSegments);
        if (count == 0) return false;
        float length = Mathf.Max(1f,
            segmentEndDistance - segmentStartDistance);
        return Mathf.Max(0f, segmentStartDistance) < length * count;
    }

    public static bool IsPolicyAttributionEligible(
        EchoContractData activeContract)
    {
        return activeContract == null
               || activeContract.type == EchoContractType.None;
    }

    public static float TurnMultiplierForIntent(AIDirectorIntent intent)
    {
        switch (intent)
        {
            case AIDirectorIntent.Observe: return 0f;
            case AIDirectorIntent.Recovery: return 1.15f;
            case AIDirectorIntent.Flow: return 0.85f;
            case AIDirectorIntent.Pressure: return 0.4f;
            case AIDirectorIntent.RecordPush: return 0.2f;
            default: return 1f;
        }
    }

    public static AITrackPlan ApplyEchoContract(AITrackPlan plan,
        EchoContractData contract, int decisionCount)
    {
        if (contract == null || contract.type == EchoContractType.None)
            return plan;

        EchoDuelPhase phase = contract.duelPhase == EchoDuelPhase.None
            ? EchoDuelPhase.Resistance : contract.duelPhase;
        if (phase == EchoDuelPhase.Detection
            || phase == EchoDuelPhase.Reveal)
        {
            plan.echoContractType = EchoContractType.None;
            plan.obstacleChance = Mathf.Min(plan.obstacleChance, 0.35f);
            plan.coinChance = Mathf.Max(plan.coinChance, 0.78f);
            plan.maxBlockedLanes = 1;
            plan.shouldTurn = false;
            return plan;
        }

        if (phase == EchoDuelPhase.Rewrite)
        {
            // The player is authoring a new style, so provide several readable
            // routes instead of continuing to order a single counter-action.
            plan.echoContractType = EchoContractType.None;
            plan.safeLane = Mathf.Abs(decisionCount) % 3;
            plan.obstacleChance = Mathf.Clamp(plan.obstacleChance, 0.42f, 0.58f);
            plan.coinChance = Mathf.Max(plan.coinChance, 0.82f);
            plan.maxBlockedLanes = 1;
            plan.shouldTurn = false;
            return plan;
        }

        plan.echoContractType = contract.type;
        plan.echoTargetAction = contract.targetAction;
        plan.shouldTurn = false;
        if (phase == EchoDuelPhase.Counterattack)
        {
            plan.safeLane = Mathf.Abs(decisionCount + contract.generation) % 3;
            plan.obstacleChance = Mathf.Max(plan.obstacleChance, 0.8f);
            plan.coinChance = Mathf.Max(plan.coinChance, 0.78f);
            plan.maxBlockedLanes = Mathf.Max(1, plan.maxBlockedLanes);
        }
        else if (phase == EchoDuelPhase.Finale)
        {
            plan.difficulty = Mathf.Max(plan.difficulty, 0.78f);
            plan.obstacleChance = Mathf.Max(plan.obstacleChance, 0.84f);
            plan.coinChance = Mathf.Max(plan.coinChance, 0.72f);
            plan.maxBlockedLanes = 2;
        }
        if (contract.type == EchoContractType.BreakLaneHabit)
        {
            if (phase != EchoDuelPhase.Counterattack
                && phase != EchoDuelPhase.Finale)
                plan.safeLane = Mathf.Clamp(contract.targetLane, 0, 2);
            plan.coinChance = Mathf.Max(plan.coinChance, 0.9f);
            plan.minCoinCount = Mathf.Max(plan.minCoinCount, 7);
            plan.maxCoinCount = Mathf.Max(plan.maxCoinCount, 10);
            return plan;
        }

        int safeLane = Mathf.Clamp(plan.safeLane, 0, 2);
        plan.echoChallengeLane = Mathf.Clamp(contract.targetLane, 0, 2);
        if (safeLane == plan.echoChallengeLane)
        {
            safeLane = (safeLane + 1) % 3;
            plan.safeLane = safeLane;
        }
        plan.obstacleChance = Mathf.Max(plan.obstacleChance, 0.72f);
        plan.coinChance = Mathf.Max(plan.coinChance, 0.8f);
        plan.maxBlockedLanes = Mathf.Max(1, plan.maxBlockedLanes);
        return plan;
    }

    public static ShadowAIDirective BuildShadowDirective(
        AIDirectorIntent intent)
    {
        ShadowAIDirective directive = ShadowAIDirective.Neutral;
        switch (intent)
        {
            case AIDirectorIntent.Observe:
                directive.styleInfluence = 1f;
                directive.riskBias = 0f;
                directive.decisionNoise = 0.05f;
                break;
            case AIDirectorIntent.Recovery:
                directive.styleInfluence = 1f;
                directive.riskBias = -0.1f;
                directive.decisionNoise = 0.04f;
                break;
            case AIDirectorIntent.Flow:
                directive.styleInfluence = 1f;
                directive.riskBias = 0f;
                directive.decisionNoise = 0.06f;
                break;
            case AIDirectorIntent.Pressure:
                directive.styleInfluence = 1f;
                directive.riskBias = 0.08f;
                directive.decisionNoise = 0.08f;
                break;
            case AIDirectorIntent.RecordPush:
                directive.styleInfluence = 1f;
                directive.riskBias = 0.12f;
                directive.decisionNoise = 0.06f;
                break;
        }
        return directive.Normalized();
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
        string contract = plan.echoContractType != EchoContractType.None
            ? " · 契约改写赛道"
            : "";
        return string.Format("AI导演 · {0} {1:0}%{2}",
            label, plan.difficulty * 100f, contract);
    }

    void OnDestroy()
    {
        if (_gameManager != null)
            _gameManager.OnStateChanged.RemoveListener(OnGameStateChanged);
        if (Instance == this) Instance = null;
    }
}
