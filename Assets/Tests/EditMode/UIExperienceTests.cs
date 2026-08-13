using NUnit.Framework;
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class UIExperienceTests
{
    [Test]
    public void MenuPresentationSeparatesCalibrationFromChallenge()
    {
        EchoMenuViewData calibration = EchoRunPresentation.BuildMenu(
            0, new PlayerStyleData(), 2, 3);

        Assert.AreEqual("首次回声校准", calibration.generation);
        StringAssert.Contains("跳跃 2 次", calibration.objective);
        StringAssert.Contains("滑铲 3 次", calibration.objective);
        Assert.AreEqual("开始校准", calibration.primaryAction);

        EchoDuelViewData calibrationHud = EchoRunPresentation.BuildDuel(
            false, null, 0f, 2, 3, 1, 2, 0.4f);
        Assert.AreEqual("跳跃 1/2 · 滑铲 2/3", calibrationHud.progress);
        Assert.AreEqual(0.4f, calibrationHud.progress01, 0.001f);

        EchoMenuViewData challenge = EchoRunPresentation.BuildMenu(
            4, new PlayerStyleData(), 2, 3);
        Assert.AreEqual("第 4 代回声", challenge.generation);
        Assert.AreEqual("挑战第 4 代回声", challenge.primaryAction);
        Assert.IsNotEmpty(challenge.learned);
        StringAssert.DoesNotContain("AI识别：", challenge.learned);
        StringAssert.DoesNotContain("权重", challenge.rule);
        StringAssert.DoesNotContain("置信", challenge.rule);
    }

    [Test]
    public void DuelPresentationMakesContractProgressAndLeadExplicit()
    {
        var contract = new EchoContractData
        {
            type = EchoContractType.ChangeVerticalHabit,
            targetAction = ShadowAction.Slide,
            progress = 2f,
            targetProgress = 3f,
            lastFeedback = "反制生效：动作正确"
        };

        EchoDuelViewData leading = EchoRunPresentation.BuildDuel(
            true, contract, 2.75f, 2, 2);
        Assert.AreEqual("滑铲躲避", leading.contract);
        Assert.AreEqual("2 / 3", leading.progress);
        Assert.AreEqual(2f / 3f, leading.progress01, 0.001f);
        Assert.AreEqual(EchoLeadState.Leading, leading.leadState);
        StringAssert.StartsWith("领先 +2.8m", leading.lead);
        StringAssert.StartsWith("反制成功", leading.feedback);

        EchoDuelViewData trailing = EchoRunPresentation.BuildDuel(
            true, contract, -1.2f, 2, 2);
        Assert.AreEqual(EchoLeadState.Trailing, trailing.leadState);
        StringAssert.StartsWith("落后 -1.2m", trailing.lead);
    }

    [Test]
    public void MenuRouterKeepsExactlyOneScreenAndHomeNavigationState()
    {
        GameObject root = new GameObject("MenuRouterTest");
        GameObject home = new GameObject("Home");
        GameObject settings = new GameObject("Settings");
        GameObject launcher = new GameObject("Launcher");
        home.transform.SetParent(root.transform);
        settings.transform.SetParent(root.transform);
        launcher.transform.SetParent(root.transform);

        try
        {
            MenuScreenRouter router = root.AddComponent<MenuScreenRouter>();
            router.Initialize(null);
            router.Register(MenuScreen.Home, home);
            router.Register(MenuScreen.Settings, settings);
            router.RegisterHomeNavigation(launcher);
            router.EnterMenu();

            Assert.IsTrue(home.activeSelf);
            Assert.IsFalse(settings.activeSelf);
            Assert.IsTrue(launcher.activeSelf);

            Assert.IsTrue(router.Show(MenuScreen.Settings));
            Assert.IsFalse(home.activeSelf);
            Assert.IsTrue(settings.activeSelf);
            Assert.IsFalse(launcher.activeSelf);

            router.BackToHome();
            Assert.IsTrue(home.activeSelf);
            Assert.IsFalse(settings.activeSelf);
            Assert.IsTrue(launcher.activeSelf);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RunnerAppearanceUsesPropertyBlocksWithoutCloningMaterial()
    {
        Shader shader = Shader.Find("EchoRun/ExoGrayBlueTech");
        Assert.IsNotNull(shader);
        GameObject runner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Material shared = new Material(shader);
        Renderer renderer = runner.GetComponent<Renderer>();
        renderer.sharedMaterial = shared;

        try
        {
            Color dark = new Color(0.02f, 0.03f, 0.04f);
            Color light = new Color(0.4f, 0.5f, 0.6f);
            Color emission = new Color(0.1f, 0.8f, 1.2f);
            Assert.AreEqual(1, RunnerAppearanceService.Apply(
                runner.transform, dark, light, emission));
            Assert.AreSame(shared, renderer.sharedMaterial);

            var properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties, 0);
            AssertColor(dark, properties.GetColor("_DarkColor"));
            AssertColor(light, properties.GetColor("_LightColor"));
            AssertColor(emission, properties.GetColor("_EmissionColor"));
        }
        finally
        {
            Object.DestroyImmediate(runner);
            Object.DestroyImmediate(shared);
        }
    }

    [Test]
    public void PlayerFacingFontCoversNewInterfaceCopy()
    {
        Font font = Resources.Load<Font>("Fonts/EchoRunSansSC-Regular");
        Assert.IsNotNull(font);
        const string copy =
            "首次回声校准挑战契约补给舱跑者外观库存装备领先落后已破解"
            + "设置音乐音量音效画面帧率辅助显示选择配色立即预览并保存"
            + "大字高对比减少动态返回百分比补充";
        foreach (char character in copy)
            Assert.IsTrue(font.HasCharacter(character), "UI font is missing: " + character);
    }

    [Test]
    public void BundledFontMatchesValidatedStaticRegularSubset()
    {
        string path = Path.Combine(Application.dataPath,
            "Resources/Fonts/EchoRunSansSC-Regular.otf");
        Assert.IsTrue(File.Exists(path));
        using (SHA256 sha = SHA256.Create())
        {
            string actual = BitConverter.ToString(
                sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
            Assert.AreEqual(
                "2BE407DC8955F124A6636C7C3DBFD25AB543DFEA340AD200C6109983F8094CD3",
                actual,
                "The bundled font must stay the validated static Regular 400 subset.");
        }
    }

    [Test]
    public void AccessibilityPreferencesApplyToRuntimeTextAndPersistMotionChoice()
    {
        bool oldLarge = EchoRunAccessibility.LargeText;
        bool oldContrast = EchoRunAccessibility.HighContrast;
        bool oldMotion = EchoRunAccessibility.ReducedMotion;
        GameObject textObject = new GameObject("AccessibleText", typeof(UnityEngine.UI.Text));

        try
        {
            UnityEngine.UI.Text text = textObject.GetComponent<UnityEngine.UI.Text>();
            text.fontSize = 20;
            EchoRunAccessibility.SetLargeText(false);
            EchoRunAccessibility.SetHighContrast(false);
            EchoRunAccessibility.Prepare(text);
            Assert.AreEqual(20, text.fontSize);

            EchoRunAccessibility.SetLargeText(true);
            EchoRunAccessibility.SetHighContrast(true);
            EchoRunAccessibility.SetReducedMotion(true);
            EchoRunAccessibility.ApplyToHierarchy(textObject.transform);

            Assert.AreEqual(22, text.fontSize);
            EchoRunAccessibleText marker = text.GetComponent<EchoRunAccessibleText>();
            Assert.IsNotNull(marker);
            Assert.IsNotNull(marker.contrastOutline);
            Assert.IsTrue(marker.contrastOutline.enabled);
            Assert.IsTrue(EchoRunAccessibility.ReducedMotion);
        }
        finally
        {
            EchoRunAccessibility.SetLargeText(oldLarge);
            EchoRunAccessibility.SetHighContrast(oldContrast);
            EchoRunAccessibility.SetReducedMotion(oldMotion);
            Object.DestroyImmediate(textObject);
        }
    }

    [Test]
    public void TouchTargetsAndCameraAdaptToOrientation()
    {
        Assert.AreEqual(104f, UILayoutRules.EnsureTouchButtonSize(
            new Vector2(180f, 56f), true, false).y);
        Assert.AreEqual(72f, UILayoutRules.EnsureTouchSliderSize(
            new Vector2(500f, 40f), true, false).y);
        Assert.AreEqual(56f, UILayoutRules.EnsureTouchButtonSize(
            new Vector2(180f, 56f), false, false).y);
        Assert.AreEqual(62f, WorldStyler.GetCameraFieldOfView(true));
        Assert.AreEqual(52f, WorldStyler.GetCameraFieldOfView(false));
        Assert.AreEqual(new Vector3(0f, 4.35f, -8f),
            WorldStyler.GetCameraOffset(true));
        Assert.AreEqual(new Vector3(0f, 3.8f, -6.75f),
            WorldStyler.GetCameraOffset(false));
    }

    private static void AssertColor(Color expected, Color actual)
    {
        Assert.AreEqual(expected.r, actual.r, 0.001f);
        Assert.AreEqual(expected.g, actual.g, 0.001f);
        Assert.AreEqual(expected.b, actual.b, 0.001f);
        Assert.AreEqual(expected.a, actual.a, 0.001f);
    }
}
