using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class InstallHumanMotion
{
    private const string AnimationFolder = "Assets/Animations/HumanMotion";
    private const string IdlePath = AnimationFolder + "/HumanIdle.fbx";
    private const string RunPath = AnimationFolder + "/HumanRunForwards.fbx";
    private const string FallingPath = AnimationFolder + "/HumanFalling.fbx";
    private const string SlidePath = AnimationFolder
        + "/Visvise/EchoRun_SlideLow_v1_TextMotion_TextMotion0.fbx";
    private const string ControllerPath = AnimationFolder + "/EchoRunHuman.controller";
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";

    [MenuItem("Tools/Echo Runner/Install Human Motion")]
    public static void Install()
    {
        AnimationClip idle = ConfigureClip(IdlePath, "HumanIdle", true);
        AnimationClip run = ConfigureClip(RunPath, "HumanRun", true);
        AnimationClip falling = ConfigureClip(
            FallingPath, "HumanFalling", true);
        AnimationClip slide = ConfigureClip(
            SlidePath, "EchoRunSlideLow_Candidate1", false, true);
        AnimatorController controller = BuildController(
            idle, run, falling, slide);
        AssignControllerToScene(controller);
        ValidateInstalledScene();
        Debug.Log("HUMAN_MOTION_INSTALL_OK");
    }

    public static void ValidateInstalledScene()
    {
        RuntimeAnimatorController expected =
            AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                ControllerPath);
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        Transform model = player != null
            ? player.transform.Find("CharacterModel")
            : null;
        Animator animator = model != null ? model.GetComponent<Animator>() : null;
        CharacterAnimator driver = model != null
            ? model.GetComponent<CharacterAnimator>()
            : null;

        if (expected == null || animator == null || driver == null)
            throw new InvalidOperationException(
                "Human motion scene validation failed: assets are missing.");
        if (animator.runtimeAnimatorController != expected)
            throw new InvalidOperationException(
                "Human motion scene validation failed: controller is not assigned.");
        if (animator.applyRootMotion)
            throw new InvalidOperationException(
                "Human motion scene validation failed: root motion must stay disabled.");
        if (!driver.enabled || !driver.useHumanoidRig
            || !driver.useAuthoredAnimations || !driver.useAuthoredSlide)
        {
            throw new InvalidOperationException(
                "Human motion scene validation failed: state driver is disabled.");
        }

        Debug.Log("HUMAN_MOTION_SCENE_OK clips="
            + expected.animationClips.Length + " scene=" + scene.path);
    }

    private static AnimationClip ConfigureClip(
        string path, string clipName, bool loop,
        bool rootHeightFromFeet = false)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("Missing ModelImporter: " + path);

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.importBlendShapes = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips == null || clips.Length == 0)
            throw new InvalidOperationException("No animation clip in " + path);
        ModelImporterClipAnimation clip = clips[0];
        clip.name = clipName;
        clip.loopTime = loop;
        clip.loopPose = loop;
        clip.lockRootRotation = true;
        clip.lockRootHeightY = true;
        clip.lockRootPositionXZ = true;
        clip.keepOriginalOrientation = true;
        clip.keepOriginalPositionY = !rootHeightFromFeet;
        clip.heightFromFeet = rootHeightFromFeet;
        clip.keepOriginalPositionXZ = true;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();

        AnimationClip imported = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<AnimationClip>()
            .FirstOrDefault(candidate => candidate.name == clipName);
        if (imported == null || !imported.isHumanMotion)
            throw new InvalidOperationException(
                "Humanoid animation import failed: " + path);
        return imported;
    }

    private static AnimatorController BuildController(
        AnimationClip idle, AnimationClip run, AnimationClip falling,
        AnimationClip slide)
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(
                ControllerPath);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ChildAnimatorState[] existingStates = stateMachine.states;
        for (int i = existingStates.Length - 1; i >= 0; i--)
            stateMachine.RemoveState(existingStates[i].state);

        AnimatorState idleState = stateMachine.AddState("Idle");
        idleState.motion = idle;
        AnimatorState runState = stateMachine.AddState("Run");
        runState.motion = run;
        AnimatorState jumpState = stateMachine.AddState("Jump");
        jumpState.motion = falling;
        AnimatorState slideState = stateMachine.AddState("Slide");
        slideState.motion = slide;
        stateMachine.defaultState = idleState;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void AssignControllerToScene(
        RuntimeAnimatorController controller)
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        GameObject player = GameObject.Find("player");
        Transform model = player != null
            ? player.transform.Find("CharacterModel")
            : null;
        if (model == null)
            throw new InvalidOperationException("Scene CharacterModel was not found.");

        Animator animator = model.GetComponent<Animator>();
        CharacterAnimator driver = model.GetComponent<CharacterAnimator>();
        if (animator == null || driver == null)
            throw new InvalidOperationException(
                "Scene CharacterModel animation components are missing.");

        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        driver.enabled = true;
        driver.useHumanoidRig = true;
        driver.useAuthoredAnimations = true;
        driver.useAuthoredSlide = true;

        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(driver);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException(
                "Could not save SampleScene after installing human motion.");
        AssetDatabase.SaveAssets();
    }
}
