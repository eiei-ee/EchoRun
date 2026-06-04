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

        Vector3 forward = Vector3.forward;
        PlayerController pc = target.GetComponent<PlayerController>();
        if (pc != null) forward = pc.ForwardDirection;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        Vector3 worldOffset = forward * offset.z + Vector3.up * offset.y + right * offset.x;
        Vector3 targetPos = target.position + worldOffset;
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _velocity, 1f / smoothSpeed);

        Vector3 lookTarget = target.position + forward * 5f;
        Quaternion targetRot = Quaternion.LookRotation(lookTarget - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * smoothSpeed);
    }
}
