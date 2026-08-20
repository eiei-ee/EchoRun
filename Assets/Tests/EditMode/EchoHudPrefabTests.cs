using NUnit.Framework;
using UnityEngine;

public class EchoHudPrefabTests
{
    private GameObject _instance;

    [TearDown]
    public void TearDown()
    {
        if (_instance != null) Object.DestroyImmediate(_instance);
    }

    [Test]
    public void ResourcePrefabUsesTwoCanvasLayersAndExplicitView()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/EchoHud");
        Assert.NotNull(prefab);
        _instance = Object.Instantiate(prefab);

        Assert.NotNull(_instance.GetComponent<EchoHudView>());
        Assert.NotNull(_instance.GetComponent<EchoHudPresenter>());
        Assert.NotNull(_instance.transform.Find("HudStaticCanvas"));
        Assert.NotNull(_instance.transform.Find("HudDynamicCanvas"));
        Assert.AreEqual(2, _instance.GetComponentsInChildren<Canvas>(true).Length);
    }

    [Test]
    public void ViewSwitchesBetweenCalibrationAndDuelWithoutAddingPanels()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/EchoHud");
        _instance = Object.Instantiate(prefab);
        EchoHudView view = _instance.GetComponent<EchoHudView>();

        EchoHudViewData calibration = EchoRunPresentation.BuildHud(false,
            null, 0f, 2, 2, 1, 2, 0.5f,
            EchoDuelPhase.Calibration, 0f);
        view.Present(calibration, true);
        Assert.IsFalse(Find("HudStaticCanvas/StageRail").activeSelf);
        Assert.IsTrue(Find("HudStaticCanvas/CalibrationRail").activeSelf);
        Assert.IsFalse(Find("HudStaticCanvas/LeadGroup").activeSelf);

        EchoContractData contract = new EchoContractData
        {
            type = EchoContractType.BreakLaneHabit,
            targetLane = 0,
            predictionLane = 2,
            targetProgress = 3,
            progress = 1,
            duelPhase = EchoDuelPhase.Resistance
        };
        EchoHudViewData duel = EchoRunPresentation.BuildHud(true, contract,
            2.4f, 2, 2, 2, 2, 1f, EchoDuelPhase.Resistance, 0.4f);
        view.Present(duel, false);

        Assert.IsTrue(Find("HudStaticCanvas/StageRail").activeSelf);
        Assert.IsFalse(Find("HudStaticCanvas/CalibrationRail").activeSelf);
        Assert.IsTrue(Find("HudStaticCanvas/LeadGroup").activeSelf);
        Assert.IsTrue(Find("HudDynamicCanvas/MeterGroup").activeSelf);
        Assert.IsTrue(Find("HudDynamicCanvas/Prediction").activeSelf);
    }

    private GameObject Find(string path)
    {
        Transform target = _instance.transform.Find(path);
        Assert.NotNull(target, path);
        return target.gameObject;
    }
}
