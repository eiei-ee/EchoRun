using UnityEngine;

/// <summary>
/// Duel-phase atmosphere. Each echo duel phase gets its own light, fog,
/// ambient and skybox grade so a viewer can read the duel state from the
/// world itself: detection is cold blue, reveal/resistance drift violet,
/// counterattack flares coral, rewrite glows cyan and the finale burns gold.
///
/// Transitions are time-bounded lerps. Values are only written while a
/// transition is in flight, never as per-frame material churn, and reduced
/// motion snaps instantly. All grades sit on top of the palette WorldStyler
/// configures; the neutral mood is captured live from the scene baseline.
/// </summary>
public enum EchoAtmosphereMood
{
    Neutral,
    Detection,
    Reveal,
    Resistance,
    Counterattack,
    Rewrite,
    Finale,
    Defeat,
    Victory
}

public struct EchoAtmospherePreset
{
    public Color fogColor;
    public float fogStartDistance;
    public float fogEndDistance;
    public Color ambientSkyColor;
    public Color ambientEquatorColor;
    public Color ambientGroundColor;
    public Color keyLightColor;
    public float keyLightIntensity;
    public Color skyboxTint;
    public float skyboxExposure;
}

public sealed class EchoAtmosphereDirector : MonoBehaviour
{
    public static EchoAtmosphereDirector Instance { get; private set; }

    [Tooltip("Seconds for a full mood transition.")]
    public float transitionSeconds = 1.35f;

    public EchoAtmosphereMood CurrentMood { get; private set; }
        = EchoAtmosphereMood.Neutral;

    private EchoAtmospherePreset _baseline;
    private EchoAtmospherePreset _current;
    private EchoAtmospherePreset _target;
    private bool _baselineCaptured;
    private bool _dirty;
    private Light _keyLight;
    private Material _skybox;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<EchoAtmosphereDirector>() != null) return;
        new GameObject("EchoAtmosphereDirector")
            .AddComponent<EchoAtmosphereDirector>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void LateUpdate()
    {
        if (!_baselineCaptured)
        {
            CaptureBaseline();
            _baselineCaptured = true;
        }

        EchoAtmosphereMood mood = ResolveMood();
        if (mood != CurrentMood)
        {
            CurrentMood = mood;
            _target = PresetForMood(mood, _baseline);
            _dirty = true;
        }

        if (!_dirty) return;

        if (EchoRunAccessibility.ReducedMotion)
        {
            _current = _target;
            ApplyCurrent();
            _dirty = false;
            return;
        }

        float step = 1f - Mathf.Exp(-Time.deltaTime
            / Mathf.Max(0.05f, transitionSeconds * 0.33f));
        LerpTowards(ref _current, _target, step);
        ApplyCurrent();
        if (Approximately(_current, _target))
        {
            _current = _target;
            ApplyCurrent();
            _dirty = false;
        }
    }

    private EchoAtmosphereMood ResolveMood()
    {
        GameManager game = GameManager.Instance;
        if (game == null) return EchoAtmosphereMood.Neutral;

        if (game.State == GameState.GameOver)
        {
            if (game.LastEndReason == RunEndReason.Collision)
                return EchoAtmosphereMood.Defeat;
            if (game.LastEndReason == RunEndReason.FinishReached)
                return EchoAtmosphereMood.Victory;
            return EchoAtmosphereMood.Neutral;
        }

        if (game.State == GameState.Paused)
            return CurrentMood;
        if (game.State != GameState.Playing)
            return EchoAtmosphereMood.Neutral;

        AIShadowRunner shadow = AIShadowRunner.Instance;
        if (shadow == null) return EchoAtmosphereMood.Neutral;
        return MoodForPhase(shadow.DuelPhase);
    }

    public static EchoAtmosphereMood MoodForPhase(EchoDuelPhase phase)
    {
        switch (phase)
        {
            case EchoDuelPhase.Detection: return EchoAtmosphereMood.Detection;
            case EchoDuelPhase.Reveal: return EchoAtmosphereMood.Reveal;
            case EchoDuelPhase.Resistance: return EchoAtmosphereMood.Resistance;
            case EchoDuelPhase.Counterattack:
                return EchoAtmosphereMood.Counterattack;
            case EchoDuelPhase.Rewrite: return EchoAtmosphereMood.Rewrite;
            case EchoDuelPhase.Finale: return EchoAtmosphereMood.Finale;
            default: return EchoAtmosphereMood.Neutral;
        }
    }

    private void CaptureBaseline()
    {
        _baseline = new EchoAtmospherePreset
        {
            fogColor = RenderSettings.fogColor,
            fogStartDistance = RenderSettings.fogStartDistance,
            fogEndDistance = RenderSettings.fogEndDistance,
            ambientSkyColor = RenderSettings.ambientSkyColor,
            ambientEquatorColor = RenderSettings.ambientEquatorColor,
            ambientGroundColor = RenderSettings.ambientGroundColor,
            keyLightColor = Color.white,
            keyLightIntensity = 1f,
            skyboxTint = Color.white,
            skyboxExposure = 0.42f
        };

        _keyLight = FindKeyLight();
        if (_keyLight != null)
        {
            _baseline.keyLightColor = _keyLight.color;
            _baseline.keyLightIntensity = _keyLight.intensity;
        }

        _skybox = RenderSettings.skybox;
        if (_skybox != null)
        {
            if (_skybox.HasProperty("_Tint"))
                _baseline.skyboxTint = _skybox.GetColor("_Tint");
            if (_skybox.HasProperty("_Exposure"))
                _baseline.skyboxExposure = _skybox.GetFloat("_Exposure");
        }

        _current = _baseline;
        _target = _baseline;
    }

    private static Light FindKeyLight()
    {
        Light[] lights = FindObjectsOfType<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].type == LightType.Directional
                && lights[i].gameObject.name != "EchoFillLight")
                return lights[i];
        }
        return null;
    }

    private void ApplyCurrent()
    {
        RenderSettings.fogColor = _current.fogColor;
        RenderSettings.fogStartDistance = _current.fogStartDistance;
        RenderSettings.fogEndDistance = _current.fogEndDistance;
        RenderSettings.ambientSkyColor = _current.ambientSkyColor;
        RenderSettings.ambientEquatorColor = _current.ambientEquatorColor;
        RenderSettings.ambientGroundColor = _current.ambientGroundColor;

        if (_keyLight == null) _keyLight = FindKeyLight();
        if (_keyLight != null)
        {
            _keyLight.color = _current.keyLightColor;
            _keyLight.intensity = _current.keyLightIntensity;
        }

        if (_skybox != RenderSettings.skybox)
            _skybox = RenderSettings.skybox;
        if (_skybox != null)
        {
            if (_skybox.HasProperty("_Tint"))
                _skybox.SetColor("_Tint", _current.skyboxTint);
            if (_skybox.HasProperty("_Exposure"))
                _skybox.SetFloat("_Exposure", _current.skyboxExposure);
        }
    }

    private static void LerpTowards(ref EchoAtmospherePreset from,
        EchoAtmospherePreset to, float t)
    {
        from.fogColor = Color.Lerp(from.fogColor, to.fogColor, t);
        from.fogStartDistance = Mathf.Lerp(from.fogStartDistance,
            to.fogStartDistance, t);
        from.fogEndDistance = Mathf.Lerp(from.fogEndDistance,
            to.fogEndDistance, t);
        from.ambientSkyColor = Color.Lerp(from.ambientSkyColor,
            to.ambientSkyColor, t);
        from.ambientEquatorColor = Color.Lerp(from.ambientEquatorColor,
            to.ambientEquatorColor, t);
        from.ambientGroundColor = Color.Lerp(from.ambientGroundColor,
            to.ambientGroundColor, t);
        from.keyLightColor = Color.Lerp(from.keyLightColor,
            to.keyLightColor, t);
        from.keyLightIntensity = Mathf.Lerp(from.keyLightIntensity,
            to.keyLightIntensity, t);
        from.skyboxTint = Color.Lerp(from.skyboxTint, to.skyboxTint, t);
        from.skyboxExposure = Mathf.Lerp(from.skyboxExposure,
            to.skyboxExposure, t);
    }

    private static bool Approximately(EchoAtmospherePreset a,
        EchoAtmospherePreset b)
    {
        return ColorClose(a.fogColor, b.fogColor)
               && ColorClose(a.ambientSkyColor, b.ambientSkyColor)
               && ColorClose(a.ambientEquatorColor, b.ambientEquatorColor)
               && ColorClose(a.ambientGroundColor, b.ambientGroundColor)
               && ColorClose(a.keyLightColor, b.keyLightColor)
               && ColorClose(a.skyboxTint, b.skyboxTint)
               && Mathf.Abs(a.fogStartDistance - b.fogStartDistance) < 0.05f
               && Mathf.Abs(a.fogEndDistance - b.fogEndDistance) < 0.05f
               && Mathf.Abs(a.keyLightIntensity - b.keyLightIntensity) < 0.005f
               && Mathf.Abs(a.skyboxExposure - b.skyboxExposure) < 0.005f;
    }

    private static bool ColorClose(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.004f
               && Mathf.Abs(a.g - b.g) < 0.004f
               && Mathf.Abs(a.b - b.b) < 0.004f;
    }

    /// <summary>
    /// Absolute grades per mood. Neutral replays the captured scene baseline
    /// so the game looks exactly as WorldStyler configured it outside duels.
    /// </summary>
    public static EchoAtmospherePreset PresetForMood(EchoAtmosphereMood mood,
        EchoAtmospherePreset baseline)
    {
        EchoAtmospherePreset p = baseline;
        switch (mood)
        {
            case EchoAtmosphereMood.Detection:
                p.fogColor = new Color(0.045f, 0.100f, 0.190f);
                p.fogStartDistance = 48f;
                p.fogEndDistance = 124f;
                p.ambientSkyColor = new Color(0.26f, 0.38f, 0.56f);
                p.ambientEquatorColor = new Color(0.11f, 0.18f, 0.30f);
                p.keyLightColor = new Color(0.92f, 0.95f, 1.00f);
                p.keyLightIntensity = baseline.keyLightIntensity * 0.94f;
                p.skyboxTint = new Color(0.45f, 0.62f, 0.88f);
                p.skyboxExposure = 0.40f;
                break;
            case EchoAtmosphereMood.Reveal:
                p.fogColor = new Color(0.100f, 0.085f, 0.220f);
                p.fogStartDistance = 46f;
                p.fogEndDistance = 118f;
                p.ambientSkyColor = new Color(0.32f, 0.32f, 0.58f);
                p.ambientEquatorColor = new Color(0.14f, 0.14f, 0.30f);
                p.keyLightColor = new Color(0.96f, 0.88f, 1.00f);
                p.keyLightIntensity = baseline.keyLightIntensity * 0.97f;
                p.skyboxTint = new Color(0.55f, 0.52f, 0.88f);
                p.skyboxExposure = 0.42f;
                break;
            case EchoAtmosphereMood.Resistance:
                p.fogColor = new Color(0.130f, 0.080f, 0.230f);
                p.fogStartDistance = 44f;
                p.fogEndDistance = 112f;
                p.ambientSkyColor = new Color(0.36f, 0.28f, 0.56f);
                p.ambientEquatorColor = new Color(0.16f, 0.12f, 0.28f);
                p.keyLightColor = new Color(1.00f, 0.86f, 0.95f);
                p.keyLightIntensity = baseline.keyLightIntensity * 0.98f;
                p.skyboxTint = new Color(0.62f, 0.48f, 0.85f);
                p.skyboxExposure = 0.44f;
                break;
            case EchoAtmosphereMood.Counterattack:
                p.fogColor = new Color(0.220f, 0.075f, 0.090f);
                p.fogStartDistance = 40f;
                p.fogEndDistance = 105f;
                p.ambientSkyColor = new Color(0.50f, 0.26f, 0.26f);
                p.ambientEquatorColor = new Color(0.20f, 0.10f, 0.10f);
                p.ambientGroundColor = new Color(0.05f, 0.03f, 0.03f);
                p.keyLightColor = new Color(1.00f, 0.72f, 0.62f);
                p.keyLightIntensity = baseline.keyLightIntensity * 1.05f;
                p.skyboxTint = new Color(0.90f, 0.52f, 0.45f);
                p.skyboxExposure = 0.46f;
                break;
            case EchoAtmosphereMood.Rewrite:
                p.fogColor = new Color(0.050f, 0.160f, 0.200f);
                p.fogStartDistance = 46f;
                p.fogEndDistance = 122f;
                p.ambientSkyColor = new Color(0.24f, 0.46f, 0.56f);
                p.ambientEquatorColor = new Color(0.10f, 0.20f, 0.26f);
                p.keyLightColor = new Color(0.82f, 0.98f, 1.00f);
                p.keyLightIntensity = baseline.keyLightIntensity * 1.07f;
                p.skyboxTint = new Color(0.48f, 0.82f, 0.92f);
                p.skyboxExposure = 0.50f;
                break;
            case EchoAtmosphereMood.Finale:
                p.fogColor = new Color(0.190f, 0.130f, 0.060f);
                p.fogStartDistance = 42f;
                p.fogEndDistance = 110f;
                p.ambientSkyColor = new Color(0.52f, 0.42f, 0.24f);
                p.ambientEquatorColor = new Color(0.20f, 0.15f, 0.08f);
                p.keyLightColor = new Color(1.00f, 0.88f, 0.62f);
                p.keyLightIntensity = baseline.keyLightIntensity * 1.14f;
                p.skyboxTint = new Color(0.95f, 0.78f, 0.50f);
                p.skyboxExposure = 0.52f;
                break;
            case EchoAtmosphereMood.Defeat:
                p.fogColor = new Color(0.050f, 0.050f, 0.070f);
                p.fogStartDistance = 30f;
                p.fogEndDistance = 80f;
                p.ambientSkyColor = new Color(0.16f, 0.16f, 0.20f);
                p.ambientEquatorColor = new Color(0.07f, 0.07f, 0.09f);
                p.keyLightColor = new Color(0.80f, 0.80f, 0.90f);
                p.keyLightIntensity = baseline.keyLightIntensity * 0.62f;
                p.skyboxTint = new Color(0.35f, 0.35f, 0.42f);
                p.skyboxExposure = 0.30f;
                break;
            case EchoAtmosphereMood.Victory:
                p.fogColor = new Color(0.170f, 0.130f, 0.070f);
                p.fogStartDistance = 46f;
                p.fogEndDistance = 118f;
                p.ambientSkyColor = new Color(0.55f, 0.46f, 0.28f);
                p.ambientEquatorColor = new Color(0.20f, 0.16f, 0.09f);
                p.keyLightColor = new Color(1.00f, 0.92f, 0.70f);
                p.keyLightIntensity = baseline.keyLightIntensity * 1.18f;
                p.skyboxTint = new Color(0.98f, 0.84f, 0.58f);
                p.skyboxExposure = 0.55f;
                break;
        }
        return p;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
