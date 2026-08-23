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
            rewriteStyleSummary, finaleSegmentSummary);

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
            ? EchoRunUITheme.Danger
            : duel.feedback.StartsWith("预测失效")
                ? EchoRunUITheme.Reward : EchoRunUITheme.RouteCyan;
        _view.ShowFeedback(duel.feedback, feedbackColor,
            Time.unscaledTime < _feedbackUntil);
    }

    public void ResetRun()
    {
        _hasMode = false;
        _lastFeedbackSequence = -1;
        _feedbackUntil = 0f;
        _announcementUntil = 0f;
    }

    private void Pause()
    {
        if (_gameManager != null) _gameManager.Pause();
    }

    private void OnDestroy()
    {
        if (_view != null && _view.PauseButton != null)
            _view.PauseButton.onClick.RemoveListener(Pause);
    }
}
