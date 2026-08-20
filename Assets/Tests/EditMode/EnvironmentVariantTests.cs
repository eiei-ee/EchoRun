using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

public class EnvironmentVariantTests
{
    private GameObject _stylerObject;
    private GameObject _segment;

    [TearDown]
    public void TearDown()
    {
        if (_segment != null) Object.DestroyImmediate(_segment);
        if (_stylerObject != null) Object.DestroyImmediate(_stylerObject);
    }

    [Test]
    public void VariantSelectionIsStableFromRunSeedAndRouteDistance()
    {
        int first = EchoEnvironmentVariantSet.SelectVariantIndex(73421, 80f, 3);
        int repeated = EchoEnvironmentVariantSet.SelectVariantIndex(73421, 80f, 3);
        Assert.AreEqual(first, repeated);
        Assert.That(first, Is.InRange(0, 2));
        Assert.AreEqual(-1,
            EchoEnvironmentVariantSet.SelectVariantIndex(73421, 80f, 0));
    }

    [Test]
    public void StraightSegmentPrebuildsThreeExclusiveVisualVariants()
    {
        WorldStyler styler = CreateStyler();
        _segment = new GameObject("TrackSegment_Test");
        TrackSegmentData data = _segment.AddComponent<TrackSegmentData>();
        data.routeDistance = 60f;

        styler.DecorateSegment(_segment, TrackSegmentType.Straight);
        Transform environment = _segment.transform.Find("EchoEnvironment");
        Assert.NotNull(environment);
        Assert.NotNull(environment.Find("Common"));
        Assert.NotNull(environment.Find("HighQualityOnly"));
        Transform variants = environment.Find("VisualVariants");
        Assert.NotNull(variants);
        Assert.AreEqual(3, variants.childCount);

        EchoEnvironmentVariantSet set =
            environment.GetComponent<EchoEnvironmentVariantSet>();
        Assert.AreEqual(3, set.VariantCount);
        Assert.AreEqual(1, CountActiveChildren(variants));
        Assert.AreEqual(0,
            environment.GetComponentsInChildren<Collider>(true).Length);
    }

    [Test]
    public void TurnSegmentUsesTwoDedicatedVariantsAndQualityGate()
    {
        WorldStyler styler = CreateStyler();
        _segment = new GameObject("TurnSegment_Test");
        _segment.AddComponent<TrackSegmentData>().routeDistance = 100f;

        styler.DecorateSegment(_segment, TrackSegmentType.TurnRight);
        Transform environment = _segment.transform.Find("EchoEnvironment");
        EchoEnvironmentVariantSet set =
            environment.GetComponent<EchoEnvironmentVariantSet>();
        Assert.AreEqual(2, set.VariantCount);
        Assert.AreEqual(1, CountActiveChildren(environment.Find("VisualVariants")));

        GameObject highOnly = environment.Find("HighQualityOnly").gameObject;
        set.ApplyQuality(VisualQuality.Low);
        Assert.IsFalse(highOnly.activeSelf);
        Renderer renderer = environment.GetComponentInChildren<Renderer>(true);
        Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode);

        set.ApplyQuality(VisualQuality.High);
        Assert.IsTrue(highOnly.activeSelf);
        Assert.AreEqual(ShadowCastingMode.On, renderer.shadowCastingMode);
    }

    private WorldStyler CreateStyler()
    {
        _stylerObject = new GameObject("WorldStyler_Test");
        return _stylerObject.AddComponent<WorldStyler>();
    }

    private static int CountActiveChildren(Transform parent)
    {
        int active = 0;
        for (int i = 0; i < parent.childCount; i++)
            if (parent.GetChild(i).gameObject.activeSelf) active++;
        return active;
    }
}
