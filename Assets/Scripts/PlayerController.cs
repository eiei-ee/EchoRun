using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Lanes")]
    public float laneDistance = 3f;
    public float laneSwitchSpeed = 20f;

    [Header("Jump")]
    public float jumpHeight = 3f;
    public float jumpDuration = 0.9f;

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
    private readonly RaycastHit[] _obstacleSweepHits = new RaycastHit[8];
    private readonly Collider[] _obstacleOverlapHits = new Collider[8];
    private Collider _lastSweptObstacle;

    private const float GROUND_RAY_DIST = 0.3f;

    // Saved originals so body-part adjustments are idempotent across scene reloads
    private bool _modelPartsSaved;
    private Vector3 _savedHeadPos, _savedTorsoScale;
    private Vector3 _savedLegUpperL, _savedLegUpperR, _savedLegLowerL, _savedLegLowerR;
    private Vector3 _savedFootL, _savedFootR;

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
        {
            _originalModelScale = characterModel.localScale;

            // Position model at capsule bottom so feet sit on the track surface
            if (_capsuleCollider != null)
            {
                float capsuleBottom = _capsuleCollider.center.y - _capsuleCollider.height * 0.5f;
                characterModel.localPosition = new Vector3(
                    characterModel.localPosition.x, capsuleBottom, characterModel.localPosition.z);
            }
            _originalModelPos = characterModel.localPosition;

            ApplyBodyPartAdjustments();
        }
    }

    void ApplyBodyPartAdjustments()
    {
        Transform head = characterModel.Find("Head");
        Transform torso = characterModel.Find("Torso");

        // Save originals on first call so adjustments never compound across restarts
        if (!_modelPartsSaved)
        {
            if (head != null) _savedHeadPos = head.localPosition;
            if (torso != null) _savedTorsoScale = torso.localScale;

            Transform t;
            t = characterModel.Find("Leg_Upper_L"); if (t != null) _savedLegUpperL = t.localScale;
            t = characterModel.Find("Leg_Upper_R"); if (t != null) _savedLegUpperR = t.localScale;
            t = characterModel.Find("Leg_Lower_L"); if (t != null) _savedLegLowerL = t.localScale;
            t = characterModel.Find("Leg_Lower_R"); if (t != null) _savedLegLowerR = t.localScale;
            t = characterModel.Find("Foot_L"); if (t != null) _savedFootL = t.localScale;
            t = characterModel.Find("Foot_R"); if (t != null) _savedFootR = t.localScale;

            _modelPartsSaved = true;
        }

        if (head != null) head.localPosition = _savedHeadPos;
        if (torso != null) torso.localScale = _savedTorsoScale;

        ApplyScaleFromSaved("Leg_Upper_L", _savedLegUpperL, 1f);
        ApplyScaleFromSaved("Leg_Upper_R", _savedLegUpperR, 1f);
        ApplyScaleFromSaved("Leg_Lower_L", _savedLegLowerL, 1f);
        ApplyScaleFromSaved("Leg_Lower_R", _savedLegLowerR, 1f);
        ApplyScaleFromSaved("Foot_L", _savedFootL, 1f);
        ApplyScaleFromSaved("Foot_R", _savedFootR, 1f);
    }

    void ApplyScaleFromSaved(string name, Vector3 saved, float multiplier)
    {
        Transform t = characterModel.Find(name);
        if (t != null) t.localScale = new Vector3(saved.x * multiplier, saved.y, saved.z * multiplier);
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
        SweepForObstacleContact(forward);
        if (_gm.State != GameState.Playing || _gm.IsDeathSequence) return;

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
            float progress = Mathf.Clamp01(_jumpTimer / Mathf.Max(0.01f, jumpDuration));
            float targetHeight = EvaluateJumpArc(progress) * jumpHeight;
            float targetY = _jumpGroundY + targetHeight;
            vel.y = (targetY - _rb.position.y) / Time.fixedDeltaTime;

            if (progress >= 1f)
            {
                IsJumping = false;
                Vector3 landedPosition = _rb.position;
                landedPosition.y = _jumpGroundY;
                _rb.position = landedPosition;
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

        int previousLane = CurrentLane;

        switch (swipe)
        {
            case SwipeDirection.Left:
                if (CurrentLane > 0)
                {
                    AIShadowRunner.Instance?.RecordPlayerAction(
                        ShadowAction.Left, CurrentLane);
                    CurrentLane--;
                }
                break;
            case SwipeDirection.Right:
                if (CurrentLane < 2)
                {
                    AIShadowRunner.Instance?.RecordPlayerAction(
                        ShadowAction.Right, CurrentLane);
                    CurrentLane++;
                }
                break;
            case SwipeDirection.Up:
               if (!IsJumping && IsGrounded())
               {
                   AIShadowRunner.Instance?.RecordPlayerAction(
                       ShadowAction.Jump, CurrentLane);
                   IsJumping = true;
                    _jumpTimer = 0f;
                    _jumpGroundY = _rb.position.y;
                    AITrackDirector.Instance?.RecordJump();
                    AudioManager.Instance?.PlayJump();
               }
                break;
            case SwipeDirection.Down:
               if (!IsSliding && IsGrounded())
               {
       AIShadowRunner.Instance?.RecordPlayerAction(
           ShadowAction.Slide, CurrentLane);
       IsSliding = true;
       _slideTimer = 0f;
         _slideTrailTimer = 0f;
       AITrackDirector.Instance?.RecordSlide();
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

        if (CurrentLane != previousLane)
            AITrackDirector.Instance?.RecordLaneChange(CurrentLane);
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
            AITrackDirector.Instance?.RecordCoin();
            AIShadowRunner.Instance?.RecordCoin();
            AudioManager.Instance?.PlayCoin();
            ParticleManager.Instance?.EmitCoin(other.transform.position);
            if (TrackManager.Instance != null)
                TrackManager.Instance.ReleaseDynamic(other.gameObject);
            else
                other.gameObject.SetActive(false);
            return;
        }

        Obstacle obs = other.GetComponentInParent<Obstacle>();
        if (obs != null && other != _lastSweptObstacle)
            HandleObstacleContact(other, obs);
   }

   void SweepForObstacleContact(Vector3 forward)
   {
       if (_capsuleCollider == null || forward.sqrMagnitude < 0.0001f) return;

       if (_lastSweptObstacle != null)
       {
           Vector3 offset = _lastSweptObstacle.bounds.center - _rb.position;
           if (Vector3.Dot(offset, forward) <= 0f)
               _lastSweptObstacle = null;
       }

       GetCapsuleSweepShape(out Vector3 pointA, out Vector3 pointB,
           out float radius);

       // CapsuleCast does not report colliders that already overlap the
       // capsule. Pooled triggers and low frame rates can place the player
       // inside an obstacle before this check runs, so handle overlaps first.
       int overlapCount = Physics.OverlapCapsuleNonAlloc(pointA, pointB,
           radius, _obstacleOverlapHits, Physics.AllLayers,
           QueryTriggerInteraction.Collide);
       Collider overlappingObstacle = FindObstacleCollider(
           _obstacleOverlapHits, overlapCount, _lastSweptObstacle);
       if (overlappingObstacle != null)
       {
           _lastSweptObstacle = overlappingObstacle;
           HandleObstacleContact(overlappingObstacle,
               overlappingObstacle.GetComponentInParent<Obstacle>());
           return;
       }

       float distance = _gm.CurrentSpeed * Time.fixedDeltaTime + 0.05f;
       int hitCount = Physics.CapsuleCastNonAlloc(pointA, pointB, radius,
           forward.normalized, _obstacleSweepHits, distance,
           Physics.AllLayers, QueryTriggerInteraction.Collide);

       Collider closestObstacle = null;
       float closestDistance = float.MaxValue;
       for (int i = 0; i < hitCount; i++)
       {
           Collider candidate = _obstacleSweepHits[i].collider;
           if (candidate == null || candidate == _lastSweptObstacle) continue;
           if (candidate.GetComponentInParent<Obstacle>() == null) continue;
           if (_obstacleSweepHits[i].distance < closestDistance)
           {
               closestObstacle = candidate;
               closestDistance = _obstacleSweepHits[i].distance;
           }
       }

       if (closestObstacle == null) return;

       _lastSweptObstacle = closestObstacle;
       HandleObstacleContact(closestObstacle,
           closestObstacle.GetComponentInParent<Obstacle>());
   }

   public static Collider FindObstacleCollider(Collider[] candidates, int count,
       Collider ignored)
   {
       if (candidates == null) return null;
       int limit = Mathf.Min(Mathf.Max(0, count), candidates.Length);
       for (int i = 0; i < limit; i++)
       {
           Collider candidate = candidates[i];
           if (candidate == null || candidate == ignored) continue;
           if (candidate.GetComponentInParent<Obstacle>() != null)
               return candidate;
       }
       return null;
   }

   void GetCapsuleSweepShape(out Vector3 pointA, out Vector3 pointB,
       out float radius)
   {
       Vector3 scale = transform.lossyScale;
       float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y),
           Mathf.Abs(scale.z));
       radius = _capsuleCollider.radius * maxScale;
       Vector3 axis = _capsuleCollider.direction == 0
           ? transform.right
           : (_capsuleCollider.direction == 2 ? transform.forward : transform.up);
       float height = _capsuleCollider.height * maxScale;
       float pointOffset = Mathf.Max(0f, height * 0.5f - radius);
       Vector3 center = transform.TransformPoint(_capsuleCollider.center);
       pointA = center + axis * pointOffset;
       pointB = center - axis * pointOffset;
   }

   void HandleObstacleContact(Collider other, Obstacle obs)
   {
       if (obs == null || _gm == null || _gm.State != GameState.Playing
           || _gm.IsDeathSequence) return;

       if (obs.type == ObstacleType.Low && IsSliding)
            {
                AIPlayerSkillEstimator.RecordObstacleOutcome(
                    obs.type, true);
                AITrackDirector.Instance?.RecordDodge();
                AIShadowRunner.Instance?.RecordDodge();
                AudioManager.Instance?.PlayDodgeObstacle();
               return;
           }
           if (obs.type == ObstacleType.High && IsJumping &&
               GetColliderBottomY() > other.bounds.max.y - 0.3f)
            {
                AIPlayerSkillEstimator.RecordObstacleOutcome(
                    obs.type, true);
                AITrackDirector.Instance?.RecordDodge();
                AIShadowRunner.Instance?.RecordDodge();
                AudioManager.Instance?.PlayDodgeObstacle();
                return;
            }
           AIPlayerSkillEstimator.RecordObstacleOutcome(
               obs.type, false);
           AITrackDirector.Instance?.RecordObstacleHit();
           AIShadowRunner.Instance?.RecordObstacleHit();
           AudioManager.Instance?.PlayCollision();
           if (PowerUpController.Instance != null
               && PowerUpController.Instance.TryAbsorbCollision())
           {
               if (TrackManager.Instance != null)
                   TrackManager.Instance.ReleaseDynamic(other.gameObject);
               else
                   other.gameObject.SetActive(false);
               return;
           }
           StopBeforeObstacle(other);
           _gm.GameOver();
   }

   public static float EvaluateJumpArc(float progress)
   {
       float t = Mathf.Clamp01(progress);
       return 4f * t * (1f - t);
   }

   public static Vector3 CalculateObstacleStopPosition(Bounds obstacleBounds,
       Vector3 playerPosition, Vector3 forward, float clearance)
   {
       Vector3 direction = forward.sqrMagnitude > 0.0001f
           ? forward.normalized
           : Vector3.forward;
       Vector3 extents = obstacleBounds.extents;
       float projectedHalfDepth = Mathf.Abs(direction.x) * extents.x
                                  + Mathf.Abs(direction.y) * extents.y
                                  + Mathf.Abs(direction.z) * extents.z;
       float obstacleFront = Vector3.Dot(obstacleBounds.center, direction)
                             - projectedHalfDepth;
       float safeProjection = obstacleFront - Mathf.Max(0f, clearance);
       float correction = safeProjection - Vector3.Dot(playerPosition, direction);
       return correction < 0f
           ? playerPosition + direction * correction
           : playerPosition;
   }

   void StopBeforeObstacle(Collider obstacle)
   {
       float clearance = _capsuleCollider != null
           ? _capsuleCollider.radius + 0.05f
           : 0.45f;
       _rb.position = CalculateObstacleStopPosition(
           obstacle.bounds, _rb.position, ForwardDirection, clearance);
       _rb.velocity = Vector3.zero;
   }

   float GetColliderBottomY()
   {
       if (_capsuleCollider == null) return _rb.position.y;
       return _rb.position.y + _capsuleCollider.center.y
              - _capsuleCollider.height * 0.5f;
   }
}
