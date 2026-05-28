using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Lanes")]
    public float laneDistance = 3f;
    public float laneSwitchSpeed = 15f;

    [Header("Jump")]
    public float jumpHeight = 3f;
    public float jumpDuration = 0.6f;
    public AnimationCurve jumpCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Slide")]
    public float slideDuration = 0.8f;
    public float slideScaleY = 0.5f;
    public float slideColliderHeight = 1f;

    [Header("Ground Check")]
    public LayerMask groundLayer;

    public int CurrentLane { get; private set; } = 1; // 0=left, 1=center, 2=right
    public bool IsJumping { get; private set; }
    public bool IsSliding { get; private set; }

    private float _jumpTimer;
    private float _slideTimer;
    private float _jumpGroundY;
    private float _originalColliderHeight;
    private Vector3 _originalScale;
    private CapsuleCollider _capsuleCollider;
    private Rigidbody _rb;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        _capsuleCollider = GetComponent<CapsuleCollider>();
        _originalScale = transform.localScale;
        if (_capsuleCollider != null)
            _originalColliderHeight = _capsuleCollider.height;
    }

    void Update()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.State != GameState.Playing) return;

        HandleInput();
        UpdateSlide();
    }

    void FixedUpdate()
    {
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.State != GameState.Playing) return;

        if (_rb.position.y < -5f)
        {
            GameManager.Instance.GameOver();
            return;
        }

        Vector3 vel = _rb.velocity;

        // Forward
        vel.z = GameManager.Instance.CurrentSpeed;

        // Lane switching
        float targetX = (CurrentLane - 1) * laneDistance;
        float laneX = Mathf.MoveTowards(_rb.position.x, targetX, laneSwitchSpeed * Time.fixedDeltaTime);
        vel.x = (laneX - _rb.position.x) / Time.fixedDeltaTime;

        // Jump
        if (IsJumping)
        {
            _jumpTimer += Time.fixedDeltaTime;
            float progress = _jumpTimer / jumpDuration;
            float targetHeight = jumpCurve.Evaluate(progress) * jumpHeight;
            float targetY = _jumpGroundY + targetHeight;
            vel.y = (targetY - _rb.position.y) / Time.fixedDeltaTime;

            if (progress >= 1f)
            {
                IsJumping = false;
                vel.y = 0;
            }
        }

        _rb.velocity = vel;
    }

    void HandleInput()
    {
        SwipeDirection swipe = InputManager.Instance != null
            ? InputManager.Instance.GetSwipe()
            : SwipeDirection.None;

        if (swipe == SwipeDirection.None) return;

        switch (swipe)
        {
            case SwipeDirection.Left:
                if (CurrentLane > 0) CurrentLane--;
                break;
            case SwipeDirection.Right:
                if (CurrentLane < 2) CurrentLane++;
                break;
            case SwipeDirection.Up:
                if (!IsJumping && IsGrounded())
                {
                    IsJumping = true;
                    _jumpTimer = 0f;
                    _jumpGroundY = _rb.position.y;
                }
                break;
            case SwipeDirection.Down:
                if (!IsSliding && IsGrounded())
                {
                    IsSliding = true;
                    _slideTimer = 0f;
                    if (_capsuleCollider != null)
                        _capsuleCollider.height = slideColliderHeight;
                    transform.localScale = new Vector3(_originalScale.x, slideScaleY, _originalScale.z);
                }
                break;
        }
    }

    void UpdateSlide()
    {
        if (!IsSliding) return;

        _slideTimer += Time.deltaTime;
        if (_slideTimer >= slideDuration)
        {
            IsSliding = false;
            transform.localScale = _originalScale;
            if (_capsuleCollider != null)
                _capsuleCollider.height = _originalColliderHeight;
        }
    }

    bool IsGrounded()
    {
        if (_capsuleCollider == null) return false;
        // Raycast from bottom of capsule, not center
        float halfHeight = _capsuleCollider.height / 2f;
        Vector3 feet = _rb.position + Vector3.down * halfHeight;
        return Physics.Raycast(feet, Vector3.down, 0.3f, groundLayer);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            GameManager.Instance.GameOver();
        }
        else if (other.CompareTag("Coin"))
        {
            GameManager.Instance.AddCoins(1);
            other.gameObject.SetActive(false);
        }
    }
}
