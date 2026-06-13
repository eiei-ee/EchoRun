using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 5f, -8f);
    public float smoothSpeed = 8f;

    private Vector3 _velocity = Vector3.zero;
    private PlayerController _pc;
   private float _invSmoothSpeed;
    private Vector3 _shakeOffset;
    private float _shakeTimer;

    void Start()
    {
        _invSmoothSpeed = 1f / smoothSpeed;
        if (target != null)
            _pc = target.GetComponent<PlayerController>();
    }

    void LateUpdate()
    {
       if (target == null) return;

        // Death camera shake
        if (GameManager.Instance != null && GameManager.Instance.IsDeathSequence && _shakeTimer <= 0f)
        {
            _shakeTimer = 0.8f;
        }
        if (_shakeTimer > 0f)
        {
            _shakeOffset = Random.insideUnitSphere * 0.4f * (_shakeTimer / 0.8f);
            _shakeTimer -= Time.unscaledDeltaTime;
            if (_shakeTimer <= 0f)
                _shakeOffset = Vector3.zero;
        }

        // Refresh cached PlayerController if target changed
        if (_pc == null && target != null)
            _pc = target.GetComponent<PlayerController>();

        Vector3 forward = _pc != null ? _pc.ForwardDirection : Vector3.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 worldOffset = forward * offset.z + Vector3.up * offset.y + right * offset.x;
        Vector3 targetPos = target.position + worldOffset;
       transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _velocity, _invSmoothSpeed);
        transform.position += _shakeOffset;

        Vector3 lookTarget = target.position + forward * 5f;
        Quaternion targetRot = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * smoothSpeed);
    }
}
