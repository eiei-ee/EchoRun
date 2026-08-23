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
        Transform entry = prefab.transform.Find("EntryStrip");
        Transform exit = prefab.transform.Find("ExitStrip");
        AssertRoadWidth(entry);
        AssertRoadWidth(exit);
        AssertTurnJoinBounds(entry, exit);
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
        AssertRoadWidth(surface);
        Assert.AreEqual(20f, surface.localScale.z * 10f, 0.001f);

        BoxCollider collider = surface.GetComponent<BoxCollider>();
        Assert.NotNull(collider);
        Assert.AreEqual(TrackGeometryStandards.WalkableWidth,
            collider.size.x * surface.localScale.x, 0.001f);
        Assert.AreEqual(20f, collider.size.z * surface.localScale.z, 0.001f);
    }

    private static void AssertRoadWidth(Transform surface)
    {
        Assert.NotNull(surface);
        Assert.AreEqual(TrackGeometryStandards.VisualRoadWidth,
            surface.localScale.x * 10f, 0.001f);

        BoxCollider collider = surface.GetComponent<BoxCollider>();
        Assert.NotNull(collider);
        Assert.AreEqual(TrackGeometryStandards.WalkableWidth,
            collider.size.x * surface.localScale.x, 0.001f);
    }

    private static void AssertTurnJoinBounds(Transform entry, Transform exit)
    {
        float segmentLength = TrackGeometryStandards.StandardSegmentLength;
        float entryLength = entry.localScale.z * 10f;
        float entryNear = entry.localPosition.z - entryLength * 0.5f;
        float entryFar = entry.localPosition.z + entryLength * 0.5f;
        Assert.AreEqual(0f, entryNear, 0.001f,
            "The turn entry must touch the previous straight without overlap.");
        Assert.AreEqual(segmentLength * 0.5f
                        + TrackGeometryStandards.VisualRoadHalfWidth,
            entryFar, 0.001f,
            "The entry may cover only the corner square, not continue as a road arm.");

        float exitLength = exit.localScale.z * 10f;
        float exitCenter = Mathf.Abs(exit.localPosition.x);
        Assert.AreEqual(TrackGeometryStandards.VisualRoadHalfWidth,
            exitCenter - exitLength * 0.5f, 0.001f,
            "The exit must begin at the corner square's outer edge.");
        Assert.AreEqual(segmentLength * 0.5f,
            exitCenter + exitLength * 0.5f, 0.001f,
            "The exit must stop at the following straight's near edge.");

        BoxCollider entryCollider = entry.GetComponent<BoxCollider>();
        BoxCollider exitCollider = exit.GetComponent<BoxCollider>();
        Assert.AreEqual(entryLength,
            entryCollider.size.z * entry.localScale.z, 0.001f);
        Assert.AreEqual(exitLength,
            exitCollider.size.z * exit.localScale.z, 0.001f);
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
