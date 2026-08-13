using UnityEngine;

public static class RunnerAppearanceService
{
    private static readonly int DarkColor = Shader.PropertyToID("_DarkColor");
    private static readonly int LightColor = Shader.PropertyToID("_LightColor");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    public static int Apply(Transform model, Color dark, Color light,
        Color emission)
    {
        if (model == null) return 0;

        int changedSlots = 0;
        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            Material[] materials = renderer.sharedMaterials;
            for (int index = 0; index < materials.Length; index++)
            {
                Material material = materials[index];
                if (material == null) continue;

                bool techMaterial = material.HasProperty(DarkColor)
                                    || material.HasProperty(LightColor)
                                    || material.HasProperty(EmissionColor);
                bool legacyOutfit = IsLegacyOutfitMaterial(material.name);
                if (!techMaterial && !legacyOutfit) continue;

                properties.Clear();
                renderer.GetPropertyBlock(properties, index);
                if (material.HasProperty(DarkColor))
                    properties.SetColor(DarkColor, dark);
                if (material.HasProperty(LightColor))
                    properties.SetColor(LightColor, light);
                if (material.HasProperty(EmissionColor))
                    properties.SetColor(EmissionColor, emission);
                if (!techMaterial && material.HasProperty(BaseColor))
                    properties.SetColor(BaseColor,
                        IsDarkMaterial(material.name) ? dark : light);
                if (!techMaterial && material.HasProperty(ColorProperty))
                    properties.SetColor(ColorProperty,
                        IsDarkMaterial(material.name) ? dark : light);
                renderer.SetPropertyBlock(properties, index);
                changedSlots++;
            }
        }
        return changedSlots;
    }

    private static bool IsLegacyOutfitMaterial(string materialName)
    {
        return materialName.Contains("Cloth")
               || materialName.Contains("Pants")
               || materialName.Contains("Shoe")
               || materialName.Contains("Accent");
    }

    private static bool IsDarkMaterial(string materialName)
    {
        return materialName.Contains("Pants") || materialName.Contains("Shoe");
    }
}
