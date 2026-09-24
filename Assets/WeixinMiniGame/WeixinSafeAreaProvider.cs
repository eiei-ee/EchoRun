using UnityEngine;

public sealed class WeixinSafeAreaProvider
{
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
    private Vector2Int _screen;
    private Rect _area;
    private float _retryAfter;
#endif
    public Rect Resolve(Rect fallback, int width, int height)
    {
#if MINIGAME_SUBPLATFORM_WEIXIN && !UNITY_EDITOR
        var size = new Vector2Int(width, height);
        if (_screen == size) return _area;
        if (Time.unscaledTime < _retryAfter) return fallback;
        _retryAfter = Time.unscaledTime + 1f;
        try
        {
            var window = WeChatWASM.WX.GetWindowInfo();
            var capsule = WeChatWASM.WX.GetMenuButtonBoundingClientRect();
            if (window.windowWidth <= 0 || window.windowHeight <= 0) return fallback;
            _area = WeChatSafeArea.Convert(Rect.MinMaxRect((float)window.safeArea.left,
                (float)window.safeArea.top, (float)window.safeArea.right,
                (float)window.safeArea.bottom), (float)capsule.bottom,
                (float)window.windowWidth, (float)window.windowHeight, width, height, fallback);
            _screen = size;
            return _area;
        }
        catch (System.Exception) { return fallback; }
#else
        return fallback;
#endif
    }
}
