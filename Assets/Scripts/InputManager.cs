using UnityEngine;

public enum SwipeDirection { None, Up, Down, Left, Right }

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public float minSwipeDistance = 30f;

    private Vector2 _touchStart;
    private bool _swipeDetected;
    private SwipeDirection _currentSwipe = SwipeDirection.None;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Update()
    {
        _currentSwipe = SwipeDirection.None;
        bool keyboardUsed = false;

        // Keyboard input for editor testing
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            _currentSwipe = SwipeDirection.Left;
            keyboardUsed = true;
        }
        else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            _currentSwipe = SwipeDirection.Right;
            keyboardUsed = true;
        }
        else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
        {
            _currentSwipe = SwipeDirection.Up;
            keyboardUsed = true;
        }
        else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
        {
            _currentSwipe = SwipeDirection.Down;
            keyboardUsed = true;
        }

        if (keyboardUsed) return;

        // Touch input
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                _touchStart = touch.position;
                _swipeDetected = false;
            }
            else if (touch.phase == TouchPhase.Ended && !_swipeDetected)
            {
                DetectSwipe(touch.position);
            }
        }

        // Mouse fallback for editor
        if (Input.GetMouseButtonDown(0))
        {
            _touchStart = Input.mousePosition;
            _swipeDetected = false;
        }
        if (Input.GetMouseButtonUp(0) && !_swipeDetected)
        {
            DetectSwipe(Input.mousePosition);
        }
    }

    void DetectSwipe(Vector2 endPos)
    {
        Vector2 delta = endPos - _touchStart;
        if (delta.magnitude < minSwipeDistance) return;

        _swipeDetected = true;
        float absX = Mathf.Abs(delta.x);
        float absY = Mathf.Abs(delta.y);

        if (absX > absY)
            _currentSwipe = delta.x > 0 ? SwipeDirection.Right : SwipeDirection.Left;
        else
            _currentSwipe = delta.y > 0 ? SwipeDirection.Up : SwipeDirection.Down;
    }

    public SwipeDirection GetSwipe() => _currentSwipe;
}
