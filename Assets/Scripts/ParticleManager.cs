using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    public static ParticleManager Instance { get; private set; }

    private ParticleSystem _coinPS;
    private ParticleSystem _dustPS;
    private ParticleSystem _deathPS;
    private ParticleSystem _trailPS;

    private Material _defaultMat;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Shader s = Shader.Find("Particles/Standard Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");
        if (s == null) s = Shader.Find("Mobile/Particles/Additive");
        _defaultMat = s != null ? new Material(s) : null;

        _coinPS  = CreateParticleSystem("CoinFX",  new Color(0.18f, 0.86f, 1f), 0.18f, 2.5f, 18);
        _dustPS  = CreateParticleSystem("DustFX",  new Color(0.16f, 0.32f, 0.42f), 0.25f, 1.5f, 5);
        _deathPS = CreateParticleSystem("DeathFX", new Color(1f, 0.34f, 0.30f), 0.5f, 4f, 30);
        _trailPS = CreateParticleSystem("TrailFX", new Color(0.12f, 0.76f, 1f), 0.62f, 1f, 12);
        VisualQualityController.Changed += ApplyQuality;
        ApplyQuality(VisualQualityController.Current);
    }

    ParticleSystem CreateParticleSystem(string name, Color color, float lifetime, float speed, int maxParticles)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = 0.18f;
        main.startColor = color;
        main.maxParticles = maxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.loop = false;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, 0f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(
            new Color(color.r, color.g, color.b, 1f),
            new Color(color.r, color.g, color.b, 0f));

        if (_defaultMat != null)
            go.GetComponent<ParticleSystemRenderer>().material = _defaultMat;

        return ps;
    }

    public void EmitCoin(Vector3 pos)    { _coinPS.transform.position = pos;  _coinPS.Emit(10); }
    public void EmitDust(Vector3 pos)    { _dustPS.transform.position = pos;  _dustPS.Emit(2); }
    public void EmitTrail(Vector3 pos)
    {
        if (VisualQualityController.Current != VisualQuality.High) return;
        _trailPS.transform.position = pos;
        _trailPS.Emit(1);
    }
    public void EmitDeath(Vector3 pos)   { _deathPS.transform.position = pos; _deathPS.Emit(20); }

    private void ApplyQuality(VisualQuality quality)
    {
        bool high = quality == VisualQuality.High;
        if (_trailPS != null)
        {
            var emission = _trailPS.emission;
            emission.enabled = high;
            var main = _trailPS.main;
            main.startLifetime = high ? 0.62f : 0.25f;
        }
    }

    private void OnDestroy()
    {
        VisualQualityController.Changed -= ApplyQuality;
        if (_defaultMat != null) Destroy(_defaultMat);
        if (Instance == this) Instance = null;
    }
}
