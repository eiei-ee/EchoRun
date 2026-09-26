using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class OrangeEchoArtInstaller
{
    const string ScenePath = "Assets/Scenes/SampleScene.scene";
    const string Root = "Assets/Art/OrangeEcho";
    static string Output = "TestResults/OrangeEcho-20260917";
    static string BoneKey(string name) => name.Replace("mixamorig:", "").Replace("mixamorig_", "");

    [MenuItem("Tools/Echo Runner/Install Orange Echo Art")]
    public static void Install()
    {
        InstallOutfit(Root + "/Models/OrangeEchoOutfit.fbx", true);
    }

    static void InstallOutfit(string outfitPath, bool installRoads)
    {
        Directory.CreateDirectory(Output);
        Directory.CreateDirectory(Root + "/Materials");
        Directory.CreateDirectory(Root + "/Meshes");
        Directory.CreateDirectory("Assets/Resources/OrangeEcho");
        AssetDatabase.Refresh();
        string[] files = installRoads ? Directory.GetFiles(Root + "/Models", "*.fbx") : new[] { outfitPath };
        foreach (string file in files)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            importer.isReadable = true;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
        }
        if (installRoads) InstallRoads();
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (!File.Exists(Output + "/SampleScene-before.scene"))
            File.Copy(ScenePath, Output + "/SampleScene-before.scene");
        var player = GameObject.Find("player");
        var model = player.transform.Find("CharacterModel");
        var animator = model.GetComponent<Animator>();
        Avatar avatar = animator.avatar;
        RuntimeAnimatorController controller = animator.runtimeAnimatorController;
        Transform previous = model.Find("OrangeEchoOutfit");
        if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var bones = model.GetComponentsInChildren<Transform>(true)
            .GroupBy(t => BoneKey(t.name)).ToDictionary(g => g.Key, g => g.First());
        var originalRenderers = model.GetComponentsInChildren<Renderer>(true);
        var source = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(outfitPath));
        source.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var outfit = new GameObject("OrangeEchoOutfit");
        outfit.transform.SetParent(model, false);
        int vertices = 0, renderers = 0;
        foreach (SkinnedMeshRenderer src in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (src.sharedMesh == null || src.sharedMesh.vertexCount == 0) continue;
            var go = new GameObject(src.name);
            go.transform.SetParent(outfit.transform, false);
            var dst = go.AddComponent<SkinnedMeshRenderer>();
            // FBX meshes may store pre-bind vertices in a different transform
            // space from the visible rest pose. Bake the imported rest pose
            // before rebinding, retaining the original per-vertex weights.
            Mesh mesh;
            if (src.name == "OE_OrangeEchoClothing")
                mesh = UnityEngine.Object.Instantiate(src.sharedMesh);
            else
            {
                mesh = new Mesh();
                src.BakeMesh(mesh);
                mesh.boneWeights = src.sharedMesh.boneWeights;
            }
            mesh.name = src.name;
            Matrix4x4 matrix = src.transform.localToWorldMatrix;
            mesh.vertices = mesh.vertices.Select(v => matrix.MultiplyPoint3x4(v)).ToArray();
            Matrix4x4 normalMatrix = matrix.inverse.transpose;
            mesh.normals = mesh.normals.Select(v => normalMatrix.MultiplyVector(v).normalized).ToArray();
            dst.bones = src.bones.Select(b => bones.TryGetValue(BoneKey(b.name), out Transform target)
                ? target : throw new InvalidOperationException("Missing existing bone: " + b.name)).ToArray();
            mesh.bindposes = dst.bones.Select(b => b.worldToLocalMatrix * dst.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            Debug.Log("ORANGE_MESH " + src.name + " bounds=" + mesh.bounds
                + " bones=" + dst.bones.Length + " source=" + src.sharedMesh.bounds);
            string path = Root + "/Meshes/" + src.name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
            else
            {
                // Assign native mesh buffers explicitly; preserve this asset's
                // GUID while invalidating skinning/GPU buffers from prior imports.
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
                UnityEngine.Object.DestroyImmediate(mesh);
            }
            dst.sharedMesh = saved;
            // Vertices are in CharacterModel space. Using Hips here would shift
            // the culling volume upward by the hip height and hide the head.
            dst.rootBone = model;
            dst.localBounds = new Bounds(new Vector3(0, .9f, 0), new Vector3(3f, 2.6f, 4f));
            dst.updateWhenOffscreen = false;
            dst.sharedMaterials = src.sharedMaterials.Select(ResolveMaterial).ToArray();
            dst.shadowCastingMode = ShadowCastingMode.On;
            dst.receiveShadows = true;
            vertices += saved.vertexCount; renderers++;
        }
        UnityEngine.Object.DestroyImmediate(source);
        if (renderers == 0) throw new InvalidOperationException("No imported clothing renderers.");
        foreach (Renderer renderer in originalRenderers) renderer.enabled = false;
        var spine = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "EchoMemorySpine");
        if (spine != null) spine.gameObject.SetActive(false);
        if (animator.avatar != avatar || animator.runtimeAnimatorController != controller)
            throw new InvalidOperationException("Original animation references changed.");
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save clothing bindings.");
        AssetDatabase.SaveAssets();
        File.WriteAllText(Output + "/install.txt", "Renderers=" + renderers + " vertices=" + vertices
            + "\nAvatar=" + AssetDatabase.GetAssetPath(avatar) + "\nController=" + AssetDatabase.GetAssetPath(controller)
            + "\nOriginal skeleton retained. No controller, collision or lane changes.\n");
        Debug.Log("ORANGE_ECHO_INSTALL_OK " + renderers + " renderers / " + vertices + " vertices");
    }

    static Material ResolveMaterial(Material source)
    {
        string name = source.name.Replace(" (Instance)", "");
        if (!name.StartsWith("OE_"))
        {
            // The preserved BODY atlas contains the face and hands. Keep their
            // native texture shading when re-authoring the outfit.
            string faceMaterial = name == "Body_MAT" ? "OE_RunnerSkin"
                : name == "Eye_MAT" ? "OE_RunnerEyes"
                : name == "Brows_MAT" ? "OE_RunnerLashes"
                : name == "Eye_Spec_MAT" ? "OE_RunnerEyeCover" : null;
            if (faceMaterial != null)
            {
                Material skin = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + faceMaterial + ".mat");
                if (skin != null) return skin;
            }
            string legacy = "Assets/Models/Mixamo/ExoGray/Materials/" + name + "_BlueTech.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(legacy);
            if (existing != null) return existing;
            throw new InvalidOperationException("Preserved head/hand material missing: " + name);
        }
        string path = Root + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("EchoRun/OrangeEchoSurface")) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        // FBX material values are read once from the authored source.
        material.color = source.HasProperty("_Color") ? source.color : Color.gray;
        material.SetFloat("_Smoothness", name.Contains("Relay") ? .36f : .18f);
        material.SetFloat("_Metallic", name.Contains("Metal") ? .35f : 0f);
        material.SetFloat("_Grain", name.Contains("RoadDeck") ? .09f : 0f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        return material;
    }

    static void InstallRoads()
    {
        foreach (string name in new[] { "OrangeRoadStraight", "OrangeRoadRight", "OrangeRoadLeft" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + name + ".fbx");
            if (source == null) throw new InvalidOperationException(name + " missing.");
            var instance = UnityEngine.Object.Instantiate(source);
            instance.name = name;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = renderer.sharedMaterials.Select(ResolveMaterial).ToArray();
            if (instance.GetComponentsInChildren<Collider>(true).Length != 0)
                throw new InvalidOperationException("Road art must not carry colliders.");
            PrefabUtility.SaveAsPrefabAsset(instance, "Assets/Resources/OrangeEcho/" + name + ".prefab");
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    public static void Capture()
    {
        Directory.CreateDirectory(Output);
        ShaderUtil.allowAsyncCompilation = false;
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var player = GameObject.Find("player");
        foreach (var root in scene.GetRootGameObjects()) if (root != player) root.SetActive(false);
        player.transform.SetPositionAndRotation(new Vector3(0,1,0), Quaternion.identity);
        var cam = new GameObject("ArtReviewCamera").AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.18f,.21f,.25f);
        cam.fieldOfView = 32;
        var key = new GameObject("Key").AddComponent<Light>(); key.type = LightType.Directional;
        var fill = new GameObject("Fill").AddComponent<Light>(); fill.type = LightType.Directional;
        CityV7PlayableEnvironment.ApplyAtmosphere(key, fill);
        RenderSettings.fog = false;
        File.WriteAllLines(Output + "/renderer-bounds.txt", player.GetComponentsInChildren<SkinnedMeshRenderer>()
            .Select(r => r.name + " enabled=" + r.enabled + " mesh=" + r.sharedMesh.bounds
                + " rendered=" + r.bounds + " scale=" + r.transform.lossyScale));
        CaptureView(cam, new Vector3(0,1.05f,4.1f), new Vector3(0,.92f,0), "runner-front", 720, 900);
        CaptureView(cam, new Vector3(0,1.05f,-4.1f), new Vector3(0,.92f,0), "runner-back", 720, 900);
        CaptureView(cam, new Vector3(3,1.1f,-2.8f), new Vector3(0,.92f,0), "runner-three-quarter", 720, 900);
        var model = player.transform.Find("CharacterModel").gameObject;
        var animator = model.GetComponent<Animator>();
        AnimationClip[] clips = animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.animationClips.Distinct().ToArray() : Array.Empty<AnimationClip>();
        File.WriteAllLines(Output + "/animation-clips.txt", clips.Select(c => c.name));
        foreach (string action in new[] { "run", "jump", "slide" })
        {
            AnimationClip clip = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains(action));
            if (clip == null) continue;
            AnimationMode.StartAnimationMode();
            try
            {
                AnimationMode.BeginSampling();
                AnimationMode.SampleAnimationClip(model, clip, clip.length * .35f);
                AnimationMode.EndSampling();
                CaptureView(cam, new Vector3(2.6f,1.0f,-3.2f), new Vector3(0,.9f,0), "pose-" + action, 900,900);
            }
            finally { AnimationMode.StopAnimationMode(); }
        }
        foreach (string name in new[] { "OrangeRoadStraight", "OrangeRoadRight", "OrangeRoadLeft" })
        {
            var road = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("OrangeEcho/" + name));
            player.SetActive(false);
            cam.fieldOfView = 48;
            CaptureView(cam, new Vector3(17,19,-22), new Vector3(0,0,5), name, 1280,720);
            UnityEngine.Object.DestroyImmediate(road);
        }
        Debug.Log("ORANGE_ECHO_CAPTURE_OK");
    }

    static void CaptureView(Camera cam, Vector3 position, Vector3 target, string name, int width, int height)
    {
        cam.transform.position = position; cam.transform.LookAt(target);
        var rt = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
        cam.targetTexture = rt; cam.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(width,height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
        File.WriteAllBytes(Output + "/" + name + ".png",image.EncodeToPNG());
        RenderTexture.active = previous; cam.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
    }

    public static void InstallAndCapture() { Install(); Capture(); }
    public static void InstallCaptureAndBuild() { Install(); Capture(); Build(); }

    public static void Build()
    {
        EditorSceneManager.OpenScene(ScenePath);
        var result = BuildPipeline.BuildPlayer(new[] { ScenePath }, Output + "/Windows/EchoRun.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.Development | BuildOptions.CompressWithLz4HC);
        if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
            throw new InvalidOperationException("Orange Echo review build failed: " + result.summary.result);
        Debug.Log("ORANGE_ECHO_BUILD_OK");
    }
}
