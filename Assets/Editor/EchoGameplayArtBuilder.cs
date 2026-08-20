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
        material.SetColor("_RingColor", new Color(0.94f, 0.68f, 0.24f, 1f));
        material.SetColor("_CoreColor", new Color(0.12f, 0.82f, 1f, 1f));
        material.SetColor("_ContractColor", new Color(1f, 0.34f, 0.30f, 1f));
        material.SetFloat("_EmissionStrength", 1.35f);
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
