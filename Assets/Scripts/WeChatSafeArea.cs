using System;
using UnityEngine;

/// <summary>Platform supplies a resolved area; shared UI never imports an SDK.</summary>
public static class WeChatSafeArea
{
    private static Func<Rect, int, int, Rect> _provider;

    public static void Register(Func<Rect, int, int, Rect> provider) { _provider = provider; }
    public static void Unregister(Func<Rect, int, int, Rect> provider)
    { if (_provider == provider) _provider = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetProvider() { _provider = null; }

    public static Rect Resolve(Rect fallback, int width, int height)
    { return _provider != null ? _provider(fallback, width, height) : fallback; }

    /// <summary>Pure top-origin logical viewport to bottom-origin Unity pixels.</summary>
    public static Rect Convert(Rect logicalArea, float capsuleBottom,
        float viewportWidth, float viewportHeight, int width, int height, Rect fallback)
    {
        if (viewportWidth <= 0f || viewportHeight <= 0f || width <= 0 || height <= 0)
            return fallback;
        float sx = width / viewportWidth;
        float sy = height / viewportHeight;
        float top = Mathf.Max(logicalArea.yMin, capsuleBottom + 8f);
        Rect area = Rect.MinMaxRect(logicalArea.xMin * sx, height - logicalArea.yMax * sy,
            logicalArea.xMax * sx, height - top * sy);
        return UILayoutRules.NormalizeSafeArea(area, width, height);
    }
}
