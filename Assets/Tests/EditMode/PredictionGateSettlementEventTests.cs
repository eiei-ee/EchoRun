using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PredictionGateSettlementEventTests
{
    private GameObject _runnerObject;

    [TearDown]
    public void TearDown()
    {
        if (_runnerObject != null)
            Object.DestroyImmediate(_runnerObject);
    }

    [Test]
    public void ConsumedCounterSettlementIsPublishedExactlyOnce()
    {
        SingleContractFlow flow = CreateFlowWithCounterSuccess();
        Assert.AreEqual(1, flow.SettlementCount);
        Assert.IsTrue(flow.GetSettlement(0).IsCounterSuccess);

        _runnerObject = new GameObject("AIShadowRunner Settlement Test");
        _runnerObject.SetActive(false);
        AIShadowRunner runner = _runnerObject.AddComponent<AIShadowRunner>();
        SetPrivateField(runner, "_singleContractFlow", flow);
        int eventCount = 0;
        PredictionGateSettlement observed = default;
        runner.PredictionGateSettlementConsumed += settlement =>
        {
            eventCount++;
            observed = settlement;
        };

        InvokePrivate(runner, "ConsumeSingleContractSettlements");
        InvokePrivate(runner, "ConsumeSingleContractSettlements");
        SetPrivateField(runner, "_nextSingleContractSettlementIndex", 0);
        InvokePrivate(runner, "ConsumeSingleContractSettlements");

        Assert.AreEqual(1, eventCount,
            "The runner and flow consumption guards must prevent duplicate publication.");
        Assert.AreEqual(flow.GetSettlement(0).gateId, observed.gateId);
        Assert.AreEqual(PredictionGateRole.Counter, observed.chosenRole);
        Assert.AreEqual(GateExecutionOutcome.Success, observed.execution);
        Assert.IsTrue(observed.IsCounterSuccess,
            "The event must preserve the authoritative settlement semantics.");
    }

    private static SingleContractFlow CreateFlowWithCounterSuccess()
    {
        var flow = new SingleContractFlow(
            new SingleContractFixedGateWindowFactory(CreateWindows()),
            2, 1f);
        flow.BeginRun(new EchoRunContext
        {
            mode = GameplayFlowMode.SingleContract,
            hasOpponent = true,
            courseDistance = 950f,
            runSequence = 7,
            runSeed = 424242,
            generation = 3
        });

        PredictionGateDefinition definition = flow.GetGate(0).Definition;
        int counterLane = FindLaneForRole(
            definition, PredictionGateRole.Counter);
        flow.Tick(new EchoRunFrame
        {
            elapsedTime = 12f,
            playerDistance = definition.commitDistance,
            currentSpeed = 20f,
            playerLane = counterLane
        });
        Assert.AreEqual(PredictionGateLifecycle.ChoiceCommitted,
            flow.GetGate(0).State);
        GateTransitionResult result = flow.ResolveObstaclePassed(
            new GateObstacleEvent
            {
                gateId = definition.gateId,
                obstacleId = definition.gateId * 10,
                physicalLane = counterLane
            });
        Assert.AreEqual(GateTransitionResult.Applied, result);
        return flow;
    }

    private static PredictionGateDistanceWindow[] CreateWindows()
    {
        var windows = new PredictionGateDistanceWindow[6];
        for (int i = 0; i < windows.Length; i++)
        {
            float start = 100f * (i + 1);
            windows[i] = new PredictionGateDistanceWindow
            {
                presentationDistance = start,
                commitDistance = start + 10f,
                resolveDistance = start + 20f,
                exitDistance = start + 30f
            };
        }
        return windows;
    }

    private static int FindLaneForRole(PredictionGateDefinition definition,
        PredictionGateRole role)
    {
        for (int i = 0; i < definition.lanes.Length; i++)
            if (definition.lanes[i].role == role)
                return definition.lanes[i].physicalLane;
        Assert.Fail("Requested role was not present in the gate.");
        return -1;
    }

    private static void SetPrivateField(object target, string name,
        object value)
    {
        FieldInfo field = target.GetType().GetField(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Missing private field: " + name);
        field.SetValue(target, value);
    }

    private static void InvokePrivate(object target, string name)
    {
        MethodInfo method = target.GetType().GetMethod(
            name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, "Missing private method: " + name);
        method.Invoke(target, null);
    }
}
