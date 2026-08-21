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
        StringAssert.Contains("路线", contract.objective);
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
        StringAssert.Contains("节拍", contract.objective);
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
            targetProgress = 100f,
            title = "lane"
        };
        var evaluator = new EchoContractEvaluator(contract);

        evaluator.SetPhase(EchoDuelPhase.Resistance);
        evaluator.TickLane(2, 2.1f);
        evaluator.RecordLaneMarker(0, 50f, 10f);
        evaluator.RecordLaneMarker(0, 51f, 10f);
        evaluator.RecordLaneMarker(0, 100f, 10f);
        evaluator.RecordLaneMarker(0, 150f, 10f);

        Assert.IsTrue(evaluator.Contract.initialBreakCompleted);
        Assert.IsFalse(evaluator.Contract.completed);

        evaluator.SetPhase(EchoDuelPhase.Counterattack);
        evaluator.RecordLaneMarker(1, 200f, 10f);
        evaluator.RecordLaneMarker(2, 250f, 10f);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.AreEqual(100f, evaluator.Contract.progress);
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
            targetProgress = 100f,
            title = "vertical"
        };
        var evaluator = new EchoContractEvaluator(contract);

        evaluator.SetPhase(EchoDuelPhase.Resistance);
        evaluator.RecordDodge(ObstacleType.High, 1);
        Assert.AreEqual(0f, evaluator.Contract.progress);
        evaluator.RecordDodge(ObstacleType.Low, 0);
        evaluator.RecordDodge(ObstacleType.High, 0);
        evaluator.RecordDodge(ObstacleType.High, 0);
        evaluator.RecordDodge(ObstacleType.High, 0);

        Assert.IsTrue(evaluator.Contract.initialBreakCompleted);
        Assert.IsFalse(evaluator.Contract.completed);

        evaluator.SetPhase(EchoDuelPhase.Counterattack);
        while (!evaluator.Contract.completed)
            evaluator.RecordDodge(RequiredObstacle(
                evaluator.Contract.targetAction), 0);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.AreEqual(100f, evaluator.Contract.progress);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, 0f);
    }

    [Test]
    public void RhythmContractRejectsRepeatedDodgeAndCountsAlternation()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.DisruptRhythm,
            targetProgress = 100f,
            title = "rhythm"
        };
        var evaluator = new EchoContractEvaluator(contract);

        evaluator.SetPhase(EchoDuelPhase.Resistance);
        ObstacleType first = RequiredObstacle(evaluator.Contract.targetAction);
        evaluator.RecordDodge(first);
        int firstFeedback = evaluator.Contract.feedbackSequence;
        evaluator.RecordDodge(first);
        while (!evaluator.Contract.initialBreakCompleted)
            evaluator.RecordDodge(RequiredObstacle(
                evaluator.Contract.targetAction));

        evaluator.SetPhase(EchoDuelPhase.Counterattack);
        while (!evaluator.Contract.completed)
            evaluator.RecordDodge(RequiredObstacle(
                evaluator.Contract.targetAction));

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.AreEqual(100f, evaluator.Contract.progress);
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
            maxBlockedLanes = 1,
            shouldTurn = true
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
        Assert.IsTrue(changed.shouldTurn,
            "Echo contracts must not flatten an already planned turn.");
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
    public void ActiveGenerationSnapshotCloneIsDeepAndStable()
    {
        var source = new EchoGenerationSnapshot
        {
            generation = 3,
            policyWeights = new[] { 1f, 2f },
            sequenceTransitions = new[] { 3f, 4f },
            sequencePairCount = 7,
            styleJson = UnityEngine.JsonUtility.ToJson(new PlayerStyleData
            {
                lanePreference = 0.75f,
                laneSamples = 12
            }),
            pace = 13.5f,
            clarity = 0.8f
        };

        EchoGenerationSnapshot clone = source.Clone();
        string frozenJson = clone.ToJson();
        source.policyWeights[0] = 99f;
        source.sequenceTransitions[0] = 99f;
        source.styleJson = "{}";
        source.pace = 99f;

        Assert.AreEqual(1f, clone.policyWeights[0]);
        Assert.AreEqual(3f, clone.sequenceTransitions[0]);
        Assert.AreEqual(13.5f, clone.pace);
        Assert.AreEqual(0.75f, clone.GetStyle().lanePreference, 0.001f);
        Assert.AreEqual(frozenJson, clone.ToJson());
    }

    [Test]
    public void PendingPaceRejectsAbandonedShortAndTurboRuns()
    {
        Assert.IsFalse(AIShadowRunner.ShouldRecordPendingPace(
            RunEndReason.Abandoned, 500f, 30f, false));
        Assert.IsFalse(AIShadowRunner.ShouldRecordPendingPace(
            RunEndReason.Collision, 59f, 30f, false));
        Assert.IsFalse(AIShadowRunner.ShouldRecordPendingPace(
            RunEndReason.FinishReached, 500f, 7.9f, false));
        Assert.IsFalse(AIShadowRunner.ShouldRecordPendingPace(
            RunEndReason.FinishReached, 500f, 30f, true));
        Assert.IsTrue(AIShadowRunner.ShouldRecordPendingPace(
            RunEndReason.FinishReached, 500f, 30f, false));
    }

    [Test]
    public void DuelKeepsRewriteAsPursuitUntilFinalWindow()
    {
        var flow = new EchoDuelFlow(true, 2f, 1f, 3f, 5f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f
        };

        Assert.IsTrue(flow.Tick(2f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.Phase);
        Assert.IsTrue(flow.Tick(1f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Resistance, flow.Phase);

        contract.initialBreakCompleted = true;
        Assert.IsTrue(flow.Tick(0.1f, 90f, contract));
        Assert.AreEqual(EchoDuelPhase.Counterattack, flow.Phase);
        contract.completed = true;
        Assert.IsTrue(flow.Tick(0.1f, 80f, contract));
        Assert.AreEqual(EchoDuelPhase.Rewrite, flow.Phase);

        flow.Tick(4f, 40f, contract);
        Assert.AreEqual(EchoDuelPhase.Rewrite, flow.Phase,
            "Rewrite must not turn the whole second half into a finale.");
        Assert.IsFalse(flow.IsRewriteLearningWindow,
            "Boosted rewrite learning must remain time-bounded.");

        Assert.IsTrue(flow.Tick(0.1f, 5f, contract));
        Assert.AreEqual(EchoDuelPhase.Finale, flow.Phase);
    }

    [Test]
    public void OrdinaryCoinsCannotAdvanceLaneContract()
    {
        Assert.IsFalse(AIShadowRunner.ShouldCountContractMarker(
            EchoContractType.BreakLaneHabit, false));
        Assert.IsTrue(AIShadowRunner.ShouldCountContractMarker(
            EchoContractType.BreakLaneHabit, true));
        Assert.IsFalse(AIShadowRunner.ShouldCountContractMarker(
            EchoContractType.ChangeVerticalHabit, true));
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
    public void CourseDurationUsesCalibrationThenChallengeTiming()
    {
        Assert.AreEqual(75f,
            GameManager.SelectCourseDuration(0, 75f, 190f));
        Assert.AreEqual(190f,
            GameManager.SelectCourseDuration(1, 75f, 190f));
    }

    [Test]
    public void PartialEchoRequiresTimeActiveInputAndMinimumEvidence()
    {
        int[] actionCounts = { 0, 0, 0, 1, 0 };

        Assert.IsFalse(AIShadowRunner.HasPartialEchoSamples(
            6, 1, actionCounts, 7.9f, 24));
        Assert.IsFalse(AIShadowRunner.HasPartialEchoSamples(
            5, 1, actionCounts, 8f, 24));
        Assert.IsFalse(AIShadowRunner.HasPartialEchoSamples(
            6, 0, actionCounts, 8f, 24));
        Assert.IsTrue(AIShadowRunner.HasPartialEchoSamples(
            6, 1, actionCounts, 8f, 24));
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

    private static ObstacleType RequiredObstacle(ShadowAction action)
    {
        return action == ShadowAction.Jump
            ? ObstacleType.High : ObstacleType.Low;
    }
}
