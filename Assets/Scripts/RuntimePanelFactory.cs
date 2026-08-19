using UnityEngine;
using UnityEngine.UI;

public static class RuntimePanelFactory
{
    private static Font _font;
    private static Sprite _roundedSprite;

    public static readonly Color Panel = EchoRunUITheme.WithAlpha(
        EchoRunUITheme.Backdrop, 0.98f);
    public static readonly Color Raised = EchoRunUITheme.WithAlpha(
        EchoRunUITheme.SurfaceRaised, 0.98f);
    public static readonly Color Action = EchoRunUITheme.RouteCyanDark;
    public static readonly Color Reward = EchoRunUITheme.Reward;
    public static readonly Color TextPrimary = EchoRunUITheme.TextPrimary;
    public static readonly Color TextMuted = EchoRunUITheme.TextMuted;

    public static GameObject PanelObject(string name, Transform parent,
        Vector2 anchor, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.color = color;
        ApplyRounded(image);
        return go;
    }

    public static Text Text(string name, Transform parent, string value,
        int size, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = GetFont();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.resizeTextForBestFit = false;
        text.supportRichText = true;
        EchoRunAccessibility.Prepare(text);
        return text;
    }

    public static Button Button(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Color color, int fontSize = 24)
    {
        if (UsesTouchLayout()) size.y = Mathf.Max(size.y, 104f);
        GameObject go = PanelObject(name, parent, anchor, size, color);
        Button button = go.AddComponent<Button>();
        Text text = Text("Label", go.transform, label, fontSize,
            TextAnchor.MiddleCenter, TextPrimary);
        text.fontStyle = FontStyle.Bold;
        Stretch(text.rectTransform);
        ColorBlock states = button.colors;
        states.normalColor = Color.white;
        states.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        states.pressedColor = new Color(0.76f, 0.84f, 0.88f, 1f);
        states.selectedColor = states.highlightedColor;
        states.disabledColor = new Color(0.48f, 0.52f, 0.56f, 0.75f);
        states.fadeDuration = 0.08f;
        button.colors = states;
        button.onClick.AddListener(() => AudioManager.Instance?.PlayUIClick());
        return button;
    }

    public static Button NavigationButton(string name, Transform parent,
        string label, string caption, string iconName, Vector2 anchor,
        Vector2 size)
    {
        if (UsesTouchLayout()) size.y = Mathf.Max(size.y, 104f);
        GameObject go = PanelObject(name, parent, anchor, size,
            EchoRunUITheme.WithAlpha(EchoRunUITheme.Surface, 0.78f));
        Button button = go.AddComponent<Button>();

        Sprite sprite = EchoIconSet.Get(iconName);
        if (sprite != null)
        {
            GameObject iconObject = new GameObject("Icon", typeof(Image));
            iconObject.transform.SetParent(go.transform, false);
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.color = EchoRunUITheme.TextPrimary;
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(0.5f, 0.68f),
                new Vector2(32f, 32f), Vector2.zero);
        }

        Text title = Text("Label", go.transform, label, 19,
            TextAnchor.MiddleCenter, TextPrimary);
        title.fontStyle = FontStyle.Bold;
        Place(title.rectTransform, new Vector2(0.5f, 0.34f),
            new Vector2(size.x - 12f, 26f), Vector2.zero);

        Text sub = Text("Caption", go.transform, caption, 10,
            TextAnchor.MiddleCenter, TextMuted);
        Place(sub.rectTransform, new Vector2(0.5f, 0.13f),
            new Vector2(size.x - 10f, 18f), Vector2.zero);

        ColorBlock states = button.colors;
        states.normalColor = Color.white;
        states.highlightedColor = new Color(1.08f, 1.08f, 1.08f, 1f);
        states.pressedColor = new Color(0.74f, 0.84f, 0.89f, 1f);
        states.selectedColor = states.highlightedColor;
        states.disabledColor = new Color(0.48f, 0.52f, 0.56f, 0.75f);
        states.fadeDuration = 0.08f;
        button.colors = states;
        button.onClick.AddListener(() => AudioManager.Instance?.PlayUIClick());
        return button;
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 size,
        Vector2 offset)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = offset;
    }

    public static Vector2 TouchButtonSize(Vector2 requested, bool portrait)
    {
        return UILayoutRules.EnsureTouchButtonSize(
            requested, UsesTouchLayout(), portrait);
    }

    public static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    public static void RefreshText(Transform root)
    {
        if (root == null) return;
        Text[] texts = root.GetComponentsInChildren<Text>(true);
        foreach (Text text in texts)
        {
            text.SetLayoutDirty();
            text.SetVerticesDirty();
        }
        Canvas.ForceUpdateCanvases();
    }

    private static Font GetFont()
    {
        if (_font != null) return _font;
        _font = Resources.Load<Font>("Fonts/EchoRunSansSC-Regular");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _font;
    }

    private static void ApplyRounded(Image image)
    {
        if (_roundedSprite == null)
        {
            const int size = 64;
            const float radius = 15f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "RuntimePanelRoundedTexture";
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x, x - (size - radius - 1f), 0f);
                float dy = Mathf.Max(radius - y, y - (size - radius - 1f), 0f);
                float alpha = Mathf.Clamp01(radius + 0.75f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            texture.Apply(false, true);
            _roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(16f, 16f, 16f, 16f));
        }
        image.sprite = _roundedSprite;
        image.type = Image.Type.Sliced;
    }

    private static bool UsesTouchLayout()
    {
        return Application.isMobilePlatform || Input.touchSupported;
    }
}
