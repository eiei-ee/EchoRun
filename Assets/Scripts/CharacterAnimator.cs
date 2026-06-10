using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Run Cycle")]
    public float runSwingSpeed = 12f;
    public float armSwingAngle = 35f;
    public float legSwingAngle = 45f;
    public float bobAmplitude = 0.06f;

    [Header("Jump Pose")]
    public float jumpArmRaiseAngle = 70f;
    public float jumpLegTuckAngle = 55f;

    [Header("Slide Pose")]
    public float slideBodySquash = 0.5f;
    public float slideLegForwardAngle = 75f;

    [Header("Turning")]
    public float lookRotationSpeed = 15f;

    [Header("Limb References (set manually or via InitFromHumanoid)")]
    public Transform leftUpperArm;
    public Transform rightUpperArm;
    public Transform leftUpperLeg;
    public Transform rightUpperLeg;
    public Transform leftFoot;
    public Transform rightFoot;
    public Transform bodyTransform;

    [Header("Humanoid")]
    public bool useHumanoidRig;

    private PlayerController _player;
    private Vector3 _bodyBasePos;
    private Quaternion _leftFootBaseRot;
    private Quaternion _rightFootBaseRot;
    private Animator _animator;
    private GameManager _gm;

    private float _runPhase;
    private const float POSE_LERP_SPEED = 10f;

    void Start()
    {
        _player = GetComponentInParent<PlayerController>();
        _gm = GameManager.Instance;

        if (useHumanoidRig)
        {
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInParent<Animator>();
            if (_animator != null) InitFromHumanoid();
        }

        _bodyBasePos = bodyTransform != null ? bodyTransform.localPosition : transform.localPosition;

        if (leftFoot != null) _leftFootBaseRot = leftFoot.localRotation;
        if (rightFoot != null) _rightFootBaseRot = rightFoot.localRotation;
    }

    void InitFromHumanoid()
    {
        leftUpperArm    = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightUpperArm   = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        leftUpperLeg    = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightUpperLeg   = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        leftFoot        = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot       = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
        bodyTransform   = _animator.GetBoneTransform(HumanBodyBones.Spine);

        if (bodyTransform == null)
            bodyTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);

        if (leftUpperArm != null && rightUpperArm != null &&
            leftUpperLeg != null && rightUpperLeg != null)
            Debug.Log("CharacterAnimator: humanoid bones mapped OK");
        else
            Debug.LogWarning("CharacterAnimator: some humanoid bones missing - check rig");
    }

    void Update()
    {
        if (_player == null) return;

        RotateTowardForward();

        if (_gm == null || _gm.State != GameState.Playing)
        {
            SmoothIdlePose();
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
        float speed = _gm.CurrentSpeed;
        _runPhase += Time.deltaTime * runSwingSpeed * (speed / 10f);

        float armSwing = Mathf.Sin(_runPhase) * armSwingAngle;
        float legSwing = Mathf.Sin(_runPhase) * legSwingAngle;

        if (leftUpperArm != null)
            leftUpperArm.localRotation = Quaternion.Euler(armSwing, 0, 0);
        if (rightUpperArm != null)
            rightUpperArm.localRotation = Quaternion.Euler(-armSwing, 0, 0);
        if (leftUpperLeg != null)
            leftUpperLeg.localRotation = Quaternion.Euler(-legSwing, 0, 0);
        if (rightUpperLeg != null)
            rightUpperLeg.localRotation = Quaternion.Euler(legSwing, 0, 0);

        // Foot roll: lift heel on forward swing, toe on back swing
        float footPitch = Mathf.Sin(_runPhase) * 25f;
        if (leftFoot != null)
            leftFoot.localRotation = _leftFootBaseRot * Quaternion.Euler(footPitch, 0, 0);
        if (rightFoot != null)
            rightFoot.localRotation = _rightFootBaseRot * Quaternion.Euler(-footPitch, 0, 0);

        float bob = Mathf.Abs(Mathf.Sin(_runPhase * 2f)) * bobAmplitude;
        if (bodyTransform != null)
            bodyTransform.localPosition = _bodyBasePos + Vector3.up * bob;
    }

    void ApplyJumpPose()
    {
        float t = Time.deltaTime * POSE_LERP_SPEED;
        if (leftUpperArm != null)
            leftUpperArm.localRotation = Quaternion.Slerp(leftUpperArm.localRotation, Quaternion.Euler(-jumpArmRaiseAngle, 0, 0), t);
        if (rightUpperArm != null)
            rightUpperArm.localRotation = Quaternion.Slerp(rightUpperArm.localRotation, Quaternion.Euler(-jumpArmRaiseAngle, 0, 0), t);
        if (leftUpperLeg != null)
            leftUpperLeg.localRotation = Quaternion.Slerp(leftUpperLeg.localRotation, Quaternion.Euler(jumpLegTuckAngle, 0, 0), t);
        if (rightUpperLeg != null)
            rightUpperLeg.localRotation = Quaternion.Slerp(rightUpperLeg.localRotation, Quaternion.Euler(jumpLegTuckAngle, 0, 0), t);
        if (leftFoot != null)
            leftFoot.localRotation = Quaternion.Slerp(leftFoot.localRotation, _leftFootBaseRot * Quaternion.Euler(30, 0, 0), t);
        if (rightFoot != null)
            rightFoot.localRotation = Quaternion.Slerp(rightFoot.localRotation, _rightFootBaseRot * Quaternion.Euler(30, 0, 0), t);
    }

    void ApplySlidePose()
    {
        if (bodyTransform != null)
        {
            Vector3 s = bodyTransform.localScale;
            s.y = Mathf.Lerp(s.y, slideBodySquash, Time.deltaTime * POSE_LERP_SPEED);
            bodyTransform.localScale = s;
        }

        float t = Time.deltaTime * POSE_LERP_SPEED;
        if (leftUpperLeg != null)
            leftUpperLeg.localRotation = Quaternion.Slerp(leftUpperLeg.localRotation, Quaternion.Euler(-slideLegForwardAngle, 0, 0), t);
        if (rightUpperLeg != null)
            rightUpperLeg.localRotation = Quaternion.Slerp(rightUpperLeg.localRotation, Quaternion.Euler(-slideLegForwardAngle, 0, 0), t);
        if (leftFoot != null)
            leftFoot.localRotation = Quaternion.Slerp(leftFoot.localRotation, _leftFootBaseRot, t);
        if (rightFoot != null)
            rightFoot.localRotation = Quaternion.Slerp(rightFoot.localRotation, _rightFootBaseRot, t);
    }

    void SmoothIdlePose()
    {
        float t = Time.deltaTime * POSE_LERP_SPEED;
        if (leftUpperArm != null)
            leftUpperArm.localRotation = Quaternion.Slerp(leftUpperArm.localRotation, Quaternion.identity, t);
        if (rightUpperArm != null)
            rightUpperArm.localRotation = Quaternion.Slerp(rightUpperArm.localRotation, Quaternion.identity, t);
        if (leftUpperLeg != null)
            leftUpperLeg.localRotation = Quaternion.Slerp(leftUpperLeg.localRotation, Quaternion.identity, t);
        if (rightUpperLeg != null)
            rightUpperLeg.localRotation = Quaternion.Slerp(rightUpperLeg.localRotation, Quaternion.identity, t);
        if (leftFoot != null)
            leftFoot.localRotation = Quaternion.Slerp(leftFoot.localRotation, _leftFootBaseRot, t);
        if (rightFoot != null)
            rightFoot.localRotation = Quaternion.Slerp(rightFoot.localRotation, _rightFootBaseRot, t);
        if (bodyTransform != null)
        {
            bodyTransform.localPosition = Vector3.Lerp(bodyTransform.localPosition, _bodyBasePos, t);
            bodyTransform.localScale = Vector3.Lerp(bodyTransform.localScale, Vector3.one, t);
        }
    }
}
