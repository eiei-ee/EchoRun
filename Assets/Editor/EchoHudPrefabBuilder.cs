using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class EchoHudPrefabBuilder
{
    private const string PrefabPath = "Assets/Resources/UI/EchoHud.prefab";
    private static readonly Color Backdrop = EchoRunUITheme.HudPanel;
    private static readonly Color Surface = EchoRunUITheme.HudPanelRaised;
    private static readonly Color Cyan = EchoRunUITheme.HudCalibrationAccent;
    private static readonly Color Coral = EchoRunUITheme.HudDangerText;
    private static readonly Color TextPrimary = EchoRunUITheme.HudInk;
    private static readonly Color TextMuted = EchoRunUITheme.HudInkMuted;
    private static readonly Color Rule = EchoRunUITheme.HudRule;
    private static Font _font;

    [MenuItem("Tools/Rebuild Echo HUD Prefab")]
    public static void Build()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/UI");
        _font = AssetDatabase.LoadAssetAtPath<Font>(
            "Assets/Resources/Fonts/EchoRunSansSC-Regular.otf");

        GameObject root = new GameObject("EchoHud", typeof(RectTransform),
            typeof(EchoHudView), typeof(EchoHudPresenter));
        Stretch(root.GetComponent<RectTransform>());

        GameObject staticLayer = Layer("HudStaticCanvas", root.transform, 10, false);
        GameObject dynamicLayer = Layer("HudDynamicCanvas", root.transform, 20, true);

        Image topInformationRail = HorizontalBand("TopInformationRail",
            staticLayer.transform, Backdrop, 12f, 12f, 8f, 48f);

        Text stats = TextElement("StatsText", staticLayer.transform,
            "SCORE 00000   RANGE 000m", 19, TextAnchor.MiddleLeft,
            TextPrimary, new Vector2(0f, 1f), new Vector2(340f, 36f),
            new Vector2(22f, -14f), new Vector2(0f, 1f));
        Image statsPlate = Panel("StatsPlate", stats.transform.parent,
            stats.rectTransform, Color.clear, true);

        GameObject stageRail = Panel("StageRail", staticLayer.transform,
            new Vector2(0.5f, 1f), new Vector2(320f, 34f),
            new Vector2(0f, -15f), new Vector2(0.5f, 1f), Color.clear);
        string[] stageLabels = { "侦", "暴", "抗", "扑", "写", "决" };
        Text[] stageNodes = new Text[stageLabels.Length];
        Image[] stageConnectors = new Image[stageLabels.Length - 1];
        for (int i = 0; i < stageLabels.Length; i++)
        {
            float x = 0.08f + i * 0.168f;
            stageNodes[i] = TextStretch("Stage_" + i, stageRail.transform,
                stageLabels[i], 18, TextAnchor.MiddleCenter, TextMuted,
                new Vector2(x - 0.07f, 0f), new Vector2(x + 0.07f, 1f));
            if (i < stageLabels.Length - 1)
            {
                Image connector = ImageStretch("Connector_" + i,
                    stageRail.transform, new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f),
                    new Vector2(x + 0.07f, 0.48f),
                    new Vector2(x + 0.098f, 0.52f));
                connector.raycastTarget = false;
                stageConnectors[i] = connector;
            }
        }

        GameObject calibrationRail = Panel("CalibrationRail", staticLayer.transform,
            new Vector2(0.5f, 1f), new Vector2(320f, 34f),
            new Vector2(0f, -15f), new Vector2(0.5f, 1f), Color.clear);
        Text calibrationObservation = TextStretch("CalibrationObservation",
            calibrationRail.transform, "路线  记录中    节奏  采集中", 19,
            TextAnchor.MiddleCenter, TextMuted, Vector2.zero, Vector2.one);

        Text distance = TextElement("DistanceText", staticLayer.transform,
            "终点 700m", 19, TextAnchor.MiddleRight, TextMuted,
            new Vector2(1f, 1f), new Vector2(160f, 36f),
            new Vector2(-136f, -14f), new Vector2(1f, 1f));
        Image distancePlate = Panel("DistancePlate", distance.transform.parent,
            distance.rectTransform, Color.clear, true);

        GameObject leadGroup = Panel("LeadGroup", staticLayer.transform,
            new Vector2(0.5f, 0f), new Vector2(520f, 50f),
            new Vector2(0f, 26f), new Vector2(0.5f, 0f), Backdrop);
        Image leadLine = ImageStretch("LeadLine", leadGroup.transform,
            new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f),
            new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.40f));
        leadLine.raycastTarget = false;
        Image leadMarkerImage = ImageElement("LeadMarker", leadGroup.transform,
            Cyan, new Vector2(0.5f, 0.37f), new Vector2(8f, 22f), Vector2.zero,
            new Vector2(0.5f, 0.5f));
        leadMarkerImage.raycastTarget = false;
        RectTransform leadMarker = leadMarkerImage.rectTransform;
        Text leadText = TextStretch("LeadText", leadGroup.transform, "+0.0m", 19,
            TextAnchor.UpperCenter, TextPrimary,
            new Vector2(0.32f, 0.43f), new Vector2(0.68f, 0.98f));

        GameObject syncGroup = Panel("SyncGroup", staticLayer.transform,
            new Vector2(0f, 0f), new Vector2(260f, 54f),
            new Vector2(18f, 26f), new Vector2(0f, 0f), Backdrop);
        Image[] syncCells = new Image[2];
        syncCells[0] = ImageElement("SyncCell0", syncGroup.transform, Cyan,
            new Vector2(0f, 0.5f), new Vector2(13f, 25f), new Vector2(21f, 0f),
            new Vector2(0f, 0.5f));
        syncCells[1] = ImageElement("SyncCell1", syncGroup.transform, Cyan,
            new Vector2(0f, 0.5f), new Vector2(13f, 25f), new Vector2(41f, 0f),
            new Vector2(0f, 0.5f));
        syncCells[0].raycastTarget = false;
        syncCells[1].raycastTarget = false;
        Text recovery = TextElement("RecoveryText", syncGroup.transform, "", 17,
            TextAnchor.MiddleLeft, Coral, new Vector2(0f, 0.5f),
            new Vector2(188f, 34f), new Vector2(62f, 0f), new Vector2(0f, 0.5f));

        GameObject markerGroup = Panel("MarkerGroup", staticLayer.transform,
            new Vector2(1f, 0f), new Vector2(190f, 42f),
            new Vector2(-18f, 26f), new Vector2(1f, 0f), Backdrop);
        Text markerText = TextStretch("MarkerText", markerGroup.transform,
            "契约标记 0", 18, TextAnchor.MiddleCenter, Coral,
            Vector2.zero, Vector2.one);

        GameObject announcementPlate = Panel("AnnouncementPlate",
            dynamicLayer.transform, new Vector2(0f, 1f), new Vector2(380f, 30f),
            new Vector2(22f, -74f), new Vector2(0f, 1f),
            EchoRunUITheme.HudMessageVeil);
        Text announcement = TextElement("Announcement", dynamicLayer.transform,
            "回声侦测", 19, TextAnchor.MiddleLeft, TextPrimary,
            new Vector2(0f, 1f), new Vector2(360f, 30f),
            new Vector2(30f, -74f), new Vector2(0f, 1f));
        GameObject directivePlate = Panel("DirectivePlate",
            dynamicLayer.transform, new Vector2(0f, 1f), new Vector2(500f, 38f),
            new Vector2(22f, -108f), new Vector2(0f, 1f),
            EchoRunUITheme.HudMessageVeil);
        Text directive = TextElement("Directive", dynamicLayer.transform,
            "复现中", 23, TextAnchor.MiddleLeft, TextPrimary,
            new Vector2(0f, 1f), new Vector2(480f, 36f),
            new Vector2(30f, -109f), new Vector2(0f, 1f));
        GameObject predictionPlate = Panel("PredictionPlate",
            dynamicLayer.transform, new Vector2(0f, 1f), new Vector2(450f, 46f),
            new Vector2(22f, -150f), new Vector2(0f, 1f),
            EchoRunUITheme.HudMessageVeil);
        Text prediction = TextElement("Prediction", dynamicLayer.transform,
            "预判右路", 20, TextAnchor.MiddleLeft, Coral,
            new Vector2(0f, 1f), new Vector2(420f, 44f),
            new Vector2(30f, -151f), new Vector2(0f, 1f));
        Image stateAccentBar = ImageElement("StateAccentBar",
            dynamicLayer.transform, EchoRunUITheme.WithAlpha(Cyan, 0.78f),
            new Vector2(0f, 1f), new Vector2(3f, 126f),
            new Vector2(15f, -72f),
            new Vector2(0f, 1f));
        stateAccentBar.raycastTarget = false;

        GameObject meterGroup = Panel("MeterGroup", dynamicLayer.transform,
            new Vector2(0.5f, 0.855f), new Vector2(520f, 34f), Vector2.zero,
            new Vector2(0.5f, 0.5f), Backdrop);
        Text meterLabel = TextStretch("MeterLabel", meterGroup.transform,
            "稳定度", 16, TextAnchor.MiddleLeft, TextMuted,
            new Vector2(0.03f, 0f), new Vector2(0.20f, 1f));
        Image meterTrack = ImageStretch("MeterTrack", meterGroup.transform,
            Rule, new Vector2(0.21f, 0.34f), new Vector2(0.96f, 0.66f));
        meterTrack.raycastTarget = false;
        Image meterFill = ImageStretch("MeterFill", meterTrack.transform, Cyan,
            Vector2.zero, Vector2.one);
        meterFill.raycastTarget = false;
        meterFill.type = Image.Type.Filled;
        meterFill.fillMethod = Image.FillMethod.Horizontal;
        meterFill.fillOrigin = 0;
        meterFill.fillAmount = 0.5f;

        GameObject buffGroup = Panel("BuffGroup", dynamicLayer.transform,
            new Vector2(1f, 1f), new Vector2(300f, 36f),
            new Vector2(-134f, -70f), new Vector2(1f, 1f), Backdrop);
        Text buffText = TextStretch("BuffText", buffGroup.transform,
            "", 20, TextAnchor.MiddleLeft, TextPrimary,
            new Vector2(0.05f, 0f), new Vector2(0.95f, 1f));

        GameObject feedbackPlate = Panel("FeedbackPlate",
            dynamicLayer.transform, new Vector2(0f, 1f), new Vector2(500f, 34f),
            new Vector2(22f, -202f), new Vector2(0f, 1f),
            EchoRunUITheme.HudMessageVeil);
        Text feedback = TextElement("Feedback", dynamicLayer.transform, "", 20,
            TextAnchor.MiddleLeft, EchoRunUITheme.HudSuccessText,
            new Vector2(0f, 1f),
            new Vector2(480f, 32f), new Vector2(30f, -203f),
            new Vector2(0f, 1f));

        GameObject transitionFxObject = new GameObject("StateTransitionFx",
            typeof(RectTransform), typeof(CanvasGroup));
        transitionFxObject.transform.SetParent(dynamicLayer.transform, false);
        RectTransform transitionFxRect =
            transitionFxObject.GetComponent<RectTransform>();
        transitionFxRect.anchorMin = new Vector2(0f, 1f);
        transitionFxRect.anchorMax = new Vector2(0f, 1f);
        transitionFxRect.pivot = new Vector2(0f, 1f);
        transitionFxRect.sizeDelta = new Vector2(520f, 140f);
        transitionFxRect.anchoredPosition = new Vector2(18f, -70f);
        CanvasGroup transitionFx = transitionFxObject.GetComponent<CanvasGroup>();
        transitionFx.alpha = 0f;
        transitionFx.interactable = false;
        transitionFx.blocksRaycasts = false;

        Image transitionScan = ImageElement("TransitionScanLine",
            transitionFxObject.transform,
            EchoRunUITheme.WithAlpha(Cyan, 0.34f),
            new Vector2(0.5f, 0.5f), new Vector2(120f, 3f),
            new Vector2(0f, 44f), new Vector2(0.5f, 0.5f));
        transitionScan.raycastTarget = false;
        Image fractureA = ImageElement("FractureSliceA",
            transitionFxObject.transform,
            EchoRunUITheme.WithAlpha(Cyan, 0.42f),
            new Vector2(0.5f, 0.5f), new Vector2(220f, 2f),
            new Vector2(-95f, 8f), new Vector2(0.5f, 0.5f));
        fractureA.raycastTarget = false;
        Image fractureB = ImageElement("FractureSliceB",
            transitionFxObject.transform,
            EchoRunUITheme.WithAlpha(Cyan, 0.32f),
            new Vector2(0.5f, 0.5f), new Vector2(170f, 2f),
            new Vector2(125f, -28f), new Vector2(0.5f, 0.5f));
        fractureB.raycastTarget = false;
        transitionFxObject.SetActive(false);

        Button pause = ButtonElement("PauseButton", dynamicLayer.transform, "Ⅱ",
            new Vector2(1f, 1f), new Vector2(38f, 38f),
            new Vector2(-17f, -13f), new Vector2(1f, 1f));

        EchoHudView view = root.GetComponent<EchoHudView>();
        SerializedObject serialized = new SerializedObject(view);
        Set(serialized, "staticLayer", staticLayer);
        Set(serialized, "dynamicLayer", dynamicLayer);
        Set(serialized, "statsText", stats);
        Set(serialized, "announcementText", announcement);
        Set(serialized, "directiveText", directive);
        Set(serialized, "predictionText", prediction);
        Set(serialized, "calibrationObservationText", calibrationObservation);
        Set(serialized, "distanceText", distance);
        Set(serialized, "stageRail", stageRail);
        SetArray(serialized, "stageNodes", stageNodes);
        Set(serialized, "calibrationRail", calibrationRail);
        Set(serialized, "meterGroup", meterGroup);
        Set(serialized, "meterLabel", meterLabel);
        Set(serialized, "meterFill", meterFill);
        Set(serialized, "leadGroup", leadGroup);
        Set(serialized, "leadText", leadText);
        Set(serialized, "leadMarker", leadMarker);
        SetArray(serialized, "syncCells", syncCells);
        Set(serialized, "recoveryText", recovery);
        Set(serialized, "markerGroup", markerGroup);
        Set(serialized, "markerText", markerText);
        Set(serialized, "buffGroup", buffGroup);
        Set(serialized, "buffText", buffText);
        Set(serialized, "feedbackText", feedback);
        Set(serialized, "pauseButton", pause);
        SetArray(serialized, "skinPanels", new[]
        {
            topInformationRail,
            leadGroup.GetComponent<Image>(),
            syncGroup.GetComponent<Image>(),
            markerGroup.GetComponent<Image>(),
            meterGroup.GetComponent<Image>(),
            buffGroup.GetComponent<Image>()
        });
        SetArray(serialized, "skinRules", new[] { meterTrack });
        SetArray(serialized, "phaseAccentRules", new[]
        {
            stageConnectors[0], stageConnectors[1], stageConnectors[2],
            stageConnectors[3], stageConnectors[4], leadLine,
            leadMarkerImage, stateAccentBar, transitionScan,
            fractureA, fractureB
        });
        Set(serialized, "announcementPlate", announcementPlate);
        Set(serialized, "directivePlate", directivePlate);
        Set(serialized, "predictionPlate", predictionPlate);
        Set(serialized, "feedbackPlate", feedbackPlate);
        Set(serialized, "stateTransitionFx", transitionFx);
        Set(serialized, "transitionScanLine", transitionScan.rectTransform);
        Set(serialized, "fractureSliceA", fractureA.rectTransform);
        Set(serialized, "fractureSliceB", fractureB.rectTransform);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("ECHO_HUD_PREFAB_BUILD_OK style=floating-information-rail path=" +
            PrefabPath);
    }

    private static GameObject Layer(string name, Transform parent, int order,
        bool raycaster)
    {
        GameObject layer = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        layer.transform.SetParent(parent, false);
        Stretch(layer.GetComponent<RectTransform>());
        Canvas canvas = layer.GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
        if (raycaster) layer.AddComponent<GraphicRaycaster>();
        return layer;
    }

    private static GameObject Panel(string name, Transform parent, Vector2 anchor,
        Vector2 size, Vector2 offset, Vector2 pivot, Color color)
    {
        Image image = ImageElement(name, parent, color, anchor, size, offset, pivot);
        image.raycastTarget = false;
        return image.gameObject;
    }

    private static Image Panel(string name, Transform parent, RectTransform target,
        Color color, bool siblingBehind)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = target.anchorMin;
        rect.anchorMax = target.anchorMax;
        rect.pivot = target.pivot;
        rect.sizeDelta = target.sizeDelta;
        rect.anchoredPosition = target.anchoredPosition;
        Image image = panel.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (siblingBehind) panel.transform.SetAsFirstSibling();
        return image;
    }

    private static Text TextElement(string name, Transform parent, string value,
        int size, TextAnchor alignment, Color color, Vector2 anchor,
        Vector2 dimensions, Vector2 offset, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = dimensions;
        rect.anchoredPosition = offset;
        Text text = go.GetComponent<Text>();
        text.text = value;
        text.font = _font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = EchoRunUITheme.HudTextShadow;
        shadow.effectDistance = new Vector2(1f, -1f);
        shadow.useGraphicAlpha = true;
        return text;
    }

    private static Text TextStretch(string name, Transform parent, string value,
        int size, TextAnchor alignment, Color color, Vector2 min, Vector2 max)
    {
        Text text = TextElement(name, parent, value, size, alignment, color,
            Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return text;
    }

    private static Image ImageElement(string name, Transform parent, Color color,
        Vector2 anchor, Vector2 size, Vector2 offset, Vector2 pivot)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static Image ImageStretch(string name, Transform parent, Color color,
        Vector2 min, Vector2 max)
    {
        Image image = ImageElement(name, parent, color, Vector2.zero,
            Vector2.zero, Vector2.zero, Vector2.zero);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private static Image HorizontalBand(string name, Transform parent, Color color,
        float left, float right, float top, float height)
    {
        Image image = ImageElement(name, parent, color, Vector2.zero,
            Vector2.zero, Vector2.zero, Vector2.zero);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height);
        rect.offsetMax = new Vector2(-right, -top);
        image.raycastTarget = false;
        image.transform.SetAsFirstSibling();
        return image;
    }

    private static Button ButtonElement(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Vector2 offset, Vector2 pivot)
    {
        Image image = ImageElement(name, parent, Surface, anchor, size, offset, pivot);
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TextStretch("Label", image.transform, label, 18, TextAnchor.MiddleCenter,
            TextPrimary, Vector2.zero, Vector2.one);
        return button;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Set(SerializedObject serialized, string property,
        Object value)
    {
        SerializedProperty target = serialized.FindProperty(property);
        if (target != null) target.objectReferenceValue = value;
    }

    private static void SetArray<T>(SerializedObject serialized, string property,
        T[] values) where T : Object
    {
        SerializedProperty target = serialized.FindProperty(property);
        if (target == null) return;
        target.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            target.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        string parent = path.Substring(0, slash);
        string leaf = path.Substring(slash + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
