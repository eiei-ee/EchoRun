using UnityEngine;

public class Coin : MonoBehaviour
{
    public float rotateSpeed = 180f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.3f;

    private float _baseY;
    private float _phaseOffset;
    private Transform _player;
    private static Transform _cachedPlayer;
    private static int _lastPlayerLookupFrame = -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlayerCache()
    {
        _cachedPlayer = null;
        _lastPlayerLookupFrame = -1;
    }

    void OnEnable()
    {
        _baseY = transform.position.y;
        _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        _player = _cachedPlayer;
    }

    void Update()
    {
        if (!EchoRunAccessibility.ReducedMotion)
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

        PowerUpController powerUps = PowerUpController.Instance;
        if (powerUps != null && powerUps.HasMagnet && ResolvePlayer() != null)
        {
            float radius = Mathf.Max(0.01f, powerUps.MagnetRadius);
            float distance = Vector3.Distance(transform.position, _player.position);
            if (distance <= radius)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, _player.position + Vector3.up * 0.7f,
                    Mathf.Lerp(8f, 24f, Mathf.Clamp01(1f - distance / radius))
                    * Time.deltaTime);
                return;
            }
        }

        float bob = EchoRunAccessibility.ReducedMotion
            ? 0f
            : Mathf.Sin((Time.time + _phaseOffset) * bobSpeed) * bobHeight;
        Vector3 pos = transform.position;
        pos.y = _baseY + bob;
        transform.position = pos;
    }

    private Transform ResolvePlayer()
    {
        if (_player != null) return _player;
        if (_cachedPlayer != null)
        {
            _player = _cachedPlayer;
            return _player;
        }
        if (_lastPlayerLookupFrame == Time.frameCount) return null;

        _lastPlayerLookupFrame = Time.frameCount;
        PlayerController player = FindObjectOfType<PlayerController>();
        _cachedPlayer = player != null ? player.transform : null;
        _player = _cachedPlayer;
        return _player;
    }
}
