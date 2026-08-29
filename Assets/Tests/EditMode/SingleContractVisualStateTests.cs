using NUnit.Framework;
using UnityEngine;

public sealed class SingleContractVisualStateTests
{
    [Test]
    public void FourStatesUseDedicatedPlayerFacingColorSignals()
    {
        EchoPhaseVisualStyle calibration = EchoPhaseVisualController.StyleFor(
            SingleContractVisualState.Calibration);
        EchoPhaseVisualStyle challenge = EchoPhaseVisualController.StyleFor(
            SingleContractVisualState.Challenge);
        EchoPhaseVisualStyle relearn = EchoPhaseVisualController.StyleFor(
            SingleContractVisualState.RelearnPulse);
        EchoPhaseVisualStyle finale = EchoPhaseVisualController.StyleFor(
            SingleContractVisualState.Finale);

        Assert.Greater(calibration.tint.b, calibration.tint.r,
            "Calibration must remain blue.");
        Assert.Greater(challenge.tint.g, challenge.tint.r,
            "Challenge must retain its cyan component.");
        Assert.AreEqual(challenge.tint.g, challenge.tint.b, 0.08f,
            "Challenge must read as restrained cyan, not violet-blue neon.");
        Assert.Greater(relearn.tint.r, relearn.tint.g + 0.5f,
            "Relearn must be an unmistakable red pulse.");
        Assert.Greater(finale.tint.r, finale.tint.g);
        Assert.Greater(finale.tint.g, finale.tint.b,
            "Finale must remain gold-orange.");

        Assert.Greater(ColorDistance(calibration.tint, challenge.tint), 0.2f);
        Assert.Greater(ColorDistance(challenge.tint, relearn.tint), 0.5f);
        Assert.Greater(ColorDistance(relearn.tint, finale.tint), 0.3f);
    }

    [Test]
    public void ReducedMotionKeepsRelearnColorAndGlowWithoutMotionDependency()
    {
        EchoPhaseVisualStyle normal = EchoPhaseVisualController.StyleFor(
            SingleContractVisualState.RelearnPulse, false);
        EchoPhaseVisualStyle reduced = EchoPhaseVisualController.StyleFor(
            SingleContractVisualState.RelearnPulse, true);

        AssertColor(normal.tint, reduced.tint);
        Assert.Greater(reduced.tint.r, reduced.tint.g + 0.5f);
        Assert.GreaterOrEqual(reduced.intensity, 0.4f);
        Assert.GreaterOrEqual(reduced.coral, 0.5f);
        Assert.Greater(reduced.bloomBoost, 0f,
            "Reduced motion must keep a non-shake glow cue.");
        Assert.Greater(reduced.contrast, 0f);
        Assert.LessOrEqual(reduced.intensity, normal.intensity);
        Assert.LessOrEqual(reduced.bloomBoost, normal.bloomBoost);
    }

    [Test]
    public void PublicEntryAppliesSingleContractPaletteUntilExplicitRelease()
    {
        Color originalTint = Shader.GetGlobalColor("_EchoPhaseTint");
        float originalIntensity = Shader.GetGlobalFloat(
            "_EchoPhaseIntensity");
        float originalCoral = Shader.GetGlobalFloat("_EchoPhaseCoral");
        float originalBloom = Shader.GetGlobalFloat(
            "_EchoPhaseBloomBoost");
        float originalContrast = Shader.GetGlobalFloat(
            "_EchoPhaseContrast");
        GameObject owner = null;

        try
        {
            if (EchoPhaseVisualController.Instance != null)
                Object.DestroyImmediate(
                    EchoPhaseVisualController.Instance.gameObject);
            owner = new GameObject("SingleContractVisualState_Test");
            EchoPhaseVisualController controller =
                owner.AddComponent<EchoPhaseVisualController>();

            controller.ApplySingleContractVisualState(
                SingleContractVisualState.RelearnPulse, true);
            EchoPhaseVisualStyle expected = EchoPhaseVisualController.StyleFor(
                SingleContractVisualState.RelearnPulse,
                EchoRunAccessibility.ReducedMotion);

            Assert.IsTrue(controller.UsesSingleContractVisualState);
            Assert.AreEqual(SingleContractVisualState.RelearnPulse,
                controller.ActiveSingleContractVisualState);
            AssertStyle(expected, controller.TargetStyle);
            AssertColor(expected.tint,
                Shader.GetGlobalColor("_EchoPhaseTint"));
            Assert.AreEqual(expected.bloomBoost,
                Shader.GetGlobalFloat("_EchoPhaseBloomBoost"), 0.0001f);

            controller.ReleaseSingleContractVisualState();
            Assert.IsFalse(controller.UsesSingleContractVisualState);
        }
        finally
        {
            if (owner != null) Object.DestroyImmediate(owner);
            Shader.SetGlobalColor("_EchoPhaseTint", originalTint);
            Shader.SetGlobalFloat("_EchoPhaseIntensity", originalIntensity);
            Shader.SetGlobalFloat("_EchoPhaseCoral", originalCoral);
            Shader.SetGlobalFloat("_EchoPhaseBloomBoost", originalBloom);
            Shader.SetGlobalFloat("_EchoPhaseContrast", originalContrast);
        }
    }

    private static float ColorDistance(Color a, Color b)
    {
        return Vector3.Distance(new Vector3(a.r, a.g, a.b),
            new Vector3(b.r, b.g, b.b));
    }

    private static void AssertStyle(EchoPhaseVisualStyle expected,
        EchoPhaseVisualStyle actual)
    {
        AssertColor(expected.tint, actual.tint);
        Assert.AreEqual(expected.intensity, actual.intensity, 0.0001f);
        Assert.AreEqual(expected.coral, actual.coral, 0.0001f);
        Assert.AreEqual(expected.bloomBoost, actual.bloomBoost, 0.0001f);
        Assert.AreEqual(expected.contrast, actual.contrast, 0.0001f);
    }

    private static void AssertColor(Color expected, Color actual)
    {
        Assert.AreEqual(expected.r, actual.r, 0.0001f);
        Assert.AreEqual(expected.g, actual.g, 0.0001f);
        Assert.AreEqual(expected.b, actual.b, 0.0001f);
        Assert.AreEqual(expected.a, actual.a, 0.0001f);
    }
}
