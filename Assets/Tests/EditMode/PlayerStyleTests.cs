using NUnit.Framework;

public class PlayerStyleTests
{
    [Test]
    public void PlayerStyleNormalizesAllPublicParameters()
    {
        var style = new PlayerStyleData
        {
            aggressiveness = 2f,
            jumpTiming = -3f,
            slideFrequency = -1f,
            lanePreference = 4f,
            rhythmStability = -2f,
            recoveryStyle = 3f
        };

        style.Normalize();

        Assert.AreEqual(1f, style.aggressiveness);
        Assert.AreEqual(-1f, style.jumpTiming);
        Assert.AreEqual(0f, style.slideFrequency);
        Assert.AreEqual(1f, style.lanePreference);
        Assert.AreEqual(0f, style.rhythmStability);
        Assert.AreEqual(1f, style.recoveryStyle);
    }

    [Test]
    public void LanePreferenceReranksEqualCandidateScores()
    {
        PlayerStyleData style = ConfidentStyle();
        style.lanePreference = -1f;
        var context = new ShadowDecisionContext { lane = 1 };
        var directive = ShadowAIDirective.Neutral;
        directive.decisionNoise = 0f;

        ShadowAction selected = new ShadowDecisionMaker().Select(
            new[] { 0.2f, 0.2f, 0.2f, 0.2f, 0.2f },
            style, context, directive, 0.5f);

        Assert.AreEqual(ShadowAction.Left, selected);
    }

    [Test]
    public void EmergencySafetyOverridesStyleAndBasePolicy()
    {
        PlayerStyleData style = ConfidentStyle();
        style.aggressiveness = 1f;
        var context = new ShadowDecisionContext
        {
            lane = 1,
            hasThreat = true,
            relativeThreatLane = 0,
            threatProximity = 0.9f,
            threatType = ObstacleType.High
        };

        ShadowAction selected = new ShadowDecisionMaker().Select(
            new[] { 1f, 0f, 0f, 0f, 0f },
            style, context, ShadowAIDirective.Neutral, 0.5f,
            out ShadowDecisionTrace trace);

        Assert.AreEqual(ShadowAction.Jump, selected);
        Assert.IsTrue(trace.safetyAdjusted);
        Assert.IsFalse(trace.feasibleActions[(int)ShadowAction.Keep]);
        Assert.IsTrue(trace.feasibleActions[(int)ShadowAction.Jump]);
        Assert.AreEqual(-999f,
            trace.finalScores[(int)ShadowAction.Keep]);
    }

    [Test]
    public void JumpTimingChangesReactionDistanceWithoutRemovingSafetyLimits()
    {
        PlayerStyleData early = ConfidentStyle();
        early.jumpTiming = -1f;
        PlayerStyleData late = ConfidentStyle();
        late.jumpTiming = 1f;

        float earlyMultiplier = ShadowDecisionMaker.ReactionDistanceMultiplier(
            early, ShadowAIDirective.Neutral);
        float lateMultiplier = ShadowDecisionMaker.ReactionDistanceMultiplier(
            late, ShadowAIDirective.Neutral);

        Assert.Greater(earlyMultiplier, 1f);
        Assert.Less(lateMultiplier, 1f);
        Assert.GreaterOrEqual(lateMultiplier, 0.62f);
        Assert.LessOrEqual(earlyMultiplier, 1.42f);
    }

    [Test]
    public void SlideOpportunityCountsLaneChangeAsUnusedChoice()
    {
        var tracker = new SlideOpportunityTracker();

        Assert.IsFalse(tracker.Update(1, false, true, 5f,
            ObstacleType.Low, 101, 7f, out _));
        Assert.IsTrue(tracker.HasPending);

        Assert.IsTrue(tracker.Update(0, false, false, 0f,
            ObstacleType.Low, 0, 7f, out bool usedSlide));
        Assert.IsFalse(usedSlide);
        Assert.IsFalse(tracker.HasPending);
        Assert.IsTrue(tracker.ResolvedIds.Contains(101));
    }

    [Test]
    public void SlideOpportunityPreservesSlideMadeInsideItsWindow()
    {
        var tracker = new SlideOpportunityTracker();
        tracker.Update(1, false, true, 5f,
            ObstacleType.Low, 202, 7f, out _);

        tracker.MarkSlide(1);
        Assert.IsTrue(tracker.Update(1, true, false, 0f,
            ObstacleType.Low, 0, 7f, out bool usedSlide));

        Assert.IsTrue(usedSlide);
        Assert.IsTrue(tracker.ResolvedIds.Contains(202));
    }

    [Test]
    public void SixCalibrationSignalsMoveInExpectedDirections()
    {
        var style = new PlayerStyleData();
        for (int i = 0; i < 12; i++)
        {
            style.ObserveAggressiveness(1f);
            style.ObserveJumpTiming(-0.8f);
            style.ObserveSlideOpportunity(true);
            style.ObserveLane(0);
            style.ObserveRhythm(0.95f);
            style.ObserveRecovery(0.9f);
        }

        Assert.Greater(style.aggressiveness, 0.75f);
        Assert.Less(style.jumpTiming, -0.55f);
        Assert.Greater(style.slideFrequency, 0.75f);
        Assert.Less(style.lanePreference, -0.7f);
        Assert.Greater(style.rhythmStability, 0.8f);
        Assert.Greater(style.recoveryStyle, 0.75f);
        Assert.Greater(style.Confidence, 0.5f);
    }

    [Test]
    public void DecisionTraceAndStyleSurviveTelemetryJsonRoundTrip()
    {
        PlayerStyleData style = ConfidentStyle();
        var data = new AIRunTelemetryData
        {
            playerStyleAtStart = style,
            shadowSamples = new System.Collections.Generic.List<AIShadowTrainingSample>
            {
                new AIShadowTrainingSample
                {
                    action = (int)ShadowAction.Left,
                    baseScores = new[] { 0.1f, 0.4f, 0.2f, 0.2f, 0.1f },
                    styleAdjustedScores = new[] { 0.1f, 0.7f, 0.1f, 0.1f, 0f },
                    finalScores = new[] { 0.1f, 0.7f, 0.1f, -999f, -999f },
                    feasibleActions = new[] { true, true, true, false, false },
                    safetyAdjusted = true,
                    directive = ShadowAIDirective.Neutral,
                    playerStyle = style.Clone()
                }
            }
        };

        AIRunTelemetryData restored = AIRunTelemetry.FromJson(
            UnityEngine.JsonUtility.ToJson(data));

        Assert.IsNotNull(restored.playerStyleAtStart);
        Assert.AreEqual(style.aggressiveness,
            restored.playerStyleAtStart.aggressiveness);
        Assert.AreEqual(0.7f,
            restored.shadowSamples[0].styleAdjustedScores[1]);
        Assert.IsFalse(restored.shadowSamples[0].feasibleActions[3]);
        Assert.IsTrue(restored.shadowSamples[0].safetyAdjusted);
    }

    private static PlayerStyleData ConfidentStyle()
    {
        return new PlayerStyleData
        {
            aggressivenessSamples = 20,
            jumpTimingSamples = 20,
            slideOpportunitySamples = 20,
            laneSamples = 20,
            rhythmSamples = 20,
            recoverySamples = 10,
            rhythmStability = 1f
        };
    }
}
