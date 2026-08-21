using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 4f, -7f);
    public float smoothSpeed = 8f;

    private Vector3 _velocity = Vector3.zero;
   private PlayerController _pc;
   private float _invSmoothSpeed;
    private Vector3 _shakeOffset;
    private float _shakeTimer;
    private float _groundedTargetY;
    private bool _hasGroundedTargetY;

    void Start()
    {
        _invSmoothSpeed = 1f / smoothSpeed;
        if (target != null)
        {
           _pc = target.GetComponent<PlayerController>();
           _groundedTargetY = target.position.y;
           _hasGroundedTargetY = true;
       }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Death camera shake can be disabled from the accessibility settings.
        if (EchoRunAccessibility.ReducedMotion)
        {
            _shakeTimer = 0f;
            _shakeOffset = Vector3.zero;
        }
        else if (GameManager.Instance != null && GameManager.Instance.IsDeathSequence
                 && _shakeTimer <= 0f)
        {
            _shakeTimer = 0.8f;
        }
        if (!EchoRunAccessibility.ReducedMotion && _shakeTimer > 0f)
        {
            _shakeOffset = Random.insideUnitSphere * 0.4f * (_shakeTimer / 0.8f);
            _shakeTimer -= Time.unscaledDeltaTime;
            if (_shakeTimer <= 0f)
                _shakeOffset = Vector3.zero;
        }

        // Refresh cached references if target changed
       if (_pc == null && target != null)
           _pc = target.GetComponent<PlayerController>();

        bool isJumping = _pc != null && _pc.IsJumping;
        if (!_hasGroundedTargetY || !isJumping)
        {
            _groundedTargetY = target.position.y;
            _hasGroundedTargetY = true;
        }
        Vector3 followAnchor = ResolveFollowAnchor(
            target.position, isJumping, _groundedTargetY);

        Vector3 forward = _pc != null ? _pc.ForwardDirection : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 worldOffset = forward * offset.z + Vector3.up * offset.y + right * offset.x;
        Vector3 targetPos = followAnchor + worldOffset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _velocity, _invSmoothSpeed);
        transform.position += _shakeOffset;

        // Look at player center (above the track), not model center (below the track)
        Vector3 lookTarget = followAnchor + forward * 5f;
        Quaternion targetRot = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * smoothSpeed);
    }

    public static Vector3 ResolveFollowAnchor(
        Vector3 targetPosition, bool isJumping, float groundedY)
    {
        if (isJumping)
            targetPosition.y = Mathf.Lerp(groundedY, targetPosition.y, 0.35f);
        return targetPosition;
    }
}
