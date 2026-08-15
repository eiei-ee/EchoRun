using NUnit.Framework;

public sealed class EchoContractTests
{
    [Test]
    public void StrongLaneBiasCreatesOppositeLaneContract()
    {
        var style = new PlayerStyleData
        {
            lanePreference = 0.9f,
            laneSamples = 30
        };

        EchoContractData contract = EchoContractPolicy.Create(style, 3);

        Assert.AreEqual(EchoContractType.BreakLaneHabit, contract.type);
        Assert.AreEqual(2, contract.learnedLane);
        Assert.AreEqual(0, contract.targetLane);
        StringAssert.Contains("右侧", contract.learnedTrait);
        StringAssert.Contains("左侧", contract.objective);
    }

    [Test]
    public void StrongVerticalBiasCreatesOppositeActionContract()
    {
        var style = new PlayerStyleData
        {
            slideFrequency = 0.95f,
            verticalActionSamples = 12
        };

        EchoContractData contract = EchoContractPolicy.Create(style, 4);

        Assert.AreEqual(EchoContractType.ChangeVerticalHabit, contract.type);
        Assert.AreEqual(ShadowAction.Slide, contract.learnedAction);
        Assert.AreEqual(ShadowAction.Jump, contract.targetAction);
    }

    [Test]
    public void StableRhythmCreatesAlternationContract()
    {
        var style = new PlayerStyleData
        {
            rhythmStability = 0.98f,
            rhythmSamples = 12
        };

        EchoContractData contract = EchoContractPolicy.Create(style, 5);

        Assert.AreEqual(EchoContractType.DisruptRhythm, contract.type);
        StringAssert.Contains("交替", contract.objective);
        Assert.AreNotEqual(ShadowAction.Keep, contract.targetAction);
    }

    [Test]
    public void SparseStyleUsesHonestExplorationCopy()
    {
        EchoContractData contract = EchoContractPolicy.Create(
            new PlayerStyleData(), 1);

        Assert.IsTrue(contract.exploratory);
        StringAssert.StartsWith("AI探测：", contract.learnedTrait);
        StringAssert.DoesNotContain("AI识别：", contract.learnedTrait);
    }

    [Test]
    public void LaneContractRequiresSpacedRouteMarkersAndPunishesLearnedRoute()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            targetLane = 0,
            targetProgress = 3f,
            title = "lane"
        };
        var evaluator = new EchoContractEvaluator(contract);

        evaluator.TickLane(2, 2.1f);
        evaluator.TickLane(0, 20f);
        evaluator.RecordLaneMarker(0, 30f);
        evaluator.RecordLaneMarker(0, 31f);
        evaluator.RecordLaneMarker(0, 48f);
        evaluator.RecordLaneMarker(0, 66f);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.AreEqual(3f, evaluator.Contract.progress);
        Assert.Greater(evaluator.Contract.playerProgressBonus, 0f);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, 0f);
    }

    [Test]
    public void VerticalContractOnlyCountsRequiredSuccessfulDodges()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            learnedAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetLane = 0,
            targetProgress = 3f,
            title = "vertical"
        };
        var evaluator = new EchoContractEvaluator(contract);

        evaluator.RecordDodge(ObstacleType.High, 1);
        Assert.AreEqual(0f, evaluator.Contract.progress);
        evaluator.RecordDodge(ObstacleType.Low, 0);
        evaluator.RecordDodge(ObstacleType.High, 0);
        evaluator.RecordDodge(ObstacleType.High, 0);
        evaluator.RecordDodge(ObstacleType.High, 0);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.AreEqual(3f, evaluator.Contract.progress);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, 0f);
    }

    [Test]
    public void RhythmContractRejectsRepeatedDodgeAndCountsAlternation()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.DisruptRhythm,
            targetProgress = 4f,
            title = "rhythm"
        };
        var evaluator = new EchoContractEvaluator(contract);

        ObstacleType first = evaluator.Contract.targetAction == ShadowAction.Jump
            ? ObstacleType.High : ObstacleType.Low;
        ObstacleType second = first == ObstacleType.High
            ? ObstacleType.Low : ObstacleType.High;
        evaluator.RecordDodge(first);
        int firstFeedback = evaluator.Contract.feedbackSequence;
        evaluator.RecordDodge(first);
        evaluator.RecordDodge(second);
        evaluator.RecordDodge(first);
        evaluator.RecordDodge(second);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.AreEqual(4f, evaluator.Contract.progress);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, 0f);
        Assert.Greater(evaluator.Contract.feedbackSequence, firstFeedback);
    }

    [Test]
    public void ContractChangesTrackPlanWithoutBlockingSafeLane()
    {
        var plan = new AITrackPlan
        {
            safeLane = 1,
            obstacleChance = 0.2f,
            coinChance = 0.3f,
            maxBlockedLanes = 1
        };
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            targetAction = ShadowAction.Jump,
            targetProgress = 3f
        };

        AITrackPlan changed = AITrackDirector.ApplyEchoContract(
            plan, contract, 2);
        int[] blocked = TrackManager.SelectContractBlockedLanes(
            changed.safeLane, changed.maxBlockedLanes, new[] { 0, 0, 0 },
            changed.echoContractType, changed.echoChallengeLane);

        Assert.AreEqual(EchoContractType.ChangeVerticalHabit,
            changed.echoContractType);
        Assert.AreNotEqual(changed.safeLane, changed.echoChallengeLane);
        Assert.IsFalse(changed.shouldTurn);
        Assert.Contains(changed.echoChallengeLane, blocked);
        Assert.IsFalse(System.Array.Exists(blocked,
            lane => lane == changed.safeLane));
        Assert.AreEqual(1, TrackManager.SelectContractObstaclePrefabIndex(
            changed.echoContractType, changed.echoTargetAction,
            10, changed.difficulty, 0.9f));
        Assert.IsTrue(TrackManager.RequiresGuaranteedContractRow(
            changed.echoContractType));
    }

    [Test]
    public void LaneContractKeepsItsPromisedTargetLaneSafe()
    {
        Assert.AreEqual(2, TrackManager.ChooseContractSafeLane(
            EchoContractType.BreakLaneHabit, 2, 1, new[] { 0, 20, 20 }));
    }

    [Test]
    public void RhythmContractRowsAlternateFromJumpToSlide()
    {
        Assert.AreEqual(1, TrackManager.SelectContractObstaclePrefabIndex(
            EchoContractType.DisruptRhythm, ShadowAction.Jump,
            0, 1f, 0f));
        Assert.AreEqual(0, TrackManager.SelectContractObstaclePrefabIndex(
            EchoContractType.DisruptRhythm, ShadowAction.Jump,
            1, 1f, 0f));
    }

    [Test]
    public void RetryContractResetsProgressButPreservesRule()
    {
        var failed = new EchoContractData
        {
            generation = 3,
            type = EchoContractType.DisruptRhythm,
            startingAction = ShadowAction.Jump,
            targetAction = ShadowAction.Slide,
            targetProgress = 4f,
            progress = 2f,
            shadowProgressBonus = 5f,
            lastFeedback = "failed"
        };

        EchoContractData retry = EchoContractPolicy.CreateForRun(
            new PlayerStyleData(), 3, UnityEngine.JsonUtility.ToJson(failed));

        Assert.AreEqual(EchoContractType.DisruptRhythm, retry.type);
        Assert.AreEqual(ShadowAction.Jump, retry.targetAction);
        Assert.AreEqual(0f, retry.progress);
        Assert.AreEqual(0f, retry.shadowProgressBonus);
        Assert.IsEmpty(retry.lastFeedback);
    }

    [Test]
    public void EmptyVerticalActionsCannotRewriteContractStyle()
    {
        Assert.IsFalse(StyleTracker.ShouldObserveVerticalAction(
            ShadowAction.Jump, false));
        Assert.IsTrue(StyleTracker.ShouldObserveVerticalAction(
            ShadowAction.Jump, true));
        Assert.IsFalse(StyleTracker.ShouldObserveVerticalAction(
            ShadowAction.Left, true));
    }

    [Test]
    public void ChallengeGenerationOnlyAdvancesAfterVictory()
    {
        Assert.IsFalse(AIShadowRunner.ShouldAdvanceGeneration(
            true, false, false, false));
        Assert.IsFalse(AIShadowRunner.ShouldAdvanceGeneration(
            true, true, false, false));
        Assert.IsTrue(AIShadowRunner.ShouldAdvanceGeneration(
            true, true, true, false));
        Assert.IsTrue(AIShadowRunner.ShouldAdvanceGeneration(
            false, true, false, true));
    }

    [Test]
    public void DuelLeadUsesOnlyRunnerRoutePositions()
    {
        Assert.AreEqual(4f,
            AIShadowRunner.CalculatePhysicalLead(104f, 100f));
    }

    [Test]
    public void DistanceLeadCannotBypassEchoContract()
    {
        Assert.IsFalse(AIShadowRunner.IsContractVictory(
            20f, true, false, RunEndReason.FinishReached));
        Assert.IsFalse(AIShadowRunner.IsContractVictory(
            -1f, true, true, RunEndReason.FinishReached));
        Assert.IsFalse(AIShadowRunner.IsContractVictory(
            20f, false, true, RunEndReason.FinishReached));
        Assert.IsFalse(AIShadowRunner.IsContractVictory(
            20f, true, true, RunEndReason.Collision));
        Assert.IsTrue(AIShadowRunner.IsContractVictory(
            0f, true, true, RunEndReason.FinishReached));
    }

    [Test]
    public void CourseDistanceUsesCalibrationThenChallengeLength()
    {
        Assert.AreEqual(450f,
            GameManager.SelectCourseDistance(0, 450f, 700f));
        Assert.AreEqual(700f,
            GameManager.SelectCourseDistance(1, 450f, 700f));
    }

    [Test]
    public void RunEndReasonsUseStableTelemetryNames()
    {
        Assert.AreEqual("finish_reached",
            GameManager.ToTelemetryReason(RunEndReason.FinishReached));
        Assert.AreEqual("collision",
            GameManager.ToTelemetryReason(RunEndReason.Collision));
        Assert.AreEqual("abandoned",
            GameManager.ToTelemetryReason(RunEndReason.Abandoned));
    }

    [Test]
    public void StyleSummaryExposesThreeHumanReadableSignals()
    {
        string summary = EchoContractPolicy.BuildStyleSummary(
            new PlayerStyleData
            {
                lanePreference = 0.8f,
                slideFrequency = 0.9f,
                verticalActionSamples = 12,
                rhythmStability = 0.9f
            });

        StringAssert.Contains("偏爱右路", summary);
        StringAssert.Contains("常用滑铲", summary);
        StringAssert.Contains("节奏固定", summary);
    }
}
