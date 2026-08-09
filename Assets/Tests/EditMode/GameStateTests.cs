using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class GameStateTests
{
    [System.Serializable]
    private sealed class ShadowGenerationProbe
    {
        public int generation;
    }

    private readonly List<GameObject> _objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        Time.timeScale = 1f;
        foreach (GameObject go in _objects)
            if (go != null)
                Object.DestroyImmediate(go);
        _objects.Clear();
    }

    [Test]
    public void StartGameResetsSessionValues()
    {
        GameManager manager = Create<GameManager>("GameManager");
        manager.BuffName = "Shield";
        manager.BuffTimeRemaining = 5f;
        manager.AddCoins(3);
        GameManager.SetNextRunSeed(424242);

        manager.StartGame();

        Assert.AreEqual(GameState.Playing, manager.State);
        Assert.AreEqual(manager.startSpeed, manager.CurrentSpeed);
        Assert.AreEqual(0, manager.Score);
        Assert.AreEqual(0, manager.Coins);
        Assert.AreEqual(0f, manager.Distance);
        Assert.IsNull(manager.BuffName);
        Assert.AreEqual(0f, manager.BuffTimeRemaining);
        Assert.AreEqual(1f, Time.timeScale);
        Assert.AreEqual(424242, manager.RunSeed);
        Assert.IsTrue(AIRunTelemetry.IsRecording);
    }

    [Test]
    public void RunRandomRepeatsTheSameSequenceForTheSameSeed()
    {
        AIRunRandom.BeginRun(9137);
        float firstValue = AIRunRandom.Value;
        int firstLane = AIRunRandom.Range(0, 3);
        float firstOffset = AIRunRandom.Range(-0.8f, 0.8f);

        AIRunRandom.BeginRun(9137);

        Assert.AreEqual(firstValue, AIRunRandom.Value);
        Assert.AreEqual(firstLane, AIRunRandom.Range(0, 3));
        Assert.AreEqual(firstOffset, AIRunRandom.Range(-0.8f, 0.8f));
    }

    [Test]
    public void BayesianAbilityGainsSkillAndConfidenceFromEvidence()
    {
        var ability = new BayesianAbilityEstimate();
        float initialMean = ability.Mean;
        float initialConfidence = ability.Confidence;

        for (int i = 0; i < 12; i++)
            ability.Observe(true);

        Assert.Greater(ability.Mean, initialMean);
        Assert.Greater(ability.Confidence, initialConfidence);
    }

    [Test]
    public void PlayerSkillProfileSeparatesJumpAndSlideEvidence()
    {
        var profile = new AIPlayerSkillProfile();
        float initialJump = profile.jumping.Mean;
        float initialSlide = profile.sliding.Mean;

        profile.RecordObstacle(ObstacleType.High, true, 0.7f);
        profile.RecordObstacle(ObstacleType.Low, false, 0.4f);

        Assert.Greater(profile.jumping.Mean, initialJump);
        Assert.Less(profile.sliding.Mean, initialSlide);
        Assert.AreEqual(1, profile.reactionSamples);
        Assert.AreEqual(0.7f, profile.reactionProximityMean, 0.0001f);
    }

    [Test]
    public void TrainingSimulatorIsDeterministicAndAccountsForEverySegment()
    {
        var config = new AITrainingSimulationConfig
        {
            seed = 4815,
            episodes = 8,
            segmentsPerEpisode = 25,
            initialPlayerSkill = 0.58f
        };

        AITrainingSimulationResult first =
            AITrainingSimulator.Run(config);
        AITrainingSimulationResult second =
            AITrainingSimulator.Run(config);

        Assert.AreEqual(200, first.totalSegments);
        Assert.AreEqual(first.meanReward, second.meanReward, 0.000001f);
        Assert.AreEqual(first.survivalRate, second.survivalRate, 0.000001f);
        CollectionAssert.AreEqual(first.actionCounts, second.actionCounts);
        CollectionAssert.AreEqual(first.finalWeights, second.finalWeights);

        int decisionCount = 0;
        foreach (int count in first.actionCounts) decisionCount += count;
        Assert.AreEqual(first.totalSegments, decisionCount);
        Assert.That(first.survivalRate, Is.InRange(0f, 1f));
    }

    [Test]
    public void TrainingComparisonUsesTheSameEpisodeBudget()
    {
        var config = new AITrainingSimulationConfig
        {
            seed = 1338,
            episodes = 4,
            segmentsPerEpisode = 30
        };

        AITrainingComparisonResult result =
            AITrainingSimulator.Compare(config);

        Assert.AreEqual("EpsilonGreedy", result.baseline.policyType);
        Assert.AreEqual("LinUCB", result.linUcb.policyType);
        Assert.AreEqual(120, result.baseline.totalSegments);
        Assert.AreEqual(120, result.linUcb.totalSegments);
        Assert.Greater(result.linUcb.meanPolicyUncertainty, 0f);
    }

    [Test]
    public void LinUcbOffersHarderRunsToHigherSkillCohorts()
    {
        var novice = new AITrainingSimulationConfig
        {
            seed = 909,
            episodes = 30,
            segmentsPerEpisode = 50,
            initialPlayerSkill = 0.3f,
            useLinUcb = true
        };
        var expert = new AITrainingSimulationConfig
        {
            seed = 909,
            episodes = 30,
            segmentsPerEpisode = 50,
            initialPlayerSkill = 0.82f,
            useLinUcb = true
        };

        AITrainingSimulationResult noviceResult =
            AITrainingSimulator.Run(novice);
        AITrainingSimulationResult expertResult =
            AITrainingSimulator.Run(expert);

        Assert.Greater(expertResult.meanDifficulty,
            noviceResult.meanDifficulty + 0.05f);
    }

    [Test]
    public void TelemetryRoundTripPreservesDecisionInputsAndReward()
    {
        float[] shadowWeights = { 0.1f, 0.2f };
        float[] directorWeights = { 0.3f, 0.4f, 0.5f };
        const string directorState =
            "{\"version\":1,\"actionPulls\":[1,2,3,4]}";
        const string shadowSequenceState =
            "{\"pairCount\":8,\"transitions\":[1,2,3]}";
        AIRunTelemetry.BeginRun(
            77, 12, 7904, 22, 48, shadowWeights,
            directorWeights, directorState, shadowSequenceState);
        AITrackPlan plan = new AITrackPlan
        {
            intent = AIDirectorIntent.Pressure,
            difficulty = 0.72f,
            obstacleChance = 0.8f,
            coinChance = 0.45f,
            safeLane = 2,
            maxBlockedLanes = 2,
            shouldTurn = true
        };
        float[] context = { 1f, 0.7f, 0.2f, 0.8f, 0.6f };

        int decisionId = AIRunTelemetry.RecordDirectorDecision(
            context, plan, 1, 0.42f, 0.18f, true);
        AIRunTelemetry.RecordDirectorOutcome(decisionId, 0.65f, 49);
        AIRunTelemetry.RecordShadowSample(
            ShadowAction.Jump, 1,
            new[] { 1f, 0f, 0.3f, 0.8f, 0f, 0.66f, 0f, 0f },
            false, 0.72f, (int)ShadowAction.Keep, 0.85f, 0.36f);

        AIRunTelemetryData restored = AIRunTelemetry.FromJson(
            AIRunTelemetry.GetLatestRunJson());

        Assert.AreEqual(AIRunTelemetry.SchemaVersion, restored.schemaVersion);
        Assert.AreEqual(77, restored.seed);
        Assert.AreEqual("0000004D-000012", restored.runId);
        CollectionAssert.AreEqual(
            shadowWeights, restored.shadowWeightsAtStart);
        CollectionAssert.AreEqual(
            directorWeights, restored.directorWeightsAtStart);
        Assert.AreEqual(
            directorState, restored.directorPolicyStateAtStart);
        Assert.AreEqual(
            shadowSequenceState, restored.shadowSequenceStateAtStart);
        Assert.AreEqual(1, restored.directorDecisions.Count);
        Assert.IsTrue(restored.directorDecisions[0].trained);
        Assert.AreEqual((int)AIDirectorIntent.Flow,
            restored.directorDecisions[0].proposedIntent);
        Assert.IsTrue(restored.directorDecisions[0].safetyAdjusted);
        Assert.AreEqual(0.18f,
            restored.directorDecisions[0].policyUncertainty, 0.0001f);
        Assert.AreEqual(0.65f, restored.directorDecisions[0].reward, 0.0001f);
        CollectionAssert.AreEqual(context,
            restored.directorDecisions[0].context);
        Assert.AreEqual(1, restored.shadowSamples.Count);
        Assert.AreEqual((int)ShadowAction.Jump,
            restored.shadowSamples[0].action);
        Assert.AreEqual((int)ShadowAction.Keep,
            restored.shadowSamples[0].baseAction);
        Assert.AreEqual(0.36f,
            restored.shadowSamples[0].sequenceInfluence, 0.0001f);
    }

    [Test]
    public void ClearInputEmptiesQueuedSwipes()
    {
        InputManager input = Create<InputManager>("InputManager");
        FieldInfo field = typeof(InputManager).GetField(
            "_swipeQueue", BindingFlags.Instance | BindingFlags.NonPublic);
        var queue = (Queue<SwipeDirection>)field.GetValue(input);
        queue.Enqueue(SwipeDirection.Left);
        queue.Enqueue(SwipeDirection.Up);

        input.ClearInput();

        Assert.AreEqual(SwipeDirection.None, input.GetSwipe());
    }

    [Test]
    public void SwipeThresholdAdaptsToScreenSizeAndDensity()
    {
        Assert.AreEqual(30f,
            InputManager.ResolveSwipeThreshold(30f, 390f, 0f), 0.0001f);
        Assert.AreEqual(54f,
            InputManager.ResolveSwipeThreshold(30f, 1200f, 0f), 0.0001f);
        Assert.AreEqual(56f,
            InputManager.ResolveSwipeThreshold(30f, 800f, 400f), 0.0001f);
    }

    [Test]
    public void IntendedJumpGesturesExceedNinetyPercentClassificationTarget()
    {
        const int samples = 101;
        int jumps = 0;
        for (int i = 0; i < samples; i++)
        {
            float horizontalNoise = Mathf.Lerp(-0.9f, 0.9f,
                i / (float)(samples - 1));
            Vector2 intendedJump = new Vector2(horizontalNoise * 120f, 120f);
            if (InputManager.ClassifySwipe(intendedJump, 30f)
                == SwipeDirection.Up)
                jumps++;
        }

        Assert.Greater((float)jumps / samples, 0.90f,
            "Representative diagonal upward gestures must classify as Jump.");
        Assert.AreEqual(SwipeDirection.Right,
            InputManager.ClassifySwipe(new Vector2(120f, 60f), 30f),
            "A clearly horizontal lane change must remain horizontal.");
    }

    [Test]
    public void SwipeResultReportsAcceptedAndBlockedActions()
    {
        InputManager input = Create<InputManager>("InputManagerFeedback");
        SwipeDirection reportedDirection = SwipeDirection.None;
        bool reportedAccepted = true;
        input.SwipeResolved += (direction, accepted) =>
        {
            reportedDirection = direction;
            reportedAccepted = accepted;
        };

        input.ReportSwipeResult(SwipeDirection.Up, false);

        Assert.AreEqual(SwipeDirection.Up, reportedDirection);
        Assert.IsFalse(reportedAccepted);
    }

    [Test]
    public void ConstrainedPlatformsCapSavedHighFrameRates()
    {
        Assert.AreEqual(30, GameManager.NormalizeFrameRate(30, true));
        Assert.AreEqual(60, GameManager.NormalizeFrameRate(120, true));
        Assert.AreEqual(120, GameManager.NormalizeFrameRate(120, false));
        Assert.AreEqual(60, GameManager.NormalizeFrameRate(75, false));
    }

    [Test]
    public void DesktopWebGlKeepsThe120FrameRateOption()
    {
        Assert.IsFalse(GameManager.ShouldConstrainHighFrameRate(
            false, true, false));
        Assert.IsTrue(GameManager.ShouldConstrainHighFrameRate(
            false, true, true));
        Assert.IsTrue(GameManager.ShouldConstrainHighFrameRate(
            true, false, false));
    }

    [Test]
    public void TrackBufferUsesRouteProgressInsteadOfWorldDisplacement()
    {
        Assert.IsTrue(TrackSpawnRules.NeedsSegment(120f, 100f, 20f, 10));
        Assert.IsFalse(TrackSpawnRules.NeedsSegment(200f, 100f, 20f, 10));
        Assert.IsTrue(TrackSpawnRules.CanRecycleSegment(80f, 200f, 20f, 5f));
        Assert.IsFalse(TrackSpawnRules.CanRecycleSegment(100f, 200f, 20f, 5f));
    }

    [Test]
    public void TouchLayoutRequestsLandscapeOnlyWhenPortrait()
    {
        Assert.IsTrue(UIManager.ShouldShowLandscapeGuard(720, 1280, true));
        Assert.IsFalse(UIManager.ShouldShowLandscapeGuard(1280, 720, true));
        Assert.IsFalse(UIManager.ShouldShowLandscapeGuard(720, 1280, false));
    }

    [Test]
    public void WeixinPortraitLayoutDoesNotRequestLandscapeGuard()
    {
        Assert.IsFalse(UILayoutRules.ShouldShowLandscapeGuard(
            720, 1280, true, true));
        Assert.IsTrue(UILayoutRules.IsCompactPortrait(720, 1280));
        Assert.IsFalse(UILayoutRules.IsCompactPortrait(1280, 720));
    }

    [Test]
    public void ShadowCalibrationRejectsPassiveKeepSamples()
    {
        int[] actionCounts = { 24, 0, 0, 0, 0 };

        Assert.IsFalse(AIShadowRunner.HasCalibrationSamples(
            24, 0, actionCounts, 24, 6, 2));
        Assert.AreEqual(0f, AIShadowRunner.CalculateCalibrationProgress(
            24, 0, actionCounts, 24, 6, 2), 0.0001f);
    }

    [Test]
    public void ShadowCalibrationRequiresDiverseActiveActions()
    {
        int[] laneOnlyCounts = { 18, 3, 3, 0, 0 };
        int[] diverseCounts = { 18, 3, 0, 3, 0 };

        Assert.IsFalse(AIShadowRunner.HasCalibrationSamples(
            24, 6, laneOnlyCounts, 24, 6, 2),
            "Repeated lane changes are only one action category.");
        Assert.IsTrue(AIShadowRunner.HasCalibrationSamples(
            24, 6, diverseCounts, 24, 6, 2),
            "Lane changes plus a vertical action provide enough behavioral variety.");
    }

    [Test]
    public void ShadowCalibrationProgressUsesTheWeakestRequirement()
    {
        int[] actionCounts = { 18, 3, 0, 3, 0 };

        Assert.AreEqual(0.5f, AIShadowRunner.CalculateCalibrationProgress(
            24, 3, actionCounts, 24, 6, 2), 0.0001f);
    }

    [Test]
    public void ShadowCanReachCalibrationContractWithinThreeRounds()
    {
        int[] actionCounts = { 0, 0, 0, 0, 0 };
        int totalSamples = 0;
        int activeSamples = 0;

        for (int round = 0; round < 3; round++)
        {
            totalSamples += 8;
            actionCounts[(int)ShadowAction.Keep] += 6;
            actionCounts[(int)(round % 2 == 0
                ? ShadowAction.Jump : ShadowAction.Left)] += 2;
            activeSamples += 2;
        }

        Assert.IsTrue(AIShadowRunner.HasCalibrationSamples(
            totalSamples, activeSamples, actionCounts, 24, 6, 2),
            "Eight representative samples per completed round must calibrate by round three.");
    }

    [Test]
    public void UiFontIsBundledForRuntime()
    {
        Font font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");

        Assert.IsNotNull(font, "The bundled Noto Sans CJK font must be included in runtime builds.");

        const string requiredCharacters =
            "开始游戏设置角色选择音量帧率返回默认红色蓝色绿色金色暗黑距离已暂停继续主页得分最高金币重新新纪录总计校准影子挑战领先落后模仿进化▶";
        foreach (char character in requiredCharacters)
            Assert.IsTrue(font.HasCharacter(character), "UI font is missing: " + character);
    }

    [Test]
    public void AITrackPolicySelectsFlowForNewPlayer()
    {
        AITrackPolicy policy = new AITrackPolicy(1);
        float[] context = { 1f, 0f, 0f, 0f, 0f };

        int action = policy.Select(context, false, 0f);

        Assert.AreEqual(1, action, "The initial model should favor a readable flow pattern.");
    }

    [Test]
    public void AITrackPolicyLearnsFromReward()
    {
        AITrackPolicy policy = new AITrackPolicy(1);
        float[] context = { 1f, 0f, 0f, 0f, 0f };
        float before = policy.Score(3, context);

        policy.Update(3, context, 1f, 0.2f);

        Assert.Greater(policy.Score(3, context), before,
            "A positive play reward must increase the selected strategy score.");
    }

    [Test]
    public void AITrackPolicyWeightsSurviveRoundTrip()
    {
        float[] context = { 1f, 0.8f, 0.1f, 0.9f, 0.6f };
        AITrackPolicy trained = new AITrackPolicy(1);
        for (int i = 0; i < 12; i++)
            trained.Update(3, context, 1f, 0.1f);

        AITrackPolicy restored = new AITrackPolicy(2, trained.ExportWeights());

        Assert.AreEqual(trained.Score(3, context),
            restored.Score(3, context), 0.0001f);
        Assert.AreEqual(trained.Select(context, false, 0f),
            restored.Select(context, false, 0f));
    }

    [Test]
    public void LinUcbStartsFromLegacyFlowPrior()
    {
        var policy = new AILinUcbPolicy(
            new AITrackPolicy(1).ExportWeights());
        float[] context = { 1f, 0f, 0f, 0f, 0f };

        int action = policy.Select(context, 0f);

        Assert.AreEqual(1, action);
        Assert.Greater(policy.LastSelectedUncertainty, 0f);
    }

    [Test]
    public void LinUcbPositiveEvidenceRaisesMeanAndReducesUncertainty()
    {
        var policy = new AILinUcbPolicy();
        float[] context = { 1f, 0.7f, 0.2f, 0.6f, 0.8f };
        float meanBefore = policy.MeanScore(2, context);
        float uncertaintyBefore = policy.Uncertainty(2, context);

        for (int i = 0; i < 20; i++)
            policy.Update(2, context, 1f);

        Assert.Greater(policy.MeanScore(2, context), meanBefore);
        Assert.Less(policy.Uncertainty(2, context), uncertaintyBefore);
    }

    [Test]
    public void LinUcbStateSurvivesJsonRoundTrip()
    {
        float[] context = { 1f, 0.4f, 0.3f, 0.2f, 0.7f };
        var trained = new AILinUcbPolicy();
        for (int i = 0; i < 8; i++)
            trained.Update(1, context, 0.75f);

        var restored = new AILinUcbPolicy(
            null, trained.ExportStateJson());

        Assert.AreEqual(trained.MeanScore(1, context),
            restored.MeanScore(1, context), 0.0001f);
        Assert.AreEqual(trained.Uncertainty(1, context),
            restored.Uncertainty(1, context), 0.0001f);
        CollectionAssert.AreEqual(
            trained.ExportWeights(), restored.ExportWeights());
    }

    [Test]
    public void DirectorSafetyLayerCapsRiskWithoutChangingSafeChoices()
    {
        Assert.AreEqual(0, AITrackDirector.ConstrainAction(
            3, 0.2f, 0.1f, true));
        Assert.AreEqual(0, AITrackDirector.ConstrainAction(
            2, 0.8f, 0.1f, false));
        Assert.AreEqual(1, AITrackDirector.ConstrainAction(
            3, 0.2f, 0.9f, false));
        Assert.AreEqual(2, AITrackDirector.ConstrainAction(
            2, 0.2f, 0.1f, false));
    }

    [Test]
    public void ArchiveJsonPreservesExistingProgressAndModels()
    {
        EchoRunSaveData original = new EchoRunSaveData
        {
            highScore = 7904,
            totalCoins = 321,
            targetFrameRate = 60,
            shadowProfileJson = "{\"generation\":22,\"sampleCount\":90}",
            directorWeights = new[] { 0.1f, 0.2f, 0.3f },
            directorModelUpdateCount = 48,
            directorPolicyJson = "{\"version\":1,\"actionPulls\":[1,2,3,4]}",
            runSequence = 12,
            lastRunTelemetryJson = "{\"schemaVersion\":1,\"seed\":77}",
            skillProfileJson = "{\"version\":1,\"completedRuns\":4}",
            savedAtUtcTicks = 123456789L
        };

        EchoRunSaveData restored = JsonUtility.FromJson<EchoRunSaveData>(
            JsonUtility.ToJson(original));

        Assert.AreEqual(7904, restored.highScore);
        Assert.AreEqual(22,
            JsonUtility.FromJson<ShadowGenerationProbe>(
                restored.shadowProfileJson).generation);
        CollectionAssert.AreEqual(original.directorWeights, restored.directorWeights);
        Assert.AreEqual(48, restored.directorModelUpdateCount);
        StringAssert.Contains("\"actionPulls\"",
            restored.directorPolicyJson);
        Assert.AreEqual(12, restored.runSequence);
        StringAssert.Contains("\"seed\":77", restored.lastRunTelemetryJson);
        StringAssert.Contains("\"completedRuns\":4", restored.skillProfileJson);
    }

    [Test]
    public void AIShadowPolicyLearnsPlayerActionFromContext()
    {
        AIShadowPolicy policy = new AIShadowPolicy();
        float[] obstacleAhead = { 1f, 0f, 0.4f, 1f, 0f, 0.33f, 0f, 0f };

        for (int i = 0; i < 30; i++)
            policy.Learn((int)ShadowAction.Jump, obstacleAhead, 0.12f);

        Assert.AreEqual((int)ShadowAction.Jump, policy.Predict(obstacleAhead),
            "The behavior clone should reproduce a repeatedly observed jump response.");
        Assert.Greater(policy.Confidence(obstacleAhead), 0.5f);
    }

    [Test]
    public void AIShadowPolicyWeightsSurviveRoundTrip()
    {
        float[] context = { 1f, -1f, 0.2f, 0.8f, 0.5f, 0.66f, 0f, 0f };
        AIShadowPolicy trained = new AIShadowPolicy();
        for (int i = 0; i < 20; i++)
            trained.Learn((int)ShadowAction.Right, context, 0.1f);

        AIShadowPolicy restored = new AIShadowPolicy(trained.ExportWeights());

        Assert.AreEqual(trained.Predict(context), restored.Predict(context));
        Assert.AreEqual(trained.Score((int)ShadowAction.Right, context),
            restored.Score((int)ShadowAction.Right, context), 0.0001f);
    }

    [Test]
    public void ShadowSequencePolicyResolvesAnAmbiguousImmediateDecision()
    {
        AIShadowSequencePolicy policy = new AIShadowSequencePolicy();
        for (int i = 0; i < 24; i++)
            policy.Learn((int)ShadowAction.Jump, (int)ShadowAction.Slide);

        float[] ambiguous = { 0.36f, 0.1f, 0.1f, 0.1f, 0.34f };
        int selected = policy.Predict(ambiguous, (int)ShadowAction.Jump,
            out float sequenceConfidence, out float sequenceInfluence);

        Assert.AreEqual((int)ShadowAction.Slide, selected);
        Assert.Greater(sequenceConfidence, 0.9f);
        Assert.Greater(sequenceInfluence, 0.5f);
    }

    [Test]
    public void ShadowSequencePolicyDefersToAConfidentImmediateDecision()
    {
        AIShadowSequencePolicy policy = new AIShadowSequencePolicy();
        for (int i = 0; i < 24; i++)
            policy.Learn((int)ShadowAction.Jump, (int)ShadowAction.Slide);

        float[] clearContext = { 0.95f, 0.01f, 0.01f, 0.01f, 0.02f };
        int selected = policy.Predict(clearContext, (int)ShadowAction.Jump,
            out _, out float sequenceInfluence);

        Assert.AreEqual((int)ShadowAction.Keep, selected);
        Assert.AreEqual(0f, sequenceInfluence, 0.0001f);
    }

    [Test]
    public void ShadowSequencePolicyStateSurvivesRoundTrip()
    {
        AIShadowSequencePolicy trained = new AIShadowSequencePolicy();
        for (int i = 0; i < 10; i++)
            trained.Learn((int)ShadowAction.Left, (int)ShadowAction.Jump);

        AIShadowSequenceState state = trained.ExportState();
        AIShadowSequencePolicy restored = new AIShadowSequencePolicy(
            state.transitions, state.pairCount);
        float[] ambiguous = { 0.3f, 0.18f, 0.18f, 0.16f, 0.18f };

        Assert.AreEqual(trained.Predict(ambiguous, (int)ShadowAction.Left,
                out _, out _),
            restored.Predict(ambiguous, (int)ShadowAction.Left, out _, out _));
        Assert.AreEqual(trained.PairCount, restored.PairCount);
    }

    [Test]
    public void AIShadowObstacleOutcomeRequiresTheCorrectIndependentAction()
    {
        Assert.IsTrue(AIShadowRunner.CanAvoidObstacle(
            ObstacleType.Low, false, true));
        Assert.IsFalse(AIShadowRunner.CanAvoidObstacle(
            ObstacleType.Low, true, false));
        Assert.IsTrue(AIShadowRunner.CanAvoidObstacle(
            ObstacleType.High, true, false));
        Assert.IsFalse(AIShadowRunner.CanAvoidObstacle(
            ObstacleType.High, false, true));
        Assert.IsFalse(AIShadowRunner.CanAvoidObstacle(
            ObstacleType.Barrier, true, true));
    }

    [Test]
    public void ShadowObstacleReflexUsesOneMutuallyExclusiveVerticalAction()
    {
        Assert.AreEqual(ShadowAction.Slide,
            AIShadowRunner.RequiredActionForObstacle(ObstacleType.Low));
        Assert.AreEqual(ShadowAction.Jump,
            AIShadowRunner.RequiredActionForObstacle(ObstacleType.High));
        Assert.AreEqual(ShadowAction.Keep,
            AIShadowRunner.RequiredActionForObstacle(ObstacleType.Barrier));

        Assert.IsTrue(AIShadowRunner.CanStartVerticalAction(
            ShadowAction.Jump, false, false, false));
        Assert.IsFalse(AIShadowRunner.CanStartVerticalAction(
            ShadowAction.Slide, true, false, false));
        Assert.IsFalse(AIShadowRunner.CanStartVerticalAction(
            ShadowAction.Jump, false, true, false));
    }

    [Test]
    public void ShadowJumpAndSlideCurvesHaveSmoothGroundedEndpoints()
    {
        Assert.AreEqual(0f, AIShadowRunner.EvaluateJumpArc(0f), 0.0001f);
        Assert.AreEqual(1f, AIShadowRunner.EvaluateJumpArc(0.5f), 0.0001f);
        Assert.AreEqual(0f, AIShadowRunner.EvaluateJumpArc(1f), 0.0001f);
        Assert.Less(AIShadowRunner.EvaluateJumpArc(0.01f), 0.002f,
            "The shadow should ease off the ground instead of popping upward.");

        Assert.AreEqual(0f, AIShadowRunner.EvaluateSlideAmount(0f, 0.8f), 0.0001f);
        Assert.Greater(AIShadowRunner.EvaluateSlideAmount(0.4f, 0.8f), 0.99f);
        Assert.Less(AIShadowRunner.EvaluateSlideAmount(0.01f, 0.8f), 0.1f,
            "The shadow should smoothly stand up at the end of a slide.");
    }

    [Test]
    public void SlideColliderLowersTopWithoutMovingBottom()
    {
        GameObject playerObject = new GameObject("player");
        _objects.Add(playerObject);
        CapsuleCollider capsule = playerObject.AddComponent<CapsuleCollider>();
        capsule.center = new Vector3(0f, 1.1f, 0f);
        capsule.height = 2.2f;
        capsule.radius = 0.4f;
        PlayerController player = playerObject.AddComponent<PlayerController>();
        player.slideColliderHeight = 0.9f;
        SetPrivateField(player, "_capsuleCollider", capsule);
        SetPrivateField(player, "_originalColliderHeight", capsule.height);
        SetPrivateField(player, "_originalColliderCenter", capsule.center);

        float originalBottom = capsule.center.y - capsule.height * 0.5f;
        InvokePrivate(player, "ApplySlideCollider");

        Assert.AreEqual(0.9f, capsule.height, 0.0001f);
        Assert.AreEqual(originalBottom,
            capsule.center.y - capsule.height * 0.5f, 0.0001f,
            "Sliding must not lift the player capsule away from the track.");
    }

    [Test]
    public void CharacterAnimatorFindsPlayerAfterLateParenting()
    {
        GameObject visual = new GameObject("CharacterModel");
        _objects.Add(visual);
        GameObject upperArm = new GameObject("Arm_Upper_L");
        _objects.Add(upperArm);
        upperArm.transform.SetParent(visual.transform, false);
        GameObject lowerArm = new GameObject("Arm_Lower_L");
        _objects.Add(lowerArm);
        lowerArm.transform.SetParent(upperArm.transform, false);
        CharacterAnimator characterAnimator = visual.AddComponent<CharacterAnimator>();
        InvokePrivate(characterAnimator, "Initialize");
        Assert.IsNull(GetPrivateField<PlayerController>(characterAnimator, "_player"));

        GameObject playerObject = new GameObject("player");
        _objects.Add(playerObject);
        PlayerController player = playerObject.AddComponent<PlayerController>();
        visual.transform.SetParent(playerObject.transform, false);
        GameManager manager = Create<GameManager>("GameManager");
        manager.StartGame();
        SetPrivateField(characterAnimator, "_gm", manager);

        InvokePrivate(characterAnimator, "LateUpdate");

        Assert.AreSame(player,
            GetPrivateField<PlayerController>(characterAnimator, "_player"),
            "The animator must recover when prefab parenting happens after Awake.");
        Assert.Greater(Quaternion.Angle(Quaternion.identity,
            upperArm.transform.localRotation), 5f,
            "Recovered animators must write a running pose to the model bones.");
    }

    [Test]
    public void CharacterAnimatorForcesLateProceduralPosesIntoSkinning()
    {
        GameObject visual = new GameObject("CharacterModel");
        _objects.Add(visual);
        GameObject meshObject = new GameObject("SkinnedMesh");
        _objects.Add(meshObject);
        meshObject.transform.SetParent(visual.transform, false);
        SkinnedMeshRenderer renderer =
            meshObject.AddComponent<SkinnedMeshRenderer>();
        renderer.updateWhenOffscreen = false;
        renderer.forceMatrixRecalculationPerRender = false;

        CharacterAnimator characterAnimator =
            visual.AddComponent<CharacterAnimator>();
        InvokePrivate(characterAnimator, "Initialize");

        Assert.IsFalse(renderer.updateWhenOffscreen,
            "Offscreen skinning should stay disabled for the WeChat budget.");
        Assert.IsTrue(renderer.forceMatrixRecalculationPerRender,
            "LateUpdate bone changes must reach WebGL/WeChat skinning.");
    }

    [Test]
    public void CharacterAnimatorRunUsesHumanOpposedLimbMechanics()
    {
        GameObject visual = new GameObject("CharacterModel");
        _objects.Add(visual);
        Transform leftUpperArm = CreateBone("LeftUpperArm", visual.transform);
        Transform rightUpperArm = CreateBone("RightUpperArm", visual.transform);
        Transform leftLowerArm = CreateBone("LeftLowerArm", leftUpperArm);
        Transform rightLowerArm = CreateBone("RightLowerArm", rightUpperArm);
        Transform leftUpperLeg = CreateBone("LeftUpperLeg", visual.transform);
        Transform rightUpperLeg = CreateBone("RightUpperLeg", visual.transform);
        Transform leftLowerLeg = CreateBone("LeftLowerLeg", leftUpperLeg);
        Transform rightLowerLeg = CreateBone("RightLowerLeg", rightUpperLeg);
        Transform leftFoot = CreateBone("LeftFoot", leftLowerLeg);
        Transform rightFoot = CreateBone("RightFoot", rightLowerLeg);
        Transform spine = CreateBone("Spine", visual.transform);

        CharacterAnimator characterAnimator =
            visual.AddComponent<CharacterAnimator>();
        characterAnimator.runSwingSpeed = 0f;
        characterAnimator.SetExternalDriver();
        SetPrivateField(characterAnimator, "_runPhase", Mathf.PI * 0.25f);
        characterAnimator.ApplyExternalMotion(
            false, false, Vector3.forward, 10f, 1f);

        Assert.Greater(Quaternion.Angle(
            Quaternion.identity, leftUpperArm.localRotation), 55f,
            "The shoulders must drop out of the horizontal T pose.");
        Assert.Greater(Quaternion.Angle(
            Quaternion.identity, rightUpperArm.localRotation), 55f,
            "Both arms must stay close to the torso while running.");
        Assert.Greater(Quaternion.Angle(
            Quaternion.identity, leftLowerArm.localRotation), 45f,
            "The left elbow must remain bent like a runner's arm.");
        Assert.Greater(Quaternion.Angle(
            Quaternion.identity, rightLowerArm.localRotation), 45f,
            "The right elbow must remain bent like a runner's arm.");
        Assert.Greater(Quaternion.Angle(
            leftUpperArm.localRotation, rightUpperArm.localRotation), 35f,
            "The arms must counter-swing instead of moving as one bar.");
        Assert.Greater(Quaternion.Angle(
            leftUpperLeg.localRotation, rightUpperLeg.localRotation), 35f,
            "The thighs must stride in opposite directions.");
        Assert.Greater(Quaternion.Angle(
            leftLowerLeg.localRotation, rightLowerLeg.localRotation), 35f,
            "The recovery leg must bend while the support leg extends.");
        Assert.Greater(Quaternion.Angle(
            leftFoot.localRotation, rightFoot.localRotation), 12f,
            "The feet must alternate between recovery and push-off angles.");
        Assert.Greater(Quaternion.Angle(
            Quaternion.identity, spine.localRotation), 4f,
            "A running torso needs a small forward lean.");
    }

    [Test]
    public void HumanAnimationControllerContainsRetargetedMotionClips()
    {
        const string controllerPath =
            "Assets/Animations/HumanMotion/EchoRunHuman.controller";
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                controllerPath);
        Assert.IsNotNull(controller,
            "The ExoGray model must use an authored Animator Controller.");

        AnimationClip run = System.Array.Find(
            controller.animationClips, clip => clip.name == "HumanRun");
        AnimationClip idle = System.Array.Find(
            controller.animationClips, clip => clip.name == "HumanIdle");
        AnimationClip falling = System.Array.Find(
            controller.animationClips, clip => clip.name == "HumanFalling");
        Assert.IsNotNull(run);
        Assert.IsNotNull(idle);
        Assert.IsNotNull(falling);
        Assert.IsTrue(run.isHumanMotion);
        Assert.IsTrue(run.isLooping,
            "The authored running clip must loop without procedural resets.");
    }

    [Test]
    public void AuthoredRunRetargetsOntoTheActualExoGraySkeleton()
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx");
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/HumanMotion/EchoRunHuman.controller");
        Assert.IsNotNull(modelAsset);
        Assert.IsNotNull(controller);

        GameObject model = Object.Instantiate(modelAsset);
        _objects.Add(model);
        Animator animator = model.GetComponent<Animator>();
        Assert.IsNotNull(animator);
        Assert.IsTrue(animator.isHuman);
        Transform leftArm =
            animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        Transform rightArm =
            animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform leftLeg =
            animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        Transform rightLeg =
            animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        Quaternion leftArmBind = leftArm.localRotation;
        Quaternion rightArmBind = rightArm.localRotation;
        Quaternion leftLegBind = leftLeg.localRotation;
        Quaternion rightLegBind = rightLeg.localRotation;

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);
        animator.Play(Animator.StringToHash("Run"), 0, 0.25f);
        animator.Update(0.1f);

        Assert.Greater(Quaternion.Angle(leftArmBind, leftArm.localRotation), 8f);
        Assert.Greater(Quaternion.Angle(rightArmBind, rightArm.localRotation), 8f);
        Assert.Greater(Quaternion.Angle(leftLegBind, leftLeg.localRotation), 8f);
        Assert.Greater(Quaternion.Angle(rightLegBind, rightLeg.localRotation), 8f);
        Assert.Greater(Quaternion.Angle(
            leftArm.localRotation, rightArm.localRotation), 15f,
            "The authored run must preserve opposing human arm motion.");
    }

    [Test]
    public void AuthoredRunKeepsTheRunnerCenteredWithoutSideRoll()
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx");
        RuntimeAnimatorController controller =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Animations/HumanMotion/EchoRunHuman.controller");
        Assert.IsNotNull(modelAsset);
        Assert.IsNotNull(controller);

        GameObject model = Object.Instantiate(modelAsset);
        _objects.Add(model);
        Animator animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.Rebind();
        animator.Update(0f);

        CharacterAnimator characterAnimator =
            model.AddComponent<CharacterAnimator>();
        characterAnimator.useHumanoidRig = true;
        SetPrivateField(characterAnimator, "_initialized", false);
        InvokePrivate(characterAnimator, "Initialize");
        characterAnimator.SetExternalDriver();

        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        float hipsCenterX = hips.localPosition.x;
        float spineCenterX = spine.localPosition.x;
        Quaternion hipsBaseRotation = hips.localRotation;
        Quaternion spineBaseRotation = spine.localRotation;
        float[] runTimes = { 0.1f, 0.35f, 0.6f, 0.85f };

        for (int i = 0; i < runTimes.Length; i++)
        {
            animator.Play(Animator.StringToHash("Run"), 0, runTimes[i]);
            animator.Update(0f);
            characterAnimator.ApplyExternalMotion(
                false, false, Vector3.forward, 10f, 0f);

            Assert.AreEqual(hipsCenterX, hips.localPosition.x, 0.0001f);
            Assert.AreEqual(spineCenterX, spine.localPosition.x, 0.0001f);
            Quaternion hipsRelative =
                Quaternion.Inverse(hipsBaseRotation) * hips.localRotation;
            Quaternion spineRelative =
                Quaternion.Inverse(spineBaseRotation) * spine.localRotation;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(
                0f, hipsRelative.eulerAngles.z)), 0.1f);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(
                0f, spineRelative.eulerAngles.z)), 0.1f);
        }
    }

    [Test]
    public void ActualRunnerForearmsPointForwardInsteadOfOutward()
    {
        const string modelPath =
            "Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx";
        GameObject modelAsset =
            AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Assert.IsNotNull(modelAsset);
        GameObject model = Object.Instantiate(modelAsset);
        _objects.Add(model);

        CharacterAnimator characterAnimator =
            model.AddComponent<CharacterAnimator>();
        characterAnimator.useHumanoidRig = true;
        characterAnimator.runSwingSpeed = 0f;
        SetPrivateField(characterAnimator, "_initialized", false);
        InvokePrivate(characterAnimator, "Initialize");
        characterAnimator.SetExternalDriver();

        Animator animator = model.GetComponentInChildren<Animator>();
        Assert.IsNotNull(animator);
        Transform leftElbow =
            animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        Transform rightElbow =
            animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        Assert.IsNotNull(leftElbow);
        Assert.IsNotNull(rightElbow);
        Assert.IsNotNull(leftHand);
        Assert.IsNotNull(rightHand);

        float[] phases = { 0f, Mathf.PI * 0.5f, Mathf.PI, Mathf.PI * 1.5f };
        for (int i = 0; i < phases.Length; i++)
        {
            SetPrivateField(characterAnimator, "_runPhase", phases[i]);
            characterAnimator.ApplyExternalMotion(
                false, false, Vector3.forward, 10f, 1f);
            Vector3 leftForearm =
                (leftHand.position - leftElbow.position).normalized;
            Vector3 rightForearm =
                (rightHand.position - rightElbow.position).normalized;
            Assert.Greater(
                Vector3.Dot(leftForearm, model.transform.forward), 0.55f,
                "The left hand must travel forward from the elbow at phase " + i);
            Assert.Greater(
                Vector3.Dot(rightForearm, model.transform.forward), 0.55f,
                "The right hand must travel forward from the elbow at phase " + i);
            Assert.Less(Mathf.Abs(
                    Vector3.Dot(leftForearm, model.transform.right)), 0.35f,
                "The left forearm must not flare sideways at phase " + i);
            Assert.Less(Mathf.Abs(
                    Vector3.Dot(rightForearm, model.transform.right)), 0.35f,
                "The right forearm must not flare sideways at phase " + i);
        }
    }

    [Test]
    public void CharacterAnimatorJumpUsesAStaggeredBentLimbPose()
    {
        GameObject visual = new GameObject("CharacterModel");
        _objects.Add(visual);
        Transform leftUpperArm = CreateBone("LeftUpperArm", visual.transform);
        Transform rightUpperArm = CreateBone("RightUpperArm", visual.transform);
        Transform leftLowerArm = CreateBone("LeftLowerArm", leftUpperArm);
        Transform rightLowerArm = CreateBone("RightLowerArm", rightUpperArm);
        Transform leftUpperLeg = CreateBone("LeftUpperLeg", visual.transform);
        Transform rightUpperLeg = CreateBone("RightUpperLeg", visual.transform);
        CreateBone("LeftLowerLeg", leftUpperLeg);
        CreateBone("RightLowerLeg", rightUpperLeg);
        Transform spine = CreateBone("Spine", visual.transform);

        CharacterAnimator characterAnimator =
            visual.AddComponent<CharacterAnimator>();
        characterAnimator.SetExternalDriver();
        characterAnimator.ApplyExternalMotion(
            true, false, Vector3.forward, 10f, 1f);

        Assert.Greater(Quaternion.Angle(
            leftUpperArm.localRotation, rightUpperArm.localRotation), 25f,
            "Jump arms must counter-swing instead of forming a symmetric V.");
        Assert.Greater(Quaternion.Angle(
            Quaternion.identity, leftLowerArm.localRotation), 35f,
            "The left elbow must bend during a jump.");
        Assert.Greater(Quaternion.Angle(
            Quaternion.identity, rightLowerArm.localRotation), 35f,
            "The right elbow must bend during a jump.");
        Assert.Greater(Quaternion.Angle(
            leftUpperLeg.localRotation, rightUpperLeg.localRotation), 40f,
            "One knee must lead while the trailing leg extends behind.");
        Assert.Greater(Quaternion.Angle(
            Quaternion.identity, spine.localRotation), 8f,
            "The torso must lean into the jump instead of staying upright.");
    }

    [Test]
    public void ShadowCreatedBeforeStartStillClonesThePlayerVisual()
    {
        GameObject playerObject = new GameObject("player");
        _objects.Add(playerObject);
        PlayerController player = playerObject.AddComponent<PlayerController>();
        GameObject visual = new GameObject("CharacterModel");
        _objects.Add(visual);
        visual.transform.SetParent(playerObject.transform, false);
        GameObject marker = new GameObject("PlayerVisualMarker");
        _objects.Add(marker);
        marker.transform.SetParent(visual.transform, false);
        player.characterModel = visual.transform;

        AIShadowRunner runner = Create<AIShadowRunner>("AIShadowRunner");
        SetPrivateField(runner, "_player", null);

        InvokePrivate(runner, "CreateGhost");

        GameObject ghost = GetPrivateField<GameObject>(runner, "_ghost");
        Assert.IsNotNull(ghost);
        Assert.IsNotNull(ghost.transform.Find("ShadowVisual/PlayerVisualMarker"),
            "CreateGhost must resolve the scene player instead of permanently " +
            "falling back to a visible capsule when Start has not run yet.");
    }

    [Test]
    public void SceneRuntimeScriptsResolveToCompiledClasses()
    {
        string[] paths =
        {
            "Assets/Scripts/AudioManager.cs",
            "Assets/Scripts/CameraFollow.cs",
            "Assets/Scripts/CharacterAnimator.cs",
            "Assets/Scripts/Coin.cs",
            "Assets/Scripts/GameManager.cs",
            "Assets/Scripts/GroundFollower.cs",
            "Assets/Scripts/InputManager.cs",
            "Assets/Scripts/Obstacle.cs",
            "Assets/Scripts/ParticleManager.cs",
            "Assets/Scripts/PlayerController.cs",
            "Assets/Scripts/TrackSegmentData.cs",
            "Assets/Scripts/UIManager.cs"
        };
        System.Type[] expected =
        {
            typeof(AudioManager),
            typeof(CameraFollow),
            typeof(CharacterAnimator),
            typeof(Coin),
            typeof(GameManager),
            typeof(GroundFollower),
            typeof(InputManager),
            typeof(Obstacle),
            typeof(ParticleManager),
            typeof(PlayerController),
            typeof(TrackSegmentData),
            typeof(UIManager)
        };

        for (int i = 0; i < paths.Length; i++)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(paths[i]);
            Assert.IsNotNull(script, paths[i] + " was not imported as a MonoScript.");
            Assert.AreEqual(expected[i], script.GetClass(),
                paths[i] + " lost its compiled class binding.");
        }
    }

    [Test]
    public void ShadowSlidesOnceForAnApproachingLowObstacle()
    {
        TrackManager manager = Create<TrackManager>("TrackManager");
        if (TrackManager.Instance != manager)
            InvokePrivate(manager, "Awake");
        GameObject owner = new GameObject("Segment");
        _objects.Add(owner);
        GameObject lowPrefab = CreateObstaclePrefab("LowObstacle", ObstacleType.Low);
        InvokePrivate(manager, "SpawnDynamic", lowPrefab, owner,
            new Vector3(0f, 1f, 3f), Quaternion.identity);
        Assert.IsTrue(manager.TryGetUpcomingObstacleInLane(
            Vector3.zero, Vector3.forward, 1, new HashSet<int>(),
            out _, out ObstacleType detectedType, out _));
        Assert.AreEqual(ObstacleType.Low, detectedType);

        GameObject playerObject = new GameObject("player");
        _objects.Add(playerObject);
        PlayerController player = playerObject.AddComponent<PlayerController>();
        GameObject ghost = new GameObject("ghost");
        _objects.Add(ghost);
        GameObject visual = new GameObject("visual");
        _objects.Add(visual);
        visual.transform.SetParent(ghost.transform, false);
        GameObject head = new GameObject("Head");
        _objects.Add(head);
        head.transform.SetParent(visual.transform, false);
        head.transform.localPosition = new Vector3(0f, 2.08f, 0f);
        GameObject torso = new GameObject("Torso");
        _objects.Add(torso);
        torso.transform.SetParent(visual.transform, false);
        torso.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        GameObject upperLeg = new GameObject("Leg_Upper_L");
        _objects.Add(upperLeg);
        upperLeg.transform.SetParent(visual.transform, false);
        GameObject lowerLeg = new GameObject("Leg_Lower_L");
        _objects.Add(lowerLeg);
        lowerLeg.transform.SetParent(upperLeg.transform, false);
        GameObject rearUpperLeg = new GameObject("Leg_Upper_R");
        _objects.Add(rearUpperLeg);
        rearUpperLeg.transform.SetParent(visual.transform, false);
        GameObject rearLowerLeg = new GameObject("Leg_Lower_R");
        _objects.Add(rearLowerLeg);
        rearLowerLeg.transform.SetParent(rearUpperLeg.transform, false);
        CharacterAnimator characterAnimator = visual.AddComponent<CharacterAnimator>();
        characterAnimator.SetExternalDriver();

        AIShadowRunner runner = manager.GetComponent<AIShadowRunner>();
        Assert.IsNotNull(runner);
        SetPrivateField(runner, "_player", player);
        SetPrivateField(runner, "_ghost", ghost);
        SetPrivateField(runner, "_ghostVisual", visual.transform);
        SetPrivateField(runner, "_ghostVisualPosition", Vector3.zero);
        SetPrivateField(runner, "_ghostAnimator", characterAnimator);
        SetPrivateField(runner, "_ghostGroundY", 0f);

        InvokePrivate(runner, "ApplyObstacleReaction");
        float startedTimer = GetPrivateField<float>(runner, "_ghostSlideTimer");
        Assert.Greater(startedTimer, 0f,
            "A low obstacle in the shadow lane must start a slide.");

        SetPrivateField(runner, "_ghostSlideTimer", startedTimer - 0.12f);
        InvokePrivate(runner, "UpdateGhostPose");
        characterAnimator.ApplyExternalMotion(
            false, true, Vector3.forward, 10f, 0.2f);
        Assert.AreEqual(Vector3.one, visual.transform.localScale,
            "The shadow root must not sink by scaling the whole body.");
        Assert.AreEqual(Vector3.one, torso.transform.localScale,
            "The torso must keep its authored proportions during the slide.");
        Assert.Greater(Quaternion.Angle(Quaternion.identity,
            torso.transform.localRotation), 20f,
            "The torso must lean into a visible slide pose.");
        Assert.Greater(Quaternion.Angle(Quaternion.identity,
            upperLeg.transform.localRotation), 50f,
            "The upper leg must swing into the slide pose.");
        Assert.Greater(Quaternion.Angle(Quaternion.identity,
            rearLowerLeg.transform.localRotation), 75f,
            "The rear knee must bend instead of sinking the whole model.");

        SetPrivateField(runner, "_ghostSlideTimer", 0f);
        InvokePrivate(runner, "ApplyObstacleReaction");
        Assert.AreEqual(0f, GetPrivateField<float>(runner, "_ghostSlideTimer"),
            "The same obstacle must not retrigger the slide.");
    }

    [Test]
    public void ShadowHeightDoesNotFollowPlayerJump()
    {
        GameObject playerObject = new GameObject("player");
        _objects.Add(playerObject);
        playerObject.transform.position = new Vector3(0f, 4f, 0f);
        PlayerController player = playerObject.AddComponent<PlayerController>();
        SetPrivateField(player, "<IsJumping>k__BackingField", true);

        AIShadowRunner runner = Create<AIShadowRunner>("AIShadowRunner");
        GameObject ghost = new GameObject("ghost");
        _objects.Add(ghost);
        SetPrivateField(runner, "_player", player);
        SetPrivateField(runner, "_ghost", ghost);
        SetPrivateField(runner, "_ghostGroundY", 1f);

        InvokePrivate(runner, "UpdateGhostPose");

        Assert.AreEqual(1f, ghost.transform.position.y, 0.001f,
            "A player jump must not lift a shadow that did not choose Jump.");
    }

    [Test]
    public void ShadowObstacleQuerySelectsItsOwnLaneAndSkipsHandledObjects()
    {
        TrackManager manager = Create<TrackManager>("TrackManager");
        GameObject owner = new GameObject("Segment");
        _objects.Add(owner);

        GameObject otherLanePrefab = CreateObstaclePrefab("OtherLane", ObstacleType.Barrier);
        GameObject ownLanePrefab = CreateObstaclePrefab("OwnLane", ObstacleType.High);
        InvokePrivate(manager, "SpawnDynamic", otherLanePrefab, owner,
            new Vector3(-manager.laneDistance, 1f, 1f), Quaternion.identity);
        InvokePrivate(manager, "SpawnDynamic", ownLanePrefab, owner,
            new Vector3(0f, 1f, 1.4f), Quaternion.identity);

        bool found = manager.TryGetUpcomingObstacleInLane(
            Vector3.zero, Vector3.forward, 1, new HashSet<int>(),
            out float distance, out ObstacleType type, out int obstacleId);

        Assert.IsTrue(found);
        Assert.AreEqual(1.4f, distance, 0.001f);
        Assert.AreEqual(ObstacleType.High, type);

        var handled = new HashSet<int> { obstacleId };
        Assert.IsFalse(manager.TryGetUpcomingObstacleInLane(
            Vector3.zero, Vector3.forward, 1, handled,
            out _, out _, out _));
    }

    [Test]
    public void AITrackPlanAlwaysLeavesAReachableLane()
    {
        AITrackDirector director = Create<AITrackDirector>("AITrackDirector");
        director.observationSegments = 0;
        director.explorationRate = 0f;
        int previousSafeLane = 1;

        for (int i = 0; i < 20; i++)
        {
            AITrackPlan plan = director.CreatePlan(
                0.8f, 0.7f, 0.6f, 0.2f, previousSafeLane, true, (i + 1) * 20f);

            Assert.That(plan.safeLane, Is.InRange(0, 2));
            Assert.LessOrEqual(Mathf.Abs(plan.safeLane - previousSafeLane), 1);
            Assert.That(plan.maxBlockedLanes, Is.InRange(1, 2));
            previousSafeLane = plan.safeLane;
        }
    }

    [Test]
    public void TrackObstacleGenerationCapsEmptyStraightsAfterWarmup()
    {
        Assert.IsFalse(TrackManager.ShouldSpawnObstacleRow(
            2, 2, 2, 3, 1f, 0f), "Warmup must remain obstacle-free.");
        Assert.IsFalse(TrackManager.ShouldSpawnObstacleRow(
            5, 3, 2, 3, 0f, 1f));
        Assert.IsTrue(TrackManager.ShouldSpawnObstacleRow(
            6, 4, 2, 3, 0f, 1f),
            "The fourth consecutive empty straight must force an obstacle row.");
    }

    [Test]
    public void PlayerJumpArcLandsWithinConfiguredDuration()
    {
        Assert.AreEqual(0f, PlayerController.EvaluateJumpArc(0f), 0.0001f);
        Assert.AreEqual(1f, PlayerController.EvaluateJumpArc(0.5f), 0.0001f);
        Assert.AreEqual(0f, PlayerController.EvaluateJumpArc(1f), 0.0001f);
    }

    [Test]
    public void ObstacleRowsLeaveAFullJumpAndRecoveryWindowAtMaximumSpeed()
    {
        float spacing = TrackSpawnRules.MinimumObstacleRowSpacing(40f, 0.9f, 20f);

        Assert.AreEqual(48f, spacing, 0.001f);
        Assert.IsFalse(TrackSpawnRules.CanSpawnObstacleRow(47.9f, 0f, spacing));
        Assert.IsTrue(TrackSpawnRules.CanSpawnObstacleRow(48f, 0f, spacing));
    }

    [Test]
    public void GeneratedObstacleTypesExcludeAmbiguousFullHeightBarrier()
    {
        for (int difficultyStep = 0; difficultyStep <= 10; difficultyStep++)
        {
            for (int rollStep = 0; rollStep <= 10; rollStep++)
            {
                int type = TrackSpawnRules.SelectObstaclePrefabIndex(
                    difficultyStep / 10f, rollStep / 10f);
                Assert.That(type, Is.InRange(0, 1));
            }
        }
    }

    [Test]
    public void FailedCollisionStopsPlayerInFrontOfObstacle()
    {
        Bounds obstacle = new Bounds(
            new Vector3(0f, 1f, 10f), new Vector3(3.4f, 2.7f, 0.9f));
        Vector3 stopped = PlayerController.CalculateObstacleStopPosition(
            obstacle, new Vector3(0f, 1f, 10.2f), Vector3.forward, 0.45f);

        Assert.LessOrEqual(stopped.z, obstacle.min.z - 0.45f + 0.0001f);
    }

    [Test]
    public void TrackObstacleFairnessTargetsStarvedEdgeLane()
    {
        int[] drought = { 1, 0, 8 };
        int safeLane = TrackManager.ChooseFairSafeLane(2, 1, drought);
        int[] blocked = TrackManager.SelectBlockedLanes(safeLane, 1, drought);

        Assert.AreNotEqual(2, safeLane,
            "A long-starved edge lane must not remain protected indefinitely.");
        CollectionAssert.Contains(blocked, 2,
            "The next obstacle row should refill the long-starved edge lane.");
        Assert.LessOrEqual(Mathf.Abs(safeLane - 1), 1,
            "Fairness must not create an unreachable safe-lane jump.");
    }

    [Test]
    public void TrackManagerRepairsPartiallyMissingObstaclePrefabs()
    {
        TrackManager manager = Create<TrackManager>("TrackManager");
        manager.obstaclePrefabs = new GameObject[3];
        MethodInfo ensureAssets = typeof(TrackManager).GetMethod(
            "EnsureProceduralAssets", BindingFlags.Instance | BindingFlags.NonPublic);

        ensureAssets.Invoke(manager, null);

        Assert.AreEqual(3, manager.obstaclePrefabs.Length);
        foreach (GameObject prefab in manager.obstaclePrefabs)
            Assert.IsNotNull(prefab);
        Assert.AreEqual(Vector3.one, manager.trackSegmentPrefab.transform.localScale,
            "Dynamic objects require an unscaled track root for correct world placement.");
    }

    [TestCase(-1)]
    [TestCase(1)]
    public void TurnCoverageTouchesFollowingStraightWithoutCoplanarOverlap(
        int turnDirection)
    {
        TrackManager manager = Create<TrackManager>("TrackManager");
        GameObject turn = new GameObject("TurnSegment");
        _objects.Add(turn);
        MethodInfo ensureCoverage = typeof(TrackManager).GetMethod(
            "EnsureTurnCoverage", BindingFlags.Instance | BindingFlags.NonPublic);

        ensureCoverage.Invoke(manager, new object[] { turn, turnDirection });

        Transform coverage = turn.transform.Find("RuntimeTurnCoverage");
        Assert.IsNotNull(coverage);
        Transform entry = coverage.Find("EntryCoverage");
        Transform exit = coverage.Find("ExitCoverage");
        Assert.IsNotNull(entry);
        Assert.IsNotNull(exit);
        Assert.IsNotNull(entry.GetComponent<BoxCollider>());
        Assert.IsNotNull(exit.GetComponent<BoxCollider>());

        float exitCenter = exit.localPosition.x * turnDirection;
        float exitHalfLength = exit.localScale.z * 0.5f;
        float coverageJoin = entry.localScale.x * 0.5f;
        float nextStraightNearEdge = manager.segmentLength * 0.5f;

        Assert.AreEqual(coverageJoin,
            exitCenter - exitHalfLength, 0.001f,
            "Exit coverage must start where entry coverage ends.");
        Assert.AreEqual(nextStraightNearEdge,
            exitCenter + exitHalfLength, 0.001f,
            "Exit coverage must stop at the next straight's near edge.");
        Assert.AreEqual(manager.segmentLength * 0.5f,
            exit.localPosition.z, 0.001f);
    }

    [Test]
    public void ShadowTrackPoseFollowsUpcomingTurnAndStaysInLane()
    {
        TrackManager manager = Create<TrackManager>("TrackManager");
        GameObject turn = new GameObject("Turn");
        _objects.Add(turn);
        TrackSegmentData data = turn.AddComponent<TrackSegmentData>();
        data.segmentType = TrackSegmentType.TurnRight;
        data.entryDirection = Vector3.forward;
        data.exitDirection = Vector3.right;
        data.turnPointWorld = new Vector3(0f, 0f, 5f);

        FieldInfo activeField = typeof(TrackManager).GetField(
            "_activeSegments", BindingFlags.Instance | BindingFlags.NonPublic);
        var activeSegments = (List<GameObject>)activeField.GetValue(manager);
        activeSegments.Add(turn);

        manager.GetTrackPoseAhead(new Vector3(0f, 1f, 0f), Vector3.forward,
            1, 2, 8f, out Vector3 position, out Vector3 forward);

        Assert.AreEqual(Vector3.right, forward);
        Assert.AreEqual(3f, position.x, 0.001f);
        Assert.AreEqual(2f, position.z, 0.001f,
            "The shadow must turn at the corner before applying its lane offset.");

        manager.GetTrackPoseAhead(new Vector3(0f, 1f, 0f), Vector3.forward,
            1, 2f, 4.9f, out Vector3 beforeCorner, out Vector3 beforeForward);
        manager.GetTrackPoseAhead(new Vector3(0f, 1f, 0f), Vector3.forward,
            1, 2f, 5.1f, out Vector3 afterCorner, out Vector3 afterForward);

        Assert.Less(Vector3.Distance(beforeCorner, afterCorner), 1f,
            "The rounded corner pose must stay continuous across the turn point.");
        Assert.Greater(Vector3.Dot(beforeForward, afterForward), 0.9f,
            "The shadow direction must rotate smoothly instead of snapping 90 degrees.");
    }

    [Test]
    public void TurnTransitionOnlyCoversTheCorner()
    {
        TrackManager manager = Create<TrackManager>("TrackManager");
        GameObject turn = new GameObject("Turn");
        _objects.Add(turn);
        TrackSegmentData data = turn.AddComponent<TrackSegmentData>();
        data.segmentType = TrackSegmentType.TurnRight;
        data.entryDirection = Vector3.forward;
        data.exitDirection = Vector3.right;
        data.turnPointWorld = new Vector3(0f, 0f, 5f);

        FieldInfo activeField = typeof(TrackManager).GetField(
            "_activeSegments", BindingFlags.Instance | BindingFlags.NonPublic);
        var activeSegments = (List<GameObject>)activeField.GetValue(manager);
        activeSegments.Add(turn);

        Assert.IsTrue(manager.IsInsideTurnTransition(new Vector3(0f, 0f, 3f)));
        Assert.IsTrue(manager.IsInsideTurnTransition(new Vector3(2f, 0f, 5f)));
        Assert.IsFalse(manager.IsInsideTurnTransition(new Vector3(0f, 0f, -5f)));
        Assert.IsFalse(manager.IsInsideTurnTransition(new Vector3(10f, 0f, 5f)));
    }

    [Test]
    public void ShadowDoesNotCountAProjectedObstacleWhileTurning()
    {
        TrackManager manager = Create<TrackManager>("TrackManager");
        if (TrackManager.Instance != manager)
            InvokePrivate(manager, "Awake");

        GameObject turn = new GameObject("Turn");
        _objects.Add(turn);
        TrackSegmentData data = turn.AddComponent<TrackSegmentData>();
        data.segmentType = TrackSegmentType.TurnRight;
        data.entryDirection = Vector3.forward;
        data.exitDirection = Vector3.right;
        data.turnPointWorld = Vector3.zero;
        FieldInfo activeField = typeof(TrackManager).GetField(
            "_activeSegments", BindingFlags.Instance | BindingFlags.NonPublic);
        var activeSegments = (List<GameObject>)activeField.GetValue(manager);
        activeSegments.Add(turn);

        GameObject owner = new GameObject("ExitSegment");
        _objects.Add(owner);
        GameObject barrierPrefab = CreateObstaclePrefab(
            "BarrierObstacle", ObstacleType.Barrier);
        InvokePrivate(manager, "SpawnDynamic", barrierPrefab, owner,
            new Vector3(1f, 1f, 0f), Quaternion.identity);

        GameObject ghost = new GameObject("ghost");
        _objects.Add(ghost);
        ghost.transform.position = new Vector3(0f, 0f, -1f);
        AIShadowRunner runner = manager.GetComponent<AIShadowRunner>();
        Assert.IsNotNull(runner);
        SetPrivateField(runner, "_ghost", ghost);
        SetPrivateField(runner, "_ghostForward",
            new Vector3(1f, 0f, 1f).normalized);
        SetPrivateField(runner, "_ghostLane", 1);

        Assert.IsTrue(manager.TryGetUpcomingObstacleInLane(
            ghost.transform.position, new Vector3(1f, 0f, 1f), 1,
            new HashSet<int>(), out float projectedDistance, out _, out _));
        Assert.Less(projectedDistance, 1.5f,
            "The setup must reproduce the old diagonal projection false positive.");

        InvokePrivate(runner, "EvaluateGhostObstacle");

        Assert.AreEqual(0, GetPrivateField<int>(runner, "_ghostMistakes"),
            "An obstacle projected across a corner must not count as a mistake.");
        Assert.AreEqual(0f, GetPrivateField<float>(runner, "_ghostStumbleTimer"));
    }

    [Test]
    public void OverlapFallbackFindsObstacleOnColliderParent()
    {
        GameObject obstacleRoot = new GameObject("PooledObstacleRoot");
        _objects.Add(obstacleRoot);
        obstacleRoot.AddComponent<Obstacle>().type = ObstacleType.Low;

        GameObject colliderChild = new GameObject("GameplayTrigger");
        _objects.Add(colliderChild);
        colliderChild.transform.SetParent(obstacleRoot.transform, false);
        BoxCollider trigger = colliderChild.AddComponent<BoxCollider>();
        trigger.isTrigger = true;

        Collider found = PlayerController.FindObstacleCollider(
            new Collider[] { null, trigger }, 2, null);

        Assert.AreSame(trigger, found,
            "An already-overlapping pooled obstacle must still be detected.");
        Assert.IsNull(PlayerController.FindObstacleCollider(
            new Collider[] { trigger }, 1, trigger),
            "The same obstacle contact must not be processed twice.");
    }

    [Test]
    public void ObstacleSweepIncludesLaneSwitchMovement()
    {
        Vector3 velocity = PlayerController.CalculatePlanarVelocity(
            Vector3.forward, 10f, Vector3.right, 0f, 3f, 20f, 0.02f,
            out float nextOffset);

        Assert.AreEqual(0.4f, nextOffset, 0.0001f);
        Assert.AreEqual(20f, velocity.x, 0.0001f,
            "The sweep must include lateral lane-switch movement.");
        Assert.AreEqual(10f, velocity.z, 0.0001f);
        Assert.Greater(velocity.magnitude, 10f,
            "Diagonal sweep distance must exceed forward-only distance.");
    }

    [Test]
    public void SlideDroneSpawnsInTheRequestedLane()
    {
        TrackManager manager = Create<TrackManager>("TrackManager");
        GameObject segment = new GameObject("StraightSegment");
        _objects.Add(segment);
        GameObject low = CreateObstaclePrefab("Low", ObstacleType.Low);
        GameObject high = CreateObstaclePrefab("High", ObstacleType.High);
        GameObject barrier = CreateObstaclePrefab("Barrier", ObstacleType.Barrier);
        manager.obstaclePrefabs = new[] { low, high, barrier };

        Assert.IsTrue((bool)InvokePrivate(
            manager, "SpawnObstacleAt", segment, 0, 5f, 0));
        Assert.AreEqual(1, segment.transform.childCount);
        Assert.AreEqual(-manager.laneDistance,
            segment.transform.GetChild(0).position.x, 0.0001f,
            "A lane-sized slide drone must remain in its requested lane.");
    }

    [Test]
    public void SlideDroneIsUpcomingOnlyInItsLane()
    {
        TrackManager manager = Create<TrackManager>("TrackManager");
        GameObject owner = new GameObject("Segment");
        _objects.Add(owner);
        GameObject low = CreateObstaclePrefab("SlideDrone", ObstacleType.Low);
        InvokePrivate(manager, "SpawnDynamic", low, owner,
            new Vector3(-manager.laneDistance, 1f, 4f), Quaternion.identity);

        for (int lane = 0; lane < 3; lane++)
        {
            Vector3 position = new Vector3((lane - 1) * manager.laneDistance,
                0f, 0f);
            bool found = manager.TryGetUpcomingObstacleInLane(
                position, Vector3.forward, lane, new HashSet<int>(),
                out _, out ObstacleType type, out _);
            Assert.AreEqual(lane == 0, found,
                "The slide drone lane query was wrong for lane " + lane + ".");
            if (found) Assert.AreEqual(ObstacleType.Low, type);
        }
    }

    [Test]
    public void ProceduralObstaclesKeepUnitRootScale()
    {
        TrackManager manager = Create<TrackManager>("TrackManager");
        var obstacles = (GameObject[])InvokePrivate(
            manager, "CreateProcObstacles");
        foreach (GameObject obstacle in obstacles) _objects.Add(obstacle);

        Assert.AreEqual(3, obstacles.Length);
        foreach (GameObject obstacle in obstacles)
            Assert.AreEqual(Vector3.one, obstacle.transform.localScale,
                "Runtime styling must not inherit a second obstacle scale.");

        BoxCollider lowCollider = obstacles[0].GetComponent<BoxCollider>();
        Assert.AreEqual(new Vector3(3.1f, 0.82f, 1.2f), lowCollider.size);
        Assert.AreEqual(new Vector3(0f, 0.95f, 0f), lowCollider.center);
    }

    private T Create<T>(string name) where T : Component
    {
        GameObject go = new GameObject(name);
        _objects.Add(go);
        return go.AddComponent<T>();
    }

    private GameObject CreateObstaclePrefab(string name, ObstacleType type)
    {
        GameObject prefab = new GameObject(name);
        prefab.AddComponent<Obstacle>().type = type;
        _objects.Add(prefab);
        return prefab;
    }

    private Transform CreateBone(string name, Transform parent)
    {
        GameObject bone = new GameObject(name);
        _objects.Add(bone);
        bone.transform.SetParent(parent, false);
        return bone.transform;
    }

    private static void SetPrivateField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Missing private field: " + name);
        field.SetValue(target, value);
    }

    private static T GetPrivateField<T>(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Missing private field: " + name);
        return (T)field.GetValue(target);
    }

    private static object InvokePrivate(object target, string name, params object[] args)
    {
        MethodInfo method = target.GetType().GetMethod(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Missing private method: " + name);
        return method.Invoke(target, args);
    }
}
