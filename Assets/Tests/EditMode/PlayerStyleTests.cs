using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PlayerStyleTests
{
    [Test]
    public void MalformedTelemetryJsonFallsBackWithoutThrowing()
    {
        LogAssert.Expect(LogType.Warning,
            new System.Text.RegularExpressions.Regex(
                "AI run telemetry could not be loaded:"));
        Assert.IsNull(AIRunTelemetry.FromJson("{not-json"));
    }

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
        Assert.AreEqual(ShadowAction.Keep, trace.originalPrediction);
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
    public void ChoiceGroupWaitsForFinalLaneBeforeSettling()
    {
        var tracker = new ObstacleOpportunityTracker();
        EchoChoiceGroup group = ChoiceGroup(11, 100f, 0, 1);

        Assert.IsFalse(tracker.UpdateGroup(group, 0, 95f,
            false, false, 7f, out _));
        Assert.IsTrue(tracker.HasPending);

        Assert.IsFalse(tracker.UpdateGroup(group, 1, 99f,
            false, false, 7f, out _),
            "Changing lanes must not close the row before the chosen obstacle.");
        tracker.MarkAction(ShadowAction.Slide, 1);
        Assert.IsTrue(tracker.UpdateGroup(null, 1, 102f,
            false, true, 7f,
            out ObstacleOpportunityResolution result));
        Assert.AreEqual(EchoResponseKind.Slide, result.response);
        Assert.AreEqual(11, result.groupId);
        Assert.AreEqual(0, result.entryLane);
        Assert.AreEqual(1, result.finalLane);
        Assert.IsTrue(result.laneChanged);
        Assert.IsTrue(result.physicallySucceeded);
        Assert.IsFalse(tracker.HasPending);
    }

    [Test]
    public void OpportunityPreservesCleanSlideMadeInsideItsWindow()
    {
        var tracker = new ObstacleOpportunityTracker();
        EchoChoiceGroup group = ChoiceGroup(22, 100f, 0, 1);
        tracker.UpdateGroup(group, 1, 95f,
            false, false, 7f, out _);

        tracker.MarkAction(ShadowAction.Slide, 1);
        Assert.IsTrue(tracker.UpdateGroup(null, 1, 102f,
            false, true, 7f,
            out ObstacleOpportunityResolution result));

        Assert.AreEqual(EchoResponseKind.Slide, result.response);
        Assert.IsTrue(result.physicallySucceeded);
        Assert.IsTrue(result.passedInLane);
        Assert.IsTrue(tracker.ResolvedOpportunityIds.Contains(222));
    }

    [Test]
    public void OpportunityPreservesCleanJumpAndDoesNotResolveGroupTwice()
    {
        var tracker = new ObstacleOpportunityTracker();
        EchoChoiceGroup group = ChoiceGroup(33, 100f, 2, 1);
        tracker.UpdateGroup(group, 2, 96f,
            false, false, 7f, out _);
        tracker.MarkAction(ShadowAction.Jump, 2);

        Assert.IsTrue(tracker.UpdateGroup(null, 2, 102f,
            true, false, 7f,
            out ObstacleOpportunityResolution result));
        Assert.AreEqual(EchoResponseKind.Jump, result.response);
        Assert.IsTrue(result.physicallySucceeded);

        Assert.IsFalse(tracker.UpdateGroup(group, 1, 96f,
            false, false, 7f, out _),
            "Another lane in the resolved row must not become a second choice.");
        Assert.IsFalse(tracker.HasPending);
    }

    [Test]
    public void HoldingAnAlreadyClearLaneIsNotReportedAsLaneChange()
    {
        var tracker = new ObstacleOpportunityTracker();
        EchoChoiceGroup group = ChoiceGroup(44, 100f, 0, 2);
        Assert.IsFalse(tracker.UpdateGroup(group, 1, 95f,
            false, false, 7f, out _));
        Assert.IsTrue(tracker.UpdateGroup(null, 1, 102f,
            true, false, 7f,
            out ObstacleOpportunityResolution result));
        Assert.AreEqual(EchoResponseKind.ClearRoute, result.response);
        Assert.IsFalse(result.laneChanged);
        Assert.AreEqual(1, result.entryLane);
        Assert.AreEqual(1, result.finalLane);
        Assert.IsTrue(result.physicallySucceeded);
        Assert.AreEqual(44, result.groupId);
    }

    [Test]
    public void MovingToClearLanePreservesLaneChangeSeparately()
    {
        var tracker = new ObstacleOpportunityTracker();
        EchoChoiceGroup group = ChoiceGroup(45, 100f, 0, 1);
        tracker.UpdateGroup(group, 0, 95f,
            false, false, 7f, out _);

        Assert.IsTrue(tracker.UpdateGroup(null, 2, 102f,
            false, false, 7f,
            out ObstacleOpportunityResolution result));
        Assert.AreEqual(EchoResponseKind.ClearRoute, result.response);
        Assert.IsTrue(result.laneChanged);
        Assert.AreEqual(0, result.entryLane);
        Assert.AreEqual(2, result.finalLane);
    }

    private static EchoChoiceGroup ChoiceGroup(int groupId,
        float routeDistance, int highLane, int lowLane)
    {
        return new EchoChoiceGroup
        {
            groupId = groupId,
            phaseSequence = 2,
            planVersion = 2,
            groupKind = EchoChoiceGroupKind.DetectionProbe,
            routeDistance = routeDistance,
            settleRouteDistance = routeDistance + 1f,
            clearLane = 3 - highLane - lowLane,
            options = new[]
            {
                new ObstacleOpportunity
                {
                    opportunityId = groupId * 10 + 1,
                    groupId = groupId,
                    phaseSequence = 2,
                    planVersion = 2,
                    lane = highLane,
                    obstacleType = ObstacleType.High,
                    routeDistance = routeDistance
                },
                new ObstacleOpportunity
                {
                    opportunityId = groupId * 10 + 2,
                    groupId = groupId,
                    phaseSequence = 2,
                    planVersion = 2,
                    lane = lowLane,
                    obstacleType = ObstacleType.Low,
                    routeDistance = routeDistance
                }
            }
        };
    }

    [Test]
    public void SixCalibrationSignalsMoveInExpectedDirections()
    {
        var style = new PlayerStyleData();
        for (int i = 0; i < 12; i++)
        {
            style.ObserveAggressiveness(1f);
            style.ObserveJumpTiming(-0.8f);
            style.ObserveVerticalAction(ShadowAction.Slide);
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
    public void LanePreferenceMeasuresChoiceBeyondOfferedRoute()
    {
        var style = new PlayerStyleData();
        for (int i = 0; i < 12; i++)
            style.ObserveLaneChoice(0, 0f);

        Assert.AreEqual(0f, style.lanePreference, 0.001f,
            "Following a left-side reward route is not evidence of left bias.");

        for (int i = 0; i < 12; i++)
            style.ObserveLaneChoice(0, 2f);
        Assert.Less(style.lanePreference, -0.5f,
            "Choosing left against a right-side offer is real preference evidence.");
    }

    [Test]
    public void LegacyLanePreferenceIsClearedDuringChoiceModelMigration()
    {
        PlayerStyleData legacy = JsonUtility.FromJson<PlayerStyleData>(
            "{\"version\":2,\"lanePreference\":0.95,\"laneSamples\":40}");

        legacy.Normalize();

        Assert.AreEqual(PlayerStyleData.CurrentVersion, legacy.version);
        Assert.AreEqual(0f, legacy.lanePreference);
        Assert.AreEqual(0, legacy.laneSamples);
    }

    [Test]
    public void ObstacleSuccessDoesNotMasqueradeAsVerticalActionPreference()
    {
        var style = new PlayerStyleData();
        for (int i = 0; i < 6; i++)
            style.ObserveVerticalAction(ShadowAction.Jump);
        float actionPreference = style.slideFrequency;

        for (int i = 0; i < 12; i++)
            style.ObserveSlideOpportunity(true);

        Assert.AreEqual(actionPreference, style.slideFrequency, 0.0001f);
        Assert.Greater(style.slideOpportunitySuccess, 0.8f);
        Assert.Less(style.slideFrequency, 0.25f);
    }

    [Test]
    public void LegacyObstacleSuccessIsNotImportedAsActionPreference()
    {
        PlayerStyleData legacy = JsonUtility.FromJson<PlayerStyleData>(
            "{\"version\":1,\"slideFrequency\":0.95,"
            + "\"slideOpportunitySamples\":20}");

        legacy.Normalize();

        Assert.AreEqual(PlayerStyleData.CurrentVersion, legacy.version);
        Assert.AreEqual(0.5f, legacy.slideFrequency, 0.0001f);
        Assert.AreEqual(0, legacy.verticalActionSamples);
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
                    originalPrediction = (int)ShadowAction.Keep,
                    baseScores = new[] { 0.1f, 0.4f, 0.2f, 0.2f, 0.1f },
                    styleAdjustedScores = new[] { 0.1f, 0.7f, 0.1f, 0.1f, 0f },
                    finalScores = new[] { 0.1f, 0.7f, 0.1f, -999f, -999f },
                    feasibleActions = new[] { true, true, true, false, false },
                    safetyAdjusted = true,
                    directive = ShadowAIDirective.Neutral,
                    playerStyle = style.Clone()
                }
            },
            obstacleContacts =
                new System.Collections.Generic.List<AIObstacleContactSample>
                {
                    new AIObstacleContactSample
                    {
                        source = (int)ObstacleContactSource.Sweep,
                        obstacleId = 42,
                        seed = 9137,
                        speed = 25f,
                        verticalClearance = 0.12f,
                        outcome = (int)ObstacleContactOutcome.Pass
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
        Assert.AreEqual((int)ShadowAction.Keep,
            restored.shadowSamples[0].originalPrediction);
        Assert.AreEqual(42, restored.obstacleContacts[0].obstacleId);
        Assert.AreEqual(9137, restored.obstacleContacts[0].seed);
    }

    private static PlayerStyleData ConfidentStyle()
    {
        return new PlayerStyleData
        {
            aggressivenessSamples = 20,
            jumpTimingSamples = 20,
            verticalActionSamples = 20,
            slideOpportunitySamples = 20,
            laneSamples = 20,
            rhythmSamples = 20,
            recoverySamples = 10,
            rhythmStability = 1f
        };
    }
}
