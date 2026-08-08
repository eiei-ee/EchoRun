using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InstallEchoRunner
{
    private const string ModelPath = "Assets/Models/EchoRunner/EchoRunner_v1.fbx";
    private const string MaterialPath = "Assets/Models/EchoRunner/EchoRunner_VertexColor.mat";
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";

    [MenuItem("Tools/Echo Runner/Install v1")]
    public static void Install()
    {
        ConfigureModelImporter();
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);

        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (modelAsset == null) throw new InvalidOperationException("Echo Runner FBX was not imported.");

        Material material = LoadOrCreateMaterial();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        if (player == null) throw new InvalidOperationException("Scene player object was not found.");

        Transform oldModel = player.transform.Find("CharacterModel");
        if (oldModel != null) UnityEngine.Object.DestroyImmediate(oldModel.gameObject);

        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, player.transform);
        model.name = "CharacterModel";
        model.transform.localPosition = new Vector3(0f, -1f, 0f);
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one;

        SkinnedMeshRenderer renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (renderer == null) throw new InvalidOperationException("Echo Runner has no skinned mesh renderer.");
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;

        CharacterAnimator animator = model.AddComponent<CharacterAnimator>();
        SerializedObject animatorObject = new SerializedObject(animator);
        SetReference(animatorObject, "leftUpperArm", model.transform, "LeftUpperArm");
        SetReference(animatorObject, "rightUpperArm", model.transform, "RightUpperArm");
        SetReference(animatorObject, "leftUpperLeg", model.transform, "LeftUpperLeg");
        SetReference(animatorObject, "rightUpperLeg", model.transform, "RightUpperLeg");
        SetReference(animatorObject, "leftFoot", model.transform, "LeftFoot");
        SetReference(animatorObject, "rightFoot", model.transform, "RightFoot");
        SetReference(animatorObject, "bodyTransform", model.transform, "Spine");
        animatorObject.FindProperty("useHumanoidRig").boolValue = false;
        animatorObject.ApplyModifiedPropertiesWithoutUndo();

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
            throw new InvalidOperationException("Could not save SampleScene after installing Echo Runner.");

        AssetDatabase.SaveAssets();
        ValidateInstalledScene();
        Debug.Log("ECHO_RUNNER_INSTALL_OK");
    }

    public static void ValidateInstalledScene()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        Transform model = player != null ? player.transform.Find("CharacterModel") : null;
        SkinnedMeshRenderer renderer = model != null
            ? model.GetComponentInChildren<SkinnedMeshRenderer>(true)
            : null;
        CharacterAnimator animator = model != null ? model.GetComponent<CharacterAnimator>() : null;

        if (player == null || model == null || renderer == null || animator == null)
            throw new InvalidOperationException("Echo Runner scene validation failed: required objects are missing.");
        if (renderer.sharedMesh == null || renderer.sharedMesh.vertexCount < 3000)
            throw new InvalidOperationException("Echo Runner scene validation failed: unexpected mesh.");
        if (renderer.sharedMaterial == null || renderer.sharedMaterial.shader.name != "EchoRun/VertexColor")
            throw new InvalidOperationException("Echo Runner scene validation failed: vertex color material is missing.");
        if (FindDescendant(model, "LeftUpperArm") == null || FindDescendant(model, "RightUpperLeg") == null)
            throw new InvalidOperationException("Echo Runner scene validation failed: deform bones are missing.");
        if (Vector3.Distance(model.localPosition, new Vector3(0f, -1f, 0f)) > 0.001f)
            throw new InvalidOperationException("Echo Runner scene validation failed: model offset changed.");

        Debug.Log($"ECHO_RUNNER_VALIDATE_OK vertices={renderer.sharedMesh.vertexCount} " +
                  $"bounds={renderer.sharedMesh.bounds.size}");
    }

    private static void ConfigureModelImporter()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
        ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Echo Runner ModelImporter was not found.");

        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = false;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.meshCompression = ModelImporterMeshCompression.Low;
        importer.isReadable = false;
        importer.optimizeMeshPolygons = true;
        importer.optimizeMeshVertices = true;
        importer.SaveAndReimport();
    }

    private static Material LoadOrCreateMaterial()
    {
        Shader shader = Shader.Find("EchoRun/VertexColor");
        if (shader == null) throw new InvalidOperationException("EchoRun/VertexColor shader was not compiled.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "EchoRunner_VertexColor" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }
        material.SetFloat("_EmissionStrength", 1.6f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void SetReference(SerializedObject target, string property,
        Transform root, string boneName)
    {
        Transform bone = FindDescendant(root, boneName);
        if (bone == null) throw new InvalidOperationException("Missing Echo Runner bone: " + boneName);
        target.FindProperty(property).objectReferenceValue = bone;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
            if (descendants[i].name == name) return descendants[i];
        return null;
    }
}
