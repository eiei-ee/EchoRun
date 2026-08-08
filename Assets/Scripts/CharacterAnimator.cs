using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Run Cycle")]
    public float runSwingSpeed = 12f;
    public float armSwingAngle = 28f;
    public float armTuckAngle = 18f;
    public float elbowBendAngle = 52f;
    public float elbowBendVariation = 8f;
    public float legSwingAngle = 45f;
    public float bobAmplitude = 0.06f;

    [Header("Jump Pose")]
    public float jumpArmRaiseAngle = 70f;
    public float jumpLegTuckAngle = 55f;

    [Header("Slide Pose")]
    public float slideHipDrop = 0.52f;
    public float slideSpineLean = 42f;
    public float slideFrontLegAngle = 72f;
    public float slideRearLegAngle = 24f;
    public float slideRearKneeBend = 105f;
    public float slideArmSweep = 48f;

    [Header("Turning")]
    public float lookRotationSpeed = 15f;

    [Header("Limb References (set manually or via InitFromHumanoid)")]
    public Transform leftUpperArm;
    public Transform rightUpperArm;
    public Transform leftLowerArm;
    public Transform rightLowerArm;
    public Transform leftUpperLeg;
    public Transform rightUpperLeg;
    public Transform leftLowerLeg;
    public Transform rightLowerLeg;
    public Transform leftFoot;
    public Transform rightFoot;
    public Transform hipsTransform;
    public Transform bodyTransform;

    [Header("Humanoid")]
    public bool useHumanoidRig;

    private PlayerController _player;
    private GameManager _gm;
    private Animator _animator;
    private bool _initialized;
    private bool _externalDriver;
    private float _runPhase;

    private Vector3 _hipsBasePos;
    private Vector3 _bodyBasePos;
    private Quaternion _hipsBaseRot;
    private Quaternion _bodyBaseRot;
    private Quaternion _leftFootBaseRot;
    private Quaternion _rightFootBaseRot;
    private Quaternion _leftUpperArmBaseRot;
    private Quaternion _rightUpperArmBaseRot;
    private Quaternion _leftLowerArmBaseRot;
    private Quaternion _rightLowerArmBaseRot;
    private Quaternion _leftUpperArmBaseRootRot;
    private Quaternion _rightUpperArmBaseRootRot;
    private Quaternion _leftLowerArmBaseRootRot;
    private Quaternion _rightLowerArmBaseRootRot;
    private Quaternion _leftUpperLegBaseRot;
    private Quaternion _rightUpperLegBaseRot;
    private Quaternion _leftLowerLegBaseRot;
    private Quaternion _rightLowerLegBaseRot;

    private const float PoseLerpSpeed = 10f;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_initialized) return;

        _player = GetComponentInParent<PlayerController>();
        _gm = GameManager.Instance;

        if (useHumanoidRig)
        {
            _animator = GetComponent<Animator>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
            if (_animator == null) _animator = GetComponentInParent<Animator>();
            if (_animator != null && _animator.isHuman)
            {
                InitFromHumanoid();

                // Keep the Animator active so WebGL/WeChat continues updating the
                // skinned-mesh bone matrices. Procedural poses are written later in
                // LateUpdate, after Humanoid evaluation.
                _animator.applyRootMotion = false;
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
        }

        PrepareSkinnedMeshes();

        // These fallbacks also support the current Generic EchoRunner skeleton.
        leftUpperArm = FindBone(leftUpperArm, "LeftUpperArm", "LeftArm", "Arm_Upper_L");
        rightUpperArm = FindBone(rightUpperArm, "RightUpperArm", "RightArm", "Arm_Upper_R");
        leftLowerArm = FindBone(leftLowerArm, "LeftLowerArm", "LeftForeArm", "Arm_Lower_L");
        rightLowerArm = FindBone(rightLowerArm, "RightLowerArm", "RightForeArm", "Arm_Lower_R");
        leftUpperLeg = FindBone(leftUpperLeg, "LeftUpperLeg", "LeftUpLeg", "Leg_Upper_L");
        rightUpperLeg = FindBone(rightUpperLeg, "RightUpperLeg", "RightUpLeg", "Leg_Upper_R");
        leftLowerLeg = FindBone(leftLowerLeg, "LeftLowerLeg", "LeftLeg", "Leg_Lower_L");
        rightLowerLeg = FindBone(rightLowerLeg, "RightLowerLeg", "RightLeg", "Leg_Lower_R");
        leftFoot = FindBone(leftFoot, "LeftFoot", "Foot_L");
        rightFoot = FindBone(rightFoot, "RightFoot", "Foot_R");
        hipsTransform = FindBone(hipsTransform, "Hips", "Pelvis");
        bodyTransform = FindBone(bodyTransform, "Spine", "Torso");

        _hipsBasePos = hipsTransform != null ? hipsTransform.localPosition : Vector3.zero;
        _bodyBasePos = bodyTransform != null ? bodyTransform.localPosition : Vector3.zero;
        _hipsBaseRot = hipsTransform != null ? hipsTransform.localRotation : Quaternion.identity;
        _bodyBaseRot = bodyTransform != null ? bodyTransform.localRotation : Quaternion.identity;
        _leftFootBaseRot = BaseRotation(leftFoot);
        _rightFootBaseRot = BaseRotation(rightFoot);
        _leftUpperArmBaseRot = BaseRotation(leftUpperArm);
        _rightUpperArmBaseRot = BaseRotation(rightUpperArm);
        _leftLowerArmBaseRot = BaseRotation(leftLowerArm);
        _rightLowerArmBaseRot = BaseRotation(rightLowerArm);
        _leftUpperLegBaseRot = BaseRotation(leftUpperLeg);
        _rightUpperLegBaseRot = BaseRotation(rightUpperLeg);
        _leftLowerLegBaseRot = BaseRotation(leftLowerLeg);
        _rightLowerLegBaseRot = BaseRotation(rightLowerLeg);

        Quaternion inverseRootRotation = Quaternion.Inverse(transform.rotation);
        _leftUpperArmBaseRootRot = RootRotation(leftUpperArm, inverseRootRotation);
        _rightUpperArmBaseRootRot = RootRotation(rightUpperArm, inverseRootRotation);
        _leftLowerArmBaseRootRot = RootRotation(leftLowerArm, inverseRootRotation);
        _rightLowerArmBaseRootRot = RootRotation(rightLowerArm, inverseRootRotation);
        _initialized = true;
    }

    private void InitFromHumanoid()
    {
        leftUpperArm = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightUpperArm = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        leftLowerArm = _animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rightLowerArm = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        leftUpperLeg = _animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightUpperLeg = _animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        leftLowerLeg = _animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rightLowerLeg = _animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        leftFoot = _animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = _animator.GetBoneTransform(HumanBodyBones.RightFoot);
        hipsTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);
        bodyTransform = _animator.GetBoneTransform(HumanBodyBones.Spine);
    }

    private void LateUpdate()
    {
        if (_externalDriver) return;
        if (_player == null) _player = GetComponentInParent<PlayerController>();
        if (_player == null) return;

        if (_gm == null) _gm = GameManager.Instance;
        if (_gm == null || _gm.State != GameState.Playing)
        {
            ApplyIdlePose(Time.deltaTime);
            return;
        }

        ApplyMotion(_player.IsJumping, _player.IsSliding,
            _player.ForwardDirection, _gm.CurrentSpeed, Time.deltaTime);
    }

    private void PrepareSkinnedMeshes()
    {
        // Procedural poses are applied in LateUpdate. WebGL/WeChat can otherwise
        // reuse the matrices produced by Animator evaluation earlier in the frame,
        // leaving the rendered mesh in its bind pose while Transforms are moving.
        SkinnedMeshRenderer[] renderers =
            GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].forceMatrixRecalculationPerRender = true;
    }

    // The AI shadow owns movement state, but reuses exactly the same pose code.
    public void SetExternalDriver()
    {
        Initialize();
        _externalDriver = true;
    }

    public void ApplyExternalMotion(bool isJumping, bool isSliding,
        Vector3 forward, float speed, float deltaTime)
    {
        Initialize();
        _externalDriver = true;
        ApplyMotion(isJumping, isSliding, forward, speed, deltaTime);
    }

    private void ApplyMotion(bool isJumping, bool isSliding,
        Vector3 forward, float speed, float deltaTime)
    {
        RotateTowardForward(forward, deltaTime);

        if (isJumping) ApplyJumpPose(deltaTime);
        else if (isSliding) ApplySlidePose(deltaTime);
        else ApplyRunPose(speed, deltaTime);
    }

    private void RotateTowardForward(Vector3 forward, float deltaTime)
    {
        if (forward.sqrMagnitude < 0.001f) return;
        Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);
        float t = 1f - Mathf.Exp(-lookRotationSpeed * deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }

    private void ApplyRunPose(float speed, float deltaTime)
    {
        _runPhase += deltaTime * runSwingSpeed * (Mathf.Max(1f, speed) / 10f);
        float armSwing = Mathf.Sin(_runPhase) * armSwingAngle;
        float legSwing = Mathf.Sin(_runPhase) * legSwingAngle;
        float elbowBend = elbowBendAngle
            + (0.5f - 0.5f * Mathf.Cos(_runPhase * 2f)) * elbowBendVariation;

        ApplyRunArm(leftUpperArm, leftLowerArm,
            _leftUpperArmBaseRootRot, _leftLowerArmBaseRootRot,
            armSwing, armTuckAngle, elbowBend);
        ApplyRunArm(rightUpperArm, rightLowerArm,
            _rightUpperArmBaseRootRot, _rightLowerArmBaseRootRot,
            -armSwing, -armTuckAngle, elbowBend);
        SetLocalRotation(leftUpperLeg,
            _leftUpperLegBaseRot * Quaternion.Euler(-legSwing, 0f, 0f));
        SetLocalRotation(rightUpperLeg,
            _rightUpperLegBaseRot * Quaternion.Euler(legSwing, 0f, 0f));
        BlendLocalRotation(leftLowerLeg, _leftLowerLegBaseRot, deltaTime);
        BlendLocalRotation(rightLowerLeg, _rightLowerLegBaseRot, deltaTime);

        float footPitch = Mathf.Sin(_runPhase) * 25f;
        SetLocalRotation(leftFoot,
            _leftFootBaseRot * Quaternion.Euler(footPitch, 0f, 0f));
        SetLocalRotation(rightFoot,
            _rightFootBaseRot * Quaternion.Euler(-footPitch, 0f, 0f));

        float bob = Mathf.Abs(Mathf.Sin(_runPhase * 2f)) * bobAmplitude;
        BlendCorePose(_hipsBasePos, _hipsBaseRot,
            _bodyBasePos + Vector3.up * bob, _bodyBaseRot, deltaTime);
    }

    private void ApplyJumpPose(float deltaTime)
    {
        BlendLocalRotation(leftUpperArm,
            _leftUpperArmBaseRot * Quaternion.Euler(-jumpArmRaiseAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(rightUpperArm,
            _rightUpperArmBaseRot * Quaternion.Euler(-jumpArmRaiseAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(leftLowerArm, _leftLowerArmBaseRot, deltaTime);
        BlendLocalRotation(rightLowerArm, _rightLowerArmBaseRot, deltaTime);
        BlendLocalRotation(leftUpperLeg,
            _leftUpperLegBaseRot * Quaternion.Euler(jumpLegTuckAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(rightUpperLeg,
            _rightUpperLegBaseRot * Quaternion.Euler(jumpLegTuckAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(leftLowerLeg,
            _leftLowerLegBaseRot * Quaternion.Euler(-35f, 0f, 0f), deltaTime);
        BlendLocalRotation(rightLowerLeg,
            _rightLowerLegBaseRot * Quaternion.Euler(-35f, 0f, 0f), deltaTime);
        BlendLocalRotation(leftFoot,
            _leftFootBaseRot * Quaternion.Euler(30f, 0f, 0f), deltaTime);
        BlendLocalRotation(rightFoot,
            _rightFootBaseRot * Quaternion.Euler(30f, 0f, 0f), deltaTime);
        BlendCorePose(_hipsBasePos, _hipsBaseRot,
            _bodyBasePos, _bodyBaseRot, deltaTime);
    }

    private void ApplySlidePose(float deltaTime)
    {
        // Crouch through the hips and joints. No Transform scale is modified.
        Vector3 crouchedHips = _hipsBasePos + Vector3.down * slideHipDrop;
        Quaternion leanedSpine = _bodyBaseRot
            * Quaternion.Euler(slideSpineLean, 0f, 0f);
        BlendCorePose(crouchedHips, _hipsBaseRot,
            _bodyBasePos, leanedSpine, deltaTime);

        BlendLocalRotation(leftUpperArm,
            _leftUpperArmBaseRot * Quaternion.Euler(slideArmSweep, 0f, -12f), deltaTime);
        BlendLocalRotation(rightUpperArm,
            _rightUpperArmBaseRot * Quaternion.Euler(slideArmSweep, 0f, 12f), deltaTime);
        BlendLocalRotation(leftLowerArm,
            _leftLowerArmBaseRot * Quaternion.Euler(-35f, 0f, 0f), deltaTime);
        BlendLocalRotation(rightLowerArm,
            _rightLowerArmBaseRot * Quaternion.Euler(-35f, 0f, 0f), deltaTime);

        // Left leg leads; right leg folds underneath the body.
        BlendLocalRotation(leftUpperLeg,
            _leftUpperLegBaseRot * Quaternion.Euler(-slideFrontLegAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(leftLowerLeg,
            _leftLowerLegBaseRot * Quaternion.Euler(12f, 0f, 0f), deltaTime);
        BlendLocalRotation(rightUpperLeg,
            _rightUpperLegBaseRot * Quaternion.Euler(slideRearLegAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(rightLowerLeg,
            _rightLowerLegBaseRot * Quaternion.Euler(-slideRearKneeBend, 0f, 0f), deltaTime);
        BlendLocalRotation(leftFoot,
            _leftFootBaseRot * Quaternion.Euler(18f, 0f, 0f), deltaTime);
        BlendLocalRotation(rightFoot,
            _rightFootBaseRot * Quaternion.Euler(-28f, 0f, 0f), deltaTime);
    }

    private void ApplyIdlePose(float deltaTime)
    {
        BlendLocalRotation(leftUpperArm, _leftUpperArmBaseRot, deltaTime);
        BlendLocalRotation(rightUpperArm, _rightUpperArmBaseRot, deltaTime);
        BlendLocalRotation(leftLowerArm, _leftLowerArmBaseRot, deltaTime);
        BlendLocalRotation(rightLowerArm, _rightLowerArmBaseRot, deltaTime);
        BlendLocalRotation(leftUpperLeg, _leftUpperLegBaseRot, deltaTime);
        BlendLocalRotation(rightUpperLeg, _rightUpperLegBaseRot, deltaTime);
        BlendLocalRotation(leftLowerLeg, _leftLowerLegBaseRot, deltaTime);
        BlendLocalRotation(rightLowerLeg, _rightLowerLegBaseRot, deltaTime);
        BlendLocalRotation(leftFoot, _leftFootBaseRot, deltaTime);
        BlendLocalRotation(rightFoot, _rightFootBaseRot, deltaTime);
        BlendCorePose(_hipsBasePos, _hipsBaseRot,
            _bodyBasePos, _bodyBaseRot, deltaTime);
    }

    private void BlendCorePose(Vector3 hipsPosition, Quaternion hipsRotation,
        Vector3 bodyPosition, Quaternion bodyRotation, float deltaTime)
    {
        float t = 1f - Mathf.Exp(-PoseLerpSpeed * deltaTime);
        if (hipsTransform != null)
        {
            hipsTransform.localPosition = Vector3.Lerp(
                hipsTransform.localPosition, hipsPosition, t);
            hipsTransform.localRotation = Quaternion.Slerp(
                hipsTransform.localRotation, hipsRotation, t);
        }
        if (bodyTransform != null)
        {
            bodyTransform.localPosition = Vector3.Lerp(
                bodyTransform.localPosition, bodyPosition, t);
            bodyTransform.localRotation = Quaternion.Slerp(
                bodyTransform.localRotation, bodyRotation, t);
        }
    }

    private void ApplyRunArm(Transform upperArm, Transform lowerArm,
        Quaternion upperBaseRootRotation, Quaternion lowerBaseRootRotation,
        float swingAngle, float tuckAngle, float bendAngle)
    {
        Quaternion tuck = Quaternion.AngleAxis(tuckAngle, Vector3.forward);
        Quaternion swing = Quaternion.AngleAxis(swingAngle, Vector3.right);
        if (upperArm != null)
            upperArm.rotation = transform.rotation * swing * tuck * upperBaseRootRotation;
        if (lowerArm != null)
        {
            Quaternion elbow = Quaternion.AngleAxis(-bendAngle, Vector3.right);
            lowerArm.rotation = transform.rotation * swing * elbow * tuck
                * lowerBaseRootRotation;
        }
    }

    private void BlendLocalRotation(Transform bone, Quaternion target, float deltaTime)
    {
        if (bone == null) return;
        float t = 1f - Mathf.Exp(-PoseLerpSpeed * deltaTime);
        bone.localRotation = Quaternion.Slerp(bone.localRotation, target, t);
    }

    private static void SetLocalRotation(Transform bone, Quaternion target)
    {
        if (bone != null) bone.localRotation = target;
    }

    private Transform FindBone(Transform current, params string[] names)
    {
        if (current != null) return current;
        for (int i = 0; i < names.Length; i++)
        {
            Transform found = FindDescendant(transform, names[i]);
            if (found != null) return found;
        }
        return null;
    }

    private static Quaternion BaseRotation(Transform bone)
    {
        return bone != null ? bone.localRotation : Quaternion.identity;
    }

    private static Quaternion RootRotation(Transform bone, Quaternion inverseRootRotation)
    {
        return bone != null
            ? inverseRootRotation * bone.rotation
            : Quaternion.identity;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            string candidate = descendants[i].name;
            int separator = candidate.LastIndexOf(':');
            if (separator >= 0) candidate = candidate.Substring(separator + 1);
            if (candidate == name) return descendants[i];
        }
        return null;
    }
}
