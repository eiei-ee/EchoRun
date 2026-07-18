using UnityEngine;

public class WorldStyler : MonoBehaviour
{
    public static WorldStyler Instance { get; private set; }

    private Material _structureMaterial;
    private Material _deepStructureMaterial;
    private Material _cyanMaterial;
    private Material _coralMaterial;
    private Material _goldMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<WorldStyler>() != null) return;
        new GameObject("WorldStyler").AddComponent<WorldStyler>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildPalette();
        ConfigureAtmosphere();
    }

    void Start()
    {
        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 140f;
            camera.backgroundColor = new Color(0.025f, 0.045f, 0.075f);
        }

        GameObject floor = GameObject.Find("Plane");
        Renderer floorRenderer = floor != null ? floor.GetComponent<Renderer>() : null;
        if (floorRenderer != null)
            floorRenderer.sharedMaterial = _deepStructureMaterial;

        BuildStartDeck();
    }

    public void DecorateSegment(GameObject segment, TrackSegmentType segmentType)
    {
        if (segment == null || segment.transform.Find("EchoEnvironment") != null)
            return;

        GameObject environment = new GameObject("EchoEnvironment");
        environment.transform.SetParent(segment.transform, false);

        if (segmentType == TrackSegmentType.Straight)
            BuildStraightEnvironment(environment.transform, segment.GetInstanceID());
        else
            BuildTurnEnvironment(environment.transform, segmentType);
    }

    private void ConfigureAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.055f, 0.11f, 0.15f);
        RenderSettings.fogStartDistance = 38f;
        RenderSettings.fogEndDistance = 118f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.22f, 0.31f, 0.38f);
        RenderSettings.ambientEquatorColor = new Color(0.08f, 0.15f, 0.19f);
        RenderSettings.ambientGroundColor = new Color(0.025f, 0.035f, 0.055f);
        RenderSettings.reflectionIntensity = 0.25f;

        Shader skyShader = Shader.Find("Skybox/Procedural");
        if (skyShader == null) return;

        Material sky = new Material(skyShader) { name = "EchoSky_Runtime" };
        sky.SetColor("_SkyTint", new Color(0.12f, 0.27f, 0.32f));
        sky.SetColor("_GroundColor", new Color(0.018f, 0.028f, 0.05f));
        sky.SetFloat("_AtmosphereThickness", 0.72f);
        sky.SetFloat("_SunSize", 0.018f);
        sky.SetFloat("_Exposure", 0.75f);
        RenderSettings.skybox = sky;
    }

    private void BuildPalette()
    {
        _structureMaterial = MakeMaterial("EchoStructure",
            new Color(0.12f, 0.17f, 0.22f), Color.black, 0.55f, 0.7f);
        _deepStructureMaterial = MakeMaterial("EchoDepth",
            new Color(0.035f, 0.055f, 0.085f), Color.black, 0.15f, 0.35f);
        _cyanMaterial = MakeMaterial("EchoCyan",
            new Color(0.05f, 0.72f, 0.75f), new Color(0.02f, 1.1f, 1.25f), 0.2f, 0.6f);
        _coralMaterial = MakeMaterial("EchoCoral",
            new Color(0.95f, 0.26f, 0.22f), new Color(1.15f, 0.1f, 0.04f), 0.15f, 0.5f);
        _goldMaterial = MakeMaterial("EchoGold",
            new Color(1f, 0.69f, 0.12f), new Color(1.25f, 0.48f, 0.02f), 0.65f, 0.78f);
    }

    private void BuildStraightEnvironment(Transform parent, int seed)
    {
        CreateCube("LeftIsland", parent, new Vector3(-11.2f, -1.2f, 0f),
            new Vector3(6.2f, 1.7f, 19.4f), _deepStructureMaterial);
        CreateCube("RightIsland", parent, new Vector3(11.2f, -1.2f, 0f),
            new Vector3(6.2f, 1.7f, 19.4f), _deepStructureMaterial);

        CreateCube("LeftRail", parent, new Vector3(-7.35f, 0.22f, 0f),
            new Vector3(0.12f, 0.12f, 19.5f), _cyanMaterial);
        CreateCube("RightRail", parent, new Vector3(7.35f, 0.22f, 0f),
            new Vector3(0.12f, 0.12f, 19.5f), _cyanMaterial);

        int variant = Mathf.Abs(seed) % 3;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 3; i++)
            {
                float z = -7f + i * 7f;
                float height = 2.8f + ((i + variant + (side > 0 ? 1 : 0)) % 3) * 1.4f;
                BuildPylon(parent, side, z, height, i == variant);
            }
        }

        if (variant == 0)
            BuildSignalArch(parent, 6.5f);
        else if (variant == 1)
            BuildFloatingMarkers(parent);
        else
            BuildDataTotems(parent);
    }

    private void BuildStartDeck()
    {
        if (GameObject.Find("EchoStartDeck") != null) return;

        GameObject deck = new GameObject("EchoStartDeck");
        CreateCube("LaunchRoad", deck.transform, new Vector3(0f, -0.22f, 34f),
            new Vector3(15f, 0.28f, 78f), _structureMaterial);
        CreateCube("LaunchRailL", deck.transform, new Vector3(-7.35f, 0.12f, 34f),
            new Vector3(0.12f, 0.12f, 78f), _cyanMaterial);
        CreateCube("LaunchRailR", deck.transform, new Vector3(7.35f, 0.12f, 34f),
            new Vector3(0.12f, 0.12f, 78f), _goldMaterial);
        CreateCube("LaunchIslandL", deck.transform, new Vector3(-12f, -1.15f, 34f),
            new Vector3(7f, 1.7f, 76f), _deepStructureMaterial);
        CreateCube("LaunchIslandR", deck.transform, new Vector3(12f, -1.15f, 34f),
            new Vector3(7f, 1.7f, 76f), _deepStructureMaterial);

        BuildSignalArch(deck.transform, 22f);
        for (int side = -1; side <= 1; side += 2)
        {
            BuildPylon(deck.transform, side, 8f, 4.2f, side > 0);
            BuildPylon(deck.transform, side, 35f, 7.2f, side < 0);
            BuildPylon(deck.transform, side, 58f, 9.5f, side > 0);
        }
    }

    private void BuildTurnEnvironment(Transform parent, TrackSegmentType segmentType)
    {
        int direction = segmentType == TrackSegmentType.TurnRight ? 1 : -1;
        CreateCube("CornerIsland", parent,
            new Vector3(-direction * 10.5f, -1.25f, 10f),
            new Vector3(6f, 1.8f, 19f), _deepStructureMaterial);
        CreateCube("CornerBeacon", parent,
            new Vector3(-direction * 7.8f, 2.2f, 10f),
            new Vector3(1.1f, 5.5f, 1.1f), _structureMaterial);
        CreateCube("CornerBeaconLight", parent,
            new Vector3(-direction * 7.8f, 5.1f, 10f),
            new Vector3(1.35f, 0.18f, 1.35f), _coralMaterial);
        CreateCube("TurnEntryRail", parent,
            new Vector3(-direction * 7.35f, 0.22f, 7f),
            new Vector3(0.12f, 0.12f, 13.5f), _cyanMaterial);
        CreateCube("TurnExitRail", parent,
            new Vector3(direction * 7f, 0.22f, 17.35f),
            new Vector3(13.5f, 0.12f, 0.12f), _goldMaterial);
    }

    private void BuildPylon(Transform parent, int side, float z, float height, bool accent)
    {
        float x = side * (10.2f + (Mathf.Abs(z) < 1f ? 0.8f : 0f));
        CreateCube("Pylon", parent, new Vector3(x, height * 0.5f - 0.25f, z),
            new Vector3(1.25f, height, 1.25f), _structureMaterial);
        CreateCube("PylonCap", parent, new Vector3(x, height + 0.05f, z),
            new Vector3(1.55f, 0.16f, 1.55f), accent ? _coralMaterial : _cyanMaterial);
        CreateCube("PylonSlot", parent,
            new Vector3(x - side * 0.64f, height * 0.58f, z),
            new Vector3(0.06f, height * 0.35f, 0.42f), accent ? _goldMaterial : _cyanMaterial);
    }

    private void BuildSignalArch(Transform parent, float z)
    {
        CreateCube("ArchLeft", parent, new Vector3(-6.2f, 3.1f, z),
            new Vector3(0.45f, 6.2f, 0.45f), _structureMaterial);
        CreateCube("ArchRight", parent, new Vector3(6.2f, 3.1f, z),
            new Vector3(0.45f, 6.2f, 0.45f), _structureMaterial);
        CreateCube("ArchBeam", parent, new Vector3(0f, 6.05f, z),
            new Vector3(12.8f, 0.42f, 0.42f), _structureMaterial);
        CreateCube("ArchSignal", parent, new Vector3(0f, 5.78f, z - 0.25f),
            new Vector3(3.3f, 0.12f, 0.12f), _coralMaterial);
    }

    private void BuildFloatingMarkers(Transform parent)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            CreateCube("FloatMarker", parent, new Vector3(side * 12f, 6f, 1f),
                new Vector3(0.8f, 3.8f, 0.8f), _structureMaterial, new Vector3(0f, 0f, side * 18f));
            CreateCube("FloatLight", parent, new Vector3(side * 12f, 6f, 0.55f),
                new Vector3(0.18f, 2.3f, 0.08f), side > 0 ? _goldMaterial : _cyanMaterial,
                new Vector3(0f, 0f, side * 18f));
        }
    }

    private void BuildDataTotems(Transform parent)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 2; i++)
            {
                float z = -4f + i * 9f;
                CreateCube("DataTotem", parent, new Vector3(side * 13.2f, 2.2f + i, z),
                    new Vector3(2.4f, 4.4f + i * 2f, 2.4f), _structureMaterial,
                    new Vector3(0f, side * (12f + i * 7f), 0f));
                CreateCube("DataBand", parent, new Vector3(side * 13.2f, 3.2f + i, z),
                    new Vector3(2.55f, 0.16f, 2.55f), i == 0 ? _coralMaterial : _cyanMaterial,
                    new Vector3(0f, side * (12f + i * 7f), 0f));
            }
        }
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 position,
        Vector3 scale, Material material, Vector3 euler = default)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent, false);
        cube.transform.localPosition = position;
        cube.transform.localEulerAngles = euler;
        cube.transform.localScale = scale;
        Collider collider = cube.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = cube.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return cube;
    }

    private static Material MakeMaterial(string name, Color color, Color emission,
        float metallic, float smoothness)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Mobile/Diffuse");
        Material material = new Material(shader) { name = name };
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (emission.maxColorComponent > 0f && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }
        return material;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
