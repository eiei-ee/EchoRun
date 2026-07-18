using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public enum SwipeDirection { None, Up, Down, Left, Right }

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public float minSwipeDistance = 30f;

    private Vector2 _touchStart;
    private bool _swipeDetected;
    private bool _ignoreTouch;
    private Queue<SwipeDirection> _swipeQueue = new Queue<SwipeDirection>();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        GameManager gameManager = GameManager.Instance;
        if (gameManager == null)
        {
            ClearInput();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (gameManager.State == GameState.Playing)
                gameManager.Pause();
            else if (gameManager.State == GameState.Paused)
                gameManager.Resume();
            return;
        }

        if (gameManager.State != GameState.Playing)
        {
            ClearInput();
            return;
        }

        // Keyboard input - queue all pressed keys instead of returning early
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            _swipeQueue.Enqueue(SwipeDirection.Left);
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            _swipeQueue.Enqueue(SwipeDirection.Right);
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space))
            _swipeQueue.Enqueue(SwipeDirection.Up);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftControl))
            _swipeQueue.Enqueue(SwipeDirection.Down);

        // Touch input
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                _ignoreTouch = EventSystem.current != null
                    && EventSystem.current.IsPointerOverGameObject(touch.fingerId);
                _touchStart = touch.position;
                _swipeDetected = false;
            }
            else if (touch.phase == TouchPhase.Moved && !_swipeDetected && !_ignoreTouch)
            {
                DetectSwipe(touch.position);
            }
            else if (touch.phase == TouchPhase.Ended && !_swipeDetected && !_ignoreTouch)
            {
                DetectSwipe(touch.position);
            }
        }

#if UNITY_EDITOR
        // Mouse fallback for editor
        if (Input.GetMouseButtonDown(0))
        {
            _ignoreTouch = EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject();
            _touchStart = Input.mousePosition;
            _swipeDetected = false;
        }
        if (Input.GetMouseButtonUp(0) && !_swipeDetected && !_ignoreTouch)
        {
            DetectSwipe(Input.mousePosition);
        }
#endif
    }

    void DetectSwipe(Vector2 endPos)
    {
        Vector2 delta = endPos - _touchStart;
        if (delta.magnitude < minSwipeDistance) return;

        _swipeDetected = true;
        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);

        if (absX > absY)
        {
            if (delta.x > 0) _swipeQueue.Enqueue(SwipeDirection.Right);
            else _swipeQueue.Enqueue(SwipeDirection.Left);
        }
        else
        {
            if (delta.y > 0) _swipeQueue.Enqueue(SwipeDirection.Up);
            else _swipeQueue.Enqueue(SwipeDirection.Down);
        }
    }

    public SwipeDirection GetSwipe()
    {
        return _swipeQueue.Count > 0 ? _swipeQueue.Dequeue() : SwipeDirection.None;
    }

    public void ClearInput()
    {
        _swipeQueue.Clear();
        _swipeDetected = false;
        _ignoreTouch = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
