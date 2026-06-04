using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Run Cycle")]
    public float runSwingSpeed = 12f;
    public float armSwingAngle = 35f;
    public float legSwingAngle = 45f;
    public float bobAmplitude = 0.06f;
    public float bobFrequency = 7f;

    [Header("Jump Pose")]
    public float jumpArmRaiseAngle = 70f;
    public float jumpLegTuckAngle = 55f;

    [Header("Slide Pose")]
    public float slideBodySquash = 0.5f;
    public float slideLegForwardAngle = 75f;

    [Header("Turning")]
    public float lookRotationSpeed = 15f;

    [Header("Limb References")]
    public Transform leftUpperArm;
    public Transform rightUpperArm;
    public Transform leftUpperLeg;
    public Transform rightUpperLeg;
    public Transform bodyTransform;

    private PlayerController _player;
    private Vector3 _bodyBasePos;

    void Start()
    {
        _player = GetComponentInParent<PlayerController>();
        _bodyBasePos = bodyTransform != null ? bodyTransform.localPosition : transform.localPosition;
    }

    void Update()
    {
        if (_player == null) return;

        RotateTowardForward();

        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
        {
            ApplyIdlePose();
            return;
        }

        if (_player.IsJumping)
            ApplyJumpPose();
        else if (_player.IsSliding)
            ApplySlidePose();
        else
            ApplyRunPose();
    }

    void RotateTowardForward()
    {
        Vector3 forward = _player.ForwardDirection;
        if (forward.sqrMagnitude < 0.001f) return;
        Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * lookRotationSpeed);
    }

    void ApplyRunPose()
    {
        float speed = GameManager.Instance.CurrentSpeed;
        float phase = Time.time * runSwingSpeed * (speed / 10f);

        float armSwing = Mathf.Sin(phase) * armSwingAngle;
        float legSwing = Mathf.Sin(phase) * legSwingAngle;

        if (leftUpperArm != null)
            leftUpperArm.localRotation = Quaternion.Euler(armSwing, 0, 0);
        if (rightUpperArm != null)
            rightUpperArm.localRotation = Quaternion.Euler(-armSwing, 0, 0);
        if (leftUpperLeg != null)
            leftUpperLeg.localRotation = Quaternion.Euler(-legSwing, 0, 0);
        if (rightUpperLeg != null)
            rightUpperLeg.localRotation = Quaternion.Euler(legSwing, 0, 0);

        float bob = Mathf.Abs(Mathf.Sin(phase * 2f)) * bobAmplitude;
        if (bodyTransform != null)
            bodyTransform.localPosition = _bodyBasePos + Vector3.up * bob;
    }

    void ApplyJumpPose()
    {
        float t = Time.deltaTime * 10f;
        if (leftUpperArm != null)
            leftUpperArm.localRotation = Quaternion.Slerp(leftUpperArm.localRotation, Quaternion.Euler(-jumpArmRaiseAngle, 0, 0), t);
        if (rightUpperArm != null)
            rightUpperArm.localRotation = Quaternion.Slerp(rightUpperArm.localRotation, Quaternion.Euler(-jumpArmRaiseAngle, 0, 0), t);
        if (leftUpperLeg != null)
            leftUpperLeg.localRotation = Quaternion.Slerp(leftUpperLeg.localRotation, Quaternion.Euler(jumpLegTuckAngle, 0, 0), t);
        if (rightUpperLeg != null)
            rightUpperLeg.localRotation = Quaternion.Slerp(rightUpperLeg.localRotation, Quaternion.Euler(jumpLegTuckAngle, 0, 0), t);
    }

    void ApplySlidePose()
    {
        if (bodyTransform != null)
        {
            Vector3 s = bodyTransform.localScale;
            s.y = Mathf.Lerp(s.y, slideBodySquash, Time.deltaTime * 10f);
            bodyTransform.localScale = s;
        }

        float t = Time.deltaTime * 10f;
        if (leftUpperLeg != null)
            leftUpperLeg.localRotation = Quaternion.Slerp(leftUpperLeg.localRotation, Quaternion.Euler(-slideLegForwardAngle, 0, 0), t);
        if (rightUpperLeg != null)
            rightUpperLeg.localRotation = Quaternion.Slerp(rightUpperLeg.localRotation, Quaternion.Euler(-slideLegForwardAngle, 0, 0), t);
    }

    void ApplyIdlePose()
    {
        if (leftUpperArm != null) leftUpperArm.localRotation = Quaternion.identity;
        if (rightUpperArm != null) rightUpperArm.localRotation = Quaternion.identity;
        if (leftUpperLeg != null) leftUpperLeg.localRotation = Quaternion.identity;
        if (rightUpperLeg != null) rightUpperLeg.localRotation = Quaternion.identity;
        if (bodyTransform != null)
        {
            bodyTransform.localPosition = _bodyBasePos;
            bodyTransform.localScale = Vector3.one;
        }
    }
}
