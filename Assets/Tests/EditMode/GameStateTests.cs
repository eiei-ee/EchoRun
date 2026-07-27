using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
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
    public void TelemetryRoundTripPreservesDecisionInputsAndReward()
    {
        float[] shadowWeights = { 0.1f, 0.2f };
        float[] directorWeights = { 0.3f, 0.4f, 0.5f };
        AIRunTelemetry.BeginRun(
            77, 12, 7904, 22, 48, shadowWeights, directorWeights);
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

        int decisionId = AIRunTelemetry.RecordDirectorDecision(context, plan);
        AIRunTelemetry.RecordDirectorOutcome(decisionId, 0.65f, 49);
        AIRunTelemetry.RecordShadowSample(
            ShadowAction.Jump, 1,
            new[] { 1f, 0f, 0.3f, 0.8f, 0f, 0.66f, 0f, 0f },
            false, 0.72f);

        AIRunTelemetryData restored = AIRunTelemetry.FromJson(
            AIRunTelemetry.GetLatestRunJson());

        Assert.AreEqual(AIRunTelemetry.SchemaVersion, restored.schemaVersion);
        Assert.AreEqual(77, restored.seed);
        Assert.AreEqual("0000004D-000012", restored.runId);
        CollectionAssert.AreEqual(
            shadowWeights, restored.shadowWeightsAtStart);
        CollectionAssert.AreEqual(
            directorWeights, restored.directorWeightsAtStart);
        Assert.AreEqual(1, restored.directorDecisions.Count);
        Assert.IsTrue(restored.directorDecisions[0].trained);
        Assert.AreEqual(0.65f, restored.directorDecisions[0].reward, 0.0001f);
        CollectionAssert.AreEqual(context,
            restored.directorDecisions[0].context);
        Assert.AreEqual(1, restored.shadowSamples.Count);
        Assert.AreEqual((int)ShadowAction.Jump,
            restored.shadowSamples[0].action);
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

        AIShadowRunner runner = manager.GetComponent<AIShadowRunner>();
        Assert.IsNotNull(runner);
        SetPrivateField(runner, "_player", player);
        SetPrivateField(runner, "_ghost", ghost);
        SetPrivateField(runner, "_ghostVisual", visual.transform);
        SetPrivateField(runner, "_ghostVisualScale", Vector3.one);
        SetPrivateField(runner, "_ghostVisualPosition", Vector3.zero);
        SetPrivateField(runner, "_ghostGroundY", 0f);

        InvokePrivate(runner, "ApplyObstacleReaction");
        float startedTimer = GetPrivateField<float>(runner, "_ghostSlideTimer");
        Assert.Greater(startedTimer, 0f,
            "A low obstacle in the shadow lane must start a slide.");

        SetPrivateField(runner, "_ghostSlideTimer", startedTimer - 0.12f);
        InvokePrivate(runner, "UpdateGhostPose");
        Assert.Less(visual.transform.localScale.y, 0.7f,
            "The reaction must be visible as a crouched shadow pose.");

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

        GameObject otherLanePrefab = CreateObstaclePrefab("OtherLane", ObstacleType.Low);
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

    [Test]
    public void TurnCoverageAlwaysProvidesConnectedEntryCornerAndExit()
    {
        TrackManager manager = Create<TrackManager>("TrackManager");
        GameObject turn = new GameObject("TurnSegment");
        _objects.Add(turn);
        MethodInfo ensureCoverage = typeof(TrackManager).GetMethod(
            "EnsureTurnCoverage", BindingFlags.Instance | BindingFlags.NonPublic);

        ensureCoverage.Invoke(manager, new object[] { turn, 1 });

        Transform coverage = turn.transform.Find("RuntimeTurnCoverage");
        Assert.IsNotNull(coverage);
        Transform entry = coverage.Find("EntryCoverage");
        Transform corner = coverage.Find("CornerCoverage");
        Transform exit = coverage.Find("ExitCoverage");
        Assert.IsNotNull(entry);
        Assert.IsNotNull(corner);
        Assert.IsNotNull(exit);
        Assert.IsNotNull(entry.GetComponent<BoxCollider>());
        Assert.IsNotNull(corner.GetComponent<BoxCollider>());
        Assert.IsNotNull(exit.GetComponent<BoxCollider>());
        Assert.AreEqual(0f, entry.localPosition.x, 0.001f);
        Assert.AreEqual(manager.segmentLength * 0.5f,
            exit.localPosition.x, 0.001f);
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
