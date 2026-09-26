using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class MemoryCourierArtTests
{
    [TestCase("Run")]
    [TestCase("Jump")]
    [TestCase("Slide")]
    public void InstalledCourierDeformsWithExistingHumanoidMotion(string state)
    {
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        Mesh baked = null;
        try
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.scene");
            // OpenScene unloads transient native objects from the previous scene.
            baked = new Mesh();
            Transform model = GameObject.Find("player").transform.Find("CharacterModel");
            var skin = model.Find("OrangeEchoOutfit/OE_OrangeEchoClothing").GetComponent<SkinnedMeshRenderer>();
            Assert.AreEqual("Assets/Art/LayeredMemory/Meshes/MemoryCourierClothing_Fitted.asset",
                AssetDatabase.GetAssetPath(skin.sharedMesh));
            Assert.AreEqual(6, skin.sharedMaterials.Length);
            Assert.IsTrue(skin.sharedMaterials.All(m => AssetDatabase.GetAssetPath(m)
                .StartsWith("Assets/Art/OrangeEcho/Materials/", StringComparison.Ordinal)));
            Assert.That(skin.sharedMesh.triangles.Length / 3, Is.LessThanOrEqualTo(11000));
            Assert.IsEmpty(skin.GetComponentsInChildren<Collider>(true));
            Animator animator = model.GetComponent<Animator>();
            Assert.AreEqual(1, Object.FindObjectsOfType<Animator>(true).Length,
                "The temporary import skeleton must not be saved into the game scene.");
            Assert.IsTrue(animator.isHuman);
            Assert.IsFalse(animator.applyRootMotion);
            Assert.AreEqual("Assets/Animations/HumanMotion/EchoRunHuman.controller",
                AssetDatabase.GetAssetPath(animator.runtimeAnimatorController));
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();
            skin.forceMatrixRecalculationPerRender = true;
            Transform leg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            Quaternion rest = leg.localRotation;
            float maximumRotation = 0f;
            foreach (float normalized in new[] { .15f, .45f, .75f })
            {
                animator.Play(state, 0, normalized);
                animator.Update(0f);
                Assert.IsTrue(animator.GetCurrentAnimatorStateInfo(0).IsName(state));
                maximumRotation = Mathf.Max(maximumRotation, Quaternion.Angle(rest, leg.localRotation));
                skin.BakeMesh(baked);
                Assert.IsTrue(baked.vertices.All(v => !float.IsNaN(v.x) && !float.IsNaN(v.y)
                    && !float.IsNaN(v.z) && !float.IsInfinity(v.sqrMagnitude)));
                Assert.That(baked.bounds.size.magnitude, Is.InRange(.7f, 3f), state + " deformation bounds");
                Assert.AreEqual(skin.sharedMesh.vertexCount, baked.vertexCount);
            }
            Assert.Greater(maximumRotation, 10f, "Do not accept T-pose captures as motion verification.");
        }
        finally
        {
            if (baked != null) Object.DestroyImmediate(baked);
            if (setup.Any(s => s.isLoaded) && setup.Any(s => s.isActive))
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
