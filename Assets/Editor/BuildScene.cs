using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BuildScene
{
    // Cached shader found on first use
    private static Shader _cachedShader;
    private static bool _shaderSearched;

    [MenuItem("Tools/Build Scene")]
    static void Build()
    {
        Debug.Log("=== BUILD SCENE START ===");

        // Detect render pipeline and find working shader first
        DetectRenderPipeline();

        AddTag("Coin");
        AddTag("Obstacle");
        AddLayer("Ground");

        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/Materials");

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
        ConfigureTrackManager();
        CreateUICanvas();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
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

        _shaderSearched = true;
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
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "player";
            player.transform.position = new Vector3(0, 1.2f, 0);
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb == null) rb = player.AddComponent<Rigidbody>();
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (player.GetComponent<PlayerController>() == null)
            player.AddComponent<PlayerController>();

        if (player.GetComponent<CapsuleCollider>() == null)
        {
            foreach (var c in player.GetComponents<Collider>())
                Object.DestroyImmediate(c);
            CapsuleCollider cc = player.AddComponent<CapsuleCollider>();
            cc.height = 2f;
            cc.radius = 0.5f;
        }

        // Apply material with working shader
        Material mat = CreateMaterial("PlayerMat", new Color(0.2f, 0.5f, 0.9f));
        if (mat != null) player.GetComponent<MeshRenderer>().material = mat;

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
        plane.transform.localScale = new Vector3(3f, 1f, 2f);

        // Remove MeshCollider (expensive, and we'll use BoxCollider instead)
        MeshCollider mc = plane.GetComponent<MeshCollider>();
        if (mc != null) Object.DestroyImmediate(mc);

        // Replace with large, efficient BoxCollider
        BoxCollider bc = plane.GetComponent<BoxCollider>();
        if (bc == null) bc = plane.AddComponent<BoxCollider>();
        bc.center = new Vector3(0, 0, 0);
        bc.size = new Vector3(9f, 1f, 300f);

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
        string path = "Assets/Prefabs/Obstacle.prefab";
        DeleteAssetAtPath(path);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Obstacle";
        go.tag = "Obstacle";
        go.transform.localScale = new Vector3(2f, 2.5f, 0.8f);
        go.AddComponent<Obstacle>();
        go.GetComponent<Collider>().isTrigger = true;

        Material mat = CreateMaterial("ObstacleMat", new Color(1f, 0.2f, 0.1f));
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

        // Ground visual
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "GroundPlane";
        ground.transform.SetParent(seg.transform);
        ground.transform.localPosition = Vector3.zero;
        ground.transform.localScale = new Vector3(1.5f, 1f, 2f);
        ground.layer = LayerMask.NameToLayer("Ground");
        Object.DestroyImmediate(ground.GetComponent<Collider>());

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

    // ── track manager wiring ───────────────────────────

    static void ConfigureTrackManager()
    {
        TrackManager tm = Object.FindObjectOfType<TrackManager>();
        if (tm == null) { Debug.LogWarning("TrackManager not found!"); return; }

        GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Coin.prefab");
        GameObject obstaclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacle.prefab");
        GameObject segmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrackSegment.prefab");

        SerializedObject so = new SerializedObject(tm);
        so.FindProperty("coinPrefab").objectReferenceValue = coinPrefab;
        so.FindProperty("trackSegmentPrefab").objectReferenceValue = segmentPrefab;

        SerializedProperty obsArr = so.FindProperty("obstaclePrefabs");
        obsArr.arraySize = 1;
        obsArr.GetArrayElementAtIndex(0).objectReferenceValue = obstaclePrefab;

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

        // Menu Panel
        GameObject menuPanel = CreatePanel("MenuPanel", canvasT, new Color(0, 0, 0, 0.85f));
        Stretch(menuPanel.GetComponent<RectTransform>());

        Text titleText = CreateText("Title", menuPanel.transform, "Temple Run", 72, TextAnchor.MiddleCenter);
        titleText.color = Color.white;
        AnchorText(titleText.GetComponent<RectTransform>(), 0.5f, 0.6f, 600, 100);

        Button startBtn = CreateButton("StartButton", menuPanel.transform, "开始游戏", 36,
            new Vector2(0.5f, 0.4f), new Vector2(400, 100));

        // HUD Panel
        GameObject hudPanel = CreatePanel("HudPanel", canvasT, Color.clear);

        Text scoreText = CreateText("ScoreText", hudPanel.transform, "Score: 0", 40, TextAnchor.UpperLeft);
        RectTransform stRT = scoreText.GetComponent<RectTransform>();
        stRT.anchorMin = new Vector2(0, 1); stRT.anchorMax = new Vector2(0, 1);
        stRT.pivot = new Vector2(0, 1);
        stRT.anchoredPosition = new Vector2(30, -30);
        stRT.sizeDelta = new Vector2(400, 60);

        Text coinText = CreateText("CoinText", hudPanel.transform, "0", 36, TextAnchor.UpperRight);
        coinText.color = Color.yellow;
        RectTransform ctRT = coinText.GetComponent<RectTransform>();
        ctRT.anchorMin = new Vector2(1, 1); ctRT.anchorMax = new Vector2(1, 1);
        ctRT.pivot = new Vector2(1, 1);
        ctRT.anchoredPosition = new Vector2(-30, -30);
        ctRT.sizeDelta = new Vector2(200, 60);

        // GameOver Panel
        GameObject goPanel = CreatePanel("GameOverPanel", canvasT, new Color(0, 0, 0, 0.85f));
        Stretch(goPanel.GetComponent<RectTransform>());

        Text goTitle = CreateText("GameOverTitle", goPanel.transform, "Game Over", 64, TextAnchor.MiddleCenter);
        goTitle.color = Color.red;
        AnchorText(goTitle.GetComponent<RectTransform>(), 0.5f, 0.65f, 500, 80);

        Text finalScoreText = CreateText("FinalScoreText", goPanel.transform, "Score: 0", 48, TextAnchor.MiddleCenter);
        finalScoreText.color = Color.white;
        AnchorText(finalScoreText.GetComponent<RectTransform>(), 0.5f, 0.5f, 400, 80);

        Button restartBtn = CreateButton("RestartButton", goPanel.transform, "重新开始", 36,
            new Vector2(0.5f, 0.35f), new Vector2(400, 100));

        // Wire to UIManager
        so.FindProperty("menuPanel").objectReferenceValue = menuPanel;
        so.FindProperty("startButton").objectReferenceValue = startBtn;
        so.FindProperty("hudPanel").objectReferenceValue = hudPanel;
        so.FindProperty("scoreText").objectReferenceValue = scoreText;
        so.FindProperty("coinText").objectReferenceValue = coinText;
        so.FindProperty("gameOverPanel").objectReferenceValue = goPanel;
        so.FindProperty("finalScoreText").objectReferenceValue = finalScoreText;
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

    static Button CreateButton(string name, Transform parent, string label, int fontSize,
        Vector2 anchor, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        go.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f);

        Text labelT = CreateText("Label", rt, label, fontSize, TextAnchor.MiddleCenter);
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
