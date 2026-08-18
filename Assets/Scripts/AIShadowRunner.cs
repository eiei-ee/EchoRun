using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class AIShadowRunner : MonoBehaviour
{
    public static AIShadowRunner Instance { get; private set; }

    [Header("Behavior Cloning")]
    [Range(0.001f, 0.5f)] public float learningRate = 0.08f;
    public int minimumTrainingSamples = 24;
    public int minimumActiveTrainingSamples = 6;
    public int minimumActionCategories = 2;
    public int minimumJumpSamples = 2;
    public int minimumSlideSamples = 2;
    public float decisionInterval = 0.35f;
    public float keepSampleInterval = 0.7f;
    public float minimumLaneHoldTime = 0.65f;

    [Header("Visual Smoothing")]
    public float laneSmoothTime = 0.14f;
    public float distanceSmoothTime = 0.12f;

    [Header("Duel")]
    public float shadowPaceMultiplier = 1.02f;
    public float maximumVisibleLead = 16f;

    [Header("Diagnostics")]
    public bool enableEmergencyReflex = true;

    public string CurrentStatus { get; private set; } = "AI影子 · 等待校准";
    public string LastResult { get; private set; } = "";
    public float PlayerLead { get; private set; }
    public bool HasActiveOpponent { get; private set; }
    public bool LastRunWasChallenge { get; private set; }
    public bool LastRunWon { get; private set; }
    public int Generation => _activeGeneration != null
        ? _activeGeneration.generation
        : _profile != null ? _profile.generation : 0;
    public int TrainingSampleCount => _profile != null ? _profile.sampleCount : 0;
    public int ActiveTrainingSampleCount =>
        _profile != null ? _profile.activeSampleCount : 0;
    public int JumpTrainingSampleCount => GetActionSampleCount(ShadowAction.Jump);
    public int SlideTrainingSampleCount => GetActionSampleCount(ShadowAction.Slide);
    public float CalibrationProgress => _profile == null || HasActiveOpponent
        ? 0f
        : CalculateCalibrationProgress(
            _profile.sampleCount, _profile.activeSampleCount,
            _profile.actionCounts, minimumTrainingSamples,
            minimumActiveTrainingSamples, minimumActionCategories,
            minimumJumpSamples, minimumSlideSamples);
    public float EchoClarity => _activeGeneration != null
        ? Mathf.Clamp01(_activeGeneration.clarity)
        : _profile != null ? Mathf.Clamp01(_profile.clarity) : 0f;
    public EchoDuelPhase DuelPhase => _duelFlow != null
        ? _duelFlow.Phase
        : HasActiveOpponent ? EchoDuelPhase.Detection : EchoDuelPhase.Calibration;
    public float DuelPhaseProgress => _duelFlow != null
        ? _duelFlow.PhaseProgress01 : 0f;
    public int DuelPhaseSequence => _duelFlow != null
        ? _duelFlow.PhaseSequence : 0;
    public string PublicPrediction => HasActiveOpponent
        ? _duelEvidence.BuildPredictionText(
            DuelPhase == EchoDuelPhase.Counterattack ? "新预判：" : "回声预判：")
        : "";
    public string PublicEvidence => HasActiveOpponent
        ? _duelEvidence.BuildEvidenceText() : "";
    public EchoPredictionSnapshot CurrentPrediction => _duelEvidence.Prediction;
    public string PublicChallenge
    {
        get
        {
            EchoContractData contract = ActiveContract;
            if (TrackManager.Instance != null && _player != null
                && TrackManager.Instance.TryGetUpcomingChoiceGroup(
                    _player.transform.position, _player.ForwardDirection,
                    out EchoChoiceGroup group))
                return EchoRunPresentation.BuildChoiceGroupChallenge(group);
            bool usesVerticalObstacle = contract != null
                && (contract.type == EchoContractType.ChangeVerticalHabit
                    || contract.type == EchoContractType.DisruptRhythm);
            if (usesVerticalObstacle && TrackManager.Instance != null
                && _player != null
                && TrackManager.Instance.TryGetUpcomingObstacleInLane(
                    _player.transform.position, _player.ForwardDirection,
                    contract.targetLane, null, out _, out ObstacleType type,
                    out _))
                return ResolvePublicChallenge(contract, true, type);
            return ResolvePublicChallenge(contract, false, ObstacleType.Low);
        }
    }
    public EchoContractData ActiveContract =>
        _contractEvaluator != null ? _contractEvaluator.Contract : null;
    public EchoContractData ContractPreview => _activeGeneration != null
        && _activeGeneration.generation > 0
        ? EchoContractPolicy.CreateForRun(_activeGeneration.GetStyle(),
            _activeGeneration.generation,
            EchoRunSaveSystem.GetLastEchoContractJson())
        : null;
    public float DuelPressure => HasActiveOpponent
        ? 1f - Mathf.Clamp01(Mathf.Abs(PlayerLead) / 14f)
        : 0f;
    public ShadowDecisionTrace LastDecisionTrace { get; private set; }
    public int PolicyCorrectDecisionCount { get; private set; }
    public int SafetyOverrideDecisionCount { get; private set; }
    public int EmergencyReflexSaveCount { get; private set; }
    public bool EmergencyReflexEnabled => enableEmergencyReflex;

    private const int SamplesPerCheckpoint = 4;

    [Serializable]
    private sealed class ShadowProfile
    {
        public int version;
        public int generation;
        public int sampleCount;
        public int activeSampleCount;
        public int[] actionCounts = new int[5];
        public float pace;
        public float bestProgress;
        public float[] weights;
        public float[] sequenceTransitions;
        public int sequencePairCount;
        public float clarity;
        public string activeGenerationJson;
    }

    private ShadowProfile _profile;
    private EchoGenerationSnapshot _activeGeneration;
    private AIShadowPolicy _policy;
    private AIShadowPolicy _opponentPolicy;
    private AIShadowSequencePolicy _sequencePolicy;
    private AIShadowSequencePolicy _opponentSequencePolicy;
    private readonly ShadowDecisionMaker _decisionMaker =
        new ShadowDecisionMaker();
    private PlayerStyleData _opponentStyle;
    private EchoContractEvaluator _contractEvaluator;
    private EchoDuelFlow _duelFlow;
    private IShadowDirectiveSource _directiveSource;
    private System.Random _decisionRandom = new System.Random(1337);
    private GameManager _gameManager;
    private PlayerController _player;
    private GameObject _ghost;
    private Transform _ghostVisual;
    private Vector3 _ghostVisualPosition;
    private CharacterAnimator _ghostAnimator;
    private Vector3 _ghostForward = Vector3.forward;
    private Material _ghostMaterial;
    private int _ghostLane = 1;
    private float _displayedGhostLane = 1f;
    private float _displayedGap;
    private float _ghostGroundY;
    private float _ghostRootToLowestPoint;
    private float _laneSmoothVelocity;
    private float _gapSmoothVelocity;
    private float _laneDecisionCooldown;
    private float _ghostProgress;
    private float _opponentPace;
    private float _playerPhysicalProgress;
    private float _playerProgress;
    private float _appliedContractPlayerBonus;
    private float _appliedContractShadowBonus;
    private float _runTime;
    private float _decisionTimer;
    private float _keepSampleTimer;
    private float _ghostJumpTimer;
    private float _ghostSlideTimer;
    private float _ghostStumbleTimer;
    private float _ghostRecoveryTimer;
    private float _decisionConfidence;
    private float _sequenceInfluence;
    private int _runCoins;
    private int _runDodges;
    private int _ghostMistakes;
    private int _samplesSinceCheckpoint;
    private int _lastTrainingAction = -1;
    private int _lastOpponentAction = -1;
    private ShadowAction _lastStyleDecision = ShadowAction.Keep;
    private bool _runStarted;
    private bool _runFinalized;
    private bool _runUsedTurboStart;
    private readonly HashSet<int> _handledGhostObstacles = new HashSet<int>();
    private readonly HashSet<int> _reactedGhostObstacles = new HashSet<int>();
    private readonly HashSet<int> _recordedPlayerDodgeIds = new HashSet<int>();
    private readonly ObstacleOpportunityTracker _opportunityTracker =
        new ObstacleOpportunityTracker();
    private readonly EchoDuelEvidence _duelEvidence = new EchoDuelEvidence();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        AIBalance balance = GameBalanceConfig.Current.ai;
        learningRate = balance.shadowLearningRate;
        minimumTrainingSamples = balance.minimumTrainingSamples;
        minimumActiveTrainingSamples = balance.minimumActiveSamples;
        minimumJumpSamples = balance.minimumJumpSamples;
        minimumSlideSamples = balance.minimumSlideSamples;
        LoadProfile();
    }

    void Start()
    {
        _gameManager = GameManager.Instance;
        _player = FindObjectOfType<PlayerController>();
        _directiveSource = AITrackDirector.Instance;
        if (_gameManager != null)
            _gameManager.OnStateChanged.AddListener(OnGameStateChanged);
    }

    void Update()
    {
        if (_gameManager == null) _gameManager = GameManager.Instance;
        if (_player == null) _player = FindObjectOfType<PlayerController>();
        if (_gameManager == null || _gameManager.State != GameState.Playing
            || _gameManager.IsDeathSequence || _player == null)
            return;

        if (!_runStarted) BeginRun();
        if (HasActiveOpponent && _ghost == null) CreateGhost();

        TrackPlayerObstacleOpportunity();

        _runTime += Time.deltaTime;
        if (HasActiveOpponent && _contractEvaluator != null)
        {
            float remainingSeconds = EchoTimeRules.EstimateRemainingSeconds(
                _gameManager.RemainingDistance, _gameManager.CurrentSpeed);
            if (_duelFlow != null && _duelFlow.Tick(Time.deltaTime,
                    remainingSeconds, _contractEvaluator.Contract))
            {
                _contractEvaluator.SetPhase(_duelFlow.Phase);
                TrackManager.Instance?.ReplanFutureDuelRows(
                    _duelFlow.PhaseSequence, _gameManager.Distance,
                    _gameManager.CurrentSpeed);
                AIRunTelemetry.RecordEvent("echo_duel_phase",
                    (int)_duelFlow.Phase, _player.CurrentLane,
                    _runTime, remainingSeconds);
            }
            SyncRhythmTarget();
            _contractEvaluator.TickLane(_player.CurrentLane, Time.deltaTime,
                _gameManager.CurrentSpeed);
            ApplyContractMotionDelta();
        }
        _playerPhysicalProgress = _gameManager.Distance;
        _playerProgress = _playerPhysicalProgress;

        _keepSampleTimer += Time.deltaTime;
        if (_keepSampleTimer >= keepSampleInterval)
        {
            _keepSampleTimer = 0f;
            float[] keepContext = BuildFeatures(_player.CurrentLane, false);
            // Do not teach "keep running" while a nearby obstacle is asking for input.
            if (keepContext[3] < 0.35f)
                Learn(ShadowAction.Keep, keepContext);
        }

        if (!HasActiveOpponent) return;

        _laneDecisionCooldown = Mathf.Max(0f, _laneDecisionCooldown - Time.deltaTime);
        _ghostStumbleTimer = Mathf.Max(0f, _ghostStumbleTimer - Time.deltaTime);
        _ghostRecoveryTimer = Mathf.Max(0f, _ghostRecoveryTimer - Time.deltaTime);
        _ghostJumpTimer = Mathf.Max(0f, _ghostJumpTimer - Time.deltaTime);
        _ghostSlideTimer = Mathf.Max(0f, _ghostSlideTimer - Time.deltaTime);
        float stumbleSpeed = _ghostStumbleTimer > 0f ? 0.25f : 1f;
        _ghostProgress += Mathf.Max(1f, _opponentPace)
                          * shadowPaceMultiplier * stumbleSpeed * Time.deltaTime;
        PlayerLead = CalculatePhysicalLead(_playerProgress, _ghostProgress);

        _decisionTimer += Time.deltaTime;
        if (_decisionTimer >= decisionInterval)
        {
            _decisionTimer = 0f;
            ApplyShadowDecision();
        }

        ApplyObstacleReaction();
        CurrentStatus = BuildDuelStatus();
    }

    void LateUpdate()
    {
        if (!HasActiveOpponent || _gameManager == null
            || _gameManager.State != GameState.Playing
            || _gameManager.IsDeathSequence || _player == null)
            return;

        UpdateGhostPose();
        EvaluateGhostObstacle();
        CurrentStatus = BuildDuelStatus();
    }

    public string GetMenuStatus()
    {
        if (!HasTrainedProfile())
            return "首局校准：AI 将学习你的路线、动作与节奏";

        PlayerStyleData style = _activeGeneration != null
            ? _activeGeneration.GetStyle()
            : StyleTracker.GetSnapshot();
        EchoContractData preview = EchoContractPolicy.Create(
            style, Generation);
        return "第 " + Generation + " 代回声已生成\n"
               + "AI画像：" + EchoContractPolicy.BuildStyleSummary(style) + "\n"
               + preview.title + " · " + preview.learnedTrait + "\n"
               + "规则：" + preview.ruleDescription + "\n目标：" + preview.objective;
    }

    public string GetContractHudText()
    {
        if (_contractEvaluator != null)
            return _contractEvaluator.BuildHudText();
        return HasActiveOpponent
            ? "回声契约正在生成"
            : "校准目标：跳跃 " + minimumJumpSamples
              + " 次、滑铲 " + minimumSlideSamples + " 次";
    }

    public void SetEmergencyReflexEnabled(bool enabled)
    {
        enableEmergencyReflex = enabled;
    }

    private int GetActionSampleCount(ShadowAction action)
    {
        if (_profile == null || _profile.actionCounts == null) return 0;
        int index = (int)action;
        return index >= 0 && index < _profile.actionCounts.Length
            ? _profile.actionCounts[index]
            : 0;
    }

    public void RecordPlayerAction(ShadowAction action, int laneBeforeAction)
    {
        if (_gameManager == null || _gameManager.State != GameState.Playing) return;
        if (!_runStarted) BeginRun();
        AIRunTelemetry.RecordEvent(
            "player_action", (int)action, laneBeforeAction);
        float[] features = BuildFeatures(laneBeforeAction, false);
        float timingOffset = 0f;
        float styleProximity = features[3];
        bool matchedActionObstacle = false;
        if ((action == ShadowAction.Jump || action == ShadowAction.Slide)
            && _gameManager != null
            && _player != null && TrackManager.Instance != null
            && TrackManager.Instance.TryGetUpcomingObstacleInLane(
                _player.transform.position, _player.ForwardDirection,
                laneBeforeAction, null, out float actionObstacleDistance,
                out ObstacleType actionObstacleType, out _))
        {
            ObstacleType expectedType = action == ShadowAction.Jump
                ? ObstacleType.High : ObstacleType.Low;
            if (actionObstacleType == expectedType)
            {
                matchedActionObstacle = true;
                float duration = action == ShadowAction.Jump
                    ? _player.jumpDuration : _player.slideDuration;
                float idealDistance = CalculateReactionDistance(
                    _gameManager.CurrentSpeed, duration);
                float normalizedTiming = CalculateActionTimingOffset(
                    actionObstacleDistance, idealDistance);
                styleProximity = (normalizedTiming + 1f) * 0.5f;
                if (action == ShadowAction.Jump)
                    timingOffset = normalizedTiming;
            }
            else styleProximity = 0f;
        }
        else if (action == ShadowAction.Jump || action == ShadowAction.Slide)
            styleProximity = 0f;
        if (!matchedActionObstacle
            && (action == ShadowAction.Jump || action == ShadowAction.Slide))
            _duelEvidence.ObserveFreeAction(action);
        if (action == ShadowAction.Jump || action == ShadowAction.Slide)
            _opportunityTracker.MarkAction(action, laneBeforeAction);
        bool airLaneChange = _player != null && _player.IsJumping
                             && (action == ShadowAction.Left
                                 || action == ShadowAction.Right);
        StyleTracker.RecordAction(action, styleProximity, timingOffset,
            airLaneChange, matchedActionObstacle);
        if (matchedActionObstacle)
        {
            float[] skillFeatures = (float[])features.Clone();
            skillFeatures[3] = styleProximity;
            AIPlayerSkillEstimator.RecordAction(action, skillFeatures);
        }
        Learn(action, features);
        _keepSampleTimer = 0f;
    }

    public void RecordCoin(bool isEchoContractMarker = false)
    {
        _runCoins++;
        if (HasActiveOpponent && _contractEvaluator != null && _player != null
            && ShouldCountContractMarker(
                _contractEvaluator.Contract.type, isEchoContractMarker))
        {
            float routeDistance = _gameManager != null
                ? _gameManager.Distance : _playerPhysicalProgress;
            _contractEvaluator.RecordLaneMarker(
                _player.CurrentLane, routeDistance,
                _gameManager != null ? _gameManager.CurrentSpeed : 10f);
            ApplyContractMotionDelta();
        }
        AIRunTelemetry.RecordEvent("coin", 0,
            _player != null ? _player.CurrentLane : -1, _runCoins,
            isEchoContractMarker ? 1f : 0f);
    }

    public static bool ShouldCountContractMarker(EchoContractType contractType,
        bool isEchoContractMarker)
    {
        return contractType == EchoContractType.BreakLaneHabit
               && isEchoContractMarker;
    }

    public bool RecordDodge()
    {
        return RecordDodge(ObstacleType.Barrier, 0,
            _player != null ? _player.CurrentLane : -1);
    }

    public bool RecordDodge(ObstacleType obstacleType, int obstacleId = 0,
        int playerLane = -1)
    {
        if (_opportunityTracker.ResolveContact(obstacleId, true,
                out ObstacleOpportunityResolution opportunity))
            ObserveDuelOpportunity(opportunity);
        if (obstacleId != 0 && !_recordedPlayerDodgeIds.Add(obstacleId))
            return false;
        _runDodges++;
        if (HasActiveOpponent && _contractEvaluator != null)
        {
            _contractEvaluator.RecordDodge(obstacleType, playerLane,
                _gameManager != null ? _gameManager.CurrentSpeed : 10f);
            ApplyContractMotionDelta();
        }
        AIRunTelemetry.RecordEvent("dodge", 0,
            playerLane >= 0
                ? playerLane : (_player != null ? _player.CurrentLane : -1),
            _runDodges);
        return true;
    }

    public void RecordObstacleHit(int obstacleId = 0)
    {
        if (_opportunityTracker.ResolveContact(obstacleId, false,
                out ObstacleOpportunityResolution opportunity))
            ObserveDuelOpportunity(opportunity);
        if (HasActiveOpponent && _contractEvaluator != null)
        {
            _contractEvaluator.RecordHit(
                _gameManager != null ? _gameManager.CurrentSpeed : 10f);
            ApplyContractMotionDelta();
        }
        AIRunTelemetry.RecordEvent("obstacle_hit", 0,
            _player != null ? _player.CurrentLane : -1, PlayerLead);
    }

    public string FinalizeRunIfNeeded()
    {
        if (!_runFinalized && _runStarted) FinishRun();
        return LastResult;
    }

    public float[] GetModelWeightsSnapshot()
    {
        if (_activeGeneration != null
            && _activeGeneration.policyWeights != null)
            return (float[])_activeGeneration.policyWeights.Clone();
        return _policy != null ? _policy.ExportWeights() : null;
    }

    public string GetSequenceStateSnapshot()
    {
        if (_activeGeneration != null)
        {
            return JsonUtility.ToJson(new AIShadowSequenceState
            {
                transitions = _activeGeneration.sequenceTransitions != null
                    ? (float[])_activeGeneration.sequenceTransitions.Clone()
                    : null,
                pairCount = _activeGeneration.sequencePairCount
            });
        }
        return _sequencePolicy == null
            ? ""
            : JsonUtility.ToJson(_sequencePolicy.ExportState());
    }

    public string GetActiveGenerationSnapshotJson()
    {
        return _activeGeneration != null ? _activeGeneration.ToJson() : "";
    }

    public void SetDirectiveSource(IShadowDirectiveSource source)
    {
        _directiveSource = source;
    }

    public void ResetTraining()
    {
        SetGhostActive(false);
        _profile = new ShadowProfile { version = 5 };
        _activeGeneration = null;
        _policy = new AIShadowPolicy();
        _sequencePolicy = new AIShadowSequencePolicy();
        _opponentPolicy = null;
        _opponentSequencePolicy = null;
        _opponentStyle = null;
        _opponentPace = 0f;
        _contractEvaluator = null;
        LastDecisionTrace = null;
        _runStarted = false;
        _runFinalized = false;
        _samplesSinceCheckpoint = 0;
        HasActiveOpponent = false;
        PlayerLead = 0f;
        LastResult = "";
        LastRunWasChallenge = false;
        LastRunWon = false;
        PolicyCorrectDecisionCount = 0;
        SafetyOverrideDecisionCount = 0;
        EmergencyReflexSaveCount = 0;
        CurrentStatus = "AI影子 · 训练已重置";
        EchoRunSaveSystem.SaveShadowProfile("");
        EchoRunSaveSystem.SaveLastEchoContract("");
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.Playing) BeginRun();
        else if (state == GameState.GameOver) FinishRun();
    }

    private void BeginRun()
    {
        if (_runStarted) return;
        _runStarted = true;
        _runFinalized = false;
        _runTime = 0f;
        _runUsedTurboStart = PowerUpController.Instance != null
                             && PowerUpController.Instance.ActivePowerUp
                             == PowerUpId.TurboStart;
        _runCoins = 0;
        _runDodges = 0;
        _playerPhysicalProgress = 0f;
        _playerProgress = 0f;
        _ghostProgress = 0f;
        _appliedContractPlayerBonus = 0f;
        _appliedContractShadowBonus = 0f;
        PlayerLead = 0f;
        _ghostLane = 1;
        _displayedGhostLane = 1f;
        _displayedGap = 0f;
        _ghostGroundY = _player != null ? _player.transform.position.y : 0f;
        _laneSmoothVelocity = 0f;
        _gapSmoothVelocity = 0f;
        _laneDecisionCooldown = 0f;
        _ghostForward = _player != null ? _player.ForwardDirection : Vector3.forward;
        _decisionTimer = 0f;
        _keepSampleTimer = 0f;
        _ghostJumpTimer = 0f;
        _ghostSlideTimer = 0f;
        _ghostStumbleTimer = 0f;
        _ghostRecoveryTimer = 0f;
        _sequenceInfluence = 0f;
        _ghostMistakes = 0;
        PolicyCorrectDecisionCount = 0;
        SafetyOverrideDecisionCount = 0;
        EmergencyReflexSaveCount = 0;
        _lastTrainingAction = -1;
        _lastOpponentAction = -1;
        _lastStyleDecision = ShadowAction.Keep;
        LastDecisionTrace = null;
        _opportunityTracker.Reset();
        _duelEvidence.Reset();
        _recordedPlayerDodgeIds.Clear();
        _opponentStyle = null;
        _opponentPace = 0f;
        int decisionSeed = _gameManager != null
            ? _gameManager.RunSeed ^ unchecked((int)0x51ED270B)
            : 1337;
        _decisionRandom = new System.Random(decisionSeed);
        _handledGhostObstacles.Clear();
        _reactedGhostObstacles.Clear();
        HasActiveOpponent = HasTrainedProfile();
        _duelFlow = new EchoDuelFlow(HasActiveOpponent);

        if (HasActiveOpponent)
        {
            // Freeze the previous generation for this duel. New player actions train
            // the next generation and cannot make the current shadow mirror inputs.
            _opponentPolicy = new AIShadowPolicy(
                _activeGeneration.policyWeights);
            _opponentSequencePolicy = new AIShadowSequencePolicy(
                _activeGeneration.sequenceTransitions,
                _activeGeneration.sequencePairCount);
            _opponentStyle = _activeGeneration.GetStyle();
            _opponentPace = Mathf.Max(1f, _activeGeneration.pace);
            _contractEvaluator = new EchoContractEvaluator(
                EchoContractPolicy.CreateForRun(_opponentStyle,
                    _activeGeneration.generation,
                    EchoRunSaveSystem.GetLastEchoContractJson()));
            _contractEvaluator.Contract.duelPhase = EchoDuelPhase.None;
            _contractEvaluator.SetPhase(_duelFlow.Phase);
            CreateGhost();
            CurrentStatus = _contractEvaluator.BuildHudText();
        }
        else
        {
            _contractEvaluator = null;
            CurrentStatus = "AI影子 · 校准中 0%";
            SetGhostActive(false);
        }
    }

    private void FinishRun()
    {
        RunEndReason endReason = _gameManager != null
                                 && _gameManager.LastEndReason != RunEndReason.None
            ? _gameManager.LastEndReason
            : RunEndReason.Abandoned;
        FinishRunWithReason(endReason);
    }

    private void FinishRunWithReason(RunEndReason endReason)
    {
        if (!_runStarted || _runFinalized) return;
        _runFinalized = true;

        bool challengedOpponent = HasActiveOpponent;
        bool reachedFinish = endReason == RunEndReason.FinishReached;
        bool contractCompleted = _contractEvaluator != null
                                 && _contractEvaluator.Contract.completed;
        bool playerWon = IsContractVictory(
            PlayerLead, challengedOpponent, contractCompleted, endReason);
        LastRunWasChallenge = challengedOpponent;
        LastRunWon = playerWon;
        float physicalDistance = _gameManager != null
            ? _gameManager.Distance
            : _playerPhysicalProgress;
        float runPace = CalculatePhysicalPace(physicalDistance, _runTime);
        if (ShouldRecordPendingPace(endReason, physicalDistance,
                _runTime, _runUsedTurboStart))
        {
            if (_profile.pace <= 0f) _profile.pace = runPace;
            else _profile.pace = Mathf.Lerp(_profile.pace, runPace, 0.35f);
        }
        _profile.bestProgress = Mathf.Max(
            _profile.bestProgress, physicalDistance);
        float calibrationProgress = CalculateCalibrationProgress(
            _profile.sampleCount, _profile.activeSampleCount,
            _profile.actionCounts, minimumTrainingSamples,
            minimumActiveTrainingSamples, minimumActionCategories,
            minimumJumpSamples, minimumSlideSamples);
        bool completedCalibration = reachedFinish
                                    && calibrationProgress >= 0.999f;
        bool formedPartialEcho = !challengedOpponent
                                 && HasPartialEchoSamples(
                                     _profile.sampleCount,
                                     _profile.activeSampleCount,
                                     _profile.actionCounts,
                                     _runTime,
                                     minimumTrainingSamples);
        if (challengedOpponent && reachedFinish && playerWon)
        {
            float nextClarity = Mathf.Max(EchoClarity,
                Mathf.Clamp01(calibrationProgress));
            PromotePendingGeneration(Generation + 1, nextClarity);
        }
        else if (!challengedOpponent && Generation <= 0
                 && (completedCalibration || formedPartialEcho))
        {
            float firstClarity = completedCalibration
                ? 1f
                : Mathf.Clamp(calibrationProgress, 0.25f, 0.85f);
            PromotePendingGeneration(1, firstClarity);
        }
        _profile.weights = _policy.ExportWeights();
        SaveProfile();

        if (!challengedOpponent && formedPartialEcho && !completedCalibration)
        {
            EchoContractData nextContract = EchoContractPolicy.Create(
                _activeGeneration.GetStyle(), Generation);
            LastResult = "校准中断，但回声已经记住了你\n"
                         + "回声清晰度 "
                         + (EchoClarity * 100f).ToString("0") + "% · "
                         + nextContract.learnedTrait + "\n"
                         + "下一局将由模糊回声继续校准："
                         + nextContract.title;
        }
        else if (!reachedFinish)
        {
            LastResult = challengedOpponent
                ? "赛程中断 · 未到达终点\n本代契约未结算，重新挑战才能获胜"
                : "校准中断 · 未到达终点\n本局样本已保留，完成赛程后才会生成回声";
        }
        else if (!challengedOpponent && !completedCalibration)
        {
            int categories = CountTrainedActionCategories(_profile.actionCounts);
            LastResult = "校准未完成 · 有效动作 "
                         + _profile.activeSampleCount + "/"
                         + Mathf.Max(1, minimumActiveTrainingSamples)
                         + " · 动作类型 " + categories + "/"
                         + Mathf.Max(1, minimumActionCategories)
                         + " · 跳/滑 "
                         + _profile.actionCounts[(int)ShadowAction.Jump]
                         + "/" + _profile.actionCounts[(int)ShadowAction.Slide]
                         + "（目标 " + minimumJumpSamples
                         + "/" + minimumSlideSamples + "）"
                         + " · 再跑一局继续训练";
        }
        else if (!challengedOpponent)
        {
            EchoContractData nextContract = EchoContractPolicy.Create(
                _activeGeneration.GetStyle(), Generation);
            LastResult = "校准完成 · 第 1 代 AI 回声已生成\n"
                         + "回声清晰度 100%\n"
                         + nextContract.learnedTrait + "\n"
                         + "下一局规则：" + nextContract.ruleDescription;
        }
        else if (playerWon)
        {
            EchoContractData nextContract = EchoContractPolicy.Create(
                _activeGeneration.GetStyle(), Generation);
            _contractEvaluator.Contract.won = true;
            LastResult = "契约破解 · 领先回声 "
                         + Mathf.Abs(PlayerLead).ToString("0.0") + "m\n"
                         + "上一代行为：" + _contractEvaluator.Contract.learnedTrait + "\n"
                         + "本代学习：AI已记录你的反制策略\n"
                         + "下一代变化：" + nextContract.title + " · "
                         + nextContract.ruleDescription;
        }
        else
        {
            bool ledButFailedContract = PlayerLead >= 0f && !contractCompleted;
            string cause;
            string learning;
            if (ledButFailedContract)
            {
                cause = "距离领先，但未破解回声契约";
                learning = "旧习惯仍被回声掌握";
            }
            else if (contractCompleted)
            {
                cause = "契约已破解，但回声在距离竞速中领先 "
                        + Mathf.Abs(PlayerLead).ToString("0.0") + "m";
                learning = "反制已经有效，重试时需要追回距离";
            }
            else
            {
                cause = "回声在距离竞速中领先 "
                        + Mathf.Abs(PlayerLead).ToString("0.0") + "m";
                learning = "旧习惯仍被回声掌握";
            }
            LastResult = "回声胜出 · " + cause + "\n"
                         + "上一代行为：" + _contractEvaluator.Contract.learnedTrait + "\n"
                         + "反制进度 "
                         + _contractEvaluator.Contract.progress.ToString("0.#")
                         + "/"
                         + _contractEvaluator.Contract.targetProgress.ToString("0.#")
                         + "\n本代结论：" + learning + "\n"
                         + "重试规则保持不变："
                         + _contractEvaluator.Contract.title;
        }

        if (_contractEvaluator != null)
        {
            EchoContractData savedContract = playerWon
                ? null : _contractEvaluator.Contract.ResetForRun();
            EchoRunSaveSystem.SaveLastEchoContract(savedContract != null
                ? JsonUtility.ToJson(savedContract) : "");
        }

        CurrentStatus = LastResult;
        AIRunTelemetry.RecordEvent("shadow_result",
            challengedOpponent ? (playerWon ? 1 : -1) : 0,
            _ghostLane, PlayerLead, _ghostMistakes);
        HasActiveOpponent = false;
        SetGhostActive(false);
    }

    private void PromotePendingGeneration(int generation, float clarity)
    {
        AIShadowSequenceState sequence = _sequencePolicy != null
            ? _sequencePolicy.ExportState()
            : new AIShadowSequenceState();
        PlayerStyleData style = StyleTracker.GetSnapshot();
        style.Normalize();
        float promotedPace = _profile.pace;
        if (promotedPace <= 0f && _gameManager != null)
        {
            float expectedDistance = EchoTimeRules.DistanceForAcceleratingRun(
                _gameManager.startSpeed, _gameManager.maxSpeed,
                _gameManager.speedIncreaseRate, Mathf.Max(1f, _runTime));
            promotedPace = CalculatePhysicalPace(expectedDistance, _runTime);
        }
        _activeGeneration = new EchoGenerationSnapshot
        {
            generation = Mathf.Max(1, generation),
            policyWeights = _policy != null
                ? _policy.ExportWeights() : _profile.weights,
            sequenceTransitions = sequence.transitions,
            sequencePairCount = sequence.pairCount,
            styleJson = JsonUtility.ToJson(style),
            pace = Mathf.Max(1f, promotedPace),
            clarity = Mathf.Clamp01(clarity)
        };
        _activeGeneration.Normalize();
        _profile.generation = _activeGeneration.generation;
        _profile.clarity = _activeGeneration.clarity;
        _profile.activeGenerationJson = _activeGeneration.ToJson();
    }

    public static bool ShouldRecordPendingPace(RunEndReason endReason,
        float physicalDistance, float runTime, bool usedTurboStart)
    {
        return endReason != RunEndReason.Abandoned
               && !usedTurboStart
               && runTime >= 8f
               && physicalDistance >= 60f;
    }

    private void Learn(ShadowAction action, float[] features)
    {
        int lane = features != null && features.Length > 1
            ? Mathf.RoundToInt(features[1] + 1f)
            : 1;
        float confidence = _policy != null ? _policy.Confidence(features) : 0f;
        AIRunTelemetry.RecordShadowSample(
            action, lane, features, false, confidence, (int)action,
            0f, 0f);
        float rewriteWeight = _duelFlow != null
                              && _duelFlow.IsRewriteLearningWindow
            ? 2f : 1f;
        float sampleLearningRate = (action == ShadowAction.Keep
            ? learningRate * 0.25f
            : learningRate) * rewriteWeight;
        _policy.Learn((int)action, features, sampleLearningRate);
        if (action != ShadowAction.Keep)
        {
            _sequencePolicy.Learn(_lastTrainingAction, (int)action);
            _lastTrainingAction = (int)action;
        }
        _profile.sampleCount++;
        EnsureActionCounts();
        int actionIndex = Mathf.Clamp((int)action, 0, _profile.actionCounts.Length - 1);
        _profile.actionCounts[actionIndex]++;
        if (action != ShadowAction.Keep)
            _profile.activeSampleCount++;
        _samplesSinceCheckpoint++;

        if (_samplesSinceCheckpoint >= SamplesPerCheckpoint)
        {
            _samplesSinceCheckpoint = 0;
            SaveProfile();
        }

        if (!HasActiveOpponent)
        {
            float progress = CalculateCalibrationProgress(
                _profile.sampleCount, _profile.activeSampleCount,
                _profile.actionCounts, minimumTrainingSamples,
                minimumActiveTrainingSamples, minimumActionCategories,
                minimumJumpSamples, minimumSlideSamples);
            CurrentStatus = "AI影子 · 校准 " + (progress * 100f).ToString("0")
                            + "% · 有效动作 " + _profile.activeSampleCount
                            + "/" + Mathf.Max(1, minimumActiveTrainingSamples)
                            + " · 跳/滑 "
                            + _profile.actionCounts[(int)ShadowAction.Jump]
                            + "/" + _profile.actionCounts[(int)ShadowAction.Slide];
        }
    }

    private float[] BuildFeatures(int lane, bool forShadow)
    {
        float speed = 0f;
        if (_gameManager != null)
        {
            speed = Mathf.InverseLerp(_gameManager.startSpeed,
                _gameManager.maxSpeed, _gameManager.CurrentSpeed);
        }

        float proximity = 0f;
        float relativeLane = 0f;
        float obstacleType = 0f;
        Vector3 samplePosition = forShadow && _ghost != null
            ? _ghost.transform.position
            : (_player != null ? _player.transform.position : Vector3.zero);
        Vector3 sampleForward = forShadow
            ? _ghostForward
            : (_player != null ? _player.ForwardDirection : Vector3.forward);

        if (_player != null && TrackManager.Instance != null
            && TrackManager.Instance.TryGetUpcomingObstacle(
                samplePosition, sampleForward, lane,
                out int threatLane, out float threatDistance,
                out ObstacleType threatType, out int ignoredObstacleId))
        {
            proximity = 1f - Mathf.Clamp01(threatDistance / 24f);
            relativeLane = Mathf.Clamp((threatLane - lane) / 2f, -1f, 1f);
            obstacleType = ((int)threatType + 1) / 3f;
        }

        return new[]
        {
            1f,
            lane - 1f,
            speed,
            proximity,
            relativeLane,
            obstacleType,
            forShadow ? (_ghostJumpTimer > 0f ? 1f : 0f)
                      : (_player != null && _player.IsJumping ? 1f : 0f),
            forShadow ? (_ghostSlideTimer > 0f ? 1f : 0f)
                      : (_player != null && _player.IsSliding ? 1f : 0f)
        };
    }

    private void ApplyShadowDecision()
    {
        if (_opponentPolicy == null) return;
        float[] features = BuildFeatures(_ghostLane, true);
        float[] baseScores = _opponentPolicy.GetProbabilities(features);
        ShadowAction baseAction = (ShadowAction)_opponentPolicy.Predict(features);
        ShadowAction sequenceAction = PredictOpponentAction(features,
            out float baseConfidence,
            out float sequenceConfidence, out float sequenceInfluence);
        baseScores[(int)sequenceAction] += sequenceInfluence * 0.25f;
        ShadowDecisionContext context = BuildDecisionContext(features);
        ShadowAIDirective directive = GetShadowDirective();
        ShadowAction action = _decisionMaker.Select(baseScores,
            _opponentStyle, context, directive,
            (float)_decisionRandom.NextDouble(), out ShadowDecisionTrace trace);
        trace.originalPrediction = sequenceAction;
        LastDecisionTrace = trace;
        CountDecisionOutcome(context, sequenceAction, action, trace);
        _lastStyleDecision = action;
        _decisionConfidence = Mathf.Max(baseConfidence, sequenceConfidence);
        _sequenceInfluence = sequenceInfluence;
        AIRunTelemetry.RecordShadowSample(
            action, _ghostLane, features, true, _decisionConfidence, (int)baseAction,
            sequenceConfidence, sequenceInfluence, trace, _opponentStyle);

        switch (action)
        {
            case ShadowAction.Left:
                if (_laneDecisionCooldown <= 0f && _ghostLane > 0)
                {
                    _ghostLane--;
                    _laneDecisionCooldown = minimumLaneHoldTime;
                    RecordOpponentAction(action);
                }
                else RecordOpponentAction(ShadowAction.Keep);
                break;
            case ShadowAction.Right:
                if (_laneDecisionCooldown <= 0f && _ghostLane < 2)
                {
                    _ghostLane++;
                    _laneDecisionCooldown = minimumLaneHoldTime;
                    RecordOpponentAction(action);
                }
                else RecordOpponentAction(ShadowAction.Keep);
                break;
            case ShadowAction.Jump:
            case ShadowAction.Slide:
                // Vertical actions are scheduled from the obstacle distance below.
                // The policy still owns route selection, but cannot spam jumps or
                // start a slide in mid-air between its regular decision ticks.
                RecordOpponentAction(ShadowAction.Keep);
                break;
            default:
                RecordOpponentAction(ShadowAction.Keep);
                break;
        }
    }

    private void ApplyObstacleReaction()
    {
        if (_ghost == null || _player == null || TrackManager.Instance == null
            || _ghostStumbleTimer > 0f || _ghostJumpTimer > 0f
            || _ghostSlideTimer > 0f)
            return;
        if (TrackManager.Instance.IsInsideTurnTransition(
                _ghost.transform.position))
            return;

        if (!TrackManager.Instance.TryGetUpcomingObstacleInLane(
                _ghost.transform.position, _ghostForward, _ghostLane,
                _reactedGhostObstacles, out float threatDistance,
                out ObstacleType threatType, out int obstacleId))
            return;

        ShadowAction requiredAction = RequiredActionForObstacle(threatType);
        if (requiredAction == ShadowAction.Keep) return;

        float duration = requiredAction == ShadowAction.Jump
            ? GetGhostJumpDuration()
            : GetGhostSlideDuration();
        float speed = _gameManager != null ? _gameManager.CurrentSpeed : 10f;
        float reactionDistance = CalculateReactionDistance(speed, duration)
                                 * ShadowDecisionMaker.ReactionDistanceMultiplier(
                                     _opponentStyle, GetShadowDirective());
        if (threatDistance > reactionDistance) return;

        // A trained clone gets the full reaction window when it predicts the
        // learned move. The close-range reflex is the physical safety layer:
        // it keeps an imperfect model readable without erasing earlier/later
        // reaction timing learned from the player.
        if (_opponentPolicy != null)
        {
            float emergencyDistance = Mathf.Clamp(speed * 0.2f, 2f, 4.5f);
            if (_lastStyleDecision != requiredAction)
            {
                if (!enableEmergencyReflex || threatDistance > emergencyDistance)
                    return;
            }
        }

        _reactedGhostObstacles.Add(obstacleId);
        bool reflexSave = _lastStyleDecision != requiredAction;
        if (StartGhostAction(requiredAction))
        {
            if (reflexSave) EmergencyReflexSaveCount++;
            RecordOpponentAction(requiredAction);
        }
    }

    private void CountDecisionOutcome(ShadowDecisionContext context,
        ShadowAction originalPrediction, ShadowAction selected,
        ShadowDecisionTrace trace)
    {
        if (!context.hasThreat || context.relativeThreatLane != 0) return;
        ShadowAction required = RequiredActionForObstacle(context.threatType);
        if (required == ShadowAction.Keep) return;

        if (originalPrediction == required && selected == required)
            PolicyCorrectDecisionCount++;
        else if (selected == required && trace != null && trace.safetyAdjusted)
            SafetyOverrideDecisionCount++;
    }

    private bool StartGhostAction(ShadowAction action)
    {
        if (!CanStartVerticalAction(action, _ghostJumpTimer > 0f,
                _ghostSlideTimer > 0f, _ghostStumbleTimer > 0f))
            return false;

        if (action == ShadowAction.Jump)
            _ghostJumpTimer = GetGhostJumpDuration();
        else if (action == ShadowAction.Slide)
            _ghostSlideTimer = GetGhostSlideDuration();
        else return false;
        return true;
    }

    private ShadowAction PredictOpponentAction(float[] features,
        out float baseConfidence, out float sequenceConfidence,
        out float sequenceInfluence)
    {
        float[] probabilities = _opponentPolicy.GetProbabilities(features);
        int baseAction = _opponentPolicy.Predict(features);
        baseConfidence = probabilities[baseAction];
        if (_opponentSequencePolicy == null)
        {
            sequenceConfidence = 0f;
            sequenceInfluence = 0f;
            return (ShadowAction)baseAction;
        }

        int action = _opponentSequencePolicy.Predict(probabilities,
            _lastOpponentAction, out sequenceConfidence, out sequenceInfluence);
        return (ShadowAction)action;
    }

    private void RecordOpponentAction(ShadowAction action)
    {
        _lastOpponentAction = (int)action;
    }

    private void UpdateGhostPose()
    {
        if (_ghost == null || _player == null) return;

        float targetGap = Mathf.Clamp(_ghostProgress - _playerProgress,
            -2.5f, maximumVisibleLead);
        _displayedGap = Mathf.SmoothDamp(_displayedGap, targetGap,
            ref _gapSmoothVelocity, Mathf.Max(0.02f, distanceSmoothTime),
            80f, Time.deltaTime);
        _displayedGhostLane = Mathf.SmoothDamp(_displayedGhostLane, _ghostLane,
            ref _laneSmoothVelocity, Mathf.Max(0.02f, laneSmoothTime),
            12f, Time.deltaTime);
        float jumpDuration = GetGhostJumpDuration();
        float jumpProgress = _ghostJumpTimer > 0f
            ? 1f - _ghostJumpTimer / jumpDuration
            : 0f;
        float jumpHeight = _ghostJumpTimer > 0f
            ? EvaluateJumpArc(jumpProgress) * _player.jumpHeight
            : 0f;

        Vector3 target;
        Vector3 targetForward;
        if (TrackManager.Instance != null)
        {
            TrackManager.Instance.GetTrackPoseAhead(
                _player.transform.position, _player.ForwardDirection,
                _player.CurrentLane, _displayedGhostLane, _displayedGap,
                out target, out targetForward);
        }
        else
        {
            targetForward = _player.ForwardDirection.normalized;
            Vector3 right = Vector3.Cross(Vector3.up, targetForward).normalized;
            target = _player.transform.position
                     + targetForward * _displayedGap
                     + right * ((_displayedGhostLane - _player.CurrentLane)
                                * _player.laneDistance);
        }

        if (TryGetGhostGroundHeight(target, out float groundHeight))
            // Authored run clips can extend the foot slightly below the bind-pose
            // bounds used by CacheGhostGroundOffset.
            _ghostGroundY = groundHeight + _ghostRootToLowestPoint + 0.04f;
        else if (!_player.IsJumping)
            _ghostGroundY = _player.transform.position.y;
        target.y = _ghostGroundY + jumpHeight;
        _ghostForward = targetForward;
        _ghost.transform.position = target;
        Quaternion targetRotation = Quaternion.LookRotation(targetForward, Vector3.up);
        float rotationBlend = 1f - Mathf.Exp(-18f * Time.deltaTime);
        _ghost.transform.rotation = Quaternion.Slerp(
            _ghost.transform.rotation, targetRotation, rotationBlend);

        if (_ghostVisual != null)
        {
            _ghostVisual.localPosition = _ghostVisualPosition;
            if (_ghostAnimator != null)
            {
                float animationSpeed = _gameManager != null
                    ? _gameManager.CurrentSpeed * shadowPaceMultiplier
                    : 10f;
                _ghostAnimator.ApplyExternalMotion(
                    _ghostJumpTimer > 0f, _ghostSlideTimer > 0f,
                    _ghostForward, animationSpeed, Time.deltaTime);
            }
        }

        if (_ghostMaterial != null)
        {
            bool reducedMotion = EchoRunAccessibility.ReducedMotion;
            float ghostAlpha = reducedMotion
                ? 0.66f
                : 0.64f + Mathf.Sin(Time.time * 4f) * 0.035f;
            _ghostMaterial.color = _ghostStumbleTimer > 0f
                ? new Color(1.0f, 0.30f, 0.24f, 0.76f)
                : new Color(0.22f, 0.84f, 1.00f, ghostAlpha);
            if (_ghostMaterial.HasProperty("_ScanStrength"))
                _ghostMaterial.SetFloat("_ScanStrength", reducedMotion ? 0f : 0.22f);
        }
    }

    private void EvaluateGhostObstacle()
    {
        if (_ghost == null || TrackManager.Instance == null) return;
        if (TrackManager.Instance.IsInsideTurnTransition(
                _ghost.transform.position))
            return;
        if (!TrackManager.Instance.TryGetUpcomingObstacleInLane(
                _ghost.transform.position, _ghostForward, _ghostLane,
                _handledGhostObstacles, out float threatDistance,
                out ObstacleType threatType, out int obstacleId))
            return;
        if (threatDistance > 1.5f) return;

        _handledGhostObstacles.Add(obstacleId);

        bool avoided = CanAvoidObstacle(
            threatType, _ghostJumpTimer > 0f, _ghostSlideTimer > 0f);
        if (avoided) return;

        _ghostMistakes++;
        _ghostProgress = Mathf.Max(0f, _ghostProgress - 6f);
        _ghostStumbleTimer = 0.85f;
        _ghostRecoveryTimer = 10f;
        PlayerLead = CalculatePhysicalLead(_playerProgress, _ghostProgress);
    }

    private ShadowDecisionContext BuildDecisionContext(float[] features)
    {
        int obstacleType = Mathf.Clamp(
            Mathf.RoundToInt(features[5] * 3f) - 1, 0, 2);
        return new ShadowDecisionContext
        {
            lane = _ghostLane,
            threatProximity = Mathf.Clamp01(features[3]),
            relativeThreatLane = Mathf.RoundToInt(features[4] * 2f),
            threatType = (ObstacleType)obstacleType,
            hasThreat = features[3] > 0f,
            isJumping = _ghostJumpTimer > 0f,
            isSliding = _ghostSlideTimer > 0f,
            isStumbling = _ghostStumbleTimer > 0f,
            isRecovering = _ghostRecoveryTimer > 0f
        };
    }

    private ShadowAIDirective GetShadowDirective()
    {
        if (_directiveSource == null)
            _directiveSource = AITrackDirector.Instance;
        return _directiveSource != null
            ? _directiveSource.CurrentShadowDirective.Normalized()
            : ShadowAIDirective.Neutral;
    }

    private void TrackPlayerObstacleOpportunity()
    {
        if (_player == null || _gameManager == null
            || TrackManager.Instance == null)
            return;

        bool found = TrackManager.Instance.TryGetUpcomingObstacleInLane(
            _player.transform.position, _player.ForwardDirection,
            _player.CurrentLane, _opportunityTracker.ResolvedOpportunityIds,
            out float distance, out ObstacleType type, out int obstacleId);
        int groupId = obstacleId;
        if (found && TrackManager.Instance.TryGetObstacleOpportunity(
                obstacleId, out ObstacleOpportunity opportunity))
            groupId = opportunity.groupId;
        float detectionDistance = CalculateReactionDistance(
            _gameManager.CurrentSpeed,
            Mathf.Max(0.2f, Mathf.Max(_player.jumpDuration,
                _player.slideDuration))) * 1.25f;
        if (_opportunityTracker.Update(
                _player.CurrentLane, _player.IsJumping, _player.IsSliding,
                found, distance, type, obstacleId, groupId,
                detectionDistance, out ObstacleOpportunityResolution result))
        {
            ObserveDuelOpportunity(result);
            bool usedRequiredAction = result.response == EchoResponseKind.Jump
                                      || result.response == EchoResponseKind.Slide;
            StyleTracker.RecordObstacleOpportunity(
                result.obstacleType, usedRequiredAction);
            if (result.passedInLane && result.physicallySucceeded
                && RecordDodge(result.obstacleType,
                    result.opportunityId, result.lane))
            {
                AIPlayerSkillEstimator.RecordObstacleOutcome(
                    result.obstacleType, true);
                AITrackDirector.Instance?.RecordDodge();
                AudioManager.Instance?.PlayDodgeObstacle();
            }
        }
    }

    private void ObserveDuelOpportunity(ObstacleOpportunityResolution result)
    {
        if (!HasActiveOpponent) return;
        _duelEvidence.Observe(result);
        AIRunTelemetry.RecordEvent("echo_choice_resolved",
            (int)result.response, result.lane, result.groupId,
            result.physicallySucceeded ? 1f : 0f);
    }

    private string BuildDuelStatus()
    {
        if (_ghostStumbleTimer > 0f)
            return "AI恢复窗口 · 回声撞击失速 · 立即完成反制";

        string lead = PlayerLead >= 0f
            ? "领先 " + PlayerLead.ToString("0.0") + "m"
            : "落后 " + Mathf.Abs(PlayerLead).ToString("0.0") + "m";
        string sequence = _sequenceInfluence > 0.01f
            ? " · 序列 " + (_sequenceInfluence * 100f).ToString("0") + "%"
            : "";
        string contract = _contractEvaluator != null
            ? _contractEvaluator.BuildHudText()
            : "回声契约未载入";
        string phase = _duelFlow != null
            ? EchoDuelFlow.PhaseName(_duelFlow.Phase) : "回声决斗";
        return phase + " · " + contract + "\n第 " + _profile.generation
                + " 代 · 回声清晰度 " + (EchoClarity * 100f).ToString("0")
                + "% · " + lead
                + " · AI置信 " + (_decisionConfidence * 100f).ToString("0")
                + "%" + sequence;
    }

    public static bool IsContractVictory(float playerLead,
        bool challengedOpponent, bool contractCompleted,
        RunEndReason endReason)
    {
        return endReason == RunEndReason.FinishReached
               && challengedOpponent && contractCompleted && playerLead >= 0f;
    }

    public static bool ShouldAdvanceGeneration(bool challengedOpponent,
        bool reachedFinish, bool playerWon, bool calibrationCompleted)
    {
        return reachedFinish
               && (challengedOpponent ? playerWon : calibrationCompleted);
    }

    public static float CalculatePhysicalLead(float playerRouteDistance,
        float ghostRouteDistance)
    {
        return Mathf.Max(0f, playerRouteDistance)
               - Mathf.Max(0f, ghostRouteDistance);
    }

    private void ApplyContractMotionDelta()
    {
        if (_contractEvaluator == null) return;
        EchoContractData contract = _contractEvaluator.Contract;
        float playerDelta = Mathf.Max(0f,
            contract.playerProgressBonus - _appliedContractPlayerBonus);
        float shadowDelta = Mathf.Max(0f,
            contract.shadowProgressBonus - _appliedContractShadowBonus);
        _appliedContractPlayerBonus = contract.playerProgressBonus;
        _appliedContractShadowBonus = contract.shadowProgressBonus;
        if (playerDelta <= 0f && shadowDelta <= 0f) return;

        _ghostProgress = Mathf.Max(0f,
            _ghostProgress + shadowDelta - playerDelta);
        PlayerLead = CalculatePhysicalLead(_playerProgress, _ghostProgress);
    }

    private void SyncRhythmTarget()
    {
        EchoContractData contract = ActiveContract;
        if (contract == null || contract.type != EchoContractType.DisruptRhythm
            || TrackManager.Instance == null || _player == null)
            return;

        if (TrackManager.Instance.TryGetUpcomingObstacleInLane(
                _player.transform.position, _player.ForwardDirection,
                contract.targetLane, null, out _, out ObstacleType type,
                out _))
            _contractEvaluator.SetRhythmTarget(type);
    }

    public static bool CanAvoidObstacle(ObstacleType obstacleType,
        bool isJumping, bool isSliding)
    {
        return AIShadowRules.CanAvoidObstacle(
            obstacleType, isJumping, isSliding);
    }

    public static ShadowAction RequiredActionForObstacle(ObstacleType obstacleType)
    {
        return AIShadowRules.RequiredActionForObstacle(obstacleType);
    }

    public static string ResolvePublicChallenge(EchoContractData contract,
        bool hasUpcomingObstacle, ObstacleType upcomingType)
    {
        if (contract == null || contract.type == EchoContractType.None)
            return "";
        if (hasUpcomingObstacle)
        {
            string upcoming = EchoRunPresentation.BuildObstacleChallenge(
                contract.targetLane, upcomingType);
            if (!string.IsNullOrEmpty(upcoming)) return upcoming;
        }
        return "破解要求：" + EchoRunPresentation.BuildContractAction(contract);
    }

    public static bool CanStartVerticalAction(ShadowAction action,
        bool isJumping, bool isSliding, bool isStumbling)
    {
        return AIShadowRules.CanStartVerticalAction(
            action, isJumping, isSliding, isStumbling);
    }

    public static float CalculateReactionDistance(float speed, float actionDuration)
    {
        return AIShadowRules.CalculateReactionDistance(speed, actionDuration);
    }

    public static float EvaluateJumpArc(float normalizedProgress)
    {
        return AIShadowRules.EvaluateJumpArc(normalizedProgress);
    }

    public static float EvaluateSlideAmount(float remainingTime, float duration)
    {
        return AIShadowRules.EvaluateSlideAmount(remainingTime, duration);
    }

    public static float CalculatePhysicalPace(float physicalDistance,
        float elapsedTime)
    {
        return Mathf.Max(0f, physicalDistance) / Mathf.Max(1f, elapsedTime);
    }

    public static float CalculateActionTimingOffset(float obstacleDistance,
        float idealDistance)
    {
        return Mathf.Clamp(
            (Mathf.Max(0f, idealDistance) - Mathf.Max(0f, obstacleDistance))
            / Mathf.Max(1f, idealDistance), -1f, 1f);
    }

    private float GetGhostJumpDuration()
    {
        return Mathf.Max(0.2f, _player != null ? _player.jumpDuration : 0.6f);
    }

    private float GetGhostSlideDuration()
    {
        return Mathf.Max(0.2f, _player != null ? _player.slideDuration : 0.8f);
    }

    private bool HasTrainedProfile()
    {
        return _activeGeneration != null
               && _activeGeneration.generation > 0
               && _activeGeneration.pace > 0f
               && _activeGeneration.clarity >= 0.2f;
    }

    public static bool HasCalibrationSamples(int totalSamples, int activeSamples,
        int[] actionCounts, int minimumTotal, int minimumActive,
        int minimumCategories, int minimumJumpSamples = 0,
        int minimumSlideSamples = 0)
    {
        return AIShadowRules.HasCalibrationSamples(totalSamples, activeSamples,
            actionCounts, minimumTotal, minimumActive, minimumCategories,
            minimumJumpSamples, minimumSlideSamples);
    }

    public static float CalculateCalibrationProgress(int totalSamples,
        int activeSamples, int[] actionCounts, int minimumTotal,
        int minimumActive, int minimumCategories, int minimumJumpSamples = 0,
        int minimumSlideSamples = 0)
    {
        return AIShadowRules.CalculateCalibrationProgress(totalSamples,
            activeSamples, actionCounts, minimumTotal, minimumActive,
            minimumCategories, minimumJumpSamples, minimumSlideSamples);
    }

    public static int CountTrainedActionCategories(int[] actionCounts)
    {
        return AIShadowRules.CountTrainedActionCategories(actionCounts);
    }

    public static bool HasPartialEchoSamples(int totalSamples,
        int activeSamples, int[] actionCounts, float runTime,
        int minimumTotalSamples)
    {
        int minimumSeedSamples = Mathf.Max(4, minimumTotalSamples / 4);
        return runTime >= 8f
               && totalSamples >= minimumSeedSamples
               && activeSamples >= 1
               && CountTrainedActionCategories(actionCounts) >= 1;
    }

    private void CreateGhost()
    {
        if (_ghost != null)
        {
            _ghost.SetActive(true);
            return;
        }

        if (_player == null) _player = FindObjectOfType<PlayerController>();
        if (_player == null || _player.characterModel == null) return;

        _ghost = new GameObject("AI Shadow Runner");

        GameObject visual = Instantiate(_player.characterModel.gameObject, _ghost.transform);
        visual.name = "ShadowVisual";
        _ghostVisual = visual.transform;
        _ghostVisualPosition = _ghostVisual.localPosition;
        _ghostAnimator = visual.GetComponent<CharacterAnimator>();
        if (_ghostAnimator == null)
            _ghostAnimator = visual.GetComponentInChildren<CharacterAnimator>(true);
        if (_ghostAnimator != null) _ghostAnimator.SetExternalDriver();

        foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
            Destroy(collider);
        ApplyGhostMaterial(visual);

        _ghost.transform.position = _player.transform.position + Vector3.forward * 2f;
        CacheGhostGroundOffset();
    }

    private void CacheGhostGroundOffset()
    {
        if (_ghost == null) return;
        Renderer[] renderers = _ghost.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        _ghostRootToLowestPoint = Mathf.Max(0f,
            _ghost.transform.position.y - bounds.min.y);
    }

    private bool TryGetGhostGroundHeight(Vector3 target, out float groundHeight)
    {
        int groundMask = _player != null ? _player.groundLayer.value : 0;
        if (groundMask == 0) groundMask = Physics.DefaultRaycastLayers;
        Vector3 origin = new Vector3(target.x, target.y + 5f, target.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f,
                groundMask, QueryTriggerInteraction.Ignore))
        {
            groundHeight = hit.point.y;
            return true;
        }

        groundHeight = 0f;
        return false;
    }

    private void ApplyGhostMaterial(GameObject visual)
    {
        Shader shader = Resources.Load<Shader>("Shaders/EchoGhost");
        if (shader == null) shader = Shader.Find("EchoRun/GhostRunner");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        _ghostMaterial = new Material(shader)
        {
            color = new Color(0.22f, 0.84f, 1.00f, 0.66f),
            renderQueue = 3000
        };

        if (_ghostMaterial.HasProperty("_RimColor"))
            _ghostMaterial.SetColor("_RimColor", new Color(0.64f, 0.94f, 1f, 1f));
        if (_ghostMaterial.HasProperty("_RimPower"))
            _ghostMaterial.SetFloat("_RimPower", 2.1f);
        if (_ghostMaterial.HasProperty("_EmissionStrength"))
            _ghostMaterial.SetFloat("_EmissionStrength", 0.72f);
        if (_ghostMaterial.HasProperty("_ScanStrength"))
            _ghostMaterial.SetFloat("_ScanStrength", 0.22f);

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            Material[] sourceMaterials = renderer.sharedMaterials;
            Material[] ghostMaterials = new Material[sourceMaterials.Length];
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            for (int slot = 0; slot < ghostMaterials.Length; slot++)
                ghostMaterials[slot] = _ghostMaterial;
            renderer.sharedMaterials = ghostMaterials;

            for (int slot = 0; slot < sourceMaterials.Length; slot++)
            {
                Material sourceMaterial = sourceMaterials[slot];
                if (sourceMaterial == null || !sourceMaterial.HasProperty("_MainTex")
                    || !_ghostMaterial.HasProperty("_MainTex"))
                    continue;

                Texture sourceTexture = sourceMaterial.GetTexture("_MainTex");
                if (sourceTexture == null) continue;
                block.Clear();
                renderer.GetPropertyBlock(block, slot);
                block.SetTexture("_MainTex", sourceTexture);
                renderer.SetPropertyBlock(block, slot);
            }
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private void SetGhostActive(bool active)
    {
        if (_ghost != null) _ghost.SetActive(active);
    }

    private void LoadProfile()
    {
        _profile = null;
        EchoRunSaveSystem.EnsureInitialized();
        string json = EchoRunSaveSystem.GetShadowProfileJson();
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                _profile = JsonUtility.FromJson<ShadowProfile>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("AI shadow profile could not be loaded: " + exception.Message);
            }
        }

        if (_profile == null) _profile = new ShadowProfile { version = 5 };
        NormalizeProfile();
        _activeGeneration = EchoGenerationSnapshot.FromJson(
            _profile.activeGenerationJson);
        if (_activeGeneration == null && _profile.generation > 0
            && _profile.pace > 0f)
        {
            PlayerStyleData legacyStyle = StyleTracker.GetSnapshot();
            legacyStyle.Normalize();
            _activeGeneration = new EchoGenerationSnapshot
            {
                generation = _profile.generation,
                policyWeights = _profile.weights,
                sequenceTransitions = _profile.sequenceTransitions,
                sequencePairCount = _profile.sequencePairCount,
                styleJson = JsonUtility.ToJson(legacyStyle),
                pace = _profile.pace,
                clarity = Mathf.Max(0.2f, _profile.clarity)
            };
            _profile.activeGenerationJson = _activeGeneration.ToJson();
        }
        if (_activeGeneration != null)
        {
            _profile.generation = _activeGeneration.generation;
            _profile.clarity = _activeGeneration.clarity;
        }
        _policy = new AIShadowPolicy(_profile.weights);
        _sequencePolicy = new AIShadowSequencePolicy(_profile.sequenceTransitions,
            _profile.sequencePairCount);
    }

    private void SaveProfile()
    {
        if (_profile == null || _policy == null) return;
        _profile.weights = _policy.ExportWeights();
        if (_sequencePolicy != null)
        {
            AIShadowSequenceState state = _sequencePolicy.ExportState();
            _profile.sequenceTransitions = state.transitions;
            _profile.sequencePairCount = state.pairCount;
        }
        _profile.activeGenerationJson = _activeGeneration != null
            ? _activeGeneration.ToJson() : "";
        EchoRunSaveSystem.SaveShadowProfile(JsonUtility.ToJson(_profile));
    }

    private void NormalizeProfile()
    {
        if (_profile.version < 2)
        {
            if (_profile.generation > 0)
            {
                _profile.sampleCount = Mathf.Max(
                    _profile.sampleCount, minimumTrainingSamples);
                _profile.activeSampleCount = Mathf.Max(
                    _profile.activeSampleCount, minimumActiveTrainingSamples);
                _profile.actionCounts = new int[5];
                _profile.actionCounts[(int)ShadowAction.Left] = 1;
                _profile.actionCounts[(int)ShadowAction.Jump] = 1;
            }
        }

        if (_profile.version < 3)
        {
            // Older sequence data mixed passive Keep samples into action habits.
            _profile.sequenceTransitions = null;
            _profile.sequencePairCount = 0;
        }
        if (_profile.version < 4 && _profile.generation > 0)
            _profile.clarity = 1f;
        _profile.version = 5;
        _profile.clarity = Mathf.Clamp01(_profile.clarity);
        _profile.activeGenerationJson = _profile.activeGenerationJson ?? "";

        _profile.sampleCount = Mathf.Max(0, _profile.sampleCount);
        _profile.activeSampleCount = Mathf.Clamp(
            _profile.activeSampleCount, 0, _profile.sampleCount);
        EnsureActionCounts();
    }

    private void EnsureActionCounts()
    {
        if (_profile.actionCounts != null && _profile.actionCounts.Length == 5)
            return;

        int[] normalized = new int[5];
        if (_profile.actionCounts != null)
            Array.Copy(_profile.actionCounts, normalized,
                Mathf.Min(_profile.actionCounts.Length, normalized.Length));
        _profile.actionCounts = normalized;
    }

    void OnDestroy()
    {
        SaveProfile();
        if (_ghostMaterial != null) Destroy(_ghostMaterial);
        if (_gameManager != null)
            _gameManager.OnStateChanged.RemoveListener(OnGameStateChanged);
        if (Instance == this) Instance = null;
    }
}
