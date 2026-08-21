using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class TrackGeometryTests
{
    [Test]
    public void LaneCentersStayFixedAtMinusThreeZeroAndThree()
    {
        Assert.AreEqual(-3f, TrackGeometryStandards.GetLaneCenter(0));
        Assert.AreEqual(0f, TrackGeometryStandards.GetLaneCenter(1));
        Assert.AreEqual(3f, TrackGeometryStandards.GetLaneCenter(2));
    }

    [Test]
    public void StraightPrefabUsesSharedRoadWidthAndCenteredRails()
    {
        GameObject prefab = LoadPrefab("Assets/Prefabs/TrackSegment.prefab");

        AssertRoadRootAndLanes(prefab);
        AssertRoadSurface(prefab.transform.Find("GroundPlane"));
        Assert.AreEqual(-TrackGeometryStandards.EdgeRailOffset,
            FindDescendant(prefab.transform, "LeftRail").localPosition.x, 0.001f);
        Assert.AreEqual(TrackGeometryStandards.EdgeRailOffset,
            FindDescendant(prefab.transform, "RightRail").localPosition.x, 0.001f);
    }

    [TestCase("Assets/Prefabs/TurnSegment_Left.prefab")]
    [TestCase("Assets/Prefabs/TurnSegment_Right.prefab")]
    public void TurnPrefabsUseTheSameEntryAndExitWidth(string path)
    {
        GameObject prefab = LoadPrefab(path);

        AssertRoadRootAndLanes(prefab);
        AssertRoadSurface(prefab.transform.Find("EntryStrip"));
        AssertRoadSurface(prefab.transform.Find("ExitStrip"));
        Assert.AreEqual(TrackGeometryStandards.EdgeRailOffset,
            Mathf.Abs(FindDescendant(prefab.transform, "TurnEntryRail")
                .localPosition.x), 0.001f);
        Assert.AreEqual(10f + TrackGeometryStandards.EdgeRailOffset,
            FindDescendant(prefab.transform, "TurnExitRail").localPosition.z,
            0.001f);
    }

    private static GameObject LoadPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.NotNull(prefab, path);
        return prefab;
    }

    private static void AssertRoadRootAndLanes(GameObject prefab)
    {
        Assert.AreEqual(Vector3.one, prefab.transform.localScale,
            $"{prefab.name} root scale must remain 1,1,1.");
        for (int lane = 0; lane < 3; lane++)
        {
            Transform laneTransform = prefab.transform.Find($"Lane_{lane}");
            Assert.NotNull(laneTransform, $"{prefab.name}/Lane_{lane}");
            Assert.AreEqual(TrackGeometryStandards.GetLaneCenter(lane),
                laneTransform.localPosition.x, 0.001f);
        }
    }

    private static void AssertRoadSurface(Transform surface)
    {
        Assert.NotNull(surface);
        Assert.AreEqual(TrackGeometryStandards.VisualRoadWidth,
            surface.localScale.x * 10f, 0.001f);
        Assert.AreEqual(20f, surface.localScale.z * 10f, 0.001f);

        BoxCollider collider = surface.GetComponent<BoxCollider>();
        Assert.NotNull(collider);
        Assert.AreEqual(TrackGeometryStandards.WalkableWidth,
            collider.size.x * surface.localScale.x, 0.001f);
        Assert.AreEqual(20f, collider.size.z * surface.localScale.z, 0.001f);
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        foreach (Transform descendant in root.GetComponentsInChildren<Transform>(true))
        {
            if (descendant.name == name) return descendant;
        }

        Assert.Fail($"Missing {root.name}/{name}");
        return null;
    }
}
