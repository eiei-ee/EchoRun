using UnityEngine;

public sealed class EchoHudPresenter : MonoBehaviour
{
    private EchoHudView _view;
    private GameManager _gameManager;
    private EchoHudMode _lastMode;
    private bool _hasMode;
    private float _announcementUntil;
    private float _feedbackUntil;
    private int _lastFeedbackSequence = -1;
    private bool _presentingSingleContract;
    private bool _hasSingleContractVisualState;
    private SingleContractVisualState _lastSingleContractVisualState;
    private bool _lastSingleContractOpeningMemory;
    private bool _lastSingleContractOpeningReplay;
    private bool _hasSingleContractPrediction;
    private string _lastSingleContractPredictionKey = "";
    private int _lastSingleContractPredictionGateNumber;

    public void Initialize(EchoHudView view, GameManager gameManager)
    {
        _view = view;
        _gameManager = gameManager;
        if (_view != null && _view.PauseButton != null)
        {
            _view.PauseButton.onClick.RemoveListener(Pause);
            _view.PauseButton.onClick.AddListener(Pause);
        }
    }

    public void Refresh(bool forceFeedback = false)
    {
        if (_view == null) return;
        if (_gameManager == null) _gameManager = GameManager.Instance;

        AIShadowRunner shadow = AIShadowRunner.Instance;
        string powerUpStatus = PowerUpController.Instance != null
            ? PowerUpController.Instance.GetStatusText() : "";
        if (IsSingleContractPresentation(shadow))
        {
            RefreshSingleContract(shadow, powerUpStatus, forceFeedback);
            return;
        }

        ReleaseSingleContractVisualState();
        bool showBuff = !string.IsNullOrEmpty(powerUpStatus)
                        || _gameManager != null
                        && _gameManager.BuffTimeRemaining > 0f;
        string buffText = !string.IsNullOrEmpty(powerUpStatus)
            ? powerUpStatus
            : _gameManager != null && showBuff
                ? string.Format("{0} {1:F1}s", _gameManager.BuffName ?? "Buff",
                    _gameManager.BuffTimeRemaining)
                : "";
        string rewriteStyleSummary = shadow != null
                                     && shadow.DuelPhase
                                     == EchoDuelPhase.Rewrite
            ? shadow.RewriteStyleSummary : "";
        string finaleSegmentSummary = shadow != null
                                      && shadow.DuelPhase
                                      == EchoDuelPhase.Finale
            ? shadow.FinaleSegmentSummary : "";

        EchoHudViewData data = EchoRunPresentation.BuildHud(
            shadow != null && shadow.HasActiveOpponent,
            shadow != null ? shadow.ActiveContract : null,
            shadow != null ? shadow.PlayerLead : 0f,
            shadow != null ? shadow.minimumJumpSamples : 2,
            shadow != null ? shadow.minimumSlideSamples : 2,
            shadow != null ? shadow.JumpTrainingSampleCount : 0,
            shadow != null ? shadow.SlideTrainingSampleCount : 0,
            shadow != null ? shadow.CalibrationProgress : 0f,
            shadow != null ? shadow.DuelPhase : EchoDuelPhase.Calibration,
            shadow != null ? shadow.DuelPhaseProgress : 0f,
            shadow != null ? shadow.PublicPrediction : "",
            _gameManager != null ? _gameManager.SyncRemaining : 2,
            _gameManager != null
                ? _gameManager.CollisionRecoveryTimeRemaining : 0f,
            _gameManager != null
                ? _gameManager.CollisionRecoveryDuration : 1.25f,
            _gameManager != null ? _gameManager.RemainingDistance : 0f,
            _gameManager != null ? _gameManager.ContractMarkerCount : 0,
            showBuff, buffText,
            shadow != null && shadow.DuelTransitionPending,
            shadow != null ? shadow.PendingDuelPhase : EchoDuelPhase.None,
            rewriteStyleSummary, finaleSegmentSummary,
            shadow != null ? shadow.ActiveChallengeStep : default);

        if (!_hasMode || data.mode != _lastMode)
        {
            _lastMode = data.mode;
            _hasMode = true;
            _announcementUntil = Time.unscaledTime + 1f;
        }
        _view.Present(data, Time.unscaledTime < _announcementUntil);
        _view.SetStats(_gameManager != null ? _gameManager.Score : 0,
            _gameManager != null ? _gameManager.Distance : 0f);

        EchoDuelViewData duel = EchoRunPresentation.BuildDuel(
            shadow != null && shadow.HasActiveOpponent,
            shadow != null ? shadow.ActiveContract : null,
            shadow != null ? shadow.PlayerLead : 0f,
            shadow != null ? shadow.minimumJumpSamples : 2,
            shadow != null ? shadow.minimumSlideSamples : 2,
            shadow != null ? shadow.JumpTrainingSampleCount : 0,
            shadow != null ? shadow.SlideTrainingSampleCount : 0,
            shadow != null ? shadow.CalibrationProgress : 0f,
            shadow != null ? shadow.DuelPhase : EchoDuelPhase.Calibration,
            shadow != null ? shadow.DuelPhaseProgress : 0f,
            shadow != null ? shadow.PublicPrediction : "");
        if (!string.IsNullOrEmpty(duel.feedback)
            && (forceFeedback || duel.feedbackSequence != _lastFeedbackSequence))
        {
            _lastFeedbackSequence = duel.feedbackSequence;
            _feedbackUntil = Time.unscaledTime + 1.8f;
        }
        Color feedbackColor = duel.feedback.StartsWith("回声施压")
                              || duel.feedback.StartsWith("命中")
                              || duel.feedback.StartsWith("预判命中")
                              || duel.feedback.StartsWith("重锁")
            ? EchoRunUITheme.HudDangerText
            : duel.feedback.StartsWith("预测失效")
              || duel.feedback.StartsWith("偏离")
              || duel.feedback.StartsWith("裂解")
              || duel.feedback.StartsWith("反制生效")
              || duel.feedback.StartsWith("锁定碎裂")
                ? EchoRunUITheme.HudRewardText
                : EchoRunUITheme.HudSuccessText;
        _view.ShowFeedback(duel.feedback, feedbackColor,
            Time.unscaledTime < _feedbackUntil);
    }

    public static SingleContractHudData BuildSingleContractHudData(
        GameManager gameManager, AIShadowRunner shadow, string powerUpStatus)
    {
        string activePowerUp = (powerUpStatus ?? "").Trim();
        if (string.IsNullOrEmpty(activePowerUp)
            && gameManager != null && gameManager.BuffTimeRemaining > 0f)
        {
            activePowerUp = string.Format("{0} {1:F1}s",
                gameManager.BuffName ?? "Buff", gameManager.BuffTimeRemaining);
        }

        return EchoRunPresentation.BuildSingleContractHud(
            new SingleContractHudInput
            {
                visualState = shadow != null
                    ? shadow.SingleContractVisualState
                    : SingleContractVisualState.Calibration,
                openingMemory = shadow != null
                                && shadow.IsSingleContractOpeningMemory,
                openingReplay = shadow != null
                                && shadow.HasSingleContractOpeningReplay,
                openingReplayAction = shadow != null
                    ? shadow.SingleContractOpeningReplayAction
                    : ShadowAction.Keep,
                openingReplayCount = shadow != null
                    ? shadow.SingleContractOpeningReplayCount : 0,
                generation = shadow != null ? shadow.Generation : 0,
                memory = shadow != null
                    ? shadow.SingleContractMemoryText
                    : "你的选择尚未形成稳定模式",
                showPrediction = shadow != null
                                 && shadow.ShowSingleContractPrediction,
                predictedLane = shadow != null
                    ? shadow.CurrentSingleContractPredictedLane : -1,
                predictionGateNumber = shadow != null
                    ? shadow.CurrentSingleContractPredictionGateNumber : 0,
                predictionGateCount = shadow != null
                    ? shadow.SingleContractPredictionGateCount : 0,
                predictionGateActive = shadow != null
                                       && shadow
                                           .IsCurrentSingleContractPredictionGateActive,
                leadMeters = shadow != null ? shadow.PlayerLead : 0f,
                injuries = gameManager != null
                    ? gameManager.CollisionStrikes : 0,
                finishRemaining = gameManager != null
                    ? gameManager.RemainingDistance : 0f,
                powerUp = activePowerUp,
                instantFeedback = shadow != null
                    ? shadow.SingleContractFeedback
                    : SingleContractInstantFeedback.None,
                feedbackLeadDeltaMeters = shadow != null
                    ? shadow.SingleContractFeedbackLeadDeltaMeters : 0f,
                feedbackSequence = shadow != null
                    ? shadow.SingleContractFeedbackSequence : 0,
                calibrationProgress = shadow != null
                    ? shadow.CurrentSingleContractCalibrationProgress
                    : default,
                result = shadow != null ? shadow.LastResult : ""
            });
    }

    public void ReleaseSingleContractVisualState()
    {
        if (_view != null) _view.StopSingleContractTransition();
        ResetSingleContractPredictionTracking();
        EchoPhaseVisualController visual = EchoPhaseVisualController.Instance;
        if (visual != null && visual.UsesSingleContractVisualState)
            visual.ReleaseSingleContractVisualState();
        if (!_presentingSingleContract) return;

        _presentingSingleContract = false;
        _hasSingleContractVisualState = false;
        _lastSingleContractOpeningMemory = false;
        _lastSingleContractOpeningReplay = false;
        _hasMode = false;
    }

    public void ResetRun()
    {
        if (_view != null) _view.StopSingleContractTransition();
        ResetSingleContractPredictionTracking();
        _hasMode = false;
        _lastFeedbackSequence = -1;
        _feedbackUntil = 0f;
        _announcementUntil = 0f;
        _hasSingleContractVisualState = false;
        _lastSingleContractOpeningMemory = false;
        _lastSingleContractOpeningReplay = false;
    }

    private void RefreshSingleContract(AIShadowRunner shadow,
        string powerUpStatus, bool forceFeedback)
    {
        SingleContractHudData data = BuildSingleContractHudData(
            _gameManager, shadow, powerUpStatus);
        bool enteringSingleContract = !_presentingSingleContract;
        if (enteringSingleContract)
            ResetSingleContractPredictionTracking();
        bool hadPreviousState = !enteringSingleContract
                                && _hasSingleContractVisualState;
        SingleContractVisualState previousState =
            _lastSingleContractVisualState;
        bool openingChanged = !enteringSingleContract
                              && data.openingMemory
                              != _lastSingleContractOpeningMemory;
        bool endingOpeningReplay = openingChanged
                                   && !data.openingMemory
                                   && _lastSingleContractOpeningReplay;
        bool stateChanged = enteringSingleContract
                            || !_hasSingleContractVisualState
                            || data.visualState
                            != _lastSingleContractVisualState;
        bool emphasizeTransition =
            ShouldEmphasizeSingleContractTransition(
                hadPreviousState, previousState, data.visualState,
                data.openingMemory,
                openingChanged && !endingOpeningReplay,
                data.openingReplay);
        bool returningFromRelearn = hadPreviousState
                                    && previousState
                                    == SingleContractVisualState.RelearnPulse
                                    && data.visualState
                                    == SingleContractVisualState.Challenge;
        bool emphasizePredictionChange =
            ShouldEmphasizeSingleContractPredictionChange(
                _hasSingleContractPrediction,
                _lastSingleContractPredictionKey,
                _lastSingleContractPredictionGateNumber,
                data, emphasizeTransition, returningFromRelearn);
        if (stateChanged || openingChanged)
        {
            _presentingSingleContract = true;
            _hasSingleContractVisualState = true;
            _lastSingleContractVisualState = data.visualState;
            _lastSingleContractOpeningMemory = data.openingMemory;
            _lastSingleContractOpeningReplay = data.openingReplay;
            _announcementUntil = emphasizeTransition
                ? Time.unscaledTime + 1f : 0f;
            if (enteringSingleContract) _lastFeedbackSequence = -1;
        }

        EchoPhaseVisualController visual = EchoPhaseVisualController.Instance;
        if (visual != null)
            visual.ApplySingleContractVisualState(data.visualState);

        _view.PresentSingleContract(data,
            Time.unscaledTime < _announcementUntil);
        if (emphasizeTransition)
            _view.PlaySingleContractTransition(data.visualState);
        else if (emphasizePredictionChange)
            _view.PlayPredictionChangeTransition();
        TrackSingleContractPrediction(data);
        _view.SetStats(_gameManager != null ? _gameManager.Score : 0,
            _gameManager != null ? _gameManager.Distance : 0f);

        if (!string.IsNullOrEmpty(data.instantFeedback)
            && (forceFeedback
                || data.feedbackSequence != _lastFeedbackSequence))
        {
            _lastFeedbackSequence = data.feedbackSequence;
            _feedbackUntil = Time.unscaledTime
                             + EchoRunPresentation
                                 .SingleContractFeedbackDurationSeconds;
        }
        _view.ShowFeedback(data.instantFeedback,
            SingleContractFeedbackColor(data.instantFeedbackKind),
            !data.openingMemory && Time.unscaledTime < _feedbackUntil);
    }

    public static bool ShouldEmphasizeSingleContractTransition(
        bool hasPreviousState, SingleContractVisualState previousState,
        SingleContractVisualState currentState, bool openingMemory,
        bool openingChanged, bool openingReplay = false)
    {
        if (openingMemory)
            return openingReplay && (!hasPreviousState || openingChanged);
        if (hasPreviousState
            && previousState == SingleContractVisualState.RelearnPulse
            && currentState == SingleContractVisualState.Challenge)
            return false;
        return !hasPreviousState || previousState != currentState
               || openingChanged;
    }

    public static bool ShouldEmphasizeSingleContractPredictionChange(
        bool hasPreviousPrediction, string previousPredictionKey,
        int previousGateNumber, SingleContractHudData current,
        bool stageTransitionEmphasized, bool returningFromRelearn)
    {
        if (current.openingMemory || stageTransitionEmphasized
            || returningFromRelearn || !hasPreviousPrediction)
            return false;

        string currentKey = SingleContractPredictionSemanticKey(current);
        if (string.IsNullOrEmpty(currentKey)) return false;
        return previousGateNumber != current.predictionGateNumber
               || !string.Equals(previousPredictionKey, currentKey);
    }

    public static string SingleContractPredictionSemanticKey(
        SingleContractHudData data)
    {
        string value = (data.prediction ?? "").Trim();
        if (string.IsNullOrEmpty(value)) return "";
        const string playerToken = "它猜";
        const string legacyToken = "预测：";
        int start = value.IndexOf(playerToken);
        int tokenLength = playerToken.Length;
        if (start < 0)
        {
            start = value.IndexOf(legacyToken);
            tokenLength = legacyToken.Length;
        }
        if (start < 0) return value;
        start += tokenLength;
        int lineEnd = value.IndexOf('\n', start);
        if (lineEnd < 0) lineEnd = value.Length;
        return value.Substring(start, lineEnd - start).Trim();
    }

    private void TrackSingleContractPrediction(SingleContractHudData data)
    {
        if (data.openingMemory) return;
        string key = SingleContractPredictionSemanticKey(data);
        if (string.IsNullOrEmpty(key))
        {
            if (data.visualState == SingleContractVisualState.Calibration)
                ResetSingleContractPredictionTracking();
            return;
        }

        _hasSingleContractPrediction = true;
        _lastSingleContractPredictionKey = key;
        _lastSingleContractPredictionGateNumber = data.predictionGateNumber;
    }

    private void ResetSingleContractPredictionTracking()
    {
        _hasSingleContractPrediction = false;
        _lastSingleContractPredictionKey = "";
        _lastSingleContractPredictionGateNumber = 0;
    }

    private bool IsSingleContractPresentation(AIShadowRunner shadow)
    {
        if (_gameManager != null) return _gameManager.IsSingleContractRun;
        return shadow != null && shadow.ActiveGameplayFlowMode
            == GameplayFlowMode.SingleContract;
    }

    private static Color SingleContractFeedbackColor(
        SingleContractInstantFeedback feedback)
    {
        switch (feedback)
        {
            case SingleContractInstantFeedback.PredictionHit:
            case SingleContractInstantFeedback.CounterFailed:
            case SingleContractInstantFeedback.EchoRelearned:
                return EchoRunUITheme.HudDangerText;
            case SingleContractInstantFeedback.RewriteSucceeded:
                return EchoRunUITheme.HudRewardText;
            default:
                return EchoRunUITheme.HudSuccessText;
        }
    }

    private void Pause()
    {
        if (_gameManager != null) _gameManager.Pause();
    }

    private void OnDestroy()
    {
        ReleaseSingleContractVisualState();
        if (_view != null && _view.PauseButton != null)
            _view.PauseButton.onClick.RemoveListener(Pause);
    }
}
