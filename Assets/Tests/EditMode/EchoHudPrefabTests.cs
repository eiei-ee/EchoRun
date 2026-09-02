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

    [Test]
    public void PhaseMeterStaysAboveTheRunnerFocusZone()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/EchoHud");
        _instance = Object.Instantiate(prefab);

        RectTransform meter = Find("HudDynamicCanvas/MeterGroup")
            .GetComponent<RectTransform>();
        Assert.NotNull(meter);
        Assert.GreaterOrEqual(meter.anchorMin.y, 0.82f,
            "The phase meter must stay above the runner and obstacle sightline.");
        Assert.AreEqual(meter.anchorMin, meter.anchorMax);
    }

    [Test]
    public void SingleContractCalibrationUsesExistingHudForLiveLearningProgress()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/EchoHud");
        _instance = Object.Instantiate(prefab);
        EchoHudView view = _instance.GetComponent<EchoHudView>();
        SingleContractHudData data = EchoRunPresentation
            .BuildSingleContractHud(new SingleContractHudInput
            {
                visualState = SingleContractVisualState.Calibration,
                injuries = 1,
                calibrationProgress = new SingleContractCalibrationProgress
                {
                    available = true,
                    totalSamples = 12,
                    minimumTotalSamples = 24,
                    activeSamples = 4,
                    minimumActiveSamples = 6,
                    actionCategories = 1,
                    minimumActionCategories = 2,
                    jumpSamples = 1,
                    minimumJumpSamples = 2,
                    slideSamples = 1,
                    minimumSlideSamples = 2,
                    formalChoices = 2,
                    minimumFormalChoices = 5,
                    successfulChoices = 1,
                    minimumSuccessfulChoices = 3,
                    preferredLane = 2,
                    preferredLaneUnique = true,
                    strongestRouteChoices = 2,
                    minimumStrongestRouteChoices = 3,
                    preferredLaneConfidence = 1f
                }
            });

        view.PresentSingleContract(data, false);

        Assert.IsTrue(Find("HudDynamicCanvas/MeterGroup").activeSelf);
        Assert.AreEqual("学习 33%",
            Find("HudDynamicCanvas/MeterGroup/MeterLabel")
                .GetComponent<UnityEngine.UI.Text>().text);
        Assert.AreEqual(data.memory,
            Find("HudDynamicCanvas/Directive")
                .GetComponent<UnityEngine.UI.Text>().text);
        UnityEngine.UI.Text route = Find("HudDynamicCanvas/Prediction")
            .GetComponent<UnityEngine.UI.Text>();
        Assert.AreEqual(data.calibrationRouteProgress, route.text);
        Assert.AreEqual(EchoRunUITheme
                .HudSkinFor(SingleContractVisualState.Calibration).accent,
            route.color,
            "Learning progress must not reuse the prediction danger red.");
        Canvas.ForceUpdateCanvases();
        Assert.LessOrEqual(route.preferredHeight,
            route.rectTransform.rect.height + 0.5f,
            "The compact route line must fit the existing HUD plate.");
        Assert.AreEqual(data.calibrationActionProgress,
            Find("HudStaticCanvas/CalibrationRail/CalibrationObservation")
                .GetComponent<UnityEngine.UI.Text>().text);
    }

    [Test]
    public void ReadyCalibrationMeterLabelFitsWithoutWrapping()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/EchoHud");
        _instance = Object.Instantiate(prefab);
        EchoHudView view = _instance.GetComponent<EchoHudView>();
        SingleContractHudData data = EchoRunPresentation
            .BuildSingleContractHud(new SingleContractHudInput
            {
                visualState = SingleContractVisualState.Calibration,
                calibrationProgress = new SingleContractCalibrationProgress
                {
                    available = true,
                    evidenceReady = true,
                    totalSamples = 24,
                    minimumTotalSamples = 24,
                    activeSamples = 6,
                    minimumActiveSamples = 6,
                    actionCategories = 2,
                    minimumActionCategories = 2,
                    jumpSamples = 2,
                    minimumJumpSamples = 2,
                    slideSamples = 2,
                    minimumSlideSamples = 2,
                    formalChoices = 5,
                    minimumFormalChoices = 5,
                    successfulChoices = 3,
                    minimumSuccessfulChoices = 3,
                    preferredLane = 1,
                    preferredLaneUnique = true,
                    strongestRouteChoices = 3,
                    minimumStrongestRouteChoices = 3,
                    preferredLaneConfidence = 0.6f
                }
            });

        view.PresentSingleContract(data, false);
        Canvas.ForceUpdateCanvases();

        UnityEngine.UI.Text label = Find(
                "HudDynamicCanvas/MeterGroup/MeterLabel")
            .GetComponent<UnityEngine.UI.Text>();
        Assert.AreEqual("学够了 · 去终点", label.text);
        Assert.LessOrEqual(label.preferredWidth,
            label.rectTransform.rect.width + 0.5f,
            "The ready state must stay on one line at the reference size.");
        Assert.LessOrEqual(label.preferredHeight,
            label.rectTransform.rect.height + 0.5f,
            "The ready state must not be vertically clipped.");
    }

    [Test]
    public void OpeningReplayTitleAndDetailFitAtNormalAndLargeTextSizes()
    {
        bool oldLargeText = EchoRunAccessibility.LargeText;
        try
        {
            GameObject prefab = Resources.Load<GameObject>("UI/EchoHud");
            _instance = Object.Instantiate(prefab);
            EchoHudView view = _instance.GetComponent<EchoHudView>();
            SingleContractHudData data = EchoRunPresentation
                .BuildSingleContractHud(new SingleContractHudInput
                {
                    visualState = SingleContractVisualState.Challenge,
                    openingMemory = true,
                    openingReplay = true,
                    openingReplayAction = ShadowAction.Slide,
                    openingReplayCount = 4095,
                    generation = 9999,
                    memory = "压力出现时，你偏向右侧"
                });

            foreach (bool largeText in new[] { false, true })
            {
                EchoRunAccessibility.SetLargeText(largeText);
                view.PresentSingleContract(data, true);
                EchoRunAccessibility.ApplyToHierarchy(_instance.transform);
                Canvas.ForceUpdateCanvases();

                AssertTextFits("HudDynamicCanvas/Announcement", largeText);
                AssertTextFits("HudDynamicCanvas/Directive", largeText);
            }
        }
        finally
        {
            EchoRunAccessibility.SetLargeText(oldLargeText);
        }
    }

    [Test]
    public void PlayerLanguageFitsAtNormalAndLargeTextSizes()
    {
        bool oldLargeText = EchoRunAccessibility.LargeText;
        try
        {
            GameObject prefab = Resources.Load<GameObject>("UI/EchoHud");
            _instance = Object.Instantiate(prefab);
            EchoHudView view = _instance.GetComponent<EchoHudView>();
            SingleContractHudData calibration = EchoRunPresentation
                .BuildSingleContractHud(new SingleContractHudInput
                {
                    visualState = SingleContractVisualState.Calibration,
                    injuries = 9,
                    calibrationProgress =
                        new SingleContractCalibrationProgress
                        {
                            available = true,
                            evidenceReady = true,
                            totalSamples = 24,
                            minimumTotalSamples = 24,
                            activeSamples = 6,
                            minimumActiveSamples = 6,
                            actionCategories = 2,
                            minimumActionCategories = 2,
                            jumpSamples = 2,
                            minimumJumpSamples = 2,
                            slideSamples = 2,
                            minimumSlideSamples = 2,
                            formalChoices = 5,
                            minimumFormalChoices = 5,
                            successfulChoices = 3,
                            minimumSuccessfulChoices = 3,
                            preferredLane = 2,
                            preferredLaneUnique = true,
                            strongestRouteChoices = 3,
                            minimumStrongestRouteChoices = 3,
                            preferredLaneConfidence = 0.6f
                        }
                });
            SingleContractHudData challenge = EchoRunPresentation
                .BuildSingleContractHud(new SingleContractHudInput
                {
                    visualState = SingleContractVisualState.Challenge,
                    generation = 9999,
                    memory = "压力出现时，你偏向右路",
                    showPrediction = true,
                    predictedLane = 2,
                    predictionGateNumber = 6,
                    predictionGateCount = 6,
                    instantFeedback =
                        SingleContractInstantFeedback.CounterFailed,
                    feedbackLeadDeltaMeters = -99.9f
                });

            foreach (bool largeText in new[] { false, true })
            {
                EchoRunAccessibility.SetLargeText(largeText);

                view.PresentSingleContract(calibration, true);
                EchoRunAccessibility.ApplyToHierarchy(_instance.transform);
                Canvas.ForceUpdateCanvases();
                AssertTextFits("HudDynamicCanvas/Announcement", largeText);
                AssertTextFits("HudDynamicCanvas/Directive", largeText);
                AssertTextFits("HudDynamicCanvas/Prediction", largeText);
                AssertTextFits(
                    "HudStaticCanvas/CalibrationRail/CalibrationObservation",
                    largeText);
                AssertTextFits(
                    "HudDynamicCanvas/MeterGroup/MeterLabel", largeText);

                view.PresentSingleContract(challenge, true);
                view.ShowFeedback(challenge.instantFeedback, Color.white,
                    true);
                EchoRunAccessibility.ApplyToHierarchy(_instance.transform);
                Canvas.ForceUpdateCanvases();
                AssertTextFits("HudDynamicCanvas/Announcement", largeText);
                AssertTextFits("HudDynamicCanvas/Prediction", largeText);
                AssertTextFits("HudDynamicCanvas/Feedback", largeText);
            }
        }
        finally
        {
            EchoRunAccessibility.SetLargeText(oldLargeText);
        }
    }

    [Test]
    public void DynamicCopyStaysAtTheLeftEdgeOutsideTheRunnerSightline()
    {
        GameObject prefab = Resources.Load<GameObject>("UI/EchoHud");
        _instance = Object.Instantiate(prefab);

        string[] paths =
        {
            "HudDynamicCanvas/Announcement",
            "HudDynamicCanvas/Directive",
            "HudDynamicCanvas/Prediction",
            "HudDynamicCanvas/Feedback"
        };
        for (int i = 0; i < paths.Length; i++)
        {
            RectTransform rect = Find(paths[i]).GetComponent<RectTransform>();
            Assert.LessOrEqual(rect.anchorMax.x, 0.05f, paths[i]);
            Assert.AreEqual(0f, rect.pivot.x, 0.0001f, paths[i]);
        }
    }

    private GameObject Find(string path)
    {
        Transform target = _instance.transform.Find(path);
        Assert.NotNull(target, path);
        return target.gameObject;
    }

    private void AssertTextFits(string path, bool largeText)
    {
        UnityEngine.UI.Text text = Find(path)
            .GetComponent<UnityEngine.UI.Text>();
        Assert.IsTrue(text.gameObject.activeSelf, path);
        Assert.LessOrEqual(text.preferredWidth,
            text.rectTransform.rect.width + 0.5f,
            path + " must not wrap at largeText=" + largeText + ".");
        Assert.LessOrEqual(text.preferredHeight,
            text.rectTransform.rect.height + 0.5f,
            path + " must not clip at largeText=" + largeText + ".");
    }
}
