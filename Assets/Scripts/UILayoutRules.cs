using UnityEngine;

public static class UILayoutRules
{
    private static readonly Vector2 LandscapeReference = new Vector2(1920f, 1080f);
    private static readonly Vector2 PortraitReference = new Vector2(1080f, 1920f);

    public static bool ShouldShowLandscapeGuard(
        int width, int height, bool touchLayout)
    {
        return ShouldShowLandscapeGuard(width, height, touchLayout, false);
    }

    public static bool ShouldShowLandscapeGuard(
        int width, int height, bool touchLayout, bool allowPortrait)
    {
        return !allowPortrait && touchLayout && width > 0 && height > width;
    }

    public static bool IsCompactPortrait(int width, int height)
    {
        return width > 0 && height > width;
    }

    public static Vector2 GetReferenceResolution(int width, int height)
    {
        return IsCompactPortrait(width, height)
            ? PortraitReference
            : LandscapeReference;
    }

    public static Vector2 EnsureTouchButtonSize(
        Vector2 requested, bool touchLayout, bool portrait)
    {
        if (touchLayout || portrait)
            requested.y = Mathf.Max(requested.y, 104f);
        return requested;
    }

    public static Vector2 EnsureTouchSliderSize(
        Vector2 requested, bool touchLayout, bool portrait)
    {
        if (touchLayout || portrait)
            requested.y = Mathf.Max(requested.y, 72f);
        return requested;
    }
}
