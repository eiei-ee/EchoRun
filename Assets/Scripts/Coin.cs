using UnityEngine;

public class Coin : MonoBehaviour
{
    public float rotateSpeed = 180f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.3f;

    private float _baseY;
    private float _phaseOffset;
    private Transform _player;

    void OnEnable()
    {
        _baseY = transform.position.y;
        _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        PlayerController player = FindObjectOfType<PlayerController>();
        _player = player != null ? player.transform : null;
    }

    void Update()
    {
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        float bob = Mathf.Sin((Time.time + _phaseOffset) * bobSpeed) * bobHeight;
        Vector3 pos = transform.position;
        pos.y = _baseY + bob;
        transform.position = pos;

        PowerUpController powerUps = PowerUpController.Instance;
        if (powerUps == null || !powerUps.HasMagnet) return;
        if (_player == null)
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            _player = player != null ? player.transform : null;
        }
        if (_player == null) return;
        float distance = Vector3.Distance(transform.position, _player.position);
        if (distance <= powerUps.MagnetRadius)
            transform.position = Vector3.MoveTowards(
                transform.position, _player.position + Vector3.up * 0.7f,
                Mathf.Lerp(8f, 24f, 1f - distance / powerUps.MagnetRadius)
                * Time.deltaTime);
    }
}
