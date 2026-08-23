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
        Assert.AreNotEqual(contract.learnedAction, contract.targetAction);
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
        Assert.IsFalse(changed.shouldTurn,
            "A contract test must remain a readable straight choice segment.");
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
    public void SixDuelPhasesProduceDistinctTrackEncounters()
    {
        var plan = new AITrackPlan
        {
            safeLane = 1,
            obstacleChance = 0.4f,
            coinChance = 0.5f,
            maxBlockedLanes = 1,
            shouldTurn = true
        };
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            generation = 2,
            learnedLane = 2,
            targetLane = 0,
            predictionLane = 2,
            targetProgress = 100f
        };

        Assert.AreEqual(EchoEncounterKind.DetectionEvidence,
            AITrackDirector.ApplyEchoContract(plan, contract, 1,
                EchoDuelPhase.Detection).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.RevealChoice,
            AITrackDirector.ApplyEchoContract(plan, contract, 2,
                EchoDuelPhase.Reveal).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.ResistanceTest,
            AITrackDirector.ApplyEchoContract(plan, contract, 3,
                EchoDuelPhase.Resistance).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.CounterTest,
            AITrackDirector.ApplyEchoContract(plan, contract, 4,
                EchoDuelPhase.Counterattack).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.RewriteChoice,
            AITrackDirector.ApplyEchoContract(plan, contract, 5,
                EchoDuelPhase.Rewrite).echoEncounterKind);

        AITrackPlan finale = AITrackDirector.ApplyEchoContract(
            plan, contract, 6, EchoDuelPhase.Finale);
        Assert.AreEqual(EchoEncounterKind.FinaleOldHabit,
            finale.echoEncounterKind);
        Assert.IsFalse(finale.shouldTurn);
    }

    [Test]
    public void RevealChoiceExposesPredictedSafeAndRiskRewardRoutes()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            targetLane = 0,
            predictionLane = 2,
            targetProgress = 100f
        };
        AITrackPlan plan = AITrackDirector.ApplyEchoContract(
            new AITrackPlan { safeLane = 1, maxBlockedLanes = 1 },
            contract, 2, EchoDuelPhase.Reveal);

        EchoEncounterLaneChoice[] choices =
            TrackManager.BuildEchoEncounterLaneChoices(plan);

        Assert.AreEqual(3, choices.Length);
        Assert.AreEqual(plan.echoPredictedLane, choices[0].lane);
        Assert.AreEqual(plan.echoSafeChoiceLane, choices[1].lane);
        Assert.AreEqual(plan.echoRiskChoiceLane, choices[2].lane);
        Assert.AreNotEqual(choices[0].lane, choices[1].lane);
        Assert.AreNotEqual(choices[0].lane, choices[2].lane);
        Assert.AreNotEqual(choices[1].lane, choices[2].lane);
        Assert.Greater(choices[0].maxCoinCount,
            choices[1].maxCoinCount);
        Assert.Greater(choices[2].maxCoinCount,
            choices[1].maxCoinCount);
        Assert.IsTrue(choices[0].echoContractMarker);
        Assert.IsTrue(choices[1].echoContractMarker);
        Assert.IsTrue(choices[2].echoContractMarker);
    }

    [Test]
    public void LaneResistanceAcceptsEitherCounterRouteButOnlyOnePerTest()
    {
        var evaluator = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 1,
            targetLane = 0,
            predictionLane = 1,
            targetProgress = 100f
        });
        evaluator.SetPhase(EchoDuelPhase.Resistance);

        evaluator.RecordLaneMarker(2, 50f, 10f);
        float afterFirstChoice = evaluator.Contract.progress;
        evaluator.RecordLaneMarker(0, 55f, 10f);

        Assert.AreEqual(34f, afterFirstChoice);
        Assert.AreEqual(afterFirstChoice, evaluator.Contract.progress,
            "A player cannot harvest both counter routes from one test segment.");
    }

    [Test]
    public void CounterattackPredictsTheMostRecentSuccessfulCounterRoute()
    {
        var evaluator = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            targetLane = 0,
            predictionLane = 0,
            initialBreakCompleted = true,
            counterattackActive = true,
            progress = 55f,
            targetProgress = 100f
        });
        evaluator.SetPhase(EchoDuelPhase.Counterattack);

        evaluator.RecordLaneMarker(1, 50f, 10f);

        Assert.AreEqual(1, evaluator.Contract.predictionLane);
        StringAssert.Contains("中间路线",
            evaluator.BuildPredictionText(true));
        float progressAfterCounter = evaluator.Contract.progress;
        evaluator.RecordLaneMarker(1, 100f, 10f);
        Assert.Less(evaluator.Contract.progress, progressAfterCounter,
            "Repeating the newly learned counter route must strengthen the echo.");
    }

    [Test]
    public void RevealActionChoiceImmediatelyChangesLeadWithoutBreakingContract()
    {
        var predicted = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetProgress = 100f
        });
        predicted.SetPhase(EchoDuelPhase.Reveal);
        predicted.RecordDodge(ObstacleType.Low, 1, 10f);

        var counter = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetProgress = 100f
        });
        counter.SetPhase(EchoDuelPhase.Reveal);
        counter.RecordDodge(ObstacleType.High, 1, 10f);

        Assert.Greater(predicted.Contract.shadowProgressBonus, 0f);
        Assert.AreEqual(0f, predicted.Contract.progress);
        Assert.Greater(counter.Contract.playerProgressBonus, 0f);
        Assert.AreEqual(0f, counter.Contract.progress);
    }

    [Test]
    public void RevealActionEncounterOffersPredictedAndCounterActionsTogether()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetLane = 0,
            targetProgress = 100f
        };
        AITrackPlan plan = AITrackDirector.ApplyEchoContract(
            new AITrackPlan { safeLane = 1, maxBlockedLanes = 1 },
            contract, 2, EchoDuelPhase.Reveal);

        int[] blocked = TrackManager.SelectEchoEncounterBlockedLanes(
            plan, new[] { 0, 0, 0 });

        Assert.AreEqual(2, blocked.Length);
        Assert.Contains(plan.echoPredictedLane, blocked);
        Assert.Contains(plan.echoRiskChoiceLane, blocked);
        Assert.AreEqual(0, TrackManager.SelectEchoEncounterObstaclePrefabIndex(
            plan, plan.echoPredictedLane, 0, 0.5f, 0.5f));
        Assert.AreEqual(1, TrackManager.SelectEchoEncounterObstaclePrefabIndex(
            plan, plan.echoRiskChoiceLane, 0, 0.5f, 0.5f));
    }

    [Test]
    public void ActionEncounterKeepsSafeLaneOpenAndPlacesTestOnRiskLane()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            generation = 3,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetLane = 0,
            targetProgress = 100f
        };
        AITrackPlan plan = AITrackDirector.ApplyEchoContract(
            new AITrackPlan { safeLane = 1, maxBlockedLanes = 2 },
            contract, 3, EchoDuelPhase.Resistance);

        int[] blocked = TrackManager.SelectEchoEncounterBlockedLanes(
            plan, new[] { 0, 0, 0 });

        Assert.AreEqual(contract.targetLane, plan.echoRiskChoiceLane);
        Assert.AreEqual(contract.targetAction, plan.echoTargetAction);
        Assert.AreEqual(contract.predictionAction,
            plan.echoPredictedAction);
        Assert.Contains(plan.echoRiskChoiceLane, blocked);
        Assert.IsFalse(System.Array.Exists(blocked,
            lane => lane == plan.echoSafeChoiceLane));
    }

    [Test]
    public void CounterattackRotatesLanesPatternsAndSpacingBands()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            generation = 2,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetLane = 0,
            targetProgress = 100f
        };
        var basePlan = new AITrackPlan
        {
            safeLane = 1,
            maxBlockedLanes = 2
        };
        var patterns = new System.Collections.Generic.HashSet<EchoObstaclePattern>();
        var spacingBands = new System.Collections.Generic.HashSet<int>();
        bool[] predictedLanes = new bool[3];
        bool[] riskLanes = new bool[3];
        string previousSignature = null;

        for (int step = 0; step < 12; step++)
        {
            AITrackPlan plan = AITrackDirector.ApplyEchoContract(
                basePlan, contract, step, EchoDuelPhase.Counterattack);
            plan = TrackManager.PrepareCounterObstacleRowPlan(plan, step);
            string signature = plan.echoObstaclePattern + ":"
                               + plan.echoPredictedLane + ":"
                               + plan.echoRiskChoiceLane + ":"
                               + plan.echoObstacleSpacingBand;

            Assert.AreNotEqual(previousSignature, signature,
                "Adjacent counter rows must not repeat the same layout.");
            Assert.AreNotEqual(plan.echoPredictedLane,
                plan.echoRiskChoiceLane);
            Assert.AreNotEqual(plan.echoSafeChoiceLane,
                plan.echoPredictedLane);
            Assert.AreNotEqual(plan.echoSafeChoiceLane,
                plan.echoRiskChoiceLane);
            patterns.Add(plan.echoObstaclePattern);
            spacingBands.Add(plan.echoObstacleSpacingBand);
            predictedLanes[plan.echoPredictedLane] = true;
            riskLanes[plan.echoRiskChoiceLane] = true;
            previousSignature = signature;
        }

        Assert.AreEqual(4, patterns.Count);
        Assert.AreEqual(3, spacingBands.Count);
        CollectionAssert.DoesNotContain(predictedLanes, false);
        CollectionAssert.DoesNotContain(riskLanes, false);
    }

    [Test]
    public void CounterattackDoesNotLeaveAnyLaneWithoutThreeRowsOfPressure()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            generation = 2,
            learnedAction = ShadowAction.Slide,
            predictionAction = ShadowAction.Slide,
            targetAction = ShadowAction.Jump,
            targetLane = 0,
            targetProgress = 100f
        };
        var basePlan = new AITrackPlan
        {
            safeLane = 1,
            maxBlockedLanes = 2
        };
        int[] drought = { 0, 0, 0 };
        var acceptedPatterns =
            new System.Collections.Generic.HashSet<EchoObstaclePattern>();

        for (int encounter = 0; encounter < 12; encounter++)
        {
            int step = encounter * 2;
            AITrackPlan plan = AITrackDirector.ApplyEchoContract(
                basePlan, contract, step, EchoDuelPhase.Counterattack);
            plan = TrackManager.BalanceCounterEncounterLanes(plan, drought);
            plan = TrackManager.PrepareCounterObstacleRowPlan(
                plan, encounter);
            acceptedPatterns.Add(plan.echoObstaclePattern);
            int[] blocked = TrackManager.SelectEchoEncounterBlockedLanes(
                plan, drought);

            Assert.IsFalse(System.Array.Exists(blocked,
                lane => lane == plan.echoSafeChoiceLane));
            for (int lane = 0; lane < drought.Length; lane++)
                drought[lane]++;
            for (int laneIndex = 0; laneIndex < blocked.Length; laneIndex++)
                drought[blocked[laneIndex]] = 0;
            for (int lane = 0; lane < drought.Length; lane++)
                Assert.LessOrEqual(drought[lane], 2,
                    "A lane stayed visually empty for three encounter rows.");
        }

        Assert.AreEqual(4, acceptedPatterns.Count,
            "Accepted rows must cycle every authored counter pattern.");
    }

    [Test]
    public void CounterattackSpacingBandsVaryAboveTheRecoveryFloor()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            targetAction = ShadowAction.Jump,
            predictionAction = ShadowAction.Slide,
            targetProgress = 100f
        };
        var multipliers = new System.Collections.Generic.HashSet<float>();
        float recoveryFloor = TrackSpawnRules.MinimumObstacleRowSpacing(
            24f, 0.9f, 20f);

        for (int step = 0; step < 6; step++)
        {
            AITrackPlan plan = AITrackDirector.ApplyEchoContract(
                new AITrackPlan { maxBlockedLanes = 2 }, contract, step,
                EchoDuelPhase.Counterattack);
            plan = TrackManager.PrepareCounterObstacleRowPlan(plan, step);
            float multiplier = TrackManager.EchoObstacleSpacingMultiplier(plan);
            multipliers.Add(multiplier);
            Assert.GreaterOrEqual(recoveryFloor * multiplier, recoveryFloor);
        }

        Assert.AreEqual(3, multipliers.Count);
    }

    [Test]
    public void CounterattackStaggeredPatternsReverseObstacleOrder()
    {
        var plan = new AITrackPlan
        {
            echoEncounterKind = EchoEncounterKind.CounterTest,
            echoPredictedLane = 0,
            echoRiskChoiceLane = 2,
            echoObstaclePattern = EchoObstaclePattern.PredictedThenRisk
        };

        Assert.Less(TrackManager.EchoObstacleLaneOffset(plan, 0), 0f);
        Assert.Greater(TrackManager.EchoObstacleLaneOffset(plan, 2), 0f);

        plan.echoObstaclePattern = EchoObstaclePattern.RiskThenPredicted;
        Assert.Greater(TrackManager.EchoObstacleLaneOffset(plan, 0), 0f);
        Assert.Less(TrackManager.EchoObstacleLaneOffset(plan, 2), 0f);
    }

    [Test]
    public void FinaleCyclesOldHabitCounterHabitAndFreeChoiceStructures()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            targetLane = 0,
            predictionLane = 1,
            targetProgress = 100f
        };
        var plan = new AITrackPlan { safeLane = 1, maxBlockedLanes = 2 };

        Assert.AreEqual(EchoEncounterKind.FinaleOldHabit,
            AITrackDirector.ApplyEchoContract(plan, contract, 6,
                EchoDuelPhase.Finale).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.FinaleCounterHabit,
            AITrackDirector.ApplyEchoContract(plan, contract, 7,
                EchoDuelPhase.Finale).echoEncounterKind);
        Assert.AreEqual(EchoEncounterKind.FinaleFreeChoice,
            AITrackDirector.ApplyEchoContract(plan, contract, 8,
                EchoDuelPhase.Finale).echoEncounterKind);
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
    public void DuelCadenceUsesTheFirstBatchPhaseWindows()
    {
        Assert.AreEqual(12f, EchoDuelFlow.DefaultDetectionDuration);
        Assert.AreEqual(4f, EchoDuelFlow.DefaultRevealDuration);
        Assert.AreEqual(24f, EchoDuelFlow.DefaultRewriteDuration);
        Assert.AreEqual(25f, EchoDuelFlow.DefaultFinaleDuration);
    }

    [Test]
    public void DuelWaitsForRouteBoundaryBeforeCommittingPhase()
    {
        var flow = new EchoDuelFlow(true, 2f, 1f, 3f, 5f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f
        };

        Assert.IsTrue(flow.Tick(2f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Detection, flow.Phase);
        Assert.IsTrue(flow.TransitionPending);
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.PendingPhase);

        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.Phase);
        Assert.IsFalse(flow.TransitionPending);

        Assert.IsTrue(flow.Tick(1f, 100f, contract));
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.Phase);
        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Resistance, flow.Phase);
    }

    [Test]
    public void RewriteRequestsFinaleAfterItsOwnWindow()
    {
        var flow = new EchoDuelFlow(true, 2f, 1f, 3f, 5f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f,
            completed = true
        };

        Assert.IsTrue(flow.TransitionTo(EchoDuelPhase.Rewrite));
        Assert.IsFalse(flow.Tick(2.9f, 80f, contract));
        Assert.AreEqual(EchoDuelPhase.Rewrite, flow.Phase);
        Assert.IsTrue(flow.IsRewriteLearningWindow);

        Assert.IsTrue(flow.Tick(0.1f, 80f, contract));
        Assert.AreEqual(EchoDuelPhase.Rewrite, flow.Phase);
        Assert.AreEqual(EchoDuelPhase.Finale, flow.PendingPhase);
        Assert.IsFalse(flow.PendingTransitionFailed);
    }

    [Test]
    public void UnbrokenResistanceRequestsExplicitFailureFinale()
    {
        var flow = new EchoDuelFlow(true, 2f, 1f, 3f, 5f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f
        };

        Assert.IsTrue(flow.TransitionTo(EchoDuelPhase.Resistance));
        Assert.IsTrue(flow.Tick(0.1f, 5f, contract));
        Assert.AreEqual(EchoDuelPhase.Resistance, flow.Phase);
        Assert.AreEqual(EchoDuelPhase.Finale, flow.PendingPhase);
        Assert.IsTrue(flow.PendingTransitionFailed);
        Assert.AreEqual(EchoDuelPhase.Resistance, flow.PendingFailurePhase);
    }

    [Test]
    public void ContractScoringFreezesAtGateAndLocksAfterFailedFinale()
    {
        var evaluator = new EchoContractEvaluator(new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetLane = 2,
            learnedLane = 0,
            targetProgress = 100f
        });
        evaluator.SetPhase(EchoDuelPhase.Resistance);

        evaluator.SetScoringSuspended(true);
        evaluator.RecordLaneMarker(2, 50f, 10f);
        Assert.AreEqual(0f, evaluator.Contract.progress);

        evaluator.SetScoringSuspended(false);
        evaluator.RecordLaneMarker(2, 50f, 10f);
        Assert.Greater(evaluator.Contract.progress, 0f);

        evaluator.LockForFinale(EchoDuelPhase.Resistance);
        float lockedProgress = evaluator.Contract.progress;
        evaluator.RecordLaneMarker(2, 100f, 10f);
        Assert.IsTrue(evaluator.Contract.duelFailed);
        Assert.AreEqual(EchoDuelPhase.Resistance,
            evaluator.Contract.failurePhase);
        Assert.AreEqual(lockedProgress, evaluator.Contract.progress);

        EchoContractData retry = evaluator.Contract.ResetForRun();
        Assert.IsFalse(retry.duelFailed);
        Assert.AreEqual(EchoDuelPhase.None, retry.failurePhase);
    }

    [Test]
    public void NextRouteBoundaryIsAlwaysTheNextSegmentEnd()
    {
        Assert.AreEqual(20f, TrackManager.NextRouteBoundary(0f, 20f));
        Assert.AreEqual(20f, TrackManager.NextRouteBoundary(19.9f, 20f));
        Assert.AreEqual(40f, TrackManager.NextRouteBoundary(20f, 20f));
    }

    [Test]
    public void PhaseGateStartsAfterAllAlreadyPreparedTrack()
    {
        Assert.AreEqual(140f, TrackManager.PreparedPhaseBoundary(
            35f, 131f, 20f));
        Assert.AreEqual(60f, TrackManager.PreparedPhaseBoundary(
            41f, 0f, 20f));
    }

    [Test]
    public void EveryPhaseKeepsAtLeastOneHundredTwentyMetersVisible()
    {
        Assert.AreEqual(12, TrackManager.PlanningLookaheadPoolSize(
            10, EchoDuelPhase.Reveal));
        Assert.AreEqual(12, TrackManager.PlanningLookaheadPoolSize(
            10, EchoDuelPhase.Resistance));
        Assert.AreEqual(12, TrackManager.PlanningLookaheadPoolSize(
            10, EchoDuelPhase.Counterattack));
        Assert.AreEqual(12, TrackManager.PlanningLookaheadPoolSize(
            10, EchoDuelPhase.Rewrite));
        Assert.IsTrue(TrackSpawnRules.NeedsSegment(
            119.9f, 0f, 20f, 12));
        Assert.IsFalse(TrackSpawnRules.NeedsSegment(
            120f, 0f, 20f, 12));
        Assert.AreEqual(60f, TrackManager.ContentLookaheadDistance(20f));
        Assert.IsTrue(TrackManager.ShouldPrepareSegmentContent(
            59.9f, 0f, 20f));
        Assert.IsFalse(TrackManager.ShouldPrepareSegmentContent(
            60f, 0f, 20f));
    }

    [Test]
    public void FixedPhaseCanPrepareItsGateBeforeTheVisibleWindowEnds()
    {
        var flow = new EchoDuelFlow(true, 12f, 4f, 24f, 25f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f
        };

        Assert.IsTrue(flow.Tick(4f, 100f, contract, 8f));
        Assert.AreEqual(EchoDuelPhase.Detection, flow.Phase);
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.PendingPhase);
    }

    [Test]
    public void OnlyScoredContractPhasesFreezeWhileWaitingForTheirGate()
    {
        Assert.IsFalse(AIShadowRunner.ShouldSuspendContractScoringAtGate(
            EchoDuelPhase.Detection));
        Assert.IsFalse(AIShadowRunner.ShouldSuspendContractScoringAtGate(
            EchoDuelPhase.Reveal));
        Assert.IsTrue(AIShadowRunner.ShouldSuspendContractScoringAtGate(
            EchoDuelPhase.Resistance));
        Assert.IsTrue(AIShadowRunner.ShouldSuspendContractScoringAtGate(
            EchoDuelPhase.Counterattack));
    }

    [Test]
    public void ScheduledPhaseOverridePreparesPlansBeyondTheRouteGate()
    {
        var plan = new AITrackPlan
        {
            safeLane = 1,
            obstacleChance = 0.4f,
            coinChance = 0.5f,
            maxBlockedLanes = 1
        };
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            duelPhase = EchoDuelPhase.Detection,
            targetLane = 2,
            targetProgress = 100f
        };

        AITrackPlan beforeGate = AITrackDirector.ApplyEchoContract(
            plan, contract, 1);
        AITrackPlan beyondGate = AITrackDirector.ApplyEchoContract(
            plan, contract, 1, EchoDuelPhase.Resistance);

        Assert.AreEqual(EchoContractType.None,
            beforeGate.echoContractType);
        Assert.AreEqual(EchoContractType.BreakLaneHabit,
            beyondGate.echoContractType);
        Assert.AreEqual(2, beyondGate.echoRiskChoiceLane);
        Assert.AreNotEqual(beyondGate.echoPredictedLane,
            beyondGate.echoSafeChoiceLane);
    }

    [Test]
    public void SuccessfulDuelCanRequestAndCommitAllSixPhases()
    {
        var flow = new EchoDuelFlow(true, 2f, 1f, 3f, 5f);
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetProgress = 100f
        };

        Assert.IsTrue(flow.Tick(2f, 100f, contract));
        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Reveal, flow.Phase);
        Assert.IsTrue(flow.Tick(1f, 100f, contract));
        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Resistance, flow.Phase);

        contract.initialBreakCompleted = true;
        Assert.IsTrue(flow.Tick(0.1f, 90f, contract));
        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Counterattack, flow.Phase);
        contract.completed = true;
        Assert.IsTrue(flow.Tick(0.1f, 80f, contract));
        Assert.IsTrue(flow.CommitPendingTransition());
        Assert.AreEqual(EchoDuelPhase.Rewrite, flow.Phase);

        Assert.IsTrue(flow.Tick(3f, 80f, contract));
        Assert.AreEqual(EchoDuelPhase.Rewrite, flow.Phase,
            "A phase request must wait for the route boundary.");
        Assert.IsTrue(flow.CommitPendingTransition());
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
