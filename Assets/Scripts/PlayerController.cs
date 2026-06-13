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

    [Header("Fall Off")]
    public float fallOffY = -5f;

    public int CurrentLane { get; private set; } = 1;
    public bool IsJumping { get; private set; }
    public bool IsSliding { get; private set; }
    public Vector3 ForwardDirection { get; private set; } = Vector3.forward;

    private float _jumpTimer;
   private float _slideTimer;
    private float _slideTrailTimer;
    private float _runTrailTimer;
    private float _jumpGroundY;
    private float _originalColliderHeight;
   private Vector3 _originalModelScale = Vector3.one;
    private Vector3 _originalModelPos;
    private CapsuleCollider _capsuleCollider;
    private Rigidbody _rb;
    private TrackSegmentData _lastTurnSegment;
    private float _laneOffset;
    private GameManager _gm;
    private InputManager _input;
    private TrackManager _trackMgr;

    private const float GROUND_RAY_DIST = 0.3f;

    void Start()
    {
        _gm = GameManager.Instance;
        _input = InputManager.Instance;
        _trackMgr = TrackManager.Instance;

        _rb = GetComponent<Rigidbody>();
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;

        _capsuleCollider = GetComponent<CapsuleCollider>();
        if (_capsuleCollider != null)
            _originalColliderHeight = _capsuleCollider.height;

       if (characterModel != null)
           _originalModelScale = characterModel.localScale;
        if (characterModel != null)
            _originalModelPos = characterModel.localPosition;
    }

    void Update()
    {
       if (_gm == null || _gm.State != GameState.Playing) return;
        if (_gm.IsDeathSequence) return;

       HandleInput();
       UpdateSlide();

        // Running trail dust
        if (_runTrailTimer > 0.12f)
        {
            _runTrailTimer = 0f;
            ParticleManager.Instance?.EmitTrail(_rb.position + Vector3.down * 0.8f);
        }
        _runTrailTimer += Time.deltaTime;
   }

   void FixedUpdate()
   {
       if (_gm == null || _gm.State != GameState.Playing) return;
        if (_gm.IsDeathSequence)
        {
            _rb.velocity = Vector3.zero;
            return;
        }

        if (_rb.position.y < fallOffY)
        {
            _gm.GameOver();
            return;
        }

        UpdateForwardDirection();

        Vector3 forward = ForwardDirection;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 vel = _rb.velocity;

        // Forward speed in facing direction
        Vector3 forwardVel = forward * _gm.CurrentSpeed;
        vel.x = forwardVel.x;
        vel.z = forwardVel.z;

        // Lane switching - use tracked scalar offset, not world position projection
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
        if (_trackMgr == null) return;

        TrackSegmentData turnSeg = _trackMgr.FindTurnAtPosition(_rb.position);
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
        SwipeDirection swipe = _input != null ? _input.GetSwipe() : SwipeDirection.None;
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
                    AudioManager.Instance?.PlayJump();
               }
                break;
            case SwipeDirection.Down:
               if (!IsSliding && IsGrounded())
               {
       IsSliding = true;
       _slideTimer = 0f;
        _slideTrailTimer = 0f;
       AudioManager.Instance?.PlaySlide();
                    if (_capsuleCollider != null)
                        _capsuleCollider.height = slideColliderHeight;
               if (characterModel != null)
                   characterModel.localScale = new Vector3(_originalModelScale.x, slideScaleY, _originalModelScale.z);
                if (characterModel != null)
                    characterModel.localPosition = _originalModelPos + Vector3.up * (_originalColliderHeight - slideColliderHeight) * 0.5f;
                }
                break;
        }
    }

    void UpdateSlide()
    {
        if (!IsSliding) return;

       _slideTimer += Time.deltaTime;

        // Slide dust trail
        if (_slideTrailTimer > 0.06f)
        {
            _slideTrailTimer = 0f;
            ParticleManager.Instance?.EmitDust(_rb.position + Vector3.down * 0.5f);
        }
        _slideTrailTimer += Time.deltaTime;

       if (_slideTimer >= slideDuration)
       {
           IsSliding = false;
           if (characterModel != null)
               characterModel.localScale = _originalModelScale;
            if (characterModel != null)
                characterModel.localPosition = _originalModelPos;
           if (_capsuleCollider != null)
               _capsuleCollider.height = _originalColliderHeight;
       }
    }

    bool IsGrounded()
    {
        if (_capsuleCollider == null) return false;
        float bottom = _rb.position.y + _capsuleCollider.center.y
                       - _capsuleCollider.height / 2f;
        float rayStart = bottom + 0.1f;
        return Physics.Raycast(new Vector3(_rb.position.x, rayStart, _rb.position.z),
                               Vector3.down, GROUND_RAY_DIST + 0.2f, groundLayer);
    }

   void OnTriggerEnter(Collider other)
   {
        Coin coin = other.GetComponent<Coin>();
        if (coin != null)
        {
            _gm.AddCoins(1);
            other.gameObject.SetActive(false);
            AudioManager.Instance?.PlayCoin();
            ParticleManager.Instance?.EmitCoin(other.transform.position);
            return;
        }

        Obstacle obs = other.GetComponent<Obstacle>();
        if (obs != null)
        {
           if (obs.type == ObstacleType.Low && IsSliding)
           {
               AudioManager.Instance?.PlayDodgeObstacle();
               return;
           }
           if ((obs.type == ObstacleType.High || obs.type == ObstacleType.Barrier) && IsJumping &&
               _rb.position.y - _capsuleCollider.height * 0.5f > other.bounds.max.y - 0.3f)
           {
               AudioManager.Instance?.PlayDodgeObstacle();
               return;
           }
           _gm.GameOver();
       }
   }
}
