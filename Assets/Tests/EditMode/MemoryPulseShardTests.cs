using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MemoryPulseShardTests
{
    private const string VisualPrefabPath =
        "Assets/Resources/Art/Pickups/MemoryPulseShard_B.prefab";
    private const string BlenderModelPath =
        "Assets/Art/Pickups/MemoryPulseShard/Models/MemoryPulseShard_B.fbx";
    private const string CoinPrefabPath = "Assets/Prefabs/Coin.prefab";

    [Test]
    public void FormalShardUsesOneColliderFreeVertexColoredRenderer()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            VisualPrefabPath);
        Assert.IsNotNull(prefab);
        Assert.AreEqual(0,
            prefab.GetComponentsInChildren<Collider>(true).Length);

        MeshFilter filter = prefab.GetComponentInChildren<MeshFilter>(true);
        MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>(true);
        Assert.IsNotNull(filter);
        Assert.IsNotNull(filter.sharedMesh);
        Assert.IsNotNull(renderer);
        Assert.AreEqual(1, renderer.sharedMaterials.Length);
        Assert.AreEqual("EchoRun/Collectible", renderer.sharedMaterial.shader.name);

        GameObject authoredModel = AssetDatabase.LoadAssetAtPath<GameObject>(
            BlenderModelPath);
        MeshFilter authored = authoredModel.GetComponentInChildren<MeshFilter>(true);
        Assert.IsNotNull(authored);
        Assert.AreSame(authored.sharedMesh, filter.sharedMesh,
            "Runtime prefab must bind the Blender-authored mesh.");
        Assert.AreEqual(1, filter.sharedMesh.subMeshCount);
        Assert.LessOrEqual(filter.sharedMesh.triangles.Length / 3, 4500);

        Vector3 size = TransformBounds(filter.sharedMesh.bounds,
            filter.transform.localToWorldMatrix).size;
        Assert.That(size.x, Is.InRange(0.85f, 1.05f));
        Assert.That(size.y, Is.InRange(0.95f, 1.12f));
        Assert.That(size.z, Is.InRange(0.12f, 0.22f));

        Color[] colors = filter.sharedMesh.colors;
        Assert.AreEqual(filter.sharedMesh.vertexCount, colors.Length);
        int frame = 0;
        int core = 0;
        int accent = 0;
        foreach (Color color in colors)
        {
            if (color.r > 0.9f) frame++;
            if (color.g > 0.9f) core++;
            if (color.b > 0.9f) accent++;
        }
        Assert.Greater(frame, 0);
        Assert.Greater(core, 0);
        Assert.Greater(accent, 0);

        Vector3[] vertices = filter.sharedMesh.vertices;
        int[] triangles = filter.sharedMesh.triangles;
        float totalArea = 0f;
        float accentArea = 0f;
        for (int index = 0; index < triangles.Length; index += 3)
        {
            int a = triangles[index];
            int b = triangles[index + 1];
            int c = triangles[index + 2];
            float area = Vector3.Cross(vertices[b] - vertices[a],
                vertices[c] - vertices[a]).magnitude * 0.5f;
            totalArea += area;
            if (colors[a].b > 0.9f) accentArea += area;
        }
        Assert.That(accentArea / totalArea, Is.LessThan(0.10f));
    }

    [Test]
    public void PickupMotionAndSpacingStayWithinMemoryPathBudget()
    {
        GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            CoinPrefabPath);
        Coin coin = coinPrefab.GetComponent<Coin>();
        BoxCollider trigger = coinPrefab.GetComponent<BoxCollider>();
        Assert.IsNotNull(coin);
        Assert.IsNotNull(trigger);
        Assert.IsTrue(trigger.isTrigger);
        Assert.AreEqual(new Vector3(1f, 1f, 0.4f), trigger.size);
        Assert.That(coin.rotateSpeed, Is.InRange(30f, 45f));
        Assert.That(coin.yawAmplitude, Is.InRange(8f, 14f));
        Assert.That(coin.bobHeight, Is.InRange(0.03f, 0.05f));

        GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(
            VisualPrefabPath);
        MeshFilter visualFilter = visual.GetComponentInChildren<MeshFilter>();
        float width = TransformBounds(visualFilter.sharedMesh.bounds,
            visualFilter.transform.localToWorldMatrix).size.x;
        Assert.GreaterOrEqual(TrackSpawnRules.CoinSpacing, width * 1.3f);
    }

    private static Bounds TransformBounds(Bounds localBounds,
        Matrix4x4 matrix)
    {
        Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
        Vector3 extents = localBounds.extents;
        Vector3 axisX = matrix.MultiplyVector(
            new Vector3(extents.x, 0f, 0f));
        Vector3 axisY = matrix.MultiplyVector(
            new Vector3(0f, extents.y, 0f));
        Vector3 axisZ = matrix.MultiplyVector(
            new Vector3(0f, 0f, extents.z));
        extents = new Vector3(
            Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
            Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
            Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
        return new Bounds(center, extents * 2f);
    }
}
