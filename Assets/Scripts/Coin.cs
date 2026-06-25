using UnityEngine;

public class Coin : MonoBehaviour
{
    public float rotateSpeed = 180f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.3f;

    private float _baseY;
    private float _phaseOffset;

    void OnEnable()
    {
        _baseY = transform.position.y;
        _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
    }

    void Update()
    {
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        float bob = Mathf.Sin((Time.time + _phaseOffset) * bobSpeed) * bobHeight;
        Vector3 pos = transform.position;
        pos.y = _baseY + bob;
        transform.position = pos;
    }
}
