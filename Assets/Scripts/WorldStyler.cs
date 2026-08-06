using UnityEngine;

public class WorldStyler : MonoBehaviour
{
    public static WorldStyler Instance { get; private set; }

    private Material _structureMaterial;
    private Material _deepStructureMaterial;
    private Material _cyanMaterial;
    private Material _coralMaterial;
    private Material _goldMaterial;
    private Material _skyMaterial;

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
            camera.clearFlags = _skyMaterial != null
                ? CameraClearFlags.Skybox
                : CameraClearFlags.SolidColor;
            camera.farClipPlane = 140f;
            camera.fieldOfView = 56f;
            camera.backgroundColor = new Color(0.16f, 0.22f, 0.31f);

            CameraFollow follow = camera.GetComponent<CameraFollow>();
            if (follow != null) follow.offset = new Vector3(0f, 4.6f, -8.2f);
        }

        ConfigureLighting();

        GameObject floor = GameObject.Find("Plane");
        Renderer floorRenderer = floor != null ? floor.GetComponent<Renderer>() : null;
        if (floorRenderer != null)
            floorRenderer.sharedMaterial = _deepStructureMaterial;

        BuildStartDeck();
        StyleCharacter();
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
        RenderSettings.fogColor = new Color(0.27f, 0.36f, 0.49f);
        RenderSettings.fogStartDistance = 58f;
        RenderSettings.fogEndDistance = 138f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.72f, 0.78f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.44f, 0.52f, 0.64f);
        RenderSettings.ambientGroundColor = new Color(0.18f, 0.23f, 0.31f);
        RenderSettings.reflectionIntensity = 0.62f;

        Material skyAsset = Resources.Load<Material>("Art/EchoSky");
        if (skyAsset != null)
        {
            _skyMaterial = new Material(skyAsset) { name = "EchoSky_Runtime" };
            RenderSettings.skybox = _skyMaterial;
        }
    }

    private void ConfigureLighting()
    {
        Light key = FindObjectOfType<Light>();
        if (key != null)
        {
            key.intensity = 1.34f;
            key.color = new Color(1f, 0.96f, 0.90f);
            key.shadows = LightShadows.Soft;
        }

        if (GameObject.Find("EchoFillLight") != null) return;
        GameObject fillObject = new GameObject("EchoFillLight");
        fillObject.transform.SetParent(transform, false);
        fillObject.transform.rotation = Quaternion.Euler(38f, 145f, 0f);
        Light fill = fillObject.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.72f;
        fill.color = new Color(0.66f, 0.78f, 1f);
        fill.shadows = LightShadows.None;
    }

    private void BuildPalette()
    {
        _structureMaterial = MakeMaterial("EchoStructure",
            new Color(0.43f, 0.49f, 0.58f), new Color(0.018f, 0.025f, 0.038f), 0.58f, 0.74f);
        _deepStructureMaterial = MakeMaterial("EchoDepth",
            new Color(0.16f, 0.21f, 0.29f), new Color(0.008f, 0.014f, 0.025f), 0.30f, 0.50f);
        _cyanMaterial = MakeMaterial("EchoCyan",
            new Color(0.30f, 0.62f, 0.94f), new Color(0.035f, 0.22f, 0.56f), 0.26f, 0.68f);
        _coralMaterial = MakeMaterial("EchoCoral",
            new Color(0.94f, 0.40f, 0.31f), new Color(0.52f, 0.055f, 0.025f), 0.16f, 0.52f);
        _goldMaterial = MakeMaterial("EchoGold",
            new Color(1.00f, 0.68f, 0.30f), new Color(0.54f, 0.20f, 0.018f), 0.58f, 0.76f);
    }

    private void BuildStraightEnvironment(Transform parent, int seed)
    {
        CreateCapsule("LeftIsland", parent, new Vector3(-11.2f, -1.28f, 0f),
            new Vector3(3.1f, 9.7f, 0.82f), _deepStructureMaterial,
            new Vector3(90f, 0f, 0f));
        CreateCapsule("RightIsland", parent, new Vector3(11.2f, -1.28f, 0f),
            new Vector3(3.1f, 9.7f, 0.82f), _deepStructureMaterial,
            new Vector3(90f, 0f, 0f));

        CreateBeam("LeftRail", parent, new Vector3(-7.35f, 0.24f, -9.7f),
            new Vector3(-7.35f, 0.24f, 9.7f), 0.09f, _cyanMaterial);
        CreateBeam("RightRail", parent, new Vector3(7.35f, 0.24f, -9.7f),
            new Vector3(7.35f, 0.24f, 9.7f), 0.09f, _cyanMaterial);

        int variant = Mathf.Abs(seed) % 3;
        for (int side = -1; side <= 1; side += 2)
        {
            float z = ((variant + (side > 0 ? 1 : 0)) % 2 == 0) ? -5.5f : 5.5f;
            float height = 3.2f + ((variant + (side > 0 ? 1 : 0)) % 3) * 1.35f;
            BuildPylon(parent, side, z, height, variant == 2);
        }

        if (variant == 0)
            BuildSignalArch(parent, 6.5f);
        else if (variant == 1)
            BuildTransitHalos(parent);
        else
            BuildDataTotems(parent);
    }

    private void BuildStartDeck()
    {
        if (GameObject.Find("EchoStartDeck") != null) return;

        GameObject deck = new GameObject("EchoStartDeck");
        CreateCube("LaunchRoad", deck.transform, new Vector3(0f, -0.22f, 34f),
            new Vector3(15f, 0.28f, 78f), _structureMaterial);
        CreateBeam("LaunchRailL", deck.transform, new Vector3(-7.35f, 0.16f, -5f),
            new Vector3(-7.35f, 0.16f, 73f), 0.09f, _cyanMaterial);
        CreateBeam("LaunchRailR", deck.transform, new Vector3(7.35f, 0.16f, -5f),
            new Vector3(7.35f, 0.16f, 73f), 0.09f, _goldMaterial);
        CreateCapsule("LaunchIslandL", deck.transform, new Vector3(-12f, -1.25f, 34f),
            new Vector3(3.5f, 38f, 0.82f), _deepStructureMaterial,
            new Vector3(90f, 0f, 0f));
        CreateCapsule("LaunchIslandR", deck.transform, new Vector3(12f, -1.25f, 34f),
            new Vector3(3.5f, 38f, 0.82f), _deepStructureMaterial,
            new Vector3(90f, 0f, 0f));

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
        CreateCapsule("CornerIsland", parent,
            new Vector3(-direction * 10.5f, -1.25f, 10f),
            new Vector3(3f, 9.5f, 0.86f), _deepStructureMaterial,
            new Vector3(90f, 0f, 0f));
        BuildPylonAt(parent, -direction * 7.8f, 10f, 5.4f, true);
        CreateBeam("TurnEntryRail", parent,
            new Vector3(-direction * 7.35f, 0.22f, 0.25f),
            new Vector3(-direction * 7.35f, 0.22f, 13.75f),
            0.09f, _cyanMaterial);
        CreateBeam("TurnExitRail", parent,
            new Vector3(0.25f, 0.22f, 17.35f),
            new Vector3(direction * 13.75f, 0.22f, 17.35f),
            0.09f, _goldMaterial);
    }

    private void BuildPylon(Transform parent, int side, float z, float height, bool accent)
    {
        float x = side * (11.4f + (Mathf.Abs(z) < 1f ? 0.8f : 0f));
        BuildPylonAt(parent, x, z, height, accent);
    }

    private void BuildPylonAt(Transform parent, float x, float z, float height, bool accent)
    {
        CreateCylinder("PylonFoot", parent, new Vector3(x, 0.12f, z),
            new Vector3(0.72f, 0.18f, 0.72f), _deepStructureMaterial);
        CreateCylinder("PylonBody", parent, new Vector3(x, height * 0.48f, z),
            new Vector3(0.48f, height * 0.48f, 0.48f), _structureMaterial);
        CreateCapsule("PylonCrown", parent, new Vector3(x, height + 0.05f, z),
            new Vector3(0.66f, 0.28f, 0.66f),
            accent ? _coralMaterial : _cyanMaterial);
        CreateCapsule("PylonLens", parent, new Vector3(x, height * 0.66f, z - 0.49f),
            new Vector3(0.12f, height * 0.16f, 0.08f),
            accent ? _goldMaterial : _cyanMaterial);
    }

    private void BuildSignalArch(Transform parent, float z)
    {
        BuildSegmentedArch("TransitArch", parent, z, 6.35f, 6.2f, 7,
            _structureMaterial, _cyanMaterial, 0.31f);
        CreateCapsule("ArchSignal", parent, new Vector3(0f, 6.05f, z - 0.28f),
            new Vector3(1.15f, 0.12f, 0.1f), _coralMaterial,
            new Vector3(0f, 0f, 90f));
    }

    private void BuildTransitHalos(Transform parent)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject halo = new GameObject("TransitHalo");
            halo.transform.SetParent(parent, false);
            Vector3 center = new Vector3(side * 12.2f, 4.8f, 0.5f);
            const int segments = 8;
            const float radius = 2.2f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 start = center + new Vector3(
                    Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0f);
                Vector3 end = center + new Vector3(
                    Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0f);
                CreateBeam("HaloArc", halo.transform, start, end, 0.13f,
                    i == 1 || i == 5
                        ? (side > 0 ? _goldMaterial : _cyanMaterial)
                        : _structureMaterial);
            }
            CreateCylinder("HaloPylon", halo.transform,
                new Vector3(center.x, 2.1f, center.z + 0.25f),
                new Vector3(0.30f, 2.1f, 0.30f), _deepStructureMaterial);
        }
    }

    private void BuildDataTotems(Transform parent)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 2; i++)
            {
                float z = -4f + i * 9f;
                CreateCapsule("DataTotem", parent, new Vector3(side * 13.2f, 2.2f + i, z),
                    new Vector3(1.15f, 2.2f + i, 1.15f), _structureMaterial,
                    new Vector3(0f, side * (12f + i * 7f), 0f));
                CreateCylinder("DataBand", parent, new Vector3(side * 13.2f, 3.2f + i, z),
                    new Vector3(1.28f, 0.09f, 1.28f),
                    i == 0 ? _coralMaterial : _cyanMaterial);
            }
        }
    }

    public void StyleCoin(GameObject coin)
    {
        if (coin == null || coin.transform.Find("StreamlinedVisual") != null) return;
        DisableRenderers(coin);
        GameObject visual = new GameObject("StreamlinedVisual");
        visual.transform.SetParent(coin.transform, false);

        // Three intersecting meshes create a complete double-sided token without
        // multiplying the renderer count along long coin trails.
        CreateCylinder("TokenRim", visual.transform, Vector3.zero,
            new Vector3(0.84f, 0.11f, 0.84f), _goldMaterial,
            new Vector3(90f, 0f, 0f));
        CreateSphere("TokenInset", visual.transform, Vector3.zero,
            new Vector3(0.62f, 0.62f, 0.27f), _deepStructureMaterial);
        CreateSphere("EnergyCore", visual.transform, Vector3.zero,
            new Vector3(0.27f, 0.27f, 0.32f), _cyanMaterial);
    }

    public void StyleObstacle(GameObject obstacle)
    {
        if (obstacle == null || obstacle.transform.Find("StreamlinedVisual") != null) return;
        Obstacle data = obstacle.GetComponent<Obstacle>();
        if (data == null) return;

        DisableRenderers(obstacle);
        GameObject visual = new GameObject("StreamlinedVisual");
        visual.transform.SetParent(obstacle.transform, false);

        if (data.type == ObstacleType.Low)
        {
            BuildSlideDrone(visual.transform);
        }
        else if (data.type == ObstacleType.High)
        {
            BuildJumpBlock(visual.transform);
        }
        else
        {
            BuildLaneBulkhead(visual.transform);
        }
    }

    private void BuildSlideDrone(Transform parent)
    {
        // A compact hovering machine replaces the old rail. Its solid outline
        // matches the gameplay collider and leaves a clear slide gap below it.
        CreateCube("SlideDroneBody", parent,
            new Vector3(0f, 0.95f, 0f),
            new Vector3(2.80f, 0.82f, 1.05f), _structureMaterial);
        CreateCube("SlideDroneFace", parent,
            new Vector3(0f, 0.95f, -0.56f),
            new Vector3(2.28f, 0.43f, 0.10f), _deepStructureMaterial);
        CreateSphere("SlideDronePodL", parent,
            new Vector3(-1.24f, 0.95f, 0f),
            new Vector3(0.55f, 0.66f, 1.18f), _deepStructureMaterial);
        CreateSphere("SlideDronePodR", parent,
            new Vector3(1.24f, 0.95f, 0f),
            new Vector3(0.55f, 0.66f, 1.18f), _deepStructureMaterial);
        CreateCube("SlideDroneSignalL", parent,
            new Vector3(-0.42f, 0.95f, -0.63f),
            new Vector3(0.13f, 0.32f, 0.06f), _cyanMaterial,
            new Vector3(0f, 0f, -30f));
        CreateCube("SlideDroneSignalR", parent,
            new Vector3(0.42f, 0.95f, -0.63f),
            new Vector3(0.13f, 0.32f, 0.06f), _cyanMaterial,
            new Vector3(0f, 0f, 30f));
    }

    private void BuildJumpBlock(Transform parent)
    {
        // One unbroken low body matches the full-width jump collider. There are
        // no joints, floating nodes or diagonal gaps that look traversable.
        CreateCapsule("JumpBlockBody", parent,
            new Vector3(0f, -0.45f, 0f),
            new Vector3(0.72f, 1.58f, 0.52f), _structureMaterial,
            new Vector3(0f, 0f, 90f));
        CreateCapsule("JumpBlockInset", parent,
            new Vector3(0f, -0.45f, -0.45f),
            new Vector3(0.46f, 1.39f, 0.08f), _deepStructureMaterial,
            new Vector3(0f, 0f, 90f));
        CreateCapsule("JumpBlockSignal", parent,
            new Vector3(0f, -0.45f, -0.55f),
            new Vector3(0.11f, 1.20f, 0.045f), _coralMaterial,
            new Vector3(0f, 0f, 90f));
    }

    private void BuildLaneBulkhead(Transform parent)
    {
        Vector3 center = new Vector3(0f, 0.25f, 0f);
        // Sphere scale is the rendered diameter. Match the 3.4 x 2.7 collider
        // instead of the previous half-width panel.
        CreateSphere("LaneBulkheadBody", parent, center,
            new Vector3(3.30f, 2.62f, 0.58f), _structureMaterial);
        CreateSphere("LaneBulkheadInset", parent,
            center + new Vector3(0f, 0f, -0.52f),
            new Vector3(2.82f, 2.16f, 0.18f), _deepStructureMaterial);
        CreateCapsule("LaneBulkheadCore", parent,
            center + new Vector3(0f, 0f, -0.67f),
            new Vector3(0.18f, 0.78f, 0.045f), _cyanMaterial);
        CreateCapsule("LaneBulkheadRailL", parent,
            center + new Vector3(-0.72f, 0f, -0.65f),
            new Vector3(0.10f, 0.72f, 0.045f), _goldMaterial,
            new Vector3(0f, 0f, -18f));
        CreateCapsule("LaneBulkheadRailR", parent,
            center + new Vector3(0.72f, 0f, -0.65f),
            new Vector3(0.10f, 0.72f, 0.045f), _goldMaterial,
            new Vector3(0f, 0f, 18f));
    }

    private void StyleCharacter()
    {
        GameObject player = GameObject.Find("player");
        if (player == null) return;
        Transform model = player.transform.Find("CharacterModel");
        if (model == null || model.Find("StreamlinedSuit") != null) return;

        string[] boxParts = { "ChestPlate", "ChestSignal", "SignalBelt", "HelmetBand", "Mouth" };
        for (int i = 0; i < boxParts.Length; i++)
        {
            Transform part = model.Find(boxParts[i]);
            if (part != null)
            {
                Renderer renderer = part.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;
            }
        }

        GameObject suit = new GameObject("StreamlinedSuit");
        suit.transform.SetParent(model, false);
        CreateSphere("ChestShell", suit.transform, new Vector3(0f, 1.43f, 0.42f),
            new Vector3(0.48f, 0.48f, 0.12f), _structureMaterial);
        CreateCapsule("ChestSignal", suit.transform, new Vector3(0f, 1.43f, 0.55f),
            new Vector3(0.07f, 0.22f, 0.035f), _goldMaterial,
            new Vector3(0f, 0f, 90f));
        CreateCylinder("SuitRing", suit.transform, new Vector3(0f, 1.03f, 0f),
            new Vector3(0.58f, 0.05f, 0.45f), _cyanMaterial);
        CreateSphere("HelmetShell", suit.transform, new Vector3(0f, 2.16f, -0.17f),
            new Vector3(0.43f, 0.25f, 0.29f), _structureMaterial);
    }

    private static void DisableRenderers(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;
    }

    private static void BuildSegmentedArch(string name, Transform parent, float z,
        float halfWidth, float height, int segments, Material body, Material accent,
        float radius)
    {
        GameObject arch = new GameObject(name);
        arch.transform.SetParent(parent, false);
        Vector3 previous = new Vector3(-halfWidth, 0f, z);
        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float x = Mathf.Lerp(-halfWidth, halfWidth, t);
            float normalized = x / halfWidth;
            float y = height * Mathf.Sqrt(Mathf.Max(0f, 1f - normalized * normalized));
            Vector3 current = new Vector3(x, y, z);
            CreateBeam("ArcJoint", arch.transform, previous, current, radius,
                (i == 2 || i == segments - 1) ? accent : body);
            CreateSphere("ArcNode", arch.transform, current,
                Vector3.one * radius * 1.22f, body);
            previous = current;
        }
    }

    private static GameObject CreateBeam(string name, Transform parent, Vector3 start,
        Vector3 end, float radius, Material material)
    {
        Vector3 delta = end - start;
        GameObject beam = CreateCylinder(name, parent, (start + end) * 0.5f,
            new Vector3(radius, delta.magnitude * 0.5f, radius), material);
        if (delta.sqrMagnitude > 0.0001f)
            beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
        return beam;
    }

    private static GameObject CreateCapsule(string name, Transform parent, Vector3 position,
        Vector3 scale, Material material, Vector3 euler = default)
    {
        return CreatePrimitive(name, PrimitiveType.Capsule, parent, position, scale, material, euler);
    }

    private static GameObject CreateCylinder(string name, Transform parent, Vector3 position,
        Vector3 scale, Material material, Vector3 euler = default)
    {
        return CreatePrimitive(name, PrimitiveType.Cylinder, parent, position, scale, material, euler);
    }

    private static GameObject CreateSphere(string name, Transform parent, Vector3 position,
        Vector3 scale, Material material, Vector3 euler = default)
    {
        return CreatePrimitive(name, PrimitiveType.Sphere, parent, position, scale, material, euler);
    }

    private static GameObject CreateCube(string name, Transform parent, Vector3 position,
        Vector3 scale, Material material, Vector3 euler = default)
    {
        return CreatePrimitive(name, PrimitiveType.Cube, parent, position, scale, material, euler);
    }

    private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent,
        Vector3 position, Vector3 scale, Material material, Vector3 euler = default)
    {
        GameObject primitive = GameObject.CreatePrimitive(type);
        primitive.name = name;
        primitive.transform.SetParent(parent, false);
        primitive.transform.localPosition = position;
        primitive.transform.localEulerAngles = euler;
        primitive.transform.localScale = scale;
        Collider collider = primitive.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        Renderer renderer = primitive.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return primitive;
    }

    private static Material MakeMaterial(string name, Color color, Color emission,
        float metallic, float smoothness)
    {
        Shader shader = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
            ? Shader.Find("Universal Render Pipeline/Lit")
            : Shader.Find("Standard");
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
        if (_skyMaterial != null) Destroy(_skyMaterial);
        if (Instance == this) Instance = null;
    }
}
