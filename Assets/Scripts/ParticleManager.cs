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

        _coinPS  = CreateParticleSystem("CoinFX",  new Color(1f, 0.85f, 0.1f), 0.15f, 2.5f, 18);
        _dustPS  = CreateParticleSystem("DustFX",  new Color(0.55f, 0.45f, 0.35f), 0.25f, 1.5f, 5);
        _deathPS = CreateParticleSystem("DeathFX", new Color(1f, 0.2f, 0.1f), 0.5f, 4f, 30);
        _trailPS = CreateParticleSystem("TrailFX", new Color(0.6f, 0.5f, 0.4f), 0.35f, 1f, 3);
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
    public void EmitTrail(Vector3 pos)   { _trailPS.transform.position = pos; _trailPS.Emit(1); }
    public void EmitDeath(Vector3 pos)   { _deathPS.transform.position = pos; _deathPS.Emit(20); }
}
