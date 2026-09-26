using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Rebind one authored garment onto the existing scene skeleton. The legacy
// OrangeEchoOutfit holder is retained because cosmetics and ghost cloning use it.
public static class LayeredMemoryRunnerInstaller
{
    public const string SourcePath = "Assets/Art/LayeredMemory/Models/MemoryCourier.fbx";
    public const string MeshPath = "Assets/Art/LayeredMemory/Meshes/MemoryCourierClothing.asset";
    private const string Materials = "Assets/Art/OrangeEcho/Materials/";

    private static string BoneKey(string name) => name.Replace("mixamorig:", "").Replace("mixamorig_", "");

    [MenuItem("Tools/Echo Runner/Layered Memory/Install Courier")]
    public static void Install()
    {
        AssetDatabase.Refresh();
        var importer = AssetImporter.GetAtPath(SourcePath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Export MemoryCourier.fbx from Blender first.");
        importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.isReadable = true; // Offline rebinding only; runtime uses the separate persistent mesh.
        importer.importCameras = false;
        importer.importLights = false;
        importer.addCollider = false;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.SaveAndReimport();
        var scene = EditorSceneManager.OpenScene(LayeredMemoryRunnerReview.ScenePath);
        var player = GameObject.Find("player");
        Transform model = player != null ? player.transform.Find("CharacterModel") : null;
        Transform holder = model != null ? model.Find("OrangeEchoOutfit") : null;
        var skin = holder != null ? holder.Find("OE_OrangeEchoClothing")?.GetComponent<SkinnedMeshRenderer>() : null;
        if (skin == null) throw new InvalidOperationException("Existing scene garment binding missing.");
        Animator animator = model.GetComponent<Animator>();
        Avatar avatar = animator.avatar;
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        var bones = model.GetComponentsInChildren<Transform>(true)
            .GroupBy(t => BoneKey(t.name)).ToDictionary(g => g.Key, g => g.First());
        GameObject source = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath));
        source.hideFlags = HideFlags.HideAndDontSave;
        Mesh mesh = null;
        try
        {
            source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            SkinnedMeshRenderer[] candidates = source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (candidates.Length != 1 || source.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Expected one visual-only garment mesh.");
            SkinnedMeshRenderer authored = candidates[0];
            Material[] materials = authored.sharedMaterials.Select(material =>
            {
                Material shared = AssetDatabase.LoadAssetAtPath<Material>(Materials + material.name + ".mat");
                if (shared == null) throw new InvalidOperationException("Unknown shared cloth material: " + material.name);
                return shared;
            }).ToArray();
            if (materials.Length != 6) throw new InvalidOperationException("Courier must reuse exactly six cloth materials.");
            Transform[] mappedBones = authored.bones.Select(b =>
                b != null && bones.TryGetValue(BoneKey(b.name), out Transform existing)
                    ? existing : throw new InvalidOperationException("Missing original bone: " + b?.name)).ToArray();
            mesh = Object.Instantiate(authored.sharedMesh);
            mesh.name = "MemoryCourierClothing";
            Matrix4x4 space = skin.transform.worldToLocalMatrix * model.localToWorldMatrix * authored.transform.localToWorldMatrix;
            mesh.vertices = mesh.vertices.Select(v => space.MultiplyPoint3x4(v)).ToArray();
            Matrix4x4 normalSpace = space.inverse.transpose;
            mesh.normals = mesh.normals.Select(v => normalSpace.MultiplyVector(v).normalized).ToArray();
            mesh.bindposes = mappedBones.Select(b => b.worldToLocalMatrix * skin.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            long triangles = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++) triangles += mesh.GetIndexCount(sub) / 3;
            if (triangles > 11000 || mesh.bounds.size.magnitude > 3f || mesh.bounds.size.y < 1.6f)
                throw new InvalidOperationException("Invalid garment scale or triangle budget: " + mesh.bounds + " / " + triangles);
            if (mesh.boneWeights.Length != mesh.vertexCount || mesh.tangents.Length != mesh.vertexCount)
                throw new InvalidOperationException("Garment skin weights or tangent basis missing.");
            foreach (BoneWeight weight in mesh.boneWeights)
                if (Mathf.Abs(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3 - 1f) > .01f)
                    throw new InvalidOperationException("Unnormalized garment weight.");

            Directory.CreateDirectory(LayeredMemoryRunnerReview.Output + "/Baseline");
            string backup = LayeredMemoryRunnerReview.Output + "/Baseline/SampleScene-before-install.scene";
            if (!File.Exists(backup)) File.Copy(LayeredMemoryRunnerReview.ScenePath, backup);
            Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(mesh, MeshPath);
                saved = mesh;
                mesh = null;
            }
            else
            {
                // Copy native buffers explicitly to invalidate cached skinning
                // buffers while preserving the persistent asset's identity.
                saved.Clear();
                saved.indexFormat = mesh.indexFormat;
                saved.vertices = mesh.vertices;
                saved.normals = mesh.normals;
                saved.tangents = mesh.tangents;
                saved.uv = mesh.uv;
                saved.boneWeights = mesh.boneWeights;
                saved.bindposes = mesh.bindposes;
                saved.subMeshCount = mesh.subMeshCount;
                for (int sub = 0; sub < mesh.subMeshCount; sub++) saved.SetTriangles(mesh.GetTriangles(sub), sub);
                saved.RecalculateBounds();
                EditorUtility.SetDirty(saved);
            }
            // Regenerate the fitted accessories from this source revision so a
            // normal courier reinstall retains the reviewed clothing binding.
            skin.sharedMesh = CourierGarmentFitRefinement.Build();
            skin.bones = mappedBones;
            skin.sharedMaterials = materials;
            skin.rootBone = model;
            if (animator.avatar != avatar || animator.runtimeAnimatorController != controller)
                throw new InvalidOperationException("Original motion binding changed.");
            // Never serialize the temporary imported skeleton into the game scene.
            Object.DestroyImmediate(source);
            source = null;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Unable to save courier binding.");
            AssetDatabase.SaveAssets();
            File.WriteAllText(LayeredMemoryRunnerReview.Output + "/install.txt",
                "garmentTriangles=" + triangles + "\ngarmentVertices=" + saved.vertexCount
                + "\nmaterials=" + materials.Length + "\nbones=" + mappedBones.Length
                + "\nsourceMesh=" + MeshPath + "\nmesh=" + AssetDatabase.GetAssetPath(skin.sharedMesh)
                + "\navatar=" + AssetDatabase.GetAssetPath(avatar)
                + "\ncontroller=" + AssetDatabase.GetAssetPath(controller)
                + "\nExisting scene renderer, skeleton, head, hands and gameplay components retained.\n");
            Debug.Log("MEMORY_COURIER_INSTALL_OK triangles=" + triangles + " materials=" + materials.Length);
        }
        finally
        {
            if (mesh != null) Object.DestroyImmediate(mesh);
            if (source != null) Object.DestroyImmediate(source);
        }
    }

    public static void InstallAndCapture()
    {
        Install();
        LayeredMemoryRunnerReview.CaptureAfter();
    }
}
