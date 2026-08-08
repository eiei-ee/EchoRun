public static class UILayoutRules
{
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
}
