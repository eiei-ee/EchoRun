using UnityEngine;

public class Coin : MonoBehaviour
{
    public float rotateSpeed = 180f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.3f;

    private Vector3 _startPos;

    void Start()
    {
        _startPos = transform.position;
    }

    void Update()
    {
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = _startPos + Vector3.up * bob;
    }
}
