public static class UILayoutRules
{
    public static bool ShouldShowLandscapeGuard(
        int width, int height, bool touchLayout)
    {
        return touchLayout && width > 0 && height > width;
    }
}
