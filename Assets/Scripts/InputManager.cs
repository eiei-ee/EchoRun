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
        // Keyboard input for editor testing
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
        {
            _currentSwipe = SwipeDirection.Left;
            return;
        }
        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
        {
            _currentSwipe = SwipeDirection.Right;
            return;
        }
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.Space))
        {
            _currentSwipe = SwipeDirection.Up;
            return;
        }
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.LeftControl))
        {
            _currentSwipe = SwipeDirection.Down;
            return;
        }

        // Touch input
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                _touchStart = touch.position;
                _swipeDetected = false;
            }
            else if (touch.phase == TouchPhase.Moved && !_swipeDetected)
            {
                DetectSwipe(touch.position);
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

    public SwipeDirection GetSwipe()
    {
        SwipeDirection s = _currentSwipe;
        _currentSwipe = SwipeDirection.None;
        return s;
    }
}
