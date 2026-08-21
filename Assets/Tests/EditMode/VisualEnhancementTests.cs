using NUnit.Framework;
using UnityEngine;

public class VisualEnhancementTests
{
    private GameObject _roadObject;
    private VisualQuality _originalQuality;

    [SetUp]
    public void SetUp()
    {
        _originalQuality = VisualQualityController.Current;
    }

    [TearDown]
    public void TearDown()
    {
        VisualQualityController.SetQuality(_originalQuality);
        if (_roadObject != null) Object.DestroyImmediate(_roadObject);
    }

    [Test]
    public void HighFxIsWindowsOnlyAndReflectionBudgetIsFrozen()
    {
        Assert.IsTrue(PostFxController.SupportsHighFx(
            RuntimePlatform.WindowsPlayer));
        Assert.IsTrue(PostFxController.SupportsHighFx(
            RuntimePlatform.WindowsEditor));
        Assert.IsFalse(PostFxController.SupportsHighFx(
            RuntimePlatform.Android));
        Assert.IsFalse(PostFxController.SupportsHighFx(
            RuntimePlatform.WebGLPlayer));
        Assert.AreEqual(256, PostFxController.PlanarReflectionResolution);
        Assert.That(PostFxController.PlanarReflectionUpdateRate,
            Is.InRange(15f, 30f));
    }

    [Test]
    public void PhaseStylesIncreaseCoralAndFinaleEmphasisWithoutFogData()
    {
        EchoPhaseVisualStyle detection = EchoPhaseVisualController.StyleFor(
            EchoDuelPhase.Detection);
        EchoPhaseVisualStyle counter = EchoPhaseVisualController.StyleFor(
            EchoDuelPhase.Counterattack);
        EchoPhaseVisualStyle finale = EchoPhaseVisualController.StyleFor(
            EchoDuelPhase.Finale);

        Assert.Greater(counter.coral, detection.coral);
        Assert.Greater(finale.coral, counter.coral);
        Assert.Greater(finale.bloomBoost, detection.bloomBoost);
        Assert.Greater(finale.contrast, detection.contrast);
        Assert.LessOrEqual(finale.intensity, 0.55f);
    }

    [Test]
    public void SixDuelPhasesUseVisiblyDistinctColorSignals()
    {
        EchoDuelPhase[] phases =
        {
            EchoDuelPhase.Detection,
            EchoDuelPhase.Reveal,
            EchoDuelPhase.Resistance,
            EchoDuelPhase.Counterattack,
            EchoDuelPhase.Rewrite,
            EchoDuelPhase.Finale
        };
        for (int i = 0; i < phases.Length; i++)
        {
            EchoPhaseVisualStyle style = EchoPhaseVisualController.StyleFor(phases[i]);
            Assert.GreaterOrEqual(style.intensity, 0.22f, phases[i].ToString());
            for (int j = 0; j < i; j++)
            {
                Color previous = EchoPhaseVisualController.StyleFor(phases[j]).tint;
                Assert.Greater(Vector3.Distance(
                    new Vector3(style.tint.r, style.tint.g, style.tint.b),
                    new Vector3(previous.r, previous.g, previous.b)), 0.18f,
                    phases[j] + " and " + phases[i]);
            }
        }
    }

    [Test]
    public void RoadPlanarReflectionRemainsOptionalAndLowForcesItOff()
    {
        _roadObject = new GameObject("EchoRoadVisualController_EnhancementTest");
        EchoRoadVisualController road =
            _roadObject.AddComponent<EchoRoadVisualController>();
        Material material = road.SharedRoadMaterial;

        VisualQualityController.SetQuality(VisualQuality.High);
        road.ApplyPlanarReflection(true);
        Assert.IsTrue(material.IsKeywordEnabled("_ECHO_PLANAR_REFLECTION"));

        road.ApplyQuality(VisualQuality.Low);
        Assert.IsFalse(material.IsKeywordEnabled("_ECHO_PLANAR_REFLECTION"));
    }
}
