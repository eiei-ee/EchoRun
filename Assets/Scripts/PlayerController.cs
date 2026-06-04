using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Lanes")]
    public float laneDistance = 3f;
    public float laneSwitchSpeed = 20f;

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

    [Header("Character Model")]
    public Transform characterModel;

    public int CurrentLane { get; private set; } = 1;
    public bool IsJumping { get; private set; }
    public bool IsSliding { get; private set; }
    public Vector3 ForwardDirection { get; private set; } = Vector3.forward;

    private float _jumpTimer;
    private float _slideTimer;
    private float _jumpGroundY;
    private float _originalColliderHeight;
    private Vector3 _originalModelScale = Vector3.one;
    private CapsuleCollider _capsuleCollider;
    private Rigidbody _rb;
    private TrackSegmentData _lastTurnSegment;
    private float _laneOffset;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        _capsuleCollider = GetComponent<CapsuleCollider>();
        if (_capsuleCollider != null)
            _originalColliderHeight = _capsuleCollider.height;

        if (characterModel != null)
            _originalModelScale = characterModel.localScale;
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

        UpdateForwardDirection();

        Vector3 forward = ForwardDirection;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 vel = _rb.velocity;

        // Forward speed in facing direction
        Vector3 forwardVel = forward * GameManager.Instance.CurrentSpeed;
        vel.x = forwardVel.x;
        vel.z = forwardVel.z;

        // Lane switching — use tracked scalar offset, not world position projection
        float targetLateral = (CurrentLane - 1) * laneDistance;
        float currentOffset = _laneOffset;
        _laneOffset = Mathf.MoveTowards(currentOffset, targetLateral,
                                         laneSwitchSpeed * Time.fixedDeltaTime);
        float lateralVel = (_laneOffset - currentOffset) / Time.fixedDeltaTime;
        vel.x += right.x * lateralVel;
        vel.z += right.z * lateralVel;

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

    void UpdateForwardDirection()
    {
        if (TrackManager.Instance == null) return;

        TrackSegmentData turnSeg = TrackManager.Instance.FindTurnAtPosition(_rb.position);
        if (turnSeg == null)
        {
            _lastTurnSegment = null;
            return;
        }

        if (turnSeg == _lastTurnSegment) return;

        Vector3 entryDir = turnSeg.entryDirection;
        float distPastCorner = Vector3.Dot(_rb.position - turnSeg.turnPointWorld, entryDir);

        if (distPastCorner > 0f)
        {
            Vector3 entryRight = Vector3.Cross(Vector3.up, entryDir).normalized;
            Vector3 exitDir = turnSeg.exitDirection;
            Vector3 exitRight = Vector3.Cross(Vector3.up, exitDir).normalized;

            float laneOffset = Vector3.Dot(_rb.position - turnSeg.turnPointWorld, entryRight);

            ForwardDirection = exitDir;
            _laneOffset = laneOffset;

            Vector3 newPos = turnSeg.turnPointWorld
                + exitDir * distPastCorner
                + exitRight * laneOffset;
            newPos.y = _rb.position.y;
            _rb.position = newPos;

            _lastTurnSegment = turnSeg;
        }
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
                    if (characterModel != null)
                        characterModel.localScale = new Vector3(_originalModelScale.x, slideScaleY, _originalModelScale.z);
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
            if (characterModel != null)
                characterModel.localScale = _originalModelScale;
            if (_capsuleCollider != null)
                _capsuleCollider.height = _originalColliderHeight;
        }
    }

    bool IsGrounded()
    {
        if (_capsuleCollider == null) return false;
        float halfHeight = _capsuleCollider.height / 2f;
        Vector3 feet = _rb.position + Vector3.down * halfHeight;
        return Physics.Raycast(feet, Vector3.down, 0.3f, groundLayer);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle"))
        {
            Obstacle obs = other.GetComponent<Obstacle>();
            if (obs == null) { GameManager.Instance.GameOver(); return; }

            if (obs.type == ObstacleType.Low && IsSliding)
            {
                other.gameObject.SetActive(false);
                return;
            }
            if (obs.type == ObstacleType.High && IsJumping &&
                _rb.position.y - _capsuleCollider.height * 0.5f > other.bounds.max.y - 0.3f)
            {
                other.gameObject.SetActive(false);
                return;
            }
            GameManager.Instance.GameOver();
        }
        else if (other.CompareTag("Coin"))
        {
            GameManager.Instance.AddCoins(1);
            other.gameObject.SetActive(false);
        }
    }
}
