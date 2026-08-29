using UnityEditor;
using UnityEngine;

public static class EchoGameplayArtBuilder
{
    private const string MaterialPath =
        "Assets/Resources/Materials/EchoCollectible.mat";

    [MenuItem("Tools/Ensure Echo Gameplay Art")]
    public static void Build()
    {
        Shader shader = Shader.Find("EchoRun/Collectible");
        if (shader == null)
        {
            Debug.LogError("EchoRun/Collectible shader is missing.");
            return;
        }
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "EchoCollectible" };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }
        material.SetColor("_FrameColor", new Color32(24, 33, 39, 255));
        material.SetColor("_FrameHighlight", new Color32(89, 102, 109, 255));
        material.SetColor("_CoreColor", new Color32(0, 220, 235, 255));
        material.SetColor("_CoreEdgeColor", new Color32(184, 250, 255, 255));
        material.SetColor("_AccentColor", new Color32(255, 120, 32, 255));
        material.SetColor("_ContractColor", new Color(1f, 0.34f, 0.30f, 1f));
        material.SetFloat("_EmissionStrength", 1.65f);
        material.SetFloat("_ScanPeriod", 1.5f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
