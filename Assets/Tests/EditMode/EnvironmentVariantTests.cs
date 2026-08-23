using NUnit.Framework;
using UnityEditor;
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

    [TestCase("Assets/Prefabs/TrackSegment.prefab", 3)]
    [TestCase("Assets/Prefabs/TurnSegment_Left.prefab", 2)]
    [TestCase("Assets/Prefabs/TurnSegment_Right.prefab", 2)]
    public void RoadPrefabsPrehangExclusiveEnvironmentVariants(
        string prefabPath, int expectedVariants)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.NotNull(prefab, prefabPath);
        Transform environment = prefab.transform.Find("EchoEnvironment");
        Assert.NotNull(environment,
            prefabPath + " must prehang its pooled visual environment.");
        Assert.NotNull(environment.Find("Common"));
        Assert.NotNull(environment.Find("HighQualityOnly"));
        Transform variants = environment.Find("VisualVariants");
        Assert.NotNull(variants);
        Assert.AreEqual(expectedVariants, variants.childCount);
        Assert.NotNull(environment.GetComponent<EchoEnvironmentVariantSet>());
        Assert.AreEqual(0,
            environment.GetComponentsInChildren<Collider>(true).Length);
        Renderer[] renderers =
            environment.GetComponentsInChildren<Renderer>(true);
        Assert.Greater(renderers.Length, 0);
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.NotNull(material, renderer.name);
                Assert.IsTrue(AssetDatabase.Contains(material),
                    renderer.name + " must use a persistent material asset.");
            }
        }
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
    public void StraightSegmentUsesAuthoredDistrictsInEveryVariantAndNoBoxSkyline()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/TrackSegment.prefab");
        Assert.NotNull(prefab);
        Transform environment = prefab.transform.Find("EchoEnvironment");
        Assert.NotNull(environment);

        int authoredStationCount = 0;
        int authoredDistrictCount = 0;
        foreach (Transform child in
                 environment.GetComponentsInChildren<Transform>(true))
        {
            Assert.AreNotEqual("SilhouetteBlock", child.name,
                "Procedural skyline blocks must not obscure the authored sky city.");
            if (child.name == "SideEnergyStation")
            {
                authoredStationCount++;
                Assert.GreaterOrEqual(Mathf.Abs(child.localPosition.x), 14f);
                Assert.AreEqual("Variant_A_CityLeft",
                    child.parent.name,
                    "The station is secondary dressing beside the main city module.");
            }
            else if (child.name == "MegacityDistrictA" ||
                     child.name == "MegacityDistrictB")
            {
                authoredDistrictCount++;
                Assert.GreaterOrEqual(Mathf.Abs(child.localPosition.x), 13f);
            }

            Assert.AreNotEqual("MidStructureFallback", child.name,
                "The baked production prefab must use the authored station asset.");
        }

        Assert.AreEqual(1, authoredStationCount,
            "The old station is retained only as a secondary accent.");
        Assert.AreEqual(4, authoredDistrictCount,
            "Every straight visual variant must contain authored city massing.");

        Transform variants = environment.Find("VisualVariants");
        foreach (Transform variant in variants)
        {
            bool hasDistrict = false;
            foreach (Transform child in
                     variant.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "MegacityDistrictA" ||
                    child.name == "MegacityDistrictB")
                    hasDistrict = true;
            }

            Assert.IsTrue(hasDistrict,
                variant.name + " must present an authored city module.");
        }
    }

    [Test]
    public void AuthoredSideEnergyStationUsesPersistentPaletteAndNoColliders()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Resources/Art/Environment/EchoSideEnergyStation.prefab");
        Assert.NotNull(prefab);
        Assert.AreEqual(0, prefab.GetComponentsInChildren<Collider>(true).Length);

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        Assert.AreEqual(4, renderers.Length,
            "The station should stay batched to one renderer per palette material.");
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.NotNull(material, renderer.name);
                string path = AssetDatabase.GetAssetPath(material);
                StringAssert.StartsWith("Assets/Prefabs/Materials/Echo", path,
                    renderer.name + " must use the persistent Echo palette.");
            }
        }
    }

    [TestCase("Assets/Resources/Art/Environment/EchoMegacityDistrictA.prefab")]
    [TestCase("Assets/Resources/Art/Environment/EchoMegacityDistrictB.prefab")]
    public void AuthoredMegacityDistrictUsesFiveMaterialBatchesAndNoColliders(
        string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.NotNull(prefab, prefabPath);
        Assert.AreEqual(0, prefab.GetComponentsInChildren<Collider>(true).Length);

        Renderer[] renderers = prefab.GetComponentsInChildren<Renderer>(true);
        Assert.AreEqual(5, renderers.Length, prefabPath);
        foreach (Renderer renderer in renderers)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                Assert.NotNull(material, renderer.name);
                StringAssert.StartsWith("Assets/Prefabs/Materials/Echo",
                    AssetDatabase.GetAssetPath(material), renderer.name);
            }
        }
    }

    [TestCase("Assets/Prefabs/TurnSegment_Left.prefab")]
    [TestCase("Assets/Prefabs/TurnSegment_Right.prefab")]
    public void TurnSegmentsDoNotBakeProceduralCornerSkyline(
        string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.NotNull(prefab, prefabPath);
        Assert.IsNull(prefab.transform.Find(
            "EchoEnvironment/HighQualityOnly/CornerSilhouette"), prefabPath);
        Assert.NotNull(prefab.transform.Find(
            "EchoEnvironment/VisualVariants/Variant_A_CornerCity/MegacityDistrictA"),
            prefabPath + " must use the authored city district.");
        Assert.NotNull(prefab.transform.Find(
            "EchoEnvironment/VisualVariants/Variant_B_CornerSignal/MegacityDistrictB"),
            prefabPath + " must keep authored city massing in its signal variant.");
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
