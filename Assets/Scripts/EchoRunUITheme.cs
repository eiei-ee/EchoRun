using UnityEngine;

public static class EchoRunUITheme
{
    public static readonly Color Backdrop = new Color32(10, 21, 35, 255);
    public static readonly Color Surface = new Color32(22, 40, 59, 255);
    public static readonly Color SurfaceRaised = new Color32(31, 55, 78, 255);
    public static readonly Color SurfaceSelected = new Color32(18, 82, 105, 255);
    public static readonly Color RouteCyan = new Color32(57, 215, 255, 255);
    public static readonly Color RouteCyanDark = new Color32(14, 105, 132, 255);
    public static readonly Color Reward = new Color32(240, 173, 61, 255);
    public static readonly Color Danger = new Color32(255, 103, 90, 255);
    public static readonly Color Success = new Color32(116, 226, 197, 255);
    public static readonly Color TextPrimary = new Color32(232, 243, 248, 255);
    public static readonly Color TextMuted = new Color32(165, 187, 200, 255);
    public static readonly Color Ink = new Color32(5, 14, 22, 255);

    // ── Type scale (reference-resolution pixels) ──
    public const int TypeCaption = 16;
    public const int TypeBody = 21;
    public const int TypeHud = 25;
    public const int TypeTitle = 32;
    public const int TypeDisplay = 48;
    public const int TypeHero = 76;

    // ── Spacing rhythm ──
    public const float SpaceXS = 6f;
    public const float SpaceS = 12f;
    public const float SpaceM = 18f;
    public const float SpaceL = 28f;
    public const float SpaceXL = 44f;

    // ── Duel phase accents, aligned with EchoAtmosphereDirector so the
    // banner and the world lighting tell the same story. ──
    public static readonly Color PhaseDetection = new Color32(115, 158, 224, 255);
    public static readonly Color PhaseReveal = new Color32(150, 138, 230, 255);
    public static readonly Color PhaseResistance = new Color32(168, 126, 224, 255);
    public static readonly Color PhaseCounterattack = new Color32(255, 122, 106, 255);
    public static readonly Color PhaseRewrite = new Color32(64, 224, 238, 255);
    public static readonly Color PhaseFinale = new Color32(244, 190, 92, 255);

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
