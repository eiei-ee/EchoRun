#if MINIGAME_SUBPLATFORM_WEIXIN
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WeChat-only presentation layer. Gameplay and the local AI remain in the
/// shared TempleRun.Runtime assembly so PC and WebGL keep one source of truth.
/// </summary>
public sealed class WeixinMiniGameRuntime : MonoBehaviour
{
    private InputManager _input;
    private Text _feedback;
    private float _feedbackUntil;
    private Vector2Int _configuredSize;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (FindObjectOfType<WeixinMiniGameRuntime>() != null) return;
        new GameObject("Weixin MiniGame Runtime")
            .AddComponent<WeixinMiniGameRuntime>();
    }

    private IEnumerator Start()
    {
        Screen.orientation = ScreenOrientation.Portrait;
        Application.targetFrameRate = 60;

        Canvas canvas = null;
        for (int i = 0; i < 120 && canvas == null; i++)
        {
            canvas = FindObjectOfType<Canvas>();
            if (canvas == null) yield return null;
        }

        if (canvas != null)
        {
            Transform parent = canvas.transform.Find("SafeArea") ?? canvas.transform;
            _feedback = RuntimePanelFactory.Text("SwipeFeedback", parent, "",
                34, TextAnchor.MiddleCenter, RuntimePanelFactory.TextPrimary);
            RuntimePanelFactory.Place(_feedback.rectTransform,
                new Vector2(0.5f, 0.18f), new Vector2(620f, 110f), Vector2.zero);
            _feedback.gameObject.SetActive(false);
        }

        BindInput();
        ConfigurePortraitPresentation(true);
    }

    private void Update()
    {
        BindInput();
        ConfigurePortraitPresentation(false);
        if (_feedback != null && _feedback.gameObject.activeSelf
            && Time.unscaledTime >= _feedbackUntil)
            _feedback.gameObject.SetActive(false);
    }

    private void BindInput()
    {
        if (_input == InputManager.Instance) return;
        if (_input != null) _input.SwipeResolved -= OnSwipeResolved;
        _input = InputManager.Instance;
        if (_input != null) _input.SwipeResolved += OnSwipeResolved;
    }

    private void OnSwipeResolved(SwipeDirection direction, bool accepted)
    {
        if (_feedback == null) return;
        _feedback.text = DirectionLabel(direction) + (accepted ? "  OK" : "  BLOCKED");
        _feedback.color = accepted
            ? new Color(0.45f, 0.95f, 0.80f, 1f)
            : new Color(1f, 0.48f, 0.42f, 1f);
        _feedbackUntil = Time.unscaledTime + 0.65f;
        _feedback.gameObject.SetActive(true);
        _feedback.transform.SetAsLastSibling();
    }

    private static string DirectionLabel(SwipeDirection direction)
    {
        switch (direction)
        {
            case SwipeDirection.Up: return "UP";
            case SwipeDirection.Down: return "DOWN";
            case SwipeDirection.Left: return "LEFT";
            case SwipeDirection.Right: return "RIGHT";
            default: return "";
        }
    }

    private void ConfigurePortraitPresentation(bool force)
    {
        Vector2Int size = new Vector2Int(Screen.width, Screen.height);
        if (!force && size == _configuredSize) return;
        _configuredSize = size;

        foreach (CanvasScaler scaler in FindObjectsOfType<CanvasScaler>())
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        Camera camera = Camera.main;
        if (camera == null) return;
        camera.fieldOfView = 70f;
        CameraFollow follow = camera.GetComponent<CameraFollow>();
        if (follow != null) follow.offset = new Vector3(0f, 5.8f, -10.8f);
    }

    private void OnDestroy()
    {
        if (_input != null) _input.SwipeResolved -= OnSwipeResolved;
    }
}
#endif
