using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class InstallExoGray
{
    private const string ModelPath = "Assets/Models/Mixamo/ExoGray/ExoGray_TPose.fbx";
    private const string MaterialFolder = "Assets/Models/Mixamo/ExoGray/Materials";
    private const string TextureFolder = "Assets/Models/Mixamo/ExoGray/Textures";
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";
    private const string ShaderName = "EchoRun/ExoGrayBlueTech";

    [MenuItem("Tools/Echo Runner/Install Exo Gray")]
    public static void Install()
    {
        ConfigureImporter();

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        if (modelAsset == null) throw new InvalidOperationException("Exo Gray FBX was not imported.");
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
            throw new InvalidOperationException("Exo Gray Humanoid Avatar is invalid.");

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        if (player == null) throw new InvalidOperationException("Scene player object was not found.");

        Transform oldModel = player.transform.Find("CharacterModel");
        RuntimeAnimatorController oldController = null;
        if (oldModel != null)
        {
            Animator oldAnimator = oldModel.GetComponent<Animator>();
            if (oldAnimator != null) oldController = oldAnimator.runtimeAnimatorController;
            UnityEngine.Object.DestroyImmediate(oldModel.gameObject);
        }

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, player.transform);
        model.name = "CharacterModel";
        model.transform.localPosition = new Vector3(0f, -1f, 0f);
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.avatar = avatar;
        animator.runtimeAnimatorController = oldController;
        animator.applyRootMotion = false;

        CharacterAnimator characterAnimator = model.GetComponent<CharacterAnimator>();
        if (characterAnimator == null) characterAnimator = model.AddComponent<CharacterAnimator>();
        characterAnimator.useHumanoidRig = true;
        characterAnimator.enabled = oldController == null;

        Shader shader = Shader.Find(ShaderName);
        if (shader == null) throw new InvalidOperationException(ShaderName + " shader was not compiled.");

        EnsureFolder(MaterialFolder);
        Dictionary<Material, Material> replacements = new Dictionary<Material, Material>();
        SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException("Exo Gray has no skinned mesh renderer.");

        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material source = materials[i];
                if (source == null) continue;
                if (!replacements.TryGetValue(source, out Material replacement))
                {
                    replacement = LoadOrCreateMaterial(source, shader);
                    replacements.Add(source, replacement);
                }
                materials[i] = replacement;
            }
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller == null) throw new InvalidOperationException("Scene player has no PlayerController.");
        SerializedObject controllerObject = new SerializedObject(controller);
        controllerObject.FindProperty("characterModel").objectReferenceValue = model.transform;
        controllerObject.ApplyModifiedPropertiesWithoutUndo();

        CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            capsule.center = new Vector3(0f, 1f, 0f);
            capsule.height = 2.2f;
            capsule.radius = 0.4f;
        }

        EditorUtility.SetDirty(player);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save SampleScene after installing Exo Gray.");

        AssetDatabase.SaveAssets();
        ReportModel();
        Debug.Log("EXO_GRAY_INSTALL_OK");
    }

    public static void ValidateInstalledScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        Transform model = player != null ? player.transform.Find("CharacterModel") : null;
        PlayerController controller = player != null ? player.GetComponent<PlayerController>() : null;
        Animator animator = model != null ? model.GetComponent<Animator>() : null;
        CharacterAnimator characterAnimator = model != null ? model.GetComponent<CharacterAnimator>() : null;
        SkinnedMeshRenderer[] renderers = model != null
            ? model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
            : Array.Empty<SkinnedMeshRenderer>();

        if (player == null || model == null || controller == null || animator == null ||
            characterAnimator == null || renderers.Length == 0)
            throw new InvalidOperationException("Exo Gray scene validation failed: required objects are missing.");
        if (controller.characterModel != model)
            throw new InvalidOperationException("Exo Gray scene validation failed: PlayerController reference is stale.");
        if (animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
            throw new InvalidOperationException("Exo Gray scene validation failed: Humanoid Avatar is invalid.");
        if (animator.applyRootMotion)
            throw new InvalidOperationException("Exo Gray scene validation failed: root motion must be disabled.");
        if (animator.runtimeAnimatorController == null && !characterAnimator.enabled)
            throw new InvalidOperationException("Exo Gray scene validation failed: no animation driver is enabled.");
        if (Vector3.Distance(model.localPosition, new Vector3(0f, -1f, 0f)) > 0.001f)
            throw new InvalidOperationException("Exo Gray scene validation failed: model offset changed.");

        Debug.Log($"EXO_GRAY_SCENE_OK renderers={renderers.Length} " +
                  $"controllerPreserved={animator.runtimeAnimatorController != null} " +
                  $"proceduralAnimator={characterAnimator.enabled}");
    }

    public static void ReportModel()
    {
        ConfigureImporter();
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null) throw new InvalidOperationException("Exo Gray FBX was not imported.");

        int vertices = 0;
        long triangles = 0;
        HashSet<string> materials = new HashSet<string>();
        foreach (SkinnedMeshRenderer renderer in modelAsset.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh != null)
            {
                vertices += mesh.vertexCount;
                for (int i = 0; i < mesh.subMeshCount; i++)
                    triangles += (long)mesh.GetIndexCount(i) / 3;
            }
            foreach (Material material in renderer.sharedMaterials)
                if (material != null) materials.Add(material.name);
        }

        Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
        Debug.Log($"EXO_GRAY_REPORT vertices={vertices} triangles={triangles} " +
                  $"materials={materials.Count} [{string.Join(", ", materials)}] " +
                  $"avatarValid={avatar != null && avatar.isValid} avatarHuman={avatar != null && avatar.isHuman}");
    }

    private static void ConfigureImporter()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Exo Gray ModelImporter was not found.");

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.meshCompression = ModelImporterMeshCompression.Low;
        importer.isReadable = false;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();

        EnsureFolder(TextureFolder);
        if (AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }).Length == 0)
        {
            importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer != null && importer.ExtractTextures(TextureFolder))
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        ConfigureExtractedTextures();
    }

    private static void ConfigureExtractedTextures()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool detailTexture = path.IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 path.IndexOf("specular", StringComparison.OrdinalIgnoreCase) >= 0;
            int maxSize = detailTexture ? 512 : 1024;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.compressionQuality = 100;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            if (path.IndexOf("normal", StringComparison.OrdinalIgnoreCase) >= 0)
                importer.textureType = TextureImporterType.NormalMap;

            TextureImporterPlatformSettings weixin = importer.GetPlatformTextureSettings("WeixinMiniGame");
            weixin.overridden = true;
            weixin.maxTextureSize = maxSize;
            weixin.format = TextureImporterFormat.ASTC_8x8;
            weixin.textureCompression = TextureImporterCompression.CompressedHQ;
            weixin.compressionQuality = 100;
            importer.SetPlatformTextureSettings(weixin);
            importer.SaveAndReimport();
        }
    }

    private static Material LoadOrCreateMaterial(Material source, Shader shader)
    {
        string safeName = string.Concat(source.name.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        string path = $"{MaterialFolder}/{safeName}_BlueTech.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = safeName + "_BlueTech" };
            AssetDatabase.CreateAsset(material, path);
        }
        else
        {
            material.shader = shader;
        }

        Texture mainTexture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
        material.SetTexture("_MainTex", mainTexture);
        InstallEchoRunnerPhaseOne.ApplyBlueTechProfile(
            material, source.name);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder(string folder)
    {
        string current = "Assets";
        foreach (string part in folder.Substring("Assets/".Length).Split('/'))
        {
            string next = current + "/" + part;
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
            current = next;
        }
    }
}
