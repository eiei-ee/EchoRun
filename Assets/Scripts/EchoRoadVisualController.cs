using System;
using UnityEngine;

public enum RoadSurfaceRole
{
    Main,
    Turn,
    Seam,
    StartDeck,
    RuntimeFallback
}

[DefaultExecutionOrder(-150)]
public sealed class EchoRoadVisualController : MonoBehaviour
{
    private const string ResourcePath = "Materials/EchoRoad";
    private static readonly int RoadRoleId = Shader.PropertyToID("_RoadRole");
    private static readonly int UvScaleId = Shader.PropertyToID("_RoadUvScale");
    private static readonly int SeamStrengthId = Shader.PropertyToID("_RoadSeamStrength");
    private static readonly int StartDeckBoostId = Shader.PropertyToID("_RoadStartDeckBoost");
    private static readonly int LaneDensityId = Shader.PropertyToID("_RoadLaneDensity");
    private static readonly int SafeLaneHintId = Shader.PropertyToID("_RoadSafeLaneHint");

    private static EchoRoadVisualController _instance;
    private Material _sharedRoadMaterial;
    private bool _ownsRuntimeMaterial;

    public static EchoRoadVisualController Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = FindObjectOfType<EchoRoadVisualController>();
            if (_instance != null) return _instance;
            _instance = new GameObject("EchoRoadVisualController")
                .AddComponent<EchoRoadVisualController>();
            return _instance;
        }
    }

    public Material SharedRoadMaterial
    {
        get
        {
            EnsureMaterial();
            return _sharedRoadMaterial;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        EchoRoadVisualController unused = Instance;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        EnsureMaterial();
        ApplyQuality(VisualQualityController.Current);
        VisualQualityController.Changed += ApplyQuality;
    }

    public void ApplyTo(Renderer renderer, RoadSurfaceRole role)
    {
        if (renderer == null) return;
        EnsureMaterial();
        if (_sharedRoadMaterial == null) return;

        renderer.sharedMaterial = _sharedRoadMaterial;
        var properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetFloat(RoadRoleId, (float)role);
        properties.SetFloat(UvScaleId, role == RoadSurfaceRole.Seam ? 2f : 1f);
        properties.SetFloat(SeamStrengthId, role == RoadSurfaceRole.Seam ? 1f : 0f);
        properties.SetFloat(StartDeckBoostId,
            role == RoadSurfaceRole.StartDeck ? 1f : 0f);
        properties.SetFloat(LaneDensityId,
            role == RoadSurfaceRole.Turn ? 0.75f : 1f);
        properties.SetFloat(SafeLaneHintId, 0f);
        renderer.SetPropertyBlock(properties);
    }

    public int ApplyToTrackSegment(GameObject segment, RoadSurfaceRole defaultRole)
    {
        if (segment == null) return 0;
        int applied = 0;
        Renderer[] renderers = segment.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsRoadRenderer(renderer)) continue;
            ApplyTo(renderer, ResolveRole(renderer.name, defaultRole));
            applied++;
        }
        return applied;
    }

    public void ApplyQuality(VisualQuality quality)
    {
        EnsureMaterial();
        if (_sharedRoadMaterial == null) return;
        SetKeyword("_ECHO_NORMALMAP", quality == VisualQuality.High);
        SetKeyword("_ECHO_FAKE_REFLECTION", quality == VisualQuality.High);
        SetKeyword("_ECHO_WET_SURFACE", quality == VisualQuality.High);
    }

    public void ApplyPhaseTint(Color tint, float intensity)
    {
        Shader.SetGlobalColor("_EchoPhaseTint", tint);
        Shader.SetGlobalFloat("_EchoPhaseIntensity", Mathf.Clamp01(intensity));
    }

    private void EnsureMaterial()
    {
        if (_sharedRoadMaterial != null) return;
        _sharedRoadMaterial = Resources.Load<Material>(ResourcePath);
        if (_sharedRoadMaterial != null) return;

        Shader shader = Shader.Find("EchoRun/Road");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return;
        _sharedRoadMaterial = new Material(shader)
        {
            name = "EchoRoad_RuntimeFallback"
        };
        if (_sharedRoadMaterial.HasProperty("_Color"))
            _sharedRoadMaterial.SetColor("_Color",
                new Color(0.045f, 0.065f, 0.095f, 1f));
        _ownsRuntimeMaterial = true;
    }

    private static bool IsRoadRenderer(Renderer renderer)
    {
        string objectName = renderer.name;
        if (objectName == "GroundPlane" || objectName == "EntryStrip"
            || objectName == "ExitStrip" || objectName == "Surface"
            || objectName == "EntryCoverage" || objectName == "ExitCoverage"
            || objectName == "LaunchRoad" || objectName == "Plane"
            || objectName.IndexOf("Seam", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        Material material = renderer.sharedMaterial;
        if (material == null) return false;
        return material.name == "TrackGroundMat"
               || material.name == "TrackGroundMat_Turn"
               || material.name == "TrackSeamMat"
               || material.name == "EchoRoad";
    }

    private static RoadSurfaceRole ResolveRole(string rendererName,
        RoadSurfaceRole defaultRole)
    {
        if (rendererName.IndexOf("Seam", StringComparison.OrdinalIgnoreCase) >= 0)
            return RoadSurfaceRole.Seam;
        if (rendererName == "LaunchRoad" || rendererName == "Plane")
            return RoadSurfaceRole.StartDeck;
        if (rendererName == "EntryCoverage" || rendererName == "ExitCoverage")
            return RoadSurfaceRole.RuntimeFallback;
        return defaultRole;
    }

    private void SetKeyword(string keyword, bool enabled)
    {
        if (enabled) _sharedRoadMaterial.EnableKeyword(keyword);
        else _sharedRoadMaterial.DisableKeyword(keyword);
    }

    private void OnDestroy()
    {
        VisualQualityController.Changed -= ApplyQuality;
        if (_ownsRuntimeMaterial && _sharedRoadMaterial != null)
        {
            if (Application.isPlaying) Destroy(_sharedRoadMaterial);
            else DestroyImmediate(_sharedRoadMaterial);
        }
        if (_instance == this) _instance = null;
    }
}
