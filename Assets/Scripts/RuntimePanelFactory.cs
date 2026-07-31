using UnityEngine;
using UnityEngine.UI;

public static class RuntimePanelFactory
{
    private static Font _font;
    private static Sprite _roundedSprite;

    public static readonly Color Panel = new Color(0.055f, 0.075f, 0.105f, 0.98f);
    public static readonly Color Raised = new Color(0.13f, 0.18f, 0.24f, 0.98f);
    public static readonly Color Action = new Color(0.24f, 0.43f, 0.62f, 1f);
    public static readonly Color Reward = new Color(0.92f, 0.63f, 0.28f, 1f);
    public static readonly Color TextPrimary = new Color(0.94f, 0.96f, 0.99f, 1f);
    public static readonly Color TextMuted = new Color(0.67f, 0.73f, 0.82f, 1f);

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
        return text;
    }

    public static Button Button(string name, Transform parent, string label,
        Vector2 anchor, Vector2 size, Color color, int fontSize = 24)
    {
        GameObject go = PanelObject(name, parent, anchor, size, color);
        Button button = go.AddComponent<Button>();
        Text text = Text("Label", go.transform, label, fontSize,
            TextAnchor.MiddleCenter, TextPrimary);
        text.fontStyle = FontStyle.Bold;
        Stretch(text.rectTransform);
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

    public static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static Font GetFont()
    {
        if (_font != null) return _font;
        _font = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
        if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return _font;
    }

    private static void ApplyRounded(Image image)
    {
        if (_roundedSprite == null)
        {
            const int size = 64;
            const float radius = 24f;
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
                new Vector4(24f, 24f, 24f, 24f));
        }
        image.sprite = _roundedSprite;
        image.type = Image.Type.Sliced;
    }
}
