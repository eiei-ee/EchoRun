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
            slideOpportunitySamples = 12
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
    }

    [Test]
    public void LaneContractRewardsCounterRouteAndPunishesLearnedRoute()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            learnedLane = 2,
            targetLane = 0,
            targetProgress = 5f,
            title = "lane"
        };
        var evaluator = new EchoContractEvaluator(contract);

        evaluator.TickLane(2, 2.1f);
        evaluator.TickLane(0, 5.1f);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.AreEqual(5f, evaluator.Contract.progress);
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
            targetProgress = 3f,
            title = "vertical"
        };
        var evaluator = new EchoContractEvaluator(contract);

        evaluator.RecordDodge(ObstacleType.Low);
        evaluator.RecordDodge(ObstacleType.High);
        evaluator.RecordDodge(ObstacleType.High);
        evaluator.RecordDodge(ObstacleType.High);

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

        evaluator.RecordDodge(ObstacleType.High);
        evaluator.RecordDodge(ObstacleType.High);
        evaluator.RecordDodge(ObstacleType.Low);
        evaluator.RecordDodge(ObstacleType.High);
        evaluator.RecordDodge(ObstacleType.Low);

        Assert.IsTrue(evaluator.Contract.completed);
        Assert.AreEqual(4f, evaluator.Contract.progress);
        Assert.Greater(evaluator.Contract.shadowProgressBonus, 0f);
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
        Assert.Contains(changed.echoChallengeLane, blocked);
        Assert.IsFalse(System.Array.Exists(blocked,
            lane => lane == changed.safeLane));
        Assert.AreEqual(1, TrackManager.SelectContractObstaclePrefabIndex(
            changed.echoContractType, changed.echoTargetAction,
            10, changed.difficulty, 0.9f));
    }

    [Test]
    public void DistanceLeadCannotBypassEchoContract()
    {
        Assert.IsFalse(AIShadowRunner.IsContractVictory(20f, true, false));
        Assert.IsFalse(AIShadowRunner.IsContractVictory(-1f, true, true));
        Assert.IsFalse(AIShadowRunner.IsContractVictory(20f, false, true));
        Assert.IsTrue(AIShadowRunner.IsContractVictory(0f, true, true));
    }

    [Test]
    public void StyleSummaryExposesThreeHumanReadableSignals()
    {
        string summary = EchoContractPolicy.BuildStyleSummary(
            new PlayerStyleData
            {
                lanePreference = 0.8f,
                slideFrequency = 0.9f,
                rhythmStability = 0.9f
            });

        StringAssert.Contains("偏爱右路", summary);
        StringAssert.Contains("常用滑铲", summary);
        StringAssert.Contains("节奏固定", summary);
    }
}
