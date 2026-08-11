using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class AIShadowRuntimeTests
{
    [UnityTest]
    public IEnumerator TrainingDashboardBuildsOptionalLiveDebugPanel()
    {
        SceneManager.LoadScene("SampleScene");
        AITrainingDashboardUI dashboard = null;
        for (int frame = 0; frame < 180 && dashboard == null; frame++)
        {
            dashboard = Object.FindObjectOfType<AITrainingDashboardUI>();
            yield return null;
        }

        Assert.IsNotNull(dashboard);
        for (int frame = 0; frame < 180
             && GetField(dashboard, "_liveDebugPanel") == null; frame++)
            yield return null;
        GameObject liveDebug = (GameObject)GetField(
            dashboard, "_liveDebugPanel");
        Assert.IsNotNull(liveDebug);
        Assert.IsNotNull(liveDebug.transform.Find("Content"));
        Assert.IsFalse(liveDebug.activeSelf,
            "Live diagnostics must be opt-in during normal play.");
    }

    [UnityTest]
    public IEnumerator GhostVisualStaysAboveTrackAndUsesDedicatedShader()
    {
        SceneManager.LoadScene("SampleScene");
        for (int frame = 0; frame < 180 &&
             (AIShadowRunner.Instance == null ||
              Object.FindObjectOfType<PlayerController>() == null); frame++)
            yield return null;

        PlayerController player = Object.FindObjectOfType<PlayerController>();
        AIShadowRunner runner = AIShadowRunner.Instance;
        Assert.IsNotNull(player);
        Assert.IsNotNull(runner);
        for (int frame = 0; frame < 30; frame++) yield return null;

        SetField(runner, "_player", player);
        Invoke(runner, "CreateGhost");
        SetField(runner, "_ghostGroundY", player.transform.position.y);
        SetField(runner, "_displayedGap", 10f);
        SetField(runner, "_ghostProgress", 10f);
        SetField(runner, "_playerProgress", 0f);
        Invoke(runner, "UpdateGhostPose");
        yield return null;

        GameObject ghost = (GameObject)GetField(runner, "_ghost");
        Material material = (Material)GetField(runner, "_ghostMaterial");
        Assert.IsNotNull(material);
        Assert.AreEqual("EchoRun/GhostRunner", material.shader.name);

        Renderer[] renderers = ghost.GetComponentsInChildren<Renderer>(true);
        Assert.IsNotEmpty(renderers);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Assert.IsTrue(Physics.Raycast(ghost.transform.position + Vector3.up * 5f,
            Vector3.down, out RaycastHit hit, 10f, player.groundLayer,
            QueryTriggerInteraction.Ignore));
        Assert.GreaterOrEqual(bounds.min.y, hit.point.y - 0.05f,
            "The AI shadow visual is submerged below the track.");

        SetField(runner, "_ghost", null);
        Object.Destroy(ghost);
        yield return null;
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method,
            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }

    private static object GetField(object target, string name)
    {
        return target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
