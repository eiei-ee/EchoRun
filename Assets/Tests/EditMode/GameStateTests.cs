using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameStateTests
{
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

        manager.StartGame();

        Assert.AreEqual(GameState.Playing, manager.State);
        Assert.AreEqual(manager.startSpeed, manager.CurrentSpeed);
        Assert.AreEqual(0, manager.Score);
        Assert.AreEqual(0, manager.Coins);
        Assert.AreEqual(0f, manager.Distance);
        Assert.IsNull(manager.BuffName);
        Assert.AreEqual(0f, manager.BuffTimeRemaining);
        Assert.AreEqual(1f, Time.timeScale);
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

    private T Create<T>(string name) where T : Component
    {
        GameObject go = new GameObject(name);
        _objects.Add(go);
        return go.AddComponent<T>();
    }
}
