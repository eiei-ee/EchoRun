using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 5f, -8f);
    public float smoothSpeed = 8f;

    private Vector3 _velocity = Vector3.zero;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 targetPos = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _velocity, 1f / smoothSpeed);
        transform.LookAt(target.position + Vector3.forward * 5f);
    }
}
