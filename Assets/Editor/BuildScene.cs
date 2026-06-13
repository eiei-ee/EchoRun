using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BuildScene
{
    // Cached shader found on first use
    private static Shader _cachedShader;

    [MenuItem("Tools/Build Scene")]
    public static void Build()
    {
        Debug.Log("=== BUILD SCENE START ===");

        // Detect render pipeline and find working shader first
        DetectRenderPipeline();

        AddTag("Coin");
        AddTag("Obstacle");
        AddLayer("Ground");

        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/Materials");

        CreateCharacterMaterials();
        EnsureMainCamera();
        CreatePlayer();
        CreateGroundPlane();
        CreateManagers();
        ConfigurePlayer();
        ConfigureGround();
        CreateCameraFollow();
        CreateLighting();
        CreateCoinPrefab();
        CreateObstaclePrefab();
        CreateTrackSegmentPrefab();
        CreateTurnSegmentPrefabs();
        ConfigureTrackManager();
        CreateUICanvas();

       AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
       AssetDatabase.Refresh();
        Debug.Log("=== BUILD COMPLETE — Save scene (Ctrl+S), then Play ===");
    }

    // ── render pipeline detection ──────────────────────

    static void DetectRenderPipeline()
    {
        var rpAsset = UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset;
        string rpName = rpAsset != null ? rpAsset.GetType().Name : "Built-in Render Pipeline";
        Debug.Log($"Detected render pipeline: {rpName}");

        // Try to find a working shader
        _cachedShader = Shader.Find("Universal Render Pipeline/Lit");
        if (_cachedShader != null) { Debug.Log("Using URP/Lit shader"); return; }

        _cachedShader = Shader.Find("Standard");
        if (_cachedShader != null) { Debug.Log("Using Standard shader"); return; }

        _cachedShader = Shader.Find("Mobile/Diffuse");
        if (_cachedShader != null) { Debug.Log("Using Mobile/Diffuse shader"); return; }

        // Last resort: get from a primitive
        GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        _cachedShader = tmp.GetComponent<MeshRenderer>().sharedMaterial.shader;
        Object.DestroyImmediate(tmp);
        Debug.Log($"Using shader from primitive: {(_cachedShader != null ? _cachedShader.name : "NULL")}");
    }

    // ── character model builders ──────────────────────

    static GameObject CreateHumanoidCharacterModel(GameObject player, GameObject prefab)
    {
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, player.transform);
        model.name = "CharacterModel";
        model.transform.localPosition = new Vector3(0, -0.55f, 0);

        // Ensure CharacterAnimator has useHumanoidRig enabled
        CharacterAnimator ca = model.GetComponent<CharacterAnimator>();
        if (ca == null) ca = model.AddComponent<CharacterAnimator>();
        SerializedObject caSo = new SerializedObject(ca);
        caSo.FindProperty("useHumanoidRig").boolValue = true;
        caSo.ApplyModifiedProperties();

        // Adjust capsule collider for humanoid proportions
        CapsuleCollider cc = player.GetComponent<CapsuleCollider>();
        if (cc != null) { cc.height = 2f; cc.radius = 0.35f; }

        Debug.Log("Using humanoid character model");
        return model;
    }

    static GameObject CreateProceduralCharacterModel(GameObject player)
    {
        // Materials
        Material skinMat  = LoadOrMakeMat("CharacterSkinMat",  new Color(0.91f, 0.72f, 0.55f)); // peach
        Material clothMat = LoadOrMakeMat("CharacterClothMat", new Color(0.17f, 0.24f, 0.31f)); // dark blue-gray shirt
        Material pantsMat = LoadOrMakeMat("CharacterPantsMat", new Color(0.13f, 0.18f, 0.25f)); // darker pants
        Material shoeMat  = LoadOrMakeMat("CharacterShoeMat",  new Color(0.25f, 0.18f, 0.12f)); // brown shoes
        Material eyeMat   = LoadOrMakeMat("CharacterEyeMat",   Color.white);
        Material pupilMat = LoadOrMakeMat("CharacterPupilMat", Color.black);

        GameObject model = new GameObject("CharacterModel");
        model.transform.SetParent(player.transform);
        model.transform.localPosition = new Vector3(0, -1f, 0);
        model.AddComponent<CharacterAnimator>();

        // Helper: create part, strip collider, assign material
        System.Func<string, PrimitiveType, Vector3, Vector3, Material, GameObject> P =
            (name, shape, pos, scale, mat) =>
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (mat != null) go.GetComponent<MeshRenderer>().material = mat;
            return go;
        };

        // ── Body ──
        GameObject pelvis = P("Pelvis", PrimitiveType.Capsule,
            new Vector3(0, 0.95f, 0), new Vector3(0.55f, 0.28f, 0.4f), pantsMat);
        pelvis.transform.SetParent(model.transform, false);

        GameObject torso = P("Torso", PrimitiveType.Capsule,
            new Vector3(0, 1.35f, 0), new Vector3(0.7f, 0.5f, 0.55f), clothMat);
        torso.transform.SetParent(model.transform, false);

        GameObject neck = P("Neck", PrimitiveType.Capsule,
            new Vector3(0, 1.85f, 0), new Vector3(0.16f, 0.12f, 0.16f), skinMat);
        neck.transform.SetParent(model.transform, false);

        // ── Head + face ──
        GameObject head = P("Head", PrimitiveType.Sphere,
            new Vector3(0, 2.08f, 0), new Vector3(0.42f, 0.42f, 0.42f), skinMat);
        head.transform.SetParent(model.transform, false);

        // Eyes (on front of head = Z+)
        foreach (float sx in new[] { -0.12f, 0.12f })
        {
            GameObject eye = P(sx < 0 ? "Eye_L" : "Eye_R", PrimitiveType.Sphere,
                new Vector3(sx, 2.1f, 0.35f), new Vector3(0.08f, 0.08f, 0.04f), eyeMat);
            eye.transform.SetParent(model.transform, false);

            GameObject pupil = P(sx < 0 ? "Pupil_L" : "Pupil_R", PrimitiveType.Sphere,
                new Vector3(0, 0, 0.45f), new Vector3(0.5f, 0.5f, 0.15f), pupilMat);
            pupil.transform.SetParent(eye.transform, false);
        }

        // Mouth
        GameObject mouth = P("Mouth", PrimitiveType.Cube,
            new Vector3(0, 1.98f, 0.41f), new Vector3(0.16f, 0.03f, 0.02f), pupilMat);
        mouth.transform.SetParent(model.transform, false);

        // ── Arms (capsules, skin color for lower, cloth for upper) ──
        float shldY = 1.7f;
        GameObject armUpperL = P("Arm_Upper_L", PrimitiveType.Capsule,
            new Vector3(-0.58f, shldY, 0), new Vector3(0.13f, 0.38f, 0.13f), clothMat);
        armUpperL.transform.SetParent(model.transform, false);
        GameObject armLowerL = P("Arm_Lower_L", PrimitiveType.Capsule,
            new Vector3(0, -0.42f, 0), new Vector3(0.11f, 0.34f, 0.11f), skinMat);
        armLowerL.transform.SetParent(armUpperL.transform, false);
        GameObject handL = P("Hand_L", PrimitiveType.Sphere,
            new Vector3(0, -0.38f, 0), new Vector3(0.1f, 0.1f, 0.1f), skinMat);
        handL.transform.SetParent(armLowerL.transform, false);

        GameObject armUpperR = P("Arm_Upper_R", PrimitiveType.Capsule,
            new Vector3(0.58f, shldY, 0), new Vector3(0.13f, 0.38f, 0.13f), clothMat);
        armUpperR.transform.SetParent(model.transform, false);
        GameObject armLowerR = P("Arm_Lower_R", PrimitiveType.Capsule,
            new Vector3(0, -0.42f, 0), new Vector3(0.11f, 0.34f, 0.11f), skinMat);
        armLowerR.transform.SetParent(armUpperR.transform, false);
        GameObject handR = P("Hand_R", PrimitiveType.Sphere,
            new Vector3(0, -0.38f, 0), new Vector3(0.1f, 0.1f, 0.1f), skinMat);
        handR.transform.SetParent(armLowerR.transform, false);

        // ── Legs (capsules) ──
        GameObject legUpperL = P("Leg_Upper_L", PrimitiveType.Capsule,
            new Vector3(-0.16f, 0.7f, 0), new Vector3(0.18f, 0.32f, 0.18f), pantsMat);
        legUpperL.transform.SetParent(model.transform, false);
        GameObject legLowerL = P("Leg_Lower_L", PrimitiveType.Capsule,
            new Vector3(0, -0.36f, 0), new Vector3(0.15f, 0.32f, 0.15f), skinMat);
        legLowerL.transform.SetParent(legUpperL.transform, false);
        GameObject footL = P("Foot_L", PrimitiveType.Cube,
            new Vector3(0, -0.36f, 0.08f), new Vector3(0.18f, 0.09f, 0.32f), shoeMat);
        footL.transform.SetParent(legLowerL.transform, false);

        GameObject legUpperR = P("Leg_Upper_R", PrimitiveType.Capsule,
            new Vector3(0.16f, 0.7f, 0), new Vector3(0.18f, 0.32f, 0.18f), pantsMat);
        legUpperR.transform.SetParent(model.transform, false);
        GameObject legLowerR = P("Leg_Lower_R", PrimitiveType.Capsule,
            new Vector3(0, -0.36f, 0), new Vector3(0.15f, 0.32f, 0.15f), skinMat);
        legLowerR.transform.SetParent(legUpperR.transform, false);
        GameObject footR = P("Foot_R", PrimitiveType.Cube,
            new Vector3(0, -0.36f, 0.08f), new Vector3(0.18f, 0.09f, 0.32f), shoeMat);
        footR.transform.SetParent(legLowerR.transform, false);

        // ── Wire CharacterAnimator ──
        CharacterAnimator anim = model.GetComponent<CharacterAnimator>();
        SerializedObject animSo = new SerializedObject(anim);
        animSo.FindProperty("leftUpperArm").objectReferenceValue = armUpperL.transform;
        animSo.FindProperty("rightUpperArm").objectReferenceValue = armUpperR.transform;
        animSo.FindProperty("leftUpperLeg").objectReferenceValue = legUpperL.transform;
        animSo.FindProperty("rightUpperLeg").objectReferenceValue = legUpperR.transform;
        animSo.FindProperty("leftFoot").objectReferenceValue = footL.transform;
        animSo.FindProperty("rightFoot").objectReferenceValue = footR.transform;
        animSo.FindProperty("bodyTransform").objectReferenceValue = torso.transform;
        animSo.ApplyModifiedProperties();

        Debug.Log("Using procedural character model");
        return model;
    }

    static Material LoadOrMakeMat(string name, Color color)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(
            $"Assets/Prefabs/Materials/{name}.mat");
        if (mat != null) return mat;
        return CreateMaterial(name, color);
    }

    static Material CreateMaterial(string name, Color color)
    {
        if (_cachedShader == null)
        {
            Debug.LogError($"No shader found! Cannot create material {name}");
            return null;
        }

        string matPath = $"Assets/Prefabs/Materials/{name}.mat";
        DeleteAssetAtPath(matPath);

        Material mat = new Material(_cachedShader);
        mat.color = color;
        AssetDatabase.CreateAsset(mat, matPath);
        Debug.Log($"Created material: {name} ({_cachedShader.name})");
        return mat;
    }

    // ── materials ──────────────────────────────────────

    static void CreateCharacterMaterials()
    {
        CreateMaterial("CharacterSkinMat", new Color(0.91f, 0.72f, 0.55f));  // peach skin
        CreateMaterial("CharacterClothMat", new Color(0.17f, 0.24f, 0.31f)); // dark blue-gray
    }

    // ── scene objects ──────────────────────────────────

    static void EnsureMainCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            cam = camGo.AddComponent<Camera>();
            camGo.tag = "MainCamera";
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.12f, 0.18f);
        EditorUtility.SetDirty(cam.gameObject);
    }

    static void CreatePlayer()
    {
        GameObject player = GameObject.Find("player");
        if (player == null)
        {
            player = new GameObject("player");
            player.transform.position = new Vector3(0, 1.0f, 0);
        }

        // Clear old visuals if re-running
        foreach (Transform child in player.transform)
            Object.DestroyImmediate(child.gameObject);
        foreach (var c in player.GetComponents<Collider>())
            Object.DestroyImmediate(c);
        MeshRenderer oldMR = player.GetComponent<MeshRenderer>();
        if (oldMR != null) Object.DestroyImmediate(oldMR);
        MeshFilter oldMF = player.GetComponent<MeshFilter>();
        if (oldMF != null) Object.DestroyImmediate(oldMF);

        // Rigidbody
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) rb = player.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // CapsuleCollider
        CapsuleCollider cc = player.AddComponent<CapsuleCollider>();
        cc.height = 2.2f;
        cc.radius = 0.4f;
        cc.center = new Vector3(0, 1.0f, 0);

        // PlayerController
        if (player.GetComponent<PlayerController>() == null)
            player.AddComponent<PlayerController>();

        // ── Character Model ──
        GameObject humanoidPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Models/HumanoidCharacter.prefab");

        GameObject model;
        if (humanoidPrefab != null)
            model = CreateHumanoidCharacterModel(player, humanoidPrefab);
        else
            model = CreateProceduralCharacterModel(player);

        // Wire characterModel on PlayerController
        PlayerController pc = player.GetComponent<PlayerController>();
        SerializedObject pcSo = new SerializedObject(pc);
        pcSo.FindProperty("characterModel").objectReferenceValue = model.transform;
        pcSo.ApplyModifiedProperties();

        EditorUtility.SetDirty(player);
    }

    static void CreateGroundPlane()
    {
        GameObject plane = GameObject.Find("Plane");
        if (plane == null)
        {
            plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "Plane";
        }

        int layer = LayerMask.NameToLayer("Ground");
        plane.layer = layer;
        plane.transform.position = new Vector3(0, -0.1f, 50f);
        plane.transform.localScale = new Vector3(20f, 1f, 20f);

        // Remove MeshCollider (expensive, and we'll use BoxCollider instead)
        MeshCollider mc = plane.GetComponent<MeshCollider>();
        if (mc != null) Object.DestroyImmediate(mc);

        // Replace with large, efficient BoxCollider
        BoxCollider bc = plane.GetComponent<BoxCollider>();
        if (bc == null) bc = plane.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0, 0);
        bc.size = new Vector3(300f, 1f, 300f);

        // Add GroundFollower to keep ground under player
        GroundFollower gf = plane.GetComponent<GroundFollower>();
        if (gf == null) gf = plane.AddComponent<GroundFollower>();

        Material mat = CreateMaterial("GroundMat", new Color(0.2f, 0.22f, 0.28f));
        if (mat != null) plane.GetComponent<MeshRenderer>().material = mat;

        EditorUtility.SetDirty(plane);
    }

    static void CreateManagers()
    {
        EnsureManager("GameManager", typeof(GameManager));
        EnsureManager("InputManager", typeof(InputManager));
       EnsureManager("TrackManager", typeof(TrackManager));
       EnsureManager("UIManager", typeof(UIManager));
       EnsureManager("AudioManager", typeof(AudioManager));
       EnsureManager("ParticleManager", typeof(ParticleManager));
        EnsureManager("HUDOverlay", typeof(HUDOverlay));
   }

    static void EnsureManager(string name, System.Type comp)
    {
        GameObject go = GameObject.Find(name);
        if (go == null) go = new GameObject(name);
        if (go.GetComponent(comp) == null) go.AddComponent(comp);
        EditorUtility.SetDirty(go);
    }

    // ── configure ──────────────────────────────────────

    static void ConfigurePlayer()
    {
        GameObject player = GameObject.Find("player");
        if (player == null) return;

        int layer = LayerMask.NameToLayer("Ground");

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            SerializedObject so = new SerializedObject(pc);
            so.FindProperty("groundLayer").intValue = 1 << layer;
            so.ApplyModifiedProperties();
        }

        EditorUtility.SetDirty(player);
    }

    static void ConfigureGround()
    {
        GameObject plane = GameObject.Find("Plane");
        if (plane == null) return;

        int layer = LayerMask.NameToLayer("Ground");
        plane.layer = layer;

        // Wire GroundFollower to player
        GroundFollower gf = plane.GetComponent<GroundFollower>();
        if (gf != null)
        {
            GameObject player = GameObject.Find("player");
            if (player != null) gf.player = player.transform;
        }

        EditorUtility.SetDirty(plane);
    }

    // ── camera ────────────────────────────────────────

    static void CreateCameraFollow()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        CameraFollow cf = cam.GetComponent<CameraFollow>();
        if (cf == null) cf = cam.gameObject.AddComponent<CameraFollow>();

        GameObject player = GameObject.Find("player");
        if (player != null) cf.target = player.transform;

        cf.offset = new Vector3(0, 4f, -7f);
        cf.smoothSpeed = 8f;

        EditorUtility.SetDirty(cam.gameObject);
    }

    static void CreateLighting()
    {
        Light sun = Object.FindObjectOfType<Light>();
        if (sun == null)
        {
            GameObject lightGo = new GameObject("Directional Light");
            sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.intensity = 1.5f;
        sun.color = Color.white;
        sun.shadows = LightShadows.Hard;
        sun.transform.rotation = Quaternion.Euler(50, -30, 0);
        EditorUtility.SetDirty(sun.gameObject);
    }

    // ── prefabs ────────────────────────────────────────

    static void CreateCoinPrefab()
    {
        string path = "Assets/Prefabs/Coin.prefab";
        DeleteAssetAtPath(path);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Coin";
        go.tag = "Coin";
        go.transform.localScale = new Vector3(0.8f, 0.5f, 0.8f);
        go.GetComponent<Collider>().isTrigger = true;
        go.AddComponent<Coin>();

        Material mat = CreateMaterial("CoinMat", Color.yellow);
        if (mat != null) go.GetComponent<MeshRenderer>().material = mat;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static void CreateObstaclePrefab()
    {
        CreateObstacleType("Assets/Prefabs/Obstacle_Low.prefab", "Obstacle_Low",
            new Vector3(3f, 1.0f, 0.6f), ObstacleType.Low, new Color(1f, 0.45f, 0.1f));
        CreateObstacleType("Assets/Prefabs/Obstacle_High.prefab", "Obstacle_High",
            new Vector3(0.8f, 3.5f, 0.6f), ObstacleType.High, new Color(0.85f, 0.15f, 0.05f));
        CreateObstacleType("Assets/Prefabs/Obstacle_Barrier.prefab", "Obstacle_Barrier",
            new Vector3(3.5f, 2.5f, 0.8f), ObstacleType.Barrier, new Color(0.9f, 0.25f, 0.15f));
    }

    static void CreateObstacleType(string path, string name, Vector3 scale, ObstacleType type, Color color)
    {
        DeleteAssetAtPath(path);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.tag = "Obstacle";
        go.transform.localScale = scale;
        Obstacle obs = go.AddComponent<Obstacle>();
        obs.type = type;
        go.GetComponent<Collider>().isTrigger = true;

        Material mat = CreateMaterial(name + "Mat", color);
        if (mat != null) go.GetComponent<MeshRenderer>().material = mat;

        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
    }

    static void CreateTrackSegmentPrefab()
    {
        string path = "Assets/Prefabs/TrackSegment.prefab";
        DeleteAssetAtPath(path);

        GameObject seg = new GameObject("TrackSegment");
        seg.layer = LayerMask.NameToLayer("Ground");
        seg.AddComponent<TrackSegmentData>();

        // Ground visual
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "GroundPlane";
        ground.transform.SetParent(seg.transform);
        ground.transform.localPosition = Vector3.zero;
        ground.transform.localScale = new Vector3(1.5f, 1f, 2f);
        ground.layer = LayerMask.NameToLayer("Ground");
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        BoxCollider groundCol = ground.AddComponent<BoxCollider>();
        groundCol.center = Vector3.zero;
        groundCol.size = new Vector3(9f, 0.2f, 20f);

        Material gm = CreateMaterial("TrackGroundMat", new Color(0.25f, 0.28f, 0.35f));
        if (gm != null) ground.GetComponent<MeshRenderer>().material = gm;

        // Lane markers
        for (int i = 0; i < 3; i++)
        {
            GameObject m = new GameObject("Lane_" + i);
            m.transform.SetParent(seg.transform);
            m.transform.localPosition = new Vector3((i - 1) * 3f, 0.06f, 0);
        }

        // White lane divider lines
        Material lineMat = CreateMaterial("LaneLineMat", Color.white);
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "LaneLine_" + (i > 0 ? "R" : "L");
            line.transform.SetParent(seg.transform);
            line.transform.localPosition = new Vector3(i * 1.5f, 0.06f, 0);
            line.transform.localScale = new Vector3(0.15f, 0.02f, 20f);
            if (lineMat != null) line.GetComponent<MeshRenderer>().material = lineMat;
            Object.DestroyImmediate(line.GetComponent<Collider>());
        }

        PrefabUtility.SaveAsPrefabAsset(seg, path);
        Object.DestroyImmediate(seg);
    }

    static void CreateTurnSegmentPrefabs()
    {
        Material trackMat = CreateMaterial("TrackGroundMat_Turn", new Color(0.25f, 0.28f, 0.35f));
        Material lineMat = CreateMaterial("LaneLineMat_Turn", Color.white);
        int groundLayer = LayerMask.NameToLayer("Ground");

        CreateTurnPrefab("Assets/Prefabs/TurnSegment_Right.prefab", "TurnSegment_Right", 1, trackMat, lineMat, groundLayer);
        CreateTurnPrefab("Assets/Prefabs/TurnSegment_Left.prefab", "TurnSegment_Left", -1, trackMat, lineMat, groundLayer);
    }

    static void CreateTurnPrefab(string path, string name, int turnDir, Material trackMat, Material lineMat, int groundLayer)
    {
        DeleteAssetAtPath(path);

        GameObject seg = new GameObject(name);
        seg.layer = groundLayer;
        seg.AddComponent<TrackSegmentData>();

        // Cross-shape geometry: both strips are full segment-length so there are no gaps
        // Entry strip: full 20 units Z, covering from previous straight through the corner
        GameObject entry = GameObject.CreatePrimitive(PrimitiveType.Plane);
        entry.name = "EntryStrip";
        entry.transform.SetParent(seg.transform);
        entry.transform.localPosition = new Vector3(0, 0.05f, 10f);
        entry.transform.localScale = new Vector3(1.5f, 1f, 2f); // 20 units in Z (full segment)
        entry.layer = groundLayer;
        Object.DestroyImmediate(entry.GetComponent<MeshCollider>());
        BoxCollider entryCol = entry.AddComponent<BoxCollider>();
        entryCol.center = Vector3.zero;
        entryCol.size = new Vector3(9f, 0.3f, 20f);
        if (trackMat != null) entry.GetComponent<MeshRenderer>().material = trackMat;

        // Exit strip: full 20 units in exit direction, starting from corner
        GameObject exitStrip = GameObject.CreatePrimitive(PrimitiveType.Plane);
        exitStrip.name = "ExitStrip";
        exitStrip.transform.SetParent(seg.transform);
        exitStrip.transform.localPosition = new Vector3(turnDir * 10f, 0.05f, 10f); // centered at half the exit length
        exitStrip.transform.localRotation = Quaternion.Euler(0, 90f, 0);
        exitStrip.transform.localScale = new Vector3(1.5f, 1f, 2f); // 20 units long in exit direction
        exitStrip.layer = groundLayer;
        Object.DestroyImmediate(exitStrip.GetComponent<MeshCollider>());
        BoxCollider exitCol = exitStrip.AddComponent<BoxCollider>();
        exitCol.center = Vector3.zero;
        exitCol.size = new Vector3(9f, 0.3f, 20f);
        if (trackMat != null) exitStrip.GetComponent<MeshRenderer>().material = trackMat;

        // Lane markers on entry strip
        for (int i = 0; i < 3; i++)
        {
            GameObject m = new GameObject("Lane_" + i);
            m.transform.SetParent(seg.transform);
            m.transform.localPosition = new Vector3((i - 1) * 3f, 0.08f, 5f);
        }
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            line.name = "LaneLine_" + (i > 0 ? "R" : "L");
            line.transform.SetParent(seg.transform);
            line.transform.localPosition = new Vector3(i * 1.5f, 0.08f, 5f);
            line.transform.localScale = new Vector3(0.15f, 0.02f, 10f);
            if (lineMat != null) line.GetComponent<MeshRenderer>().material = lineMat;
            Object.DestroyImmediate(line.GetComponent<Collider>());
        }

        PrefabUtility.SaveAsPrefabAsset(seg, path);
        Object.DestroyImmediate(seg);
    }

    // ── track manager wiring ───────────────────────────

    static void ConfigureTrackManager()
    {
        TrackManager tm = Object.FindObjectOfType<TrackManager>();
        if (tm == null) { Debug.LogWarning("TrackManager not found!"); return; }

        GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");
        GameObject obsLow = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacle_Low.prefab");
        GameObject obsHigh = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacle_High.prefab");
        GameObject obsBarrier = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacle_Barrier.prefab");
        GameObject segmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrackSegment.prefab");
        GameObject turnLeftPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TurnSegment_Left.prefab");
        GameObject turnRightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TurnSegment_Right.prefab");

        SerializedObject so = new SerializedObject(tm);
        so.FindProperty("coinPrefab").objectReferenceValue = coinPrefab;
        so.FindProperty("trackSegmentPrefab").objectReferenceValue = segmentPrefab;
        so.FindProperty("turnLeftPrefab").objectReferenceValue = turnLeftPrefab;
        so.FindProperty("turnRightPrefab").objectReferenceValue = turnRightPrefab;

        SerializedProperty obsArr = so.FindProperty("obstaclePrefabs");
        obsArr.arraySize = 3;
        obsArr.GetArrayElementAtIndex(0).objectReferenceValue = obsLow;
        obsArr.GetArrayElementAtIndex(1).objectReferenceValue = obsHigh;
        obsArr.GetArrayElementAtIndex(2).objectReferenceValue = obsBarrier;

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(tm.gameObject);
    }

    // ── canvas / UI ────────────────────────────────────

    static void CreateUICanvas()
    {
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("Canvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        Transform canvasT = canvas.transform;

        UIManager ui = Object.FindObjectOfType<UIManager>();
        if (ui == null) { Debug.LogWarning("UIManager not found!"); return; }
        SerializedObject so = new SerializedObject(ui);

        // ═══ Menu Panel ═══
        GameObject menuPanel = CreatePanel("MenuPanel", canvasT, new Color(0, 0, 0, 0.85f));
        Stretch(menuPanel.GetComponent<RectTransform>());

        Text titleText = CreateText("Title", menuPanel.transform, "TEMPLE RUN", 80, TextAnchor.MiddleCenter);
        titleText.color = new Color(1f, 0.85f, 0.1f);
        titleText.fontStyle = FontStyle.Bold;
        AddOutline(titleText.gameObject, new Color(0.4f, 0.2f, 0f));
        AddShadow(titleText.gameObject, new Color(0, 0, 0, 0.8f));
        RectTransform titleRT = titleText.GetComponent<RectTransform>();
        AnchorText(titleRT, 0.5f, 0.65f, 700, 110);

        Button startBtn = CreateButton("StartButton", menuPanel.transform, "开始游戏", 40,
            new Vector2(0.5f, 0.38f), new Vector2(460, 120),
            new Color(0.15f, 0.7f, 0.2f), new Color(0.1f, 0.5f, 0.15f));

        // FPS selector label
        Text fpsLabel = CreateText("FpsLabel", menuPanel.transform, "帧率", 30, TextAnchor.MiddleCenter);
        fpsLabel.color = new Color(0.7f, 0.7f, 0.7f);
        AnchorText(fpsLabel.GetComponent<RectTransform>(), 0.5f, 0.22f, 200, 40);

        // FPS buttons row
        Button fps30 = CreateSmallButton("Fps30", menuPanel.transform, "30",
            new Vector2(0.28f, 0.14f), new Vector2(150, 70),
            new Color(0.3f, 0.3f, 0.35f));
        Button fps60 = CreateSmallButton("Fps60", menuPanel.transform, "60",
            new Vector2(0.5f, 0.14f), new Vector2(150, 70),
            new Color(0.2f, 0.75f, 1f));
        Button fps120 = CreateSmallButton("Fps120", menuPanel.transform, "120",
            new Vector2(0.72f, 0.14f), new Vector2(150, 70),
            new Color(0.3f, 0.3f, 0.35f));

        // ═══ HUD Panel (top-left corner) ═══
        GameObject hudPanel = CreatePanel("HudPanel", canvasT, Color.clear);
        Stretch(hudPanel.GetComponent<RectTransform>());

        // Compact bar in top-left
        GameObject hudBar = CreatePanel("HudBar", hudPanel.transform, new Color(0, 0, 0, 0.5f));
        RectTransform barRT = hudBar.GetComponent<RectTransform>();
        barRT.anchorMin = new Vector2(0, 1); barRT.anchorMax = new Vector2(0, 1);
        barRT.pivot = new Vector2(0, 1);
        barRT.sizeDelta = new Vector2(520, 80);
        barRT.anchoredPosition = new Vector2(20, -20); // margin from top-left edge

        // Score: "Score: 0"
        Text scoreText = CreateText("ScoreText", hudBar.transform, "Score: 0", 36, TextAnchor.MiddleLeft);
        scoreText.color = Color.white;
        scoreText.fontStyle = FontStyle.Bold;
        AddOutline(scoreText.gameObject, new Color(0, 0, 0, 0.6f));
        RectTransform stRT = scoreText.GetComponent<RectTransform>();
        stRT.anchorMin = new Vector2(0, 0.5f); stRT.anchorMax = new Vector2(0, 0.5f);
        stRT.pivot = new Vector2(0, 0.5f);
        stRT.anchoredPosition = new Vector2(24, 0);
        stRT.sizeDelta = new Vector2(280, 44);

        // Coin icon: "$"
        Text coinIcon = CreateText("CoinIcon", hudBar.transform, "$", 38, TextAnchor.MiddleRight);
        coinIcon.color = new Color(1f, 0.85f, 0.1f);
        coinIcon.fontStyle = FontStyle.Bold;
        AddOutline(coinIcon.gameObject, new Color(0.4f, 0.3f, 0f));
        RectTransform ciRT = coinIcon.GetComponent<RectTransform>();
        ciRT.anchorMin = new Vector2(0, 0.5f); ciRT.anchorMax = new Vector2(0, 0.5f);
        ciRT.pivot = new Vector2(0, 0.5f);
        ciRT.anchoredPosition = new Vector2(340, 0);
        ciRT.sizeDelta = new Vector2(36, 44);

        // Coin count: "0"
        Text coinText = CreateText("CoinText", hudBar.transform, "0", 36, TextAnchor.MiddleLeft);
        coinText.color = new Color(1f, 0.85f, 0.1f);
        coinText.fontStyle = FontStyle.Bold;
        AddOutline(coinText.gameObject, new Color(0.4f, 0.3f, 0f));
        RectTransform ctRT = coinText.GetComponent<RectTransform>();
        ctRT.anchorMin = new Vector2(0, 0.5f); ctRT.anchorMax = new Vector2(0, 0.5f);
        ctRT.pivot = new Vector2(0, 0.5f);
        ctRT.anchoredPosition = new Vector2(380, 0);
        ctRT.sizeDelta = new Vector2(120, 44);

        // ═══ GameOver Panel ═══
        GameObject goPanel = CreatePanel("GameOverPanel", canvasT, new Color(0, 0, 0, 0.88f));
        Stretch(goPanel.GetComponent<RectTransform>());

        Text goTitle = CreateText("GameOverTitle", goPanel.transform, "Game Over", 72, TextAnchor.MiddleCenter);
        goTitle.color = new Color(1f, 0.2f, 0.15f);
        goTitle.fontStyle = FontStyle.Bold;
        AddOutline(goTitle.gameObject, new Color(0.5f, 0.05f, 0f));
        AddShadow(goTitle.gameObject, new Color(0, 0, 0, 0.8f));
        RectTransform goTitleRT = goTitle.GetComponent<RectTransform>();
        AnchorText(goTitleRT, 0.5f, 0.7f, 500, 90);

        Text finalScoreText = CreateText("FinalScoreText", goPanel.transform, "Score: 0", 52, TextAnchor.MiddleCenter);
        finalScoreText.color = Color.white;
        finalScoreText.fontStyle = FontStyle.Bold;
        AddOutline(finalScoreText.gameObject, new Color(0, 0, 0, 0.6f));
        RectTransform fsRT = finalScoreText.GetComponent<RectTransform>();
        AnchorText(fsRT, 0.5f, 0.52f, 500, 80);

        Text coinResultText = CreateText("CoinResultText", goPanel.transform, "Coins: 0", 42, TextAnchor.MiddleCenter);
        coinResultText.color = new Color(1f, 0.85f, 0.1f);
        coinResultText.fontStyle = FontStyle.Bold;
        AddOutline(coinResultText.gameObject, new Color(0.3f, 0.2f, 0f));
        RectTransform crRT = coinResultText.GetComponent<RectTransform>();
        AnchorText(crRT, 0.5f, 0.42f, 400, 70);

        Button restartBtn = CreateButton("RestartButton", goPanel.transform, "再来一局", 40,
            new Vector2(0.5f, 0.28f), new Vector2(460, 120),
            new Color(0.15f, 0.7f, 0.2f), new Color(0.1f, 0.5f, 0.15f));

        // Wire to UIManager
        so.FindProperty("menuPanel").objectReferenceValue = menuPanel;
        so.FindProperty("startButton").objectReferenceValue = startBtn;
        so.FindProperty("fps30Button").objectReferenceValue = fps30;
        so.FindProperty("fps60Button").objectReferenceValue = fps60;
        so.FindProperty("fps120Button").objectReferenceValue = fps120;
        so.FindProperty("hudPanel").objectReferenceValue = hudPanel;
        so.FindProperty("scoreText").objectReferenceValue = scoreText;
        so.FindProperty("coinText").objectReferenceValue = coinText;
        so.FindProperty("gameOverPanel").objectReferenceValue = goPanel;
        so.FindProperty("finalScoreText").objectReferenceValue = finalScoreText;
        so.FindProperty("coinResultText").objectReferenceValue = coinResultText;
        so.FindProperty("restartButton").objectReferenceValue = restartBtn;
        so.ApplyModifiedProperties();

        menuPanel.SetActive(false);
        hudPanel.SetActive(false);
        goPanel.SetActive(false);

        EditorUtility.SetDirty(ui.gameObject);
    }

    static GameObject CreatePanel(string name, Transform parent, Color color)
    {
        GameObject panel = new GameObject(name, typeof(Image));
        panel.GetComponent<Image>().color = color;
        panel.transform.SetParent(parent, false);
        return panel;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static void AnchorText(RectTransform rt, float ax, float ay, float w, float h)
    {
        rt.anchorMin = new Vector2(ax, ay);
        rt.anchorMax = new Vector2(ax, ay);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = Vector2.zero;
    }

    static Text CreateText(string name, Transform parent, string content, int fontSize, TextAnchor align)
    {
        GameObject go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = Color.white;
        Font font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/arial.ttf");
        if (font != null) text.font = font;
        else text.font = Font.CreateDynamicFontFromOSFont("Arial", fontSize);
        return text;
    }

    static void AddOutline(GameObject go, Color color)
    {
        Outline outline = go.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(2.5f, -2.5f);
    }

    static void AddShadow(GameObject go, Color color)
    {
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = color;
        shadow.effectDistance = new Vector2(3f, -3f);
    }

    static Button CreateButton(string name, Transform parent, string label, int fontSize,
        Vector2 anchor, Vector2 size, Color mainColor, Color edgeColor)
    {
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        go.GetComponent<Image>().color = mainColor;

        // Button border/depth via inner shadow panel
        GameObject border = new GameObject("Border", typeof(Image));
        border.transform.SetParent(go.transform, false);
        Image borderImg = border.GetComponent<Image>();
        borderImg.color = edgeColor;
        RectTransform brt = border.GetComponent<RectTransform>();
        Stretch(brt);
        brt.offsetMin = new Vector2(4, 4);
        brt.offsetMax = new Vector2(-4, -4);

        Text labelT = CreateText("Label", go.transform, label, fontSize, TextAnchor.MiddleCenter);
        labelT.color = Color.white;
        labelT.fontStyle = FontStyle.Bold;
        AddOutline(labelT.gameObject, new Color(0, 0, 0, 0.5f));
        Stretch(labelT.GetComponent<RectTransform>());

        return go.GetComponent<Button>();
    }

    static Button CreateSmallButton(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
        go.GetComponent<Image>().color = color;

        Text labelT = CreateText("Label", go.transform, label, 28, TextAnchor.MiddleCenter);
        labelT.color = Color.white;
        labelT.fontStyle = FontStyle.Bold;
        Stretch(labelT.GetComponent<RectTransform>());

        return go.GetComponent<Button>();
    }

    // ── asset helpers ──────────────────────────────────

    static void DeleteAssetAtPath(string assetPath)
    {
        string fullPath = System.IO.Path.Combine(Application.dataPath, "../", assetPath);
        string metaPath = fullPath + ".meta";
        try
        {
            if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
            if (System.IO.File.Exists(metaPath)) System.IO.File.Delete(metaPath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Failed to delete {assetPath}: {e.Message}");
        }
    }

    // ── tag / layer helpers ────────────────────────────

    static void AddTag(string tag)
    {
        SerializedObject o = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty arr = o.FindProperty("tags");
        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).stringValue == tag) return;
        arr.InsertArrayElementAtIndex(arr.arraySize);
        arr.GetArrayElementAtIndex(arr.arraySize - 1).stringValue = tag;
        o.ApplyModifiedProperties();
    }

    static void AddLayer(string layer)
    {
        SerializedObject o = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty arr = o.FindProperty("layers");
        for (int i = 8; i < 32; i++)
            if (arr.GetArrayElementAtIndex(i).stringValue == layer) return;
        for (int i = 8; i < 32; i++)
        {
            if (string.IsNullOrEmpty(arr.GetArrayElementAtIndex(i).stringValue))
            { arr.GetArrayElementAtIndex(i).stringValue = layer; o.ApplyModifiedProperties(); return; }
        }
    }

    static void EnsureFolder(string p)
    {
        if (!AssetDatabase.IsValidFolder(p))
        {
            string parent = System.IO.Path.GetDirectoryName(p).Replace('\\', '/');
            string folder = System.IO.Path.GetFileName(p);
            if (!AssetDatabase.IsValidFolder(parent)) AssetDatabase.CreateFolder("Assets", System.IO.Path.GetFileName(parent));
            AssetDatabase.CreateFolder(parent, folder);
        }
    }
}
