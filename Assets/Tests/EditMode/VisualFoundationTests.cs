using NUnit.Framework;
using UnityEngine;

public class VisualFoundationTests
{
    private GameObject _controllerObject;

    [TearDown]
    public void TearDown()
    {
        if (_controllerObject != null)
            Object.DestroyImmediate(_controllerObject);
    }

    [Test]
    public void PlatformDefaultsKeepAndroidLowAndWindowsHigh()
    {
        Assert.AreEqual(VisualQuality.Low,
            VisualQualityController.DefaultForPlatform(RuntimePlatform.Android));
        Assert.AreEqual(VisualQuality.Low,
            VisualQualityController.DefaultForPlatform(RuntimePlatform.WebGLPlayer));
        Assert.AreEqual(VisualQuality.High,
            VisualQualityController.DefaultForPlatform(RuntimePlatform.WindowsPlayer));
        Assert.AreEqual(VisualQuality.High,
            VisualQualityController.DefaultForPlatform(RuntimePlatform.WindowsEditor));
    }

    [Test]
    public void ReapplyingQualityDoesNotBroadcastDuplicateChange()
    {
        VisualQuality original = VisualQualityController.Current;
        VisualQuality target = original == VisualQuality.Low
            ? VisualQuality.High
            : VisualQuality.Low;
        int changeCount = 0;
        System.Action<VisualQuality> listener = _ => changeCount++;
        VisualQualityController.Changed += listener;
        try
        {
            VisualQualityController.SetQuality(target);
            VisualQualityController.SetQuality(target);
            Assert.AreEqual(1, changeCount);
        }
        finally
        {
            VisualQualityController.Changed -= listener;
            VisualQualityController.SetQuality(original);
        }
    }

    [Test]
    public void RoadControllerSharesOneMaterialAcrossRoadRoles()
    {
        EchoRoadVisualController controller = CreateController();
        GameObject main = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject seam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            Renderer mainRenderer = main.GetComponent<Renderer>();
            Renderer seamRenderer = seam.GetComponent<Renderer>();
            controller.ApplyTo(mainRenderer, RoadSurfaceRole.Main);
            controller.ApplyTo(seamRenderer, RoadSurfaceRole.Seam);

            Assert.NotNull(controller.SharedRoadMaterial);
            Assert.AreSame(controller.SharedRoadMaterial,
                mainRenderer.sharedMaterial);
            Assert.AreSame(controller.SharedRoadMaterial,
                seamRenderer.sharedMaterial);

            var properties = new MaterialPropertyBlock();
            seamRenderer.GetPropertyBlock(properties);
            Assert.AreEqual((float)RoadSurfaceRole.Seam,
                properties.GetFloat("_RoadRole"));
        }
        finally
        {
            Object.DestroyImmediate(main);
            Object.DestroyImmediate(seam);
        }
    }

    [Test]
    public void TrackBindingLeavesEnvironmentRendererUntouched()
    {
        EchoRoadVisualController controller = CreateController();
        GameObject root = new GameObject("TrackSegment");
        GameObject road = GameObject.CreatePrimitive(PrimitiveType.Cube);
        road.name = "GroundPlane";
        road.transform.SetParent(root.transform);
        GameObject environment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        environment.name = "Building";
        environment.transform.SetParent(root.transform);
        Material environmentMaterial = environment.GetComponent<Renderer>().sharedMaterial;
        try
        {
            int bound = controller.ApplyToTrackSegment(root,
                RoadSurfaceRole.Main);

            Assert.AreEqual(1, bound);
            Assert.AreSame(controller.SharedRoadMaterial,
                road.GetComponent<Renderer>().sharedMaterial);
            Assert.AreSame(environmentMaterial,
                environment.GetComponent<Renderer>().sharedMaterial);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void NightBaselineStaysInsideFrozenExposureRanges()
    {
        Assert.That(WorldStyler.StructureMetallic,
            Is.InRange(0.10f, 0.20f));
        Assert.That(WorldStyler.StructureSmoothness,
            Is.InRange(0.25f, 0.35f));
        Assert.That(WorldStyler.HighFillLightIntensity,
            Is.InRange(0.25f, 0.28f));
        Assert.That(WorldStyler.HighReflectionIntensity,
            Is.InRange(0.22f, 0.25f));
        Assert.That(WorldStyler.KeyLightIntensity,
            Is.InRange(0.95f, 1.05f));
    }

    [Test]
    public void RuntimeSkyKeepsCityPanoramaAndBlendsItsWrapBoundary()
    {
        Material material = WorldStyler.CreateSeamlessSkyMaterial();
        try
        {
            Assert.NotNull(material);
            Assert.NotNull(material.shader);
            Assert.AreEqual(WorldStyler.SeamlessSkyShaderName,
                material.shader.name);
            Assert.AreNotEqual("Skybox/Panoramic", material.shader.name);
            Assert.AreNotEqual("Skybox/Procedural", material.shader.name);
            Assert.NotNull(material.GetTexture("_MainTex"));
            Assert.That(material.GetFloat("_SeamBlend"),
                Is.InRange(0.001f, 0.10f));
        }
        finally
        {
            if (material != null) Object.DestroyImmediate(material);
        }
    }

    [Test]
    public void RoadMaterialCarriesAtlasAndHighQualityKeywords()
    {
        EchoRoadVisualController controller = CreateController();
        Material material = controller.SharedRoadMaterial;
        Assert.NotNull(material);
        Assert.AreEqual("EchoRun/Road", material.shader.name);
        Assert.NotNull(material.GetTexture("_RoadAtlas"));
        Assert.NotNull(material.GetTexture("_NormalMap"));

        controller.ApplyQuality(VisualQuality.Low);
        Assert.IsFalse(material.IsKeywordEnabled("_ECHO_NORMALMAP"));
        Assert.IsFalse(material.IsKeywordEnabled("_ECHO_FAKE_REFLECTION"));
        Assert.IsFalse(material.IsKeywordEnabled("_ECHO_WET_SURFACE"));

        controller.ApplyQuality(VisualQuality.High);
        Assert.IsTrue(material.IsKeywordEnabled("_ECHO_NORMALMAP"));
        Assert.IsTrue(material.IsKeywordEnabled("_ECHO_FAKE_REFLECTION"));
        Assert.IsTrue(material.IsKeywordEnabled("_ECHO_WET_SURFACE"));
    }

    private EchoRoadVisualController CreateController()
    {
        _controllerObject = new GameObject("EchoRoadVisualController_Test");
        return _controllerObject.AddComponent<EchoRoadVisualController>();
    }
}
