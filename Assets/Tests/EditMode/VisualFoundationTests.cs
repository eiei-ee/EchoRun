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

    private EchoRoadVisualController CreateController()
    {
        _controllerObject = new GameObject("EchoRoadVisualController_Test");
        return _controllerObject.AddComponent<EchoRoadVisualController>();
    }
}
