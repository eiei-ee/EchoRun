using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum ShadowAction
{
    Keep,
    Left,
    Right,
    Jump,
    Slide
}

// Small online softmax classifier used for runtime behavior cloning.
// The model is intentionally pure C# so it runs identically in WebGL.
public sealed class AIShadowPolicy
{
    public const int FeatureCount = 8;
    public const int ActionCount = 5;

    private readonly float[,] _weights = new float[ActionCount, FeatureCount];

    public AIShadowPolicy(float[] savedWeights = null)
    {
        if (savedWeights != null && savedWeights.Length == ActionCount * FeatureCount)
        {
            int index = 0;
            for (int action = 0; action < ActionCount; action++)
                for (int feature = 0; feature < FeatureCount; feature++)
                    _weights[action, feature] = savedWeights[index++];
        }
        else
        {
            // A slight keep prior prevents an untrained shadow from changing lanes constantly.
            _weights[(int)ShadowAction.Keep, 0] = 0.25f;
        }
    }

    public int Predict(float[] features)
    {
        Validate(features);
        int bestAction = 0;
        float bestScore = Score(0, features);
        for (int action = 1; action < ActionCount; action++)
        {
            float score = Score(action, features);
            if (score <= bestScore) continue;
            bestAction = action;
            bestScore = score;
        }
        return bestAction;
    }

    public float Confidence(float[] features)
    {
        float[] probabilities = Probabilities(features);
        float best = 0f;
        for (int i = 0; i < probabilities.Length; i++)
            best = Mathf.Max(best, probabilities[i]);
        return best;
    }

    public float[] GetProbabilities(float[] features)
    {
        return Probabilities(features);
    }

    public void Learn(int label, float[] features, float learningRate)
    {
        Validate(features);
        if (label < 0 || label >= ActionCount)
            throw new ArgumentOutOfRangeException(nameof(label));

        float[] probabilities = Probabilities(features);
        float rate = Mathf.Clamp(learningRate, 0.001f, 0.5f);
        for (int action = 0; action < ActionCount; action++)
        {
            float target = action == label ? 1f : 0f;
            float error = target - probabilities[action];
            for (int feature = 0; feature < FeatureCount; feature++)
            {
                float updated = _weights[action, feature]
                                + rate * error * features[feature];
                _weights[action, feature] = Mathf.Clamp(updated, -4f, 4f);
            }
        }
    }

    public float Score(int action, float[] features)
    {
        Validate(features);
        if (action < 0 || action >= ActionCount)
            throw new ArgumentOutOfRangeException(nameof(action));

        float score = 0f;
        for (int feature = 0; feature < FeatureCount; feature++)
            score += _weights[action, feature] * features[feature];
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

    private float[] Probabilities(float[] features)
    {
        Validate(features);
        float[] result = new float[ActionCount];
        float maxScore = Score(0, features);
        for (int action = 1; action < ActionCount; action++)
            maxScore = Mathf.Max(maxScore, Score(action, features));

        float total = 0f;
        for (int action = 0; action < ActionCount; action++)
        {
            result[action] = Mathf.Exp(Score(action, features) - maxScore);
            total += result[action];
        }

        for (int action = 0; action < ActionCount; action++)
            result[action] /= Mathf.Max(0.0001f, total);
        return result;
    }

    private static void Validate(float[] features)
    {
        if (features == null || features.Length != FeatureCount)
            throw new ArgumentException("AI shadow input must contain eight features.", nameof(features));
    }
}

public class AIShadowRunner : MonoBehaviour
{
    public static AIShadowRunner Instance { get; private set; }

    [Header("Behavior Cloning")]
    [Range(0.001f, 0.5f)] public float learningRate = 0.08f;
    public int minimumTrainingSamples = 24;
    public int minimumActiveTrainingSamples = 6;
    public int minimumActionCategories = 2;
    public float decisionInterval = 0.35f;
    public float keepSampleInterval = 0.7f;
    public float minimumLaneHoldTime = 0.65f;

    [Header("Visual Smoothing")]
    public float laneSmoothTime = 0.14f;
    public float distanceSmoothTime = 0.12f;

    [Header("Duel")]
    public float coinProgressBonus = 1.5f;
    public float dodgeProgressBonus = 2.5f;
    public float shadowPaceMultiplier = 1.02f;
    public float maximumVisibleLead = 16f;

    public string CurrentStatus { get; private set; } = "AI影子 · 等待校准";
    public string LastResult { get; private set; } = "";
    public float PlayerLead { get; private set; }
    public bool HasActiveOpponent { get; private set; }
    public int Generation => _profile != null ? _profile.generation : 0;
    public int TrainingSampleCount => _profile != null ? _profile.sampleCount : 0;
    public int ActiveTrainingSampleCount =>
        _profile != null ? _profile.activeSampleCount : 0;
    public float DuelPressure => HasActiveOpponent
        ? 1f - Mathf.Clamp01(Mathf.Abs(PlayerLead) / 14f)
        : 0f;

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
    }

    private ShadowProfile _profile;
    private AIShadowPolicy _policy;
    private AIShadowPolicy _opponentPolicy;
    private AIShadowSequencePolicy _sequencePolicy;
    private AIShadowSequencePolicy _opponentSequencePolicy;
    private GameManager _gameManager;
    private PlayerController _player;
    private GameObject _ghost;
    private Transform _ghostVisual;
    private Vector3 _ghostVisualScale = Vector3.one;
    private Vector3 _ghostVisualPosition;
    private Vector3 _ghostForward = Vector3.forward;
    private Material _ghostMaterial;
    private int _ghostLane = 1;
    private float _displayedGhostLane = 1f;
    private float _displayedGap;
    private float _ghostGroundY;
    private float _laneSmoothVelocity;
    private float _gapSmoothVelocity;
    private float _laneDecisionCooldown;
    private float _ghostProgress;
    private float _playerProgress;
    private float _runTime;
    private float _decisionTimer;
    private float _keepSampleTimer;
    private float _ghostJumpTimer;
    private float _ghostSlideTimer;
    private float _ghostStumbleTimer;
    private float _decisionConfidence;
    private float _sequenceInfluence;
    private int _runCoins;
    private int _runDodges;
    private int _ghostMistakes;
    private int _samplesSinceCheckpoint;
    private int _lastTrainingAction = -1;
    private int _lastOpponentAction = -1;
    private bool _runStarted;
    private bool _runFinalized;
    private readonly HashSet<int> _handledGhostObstacles = new HashSet<int>();
    private readonly HashSet<int> _reactedGhostObstacles = new HashSet<int>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        LoadProfile();
    }

    void Start()
    {
        _gameManager = GameManager.Instance;
        _player = FindObjectOfType<PlayerController>();
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

        _runTime += Time.deltaTime;
        _playerProgress = _gameManager.Distance
                          + _runCoins * coinProgressBonus
                          + _runDodges * dodgeProgressBonus;

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
        _ghostJumpTimer = Mathf.Max(0f, _ghostJumpTimer - Time.deltaTime);
        _ghostSlideTimer = Mathf.Max(0f, _ghostSlideTimer - Time.deltaTime);
        float stumbleSpeed = _ghostStumbleTimer > 0f ? 0.25f : 1f;
        _ghostProgress += Mathf.Max(1f, _profile.pace)
                          * shadowPaceMultiplier * stumbleSpeed * Time.deltaTime;
        PlayerLead = _playerProgress - _ghostProgress;

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
        return HasTrainedProfile()
            ? "挑战第 " + _profile.generation + " 代个人 AI 影子"
            : "首局校准：AI 将学习你的跑酷习惯";
    }

    public void RecordPlayerAction(ShadowAction action, int laneBeforeAction)
    {
        if (_gameManager == null || _gameManager.State != GameState.Playing) return;
        if (!_runStarted) BeginRun();
        AIRunTelemetry.RecordEvent(
            "player_action", (int)action, laneBeforeAction);
        float[] features = BuildFeatures(laneBeforeAction, false);
        AIPlayerSkillEstimator.RecordAction(action, features);
        Learn(action, features);
        _keepSampleTimer = 0f;
    }

    public void RecordCoin()
    {
        _runCoins++;
        AIRunTelemetry.RecordEvent("coin", 0,
            _player != null ? _player.CurrentLane : -1, _runCoins);
    }

    public void RecordDodge()
    {
        _runDodges++;
        AIRunTelemetry.RecordEvent("dodge", 0,
            _player != null ? _player.CurrentLane : -1, _runDodges);
    }

    public void RecordObstacleHit()
    {
        if (HasActiveOpponent) PlayerLead -= 2f;
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
        return _policy != null ? _policy.ExportWeights() : null;
    }

    public string GetSequenceStateSnapshot()
    {
        return _sequencePolicy == null
            ? ""
            : JsonUtility.ToJson(_sequencePolicy.ExportState());
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
        _runCoins = 0;
        _runDodges = 0;
        _playerProgress = 0f;
        _ghostProgress = 0f;
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
        _sequenceInfluence = 0f;
        _ghostMistakes = 0;
        _lastTrainingAction = -1;
        _lastOpponentAction = -1;
        _handledGhostObstacles.Clear();
        _reactedGhostObstacles.Clear();
        HasActiveOpponent = HasTrainedProfile();

        if (HasActiveOpponent)
        {
            // Freeze the previous generation for this duel. New player actions train
            // the next generation and cannot make the current shadow mirror inputs.
            _opponentPolicy = new AIShadowPolicy(_policy.ExportWeights());
            AIShadowSequenceState state = _sequencePolicy.ExportState();
            _opponentSequencePolicy = new AIShadowSequencePolicy(
                state.transitions, state.pairCount);
            CreateGhost();
            CurrentStatus = "AI影子 · 第 " + _profile.generation + " 代已加入挑战";
        }
        else
        {
            CurrentStatus = "AI影子 · 校准中 0%";
            SetGhostActive(false);
        }
    }

    private void FinishRun()
    {
        if (!_runStarted || _runFinalized) return;
        _runFinalized = true;

        bool challengedOpponent = HasActiveOpponent;
        bool playerWon = PlayerLead >= 0f;
        float runPace = _playerProgress / Mathf.Max(1f, _runTime);
        if (_profile.pace <= 0f) _profile.pace = runPace;
        else _profile.pace = Mathf.Lerp(_profile.pace, runPace, 0.35f);
        _profile.bestProgress = Mathf.Max(_profile.bestProgress, _playerProgress);
        bool completedCalibration = HasCalibrationSamples(
            _profile.sampleCount, _profile.activeSampleCount,
            _profile.actionCounts, minimumTrainingSamples,
            minimumActiveTrainingSamples, minimumActionCategories);
        if (challengedOpponent || completedCalibration)
            _profile.generation++;
        _profile.weights = _policy.ExportWeights();
        SaveProfile();

        if (!challengedOpponent && !completedCalibration)
        {
            int categories = CountTrainedActionCategories(_profile.actionCounts);
            LastResult = "校准未完成 · 有效动作 "
                         + _profile.activeSampleCount + "/"
                         + Mathf.Max(1, minimumActiveTrainingSamples)
                         + " · 动作类型 " + categories + "/"
                         + Mathf.Max(1, minimumActionCategories)
                         + " · 再跑一局继续训练";
        }
        else if (!challengedOpponent)
        {
            LastResult = "校准完成 · 第 1 代 AI 影子已生成";
        }
        else if (playerWon)
        {
            LastResult = "挑战成功 · 领先影子 " + Mathf.Abs(PlayerLead).ToString("0.0")
                         + "m · 影子失误 " + _ghostMistakes
                         + " 次 · 第 " + _profile.generation + " 代已进化";
        }
        else
        {
            LastResult = "影子胜出 · 落后 " + Mathf.Abs(PlayerLead).ToString("0.0")
                         + "m · 影子失误 " + _ghostMistakes
                         + " 次 · 第 " + _profile.generation + " 代已进化";
        }

        CurrentStatus = LastResult;
        AIRunTelemetry.RecordEvent("shadow_result",
            challengedOpponent ? (playerWon ? 1 : -1) : 0,
            _ghostLane, PlayerLead, _ghostMistakes);
        HasActiveOpponent = false;
        SetGhostActive(false);
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
        _policy.Learn((int)action, features, learningRate);
        _sequencePolicy.Learn(_lastTrainingAction, (int)action);
        _lastTrainingAction = (int)action;
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
                minimumActiveTrainingSamples, minimumActionCategories);
            CurrentStatus = "AI影子 · 校准 " + (progress * 100f).ToString("0")
                            + "% · 有效动作 " + _profile.activeSampleCount
                            + "/" + Mathf.Max(1, minimumActiveTrainingSamples);
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
        ShadowAction baseAction = (ShadowAction)_opponentPolicy.Predict(features);
        ShadowAction action = PredictOpponentAction(features, out float baseConfidence,
            out float sequenceConfidence, out float sequenceInfluence);
        _decisionConfidence = Mathf.Max(baseConfidence, sequenceConfidence);
        _sequenceInfluence = sequenceInfluence;
        AIRunTelemetry.RecordShadowSample(
            action, _ghostLane, features, true, _decisionConfidence, (int)baseAction,
            sequenceConfidence, sequenceInfluence);

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
        if (threatDistance > CalculateReactionDistance(speed, duration)) return;

        // A trained clone gets the full reaction window when it predicts the
        // learned move. The close-range reflex is the physical safety layer:
        // it keeps an imperfect model readable without erasing earlier/later
        // reaction timing learned from the player.
        if (_opponentPolicy != null)
        {
            ShadowAction predicted = PredictOpponentAction(
                BuildFeatures(_ghostLane, true), out _, out _, out _);
            float emergencyDistance = Mathf.Clamp(speed * 0.2f, 2f, 4.5f);
            if (predicted != requiredAction && threatDistance > emergencyDistance)
                return;
        }

        _reactedGhostObstacles.Add(obstacleId);
        if (StartGhostAction(requiredAction))
            RecordOpponentAction(requiredAction);
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

        if (!_player.IsJumping)
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
            Vector3 scale = _ghostVisualScale;
            float slideAmount = EvaluateSlideAmount(
                _ghostSlideTimer, GetGhostSlideDuration());
            scale.y *= Mathf.Lerp(1f, 0.52f, slideAmount);
            _ghostVisual.localScale = scale;
            _ghostVisual.localPosition = _ghostVisualPosition;
        }

        if (_ghostMaterial != null)
        {
            _ghostMaterial.color = _ghostStumbleTimer > 0f
                ? new Color(0.9f, 0.2f, 0.16f, 0.48f)
                : new Color(0.16f, 0.68f, 0.74f, 0.28f);
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
        PlayerLead = _playerProgress - _ghostProgress;
    }

    private string BuildDuelStatus()
    {
        if (_ghostStumbleTimer > 0f)
            return "AI影子 · 撞击失速 · 你获得追赶机会";

        string lead = PlayerLead >= 0f
            ? "领先 " + PlayerLead.ToString("0.0") + "m"
            : "落后 " + Mathf.Abs(PlayerLead).ToString("0.0") + "m";
        string sequence = _sequenceInfluence > 0.01f
            ? " · 序列 " + (_sequenceInfluence * 100f).ToString("0") + "%"
            : "";
        return "AI影子 · 第 " + _profile.generation + " 代 · " + lead
                + " · 决策置信 " + (_decisionConfidence * 100f).ToString("0")
                + "%" + sequence + " · 失误 " + _ghostMistakes;
    }

    public static bool CanAvoidObstacle(ObstacleType obstacleType,
        bool isJumping, bool isSliding)
    {
        if (obstacleType == ObstacleType.Low) return isSliding;
        if (obstacleType == ObstacleType.High) return isJumping;
        return false;
    }

    public static ShadowAction RequiredActionForObstacle(ObstacleType obstacleType)
    {
        if (obstacleType == ObstacleType.Low) return ShadowAction.Slide;
        if (obstacleType == ObstacleType.High) return ShadowAction.Jump;
        return ShadowAction.Keep;
    }

    public static bool CanStartVerticalAction(ShadowAction action,
        bool isJumping, bool isSliding, bool isStumbling)
    {
        if (isJumping || isSliding || isStumbling) return false;
        return action == ShadowAction.Jump || action == ShadowAction.Slide;
    }

    public static float CalculateReactionDistance(float speed, float actionDuration)
    {
        return Mathf.Clamp(Mathf.Max(0f, speed) * Mathf.Max(0.1f, actionDuration)
                           * 0.48f, 3.5f, 8f);
    }

    public static float EvaluateJumpArc(float normalizedProgress)
    {
        float sine = Mathf.Sin(Mathf.Clamp01(normalizedProgress) * Mathf.PI);
        return sine * sine;
    }

    public static float EvaluateSlideAmount(float remainingTime, float duration)
    {
        if (remainingTime <= 0f || duration <= 0f) return 0f;
        float elapsed = duration - Mathf.Clamp(remainingTime, 0f, duration);
        const float blendTime = 0.08f;
        float enter = Mathf.Clamp01(elapsed / blendTime);
        float exit = Mathf.Clamp01(remainingTime / blendTime);
        return Mathf.SmoothStep(0f, 1f, Mathf.Min(enter, exit));
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
        return _profile != null && _profile.generation > 0
               && HasCalibrationSamples(
                   _profile.sampleCount, _profile.activeSampleCount,
                   _profile.actionCounts, minimumTrainingSamples,
                   minimumActiveTrainingSamples, minimumActionCategories)
               && _profile.pace > 0f;
    }

    public static bool HasCalibrationSamples(int totalSamples, int activeSamples,
        int[] actionCounts, int minimumTotal, int minimumActive,
        int minimumCategories)
    {
        return totalSamples >= Mathf.Max(1, minimumTotal)
               && activeSamples >= Mathf.Max(1, minimumActive)
               && CountTrainedActionCategories(actionCounts)
               >= Mathf.Max(1, minimumCategories);
    }

    public static float CalculateCalibrationProgress(int totalSamples,
        int activeSamples, int[] actionCounts, int minimumTotal,
        int minimumActive, int minimumCategories)
    {
        float totalProgress = Mathf.Clamp01(
            (float)Mathf.Max(0, totalSamples) / Mathf.Max(1, minimumTotal));
        float activeProgress = Mathf.Clamp01(
            (float)Mathf.Max(0, activeSamples) / Mathf.Max(1, minimumActive));
        float categoryProgress = Mathf.Clamp01(
            (float)CountTrainedActionCategories(actionCounts)
            / Mathf.Max(1, minimumCategories));
        return Mathf.Min(totalProgress, activeProgress, categoryProgress);
    }

    public static int CountTrainedActionCategories(int[] actionCounts)
    {
        if (actionCounts == null || actionCounts.Length < 5) return 0;
        int categories = actionCounts[(int)ShadowAction.Left]
                         + actionCounts[(int)ShadowAction.Right] > 0 ? 1 : 0;
        if (actionCounts[(int)ShadowAction.Jump] > 0) categories++;
        if (actionCounts[(int)ShadowAction.Slide] > 0) categories++;
        return categories;
    }

    private void CreateGhost()
    {
        if (_ghost != null)
        {
            _ghost.SetActive(true);
            return;
        }

        _ghost = new GameObject("AI Shadow Runner");
        if (_player != null && _player.characterModel != null)
        {
            GameObject visual = Instantiate(_player.characterModel.gameObject, _ghost.transform);
            visual.name = "ShadowVisual";
            _ghostVisual = visual.transform;
            _ghostVisualScale = _ghostVisual.localScale;
            _ghostVisualPosition = _ghostVisual.localPosition;

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                Destroy(collider);
            ApplyGhostMaterial(visual);
        }
        else
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "ShadowVisual";
            body.transform.SetParent(_ghost.transform, false);
            body.transform.localPosition = Vector3.up;
            Destroy(body.GetComponent<Collider>());
            _ghostVisual = body.transform;
            _ghostVisualScale = body.transform.localScale;
            _ghostVisualPosition = body.transform.localPosition;
            ApplyGhostMaterial(body);
        }

        _ghost.transform.position = _player != null
            ? _player.transform.position + Vector3.forward * 2f
            : Vector3.zero;
    }

    private void ApplyGhostMaterial(GameObject visual)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return;

        _ghostMaterial = new Material(shader)
        {
            color = new Color(0.16f, 0.68f, 0.74f, 0.28f),
            renderQueue = 3000
        };

        foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterial = _ghostMaterial;
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

        if (_profile == null) _profile = new ShadowProfile { version = 2 };
        NormalizeProfile();
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
        EchoRunSaveSystem.SaveShadowProfile(JsonUtility.ToJson(_profile));
    }

    private void NormalizeProfile()
    {
        if (_profile.version < 2)
        {
            _profile.version = 2;
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
        if (_gameManager != null)
            _gameManager.OnStateChanged.RemoveListener(OnGameStateChanged);
        if (Instance == this) Instance = null;
    }
}
