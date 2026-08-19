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
        Assert.AreEqual(ShadowAction.Keep, contract.learnedAction);
        Assert.AreEqual(ShadowAction.Keep, contract.predictionAction);
    }

    [Test]
    public void RunEvidenceDoesNotInventJumpWhenPlayerOnlySlides()
    {
        var evidence = new EchoDuelEvidence();
        for (int i = 0; i < 3; i++)
            evidence.Observe(Resolved(EchoResponseKind.Slide, i + 1));

        EchoPredictionSnapshot prediction = evidence.Prediction;
        Assert.AreEqual(EchoEvidenceConclusion.Slide, prediction.conclusion);
        Assert.AreEqual(EchoResponseKind.Slide, prediction.predictedResponse);
        StringAssert.Contains("继续滑铲", evidence.BuildPredictionText());
    }

    [Test]
    public void RunEvidenceReportsInsufficientWhenPlayerOnlyRuns()
    {
        var evidence = new EchoDuelEvidence();
        for (int i = 0; i < 6; i++)
            evidence.Observe(Resolved(EchoResponseKind.NoAction, i + 1));

        Assert.AreEqual(EchoEvidenceConclusion.Insufficient,
            evidence.Prediction.conclusion);
        StringAssert.Contains("证据不足", evidence.BuildPredictionText());
    }

    [Test]
    public void FailedRowsDoNotCompleteThePredictionEvidenceThreshold()
    {
        var evidence = new EchoDuelEvidence();
        evidence.Observe(Resolved(EchoResponseKind.Jump, 1));
        evidence.Observe(Resolved(EchoResponseKind.Hit, 2));
        evidence.Observe(Resolved(EchoResponseKind.NoAction, 3));

        Assert.AreEqual(1, evidence.SuccessfulChoiceCount);
        Assert.AreEqual(EchoEvidenceConclusion.Insufficient,
            evidence.Prediction.conclusion);
    }

    [Test]
    public void FreeActionsTrainStyleButDoNotCreateDuelPrediction()
    {
        var evidence = new EchoDuelEvidence();
        for (int i = 0; i < 12; i++)
            evidence.ObserveFreeAction(ShadowAction.Jump);

        Assert.AreEqual(EchoEvidenceConclusion.Insufficient,
            evidence.Prediction.conclusion);
        Assert.AreEqual(0, evidence.SuccessfulChoiceCount);
    }

    [Test]
    public void RunEvidenceKeepsBalancedActionsDistinctFromInsufficient()
    {
        var evidence = new EchoDuelEvidence();
        evidence.Observe(Resolved(EchoResponseKind.Jump, 1));
        evidence.Observe(Resolved(EchoResponseKind.Slide, 2));
        evidence.Observe(Resolved(EchoResponseKind.Jump, 3));
        evidence.Observe(Resolved(EchoResponseKind.Slide, 4));

        Assert.AreEqual(EchoEvidenceConclusion.Balanced,
            evidence.Prediction.conclusion);
        StringAssert.Contains("行为均衡", evidence.BuildPredictionText());
    }

    private static ObstacleOpportunityResolution Resolved(
        EchoResponseKind response, int groupId)
    {
        return new ObstacleOpportunityResolution
        {
            opportunityId = groupId,
            groupId = groupId,
            lane = 1,
            obstacleType = response == EchoResponseKind.Jump
                ? ObstacleType.High : ObstacleType.Low,
            response = response,
            physicallySucceeded = response != EchoResponseKind.NoAction
                                  && response != EchoResponseKind.Hit,
            passedInLane = true
        };
    }

    [Test]
    public void ChoiceGroupsBreakContractOnTwoOfThreeThenThreeOfFourMisses()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            targetProgress = 100f,
            title = "choice"
        };
        var evaluator = new EchoContractEvaluator(contract);
        var predictsJump = Prediction(EchoResponseKind.Jump);

        evaluator.SetPhase(EchoDuelPhase.Resistance);
        evaluator.RecordChoice(Resolved(EchoResponseKind.Slide, 10),
            predictsJump);
        evaluator.RecordChoice(Resolved(EchoResponseKind.Jump, 11),
            predictsJump);
        evaluator.RecordChoice(Resolved(EchoResponseKind.RouteAvoid, 12),
            predictsJump);

        Assert.IsTrue(evaluator.Contract.initialBreakCompleted);
        Assert.AreEqual(2, evaluator.Contract.resistancePredictionMisses);
        Assert.IsFalse(evaluator.Contract.completed);

        evaluator.SetPhase(EchoDuelPhase.Counterattack);
        evaluator.RecordChoice(Resolved(EchoResponseKind.Slide, 20),
            predictsJump);
        evaluator.RecordChoice(Resolved(EchoResponseKind.RouteAvoid, 21),
            predictsJump);
        evaluator.RecordChoice(Resolved(EchoResponseKind.Jump, 22),
            predictsJump);
        evaluator.RecordChoice(Resolved(EchoResponseKind.Slide, 23),
            predictsJump);
        evaluator.SetPhase(EchoDuelPhase.Rewrite);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.IsTrue(evaluator.Contract.contractBroken);
        Assert.AreEqual(100f, evaluator.Contract.progress);
        Assert.Greater(evaluator.Contract.playerProgressBonus, 0f);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, 0f);
    }

    [Test]
    public void ChoiceGroupIsSettledExactlyOnce()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            targetProgress = 100f
        };
        var evaluator = new EchoContractEvaluator(contract);
        evaluator.SetPhase(EchoDuelPhase.Resistance);
        Assert.IsTrue(evaluator.RecordChoice(
            Resolved(EchoResponseKind.Slide, 99),
            Prediction(EchoResponseKind.Jump)));
        Assert.IsFalse(evaluator.RecordChoice(
            Resolved(EchoResponseKind.RouteAvoid, 99),
            Prediction(EchoResponseKind.Jump)));
        Assert.AreEqual(1, evaluator.Contract.resistanceGroupsResolved);
        Assert.AreEqual(1, evaluator.Contract.resistancePredictionMisses);
    }

    [Test]
    public void RepeatingOneCounterActionDoesNotBreakResistance()
    {
        var evaluator = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            targetProgress = 100f
        });
        evaluator.SetPhase(EchoDuelPhase.Resistance);
        var predictsJump = Prediction(EchoResponseKind.Jump);

        evaluator.RecordChoice(Resolved(EchoResponseKind.Slide, 301),
            predictsJump);
        evaluator.RecordChoice(Resolved(EchoResponseKind.Slide, 302),
            predictsJump);
        evaluator.RecordChoice(Resolved(EchoResponseKind.Slide, 303),
            predictsJump);

        Assert.AreEqual(3, evaluator.Contract.resistancePredictionMisses);
        Assert.IsFalse(evaluator.Contract.initialBreakCompleted,
            "Breaking the prediction requires two distinct successful strategies.");
    }

    [Test]
    public void CalibrationStatusNamesTheMissingRequirement()
    {
        int[] actions = new int[5];
        actions[(int)ShadowAction.Jump] = 2;
        var status = AIShadowRules.BuildCalibrationStatus(
            24, 6, actions, 24, 6, 2, 2, 2);

        Assert.IsFalse(status.requirementsMet);
        Assert.AreEqual(1, status.categories);
        StringAssert.Contains("不同动作", status.nextRequirement);

        actions[(int)ShadowAction.Slide] = 2;
        status = AIShadowRules.BuildCalibrationStatus(
            24, 6, actions, 24, 6, 2, 2, 2);
        Assert.IsTrue(status.requirementsMet);
        StringAssert.Contains("终点", status.nextRequirement);
    }

    [Test]
    public void FailedResistanceAndCounterattackStillReachRewrite()
    {
        var contract = new EchoContractData { type = EchoContractType.DisruptRhythm };
        var flow = new EchoDuelFlow(true, 1f, 1f, 2f, 5f, 1f, 1f);
        Assert.IsTrue(flow.Tick(1f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.Phase);
        Assert.IsTrue(flow.Tick(1f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Resistance, flow.Phase);
        Assert.IsTrue(flow.Tick(1f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Counterattack, flow.Phase);
        Assert.IsTrue(flow.Tick(1f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Rewrite, flow.Phase);
        Assert.IsFalse(contract.completed);
    }

    private static EchoPredictionSnapshot Prediction(EchoResponseKind response)
    {
        return new EchoPredictionSnapshot
        {
            conclusion = response == EchoResponseKind.Jump
                ? EchoEvidenceConclusion.Jump
                : response == EchoResponseKind.Slide
                    ? EchoEvidenceConclusion.Slide
                    : EchoEvidenceConclusion.RouteAvoid,
            predictedResponse = response,
            confidence = 0.8f
        };
    }

    [Test]
    public void ContractChangesTrackPlanWithoutBlockingSafeLane()
    {
        var plan = new AITrackPlan
        {
            shouldTurn = true,
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
        Assert.IsTrue(changed.shouldTurn,
            "Contract content must not own track topology.");
        Assert.IsTrue(changed.echoChoiceGroup);
        Assert.Contains(changed.echoChallengeLane, blocked);
        Assert.IsFalse(System.Array.Exists(blocked,
            lane => lane == changed.safeLane));
        Assert.AreEqual(1, TrackManager.SelectContractObstaclePrefabIndex(
            changed.echoContractType, changed.echoTargetAction,
            10, changed.difficulty, 0.9f));
        Assert.IsTrue(TrackManager.RequiresGuaranteedContractRow(
            changed.echoContractType));
    }

    [TestCase(EchoDuelPhase.Detection)]
    [TestCase(EchoDuelPhase.Reveal)]
    [TestCase(EchoDuelPhase.Rewrite)]
    [TestCase(EchoDuelPhase.Resistance)]
    [TestCase(EchoDuelPhase.Counterattack)]
    [TestCase(EchoDuelPhase.Finale)]
    public void ContractNeverSuppressesTopologyTurns(EchoDuelPhase phase)
    {
        var plan = new AITrackPlan
        {
            shouldTurn = true,
            safeLane = 1,
            obstacleChance = 0.5f,
            coinChance = 0.5f,
            maxBlockedLanes = 1
        };
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            targetLane = 2,
            targetAction = ShadowAction.Jump,
            targetProgress = 3f,
            duelPhase = phase
        };

        AITrackPlan changed = AITrackDirector.ApplyEchoContract(plan, contract, 2);

        Assert.IsTrue(changed.shouldTurn,
            phase + " must not flatten the entire run.");
    }

    [Test]
    public void ChoiceRowsAlwaysContainJumpAndSlideOptions()
    {
        int first = TrackManager.SelectChoiceObstaclePrefabIndex(7, 0);
        int second = TrackManager.SelectChoiceObstaclePrefabIndex(7, 1);

        CollectionAssert.AreEquivalent(new[] { 0, 1 },
            new[] { first, second });
    }

    [Test]
    public void TopologyForcesTurnAfterTwelveStraights()
    {
        Assert.IsFalse(TrackManager.ShouldTurn(
            true, false, false, 11, 6, 12));
        Assert.IsTrue(TrackManager.ShouldTurn(
            true, false, false, 12, 6, 12));
        Assert.IsFalse(TrackManager.ShouldTurn(
            true, true, true, 12, 6, 12));
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

        contract.resistanceGroupsResolved = 3;
        Assert.IsTrue(flow.Tick(0.1f, 90f, contract));
        Assert.AreEqual(EchoDuelPhase.Counterattack, flow.Phase);
        contract.counterattackGroupsResolved = 4;
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
