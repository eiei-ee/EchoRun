using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    [Header("Run Cycle")]
    public float runSwingSpeed = 10f;
    public float armSwingAngle = 32f;
    public float armTuckAngle = 68f;
    public float elbowBendAngle = 72f;
    public float elbowBendVariation = 12f;
    public float legSwingAngle = 34f;
    public float kneeFlexAngle = 72f;
    public float runSpineLean = 14f;
    public float bobAmplitude = 0.045f;

    [Header("Jump Pose")]
    public float jumpArmRaiseAngle = 52f;
    public float jumpLegTuckAngle = 58f;
    public float jumpElbowBendAngle = 72f;
    public float jumpKneeBendAngle = 88f;
    public float jumpSpineLean = 14f;

    [Header("Slide Pose")]
    public float slideHipDrop = 0.78f;
    public float slideSpineLean = 50f;
    public float slideChestLean = 78f;
    public float slideUpperChestLean = 86f;
    public float slideYawAngle = -28f;
    public float slideBodyRoll = 14f;
    public float slideHeadLean = 58f;
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
    public Transform leftHand;
    public Transform rightHand;
    public Transform headTransform;
    public Transform hipsTransform;
    public Transform bodyTransform;
    public Transform chestTransform;
    public Transform upperChestTransform;

    [Header("Humanoid")]
    public bool useHumanoidRig;
    public bool useAuthoredAnimations = true;
    public bool stabilizeAuthoredRun = true;

    private PlayerController _player;
    private GameManager _gm;
    private Animator _animator;
    private bool _initialized;
    private bool _externalDriver;
    private float _runPhase;
    private float _slideTime;
    private int _activeAuthoredState;

    private Vector3 _hipsBasePos;
    private Vector3 _bodyBasePos;
    private Quaternion _hipsBaseRot;
    private Quaternion _bodyBaseRot;
    private Quaternion _hipsBaseRootRot;
    private Quaternion _bodyBaseRootRot;
    private Quaternion _chestBaseRootRot;
    private Quaternion _upperChestBaseRootRot;
    private Quaternion _headBaseRootRot;
    private Quaternion _leftFootBaseRot;
    private Quaternion _rightFootBaseRot;
    private Quaternion _leftFootBaseRootRot;
    private Quaternion _rightFootBaseRootRot;
    private Quaternion _leftHandBaseRootRot;
    private Quaternion _rightHandBaseRootRot;
    private Vector3 _leftFootBaseRootPos;
    private Vector3 _rightFootBaseRootPos;
    private Vector3 _hipsBaseRootPos;
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
    private Transform _leftToes;
    private Transform _rightToes;

    private const float PoseLerpSpeed = 10f;
    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int RunState = Animator.StringToHash("Run");
    private static readonly int JumpState = Animator.StringToHash("Jump");

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
        _leftToes = FindBone(_leftToes, "LeftToes", "LeftToeBase", "Toes_L");
        _rightToes = FindBone(_rightToes, "RightToes", "RightToeBase", "Toes_R");
        leftHand = FindBone(leftHand, "LeftHand", "Hand_L");
        rightHand = FindBone(rightHand, "RightHand", "Hand_R");
        headTransform = FindBone(headTransform, "Head");
        hipsTransform = FindBone(hipsTransform, "Hips", "Pelvis");
        bodyTransform = FindBone(bodyTransform, "Spine", "Torso");
        chestTransform = FindBone(chestTransform, "Chest", "Spine1");
        upperChestTransform = FindBone(
            upperChestTransform, "UpperChest", "Spine2");

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
        _leftFootBaseRootRot = RootRotation(leftFoot, inverseRootRotation);
        _rightFootBaseRootRot = RootRotation(rightFoot, inverseRootRotation);
        _leftHandBaseRootRot = RootRotation(leftHand, inverseRootRotation);
        _rightHandBaseRootRot = RootRotation(rightHand, inverseRootRotation);
        _leftFootBaseRootPos = RootPosition(leftFoot);
        _rightFootBaseRootPos = RootPosition(rightFoot);
        _hipsBaseRootPos = RootPosition(hipsTransform);
        _hipsBaseRootRot = RootRotation(hipsTransform, inverseRootRotation);
        _bodyBaseRootRot = RootRotation(bodyTransform, inverseRootRotation);
        _chestBaseRootRot = RootRotation(chestTransform, inverseRootRotation);
        _upperChestBaseRootRot = RootRotation(
            upperChestTransform, inverseRootRotation);
        _headBaseRootRot = RootRotation(headTransform, inverseRootRotation);
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
        _leftToes = _animator.GetBoneTransform(HumanBodyBones.LeftToes);
        _rightToes = _animator.GetBoneTransform(HumanBodyBones.RightToes);
        leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
        rightHand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
        headTransform = _animator.GetBoneTransform(HumanBodyBones.Head);
        hipsTransform = _animator.GetBoneTransform(HumanBodyBones.Hips);
        bodyTransform = _animator.GetBoneTransform(HumanBodyBones.Spine);
        chestTransform = _animator.GetBoneTransform(HumanBodyBones.Chest);
        upperChestTransform =
            _animator.GetBoneTransform(HumanBodyBones.UpperChest);
    }

    private void LateUpdate()
    {
        if (_externalDriver) return;
        if (_player == null) _player = GetComponentInParent<PlayerController>();
        if (_player == null) return;

        if (_gm == null) _gm = GameManager.Instance;
        if (_gm == null || _gm.State != GameState.Playing)
        {
            if (CanUseAuthoredAnimations())
                ApplyAuthoredMotion(false, 0f, true);
            else
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
        _slideTime = isSliding ? _slideTime + deltaTime : 0f;
        float slideDuration = _player != null
            ? Mathf.Max(0.2f, _player.slideDuration)
            : 0.8f;
        float slidePhase = Mathf.Clamp01(_slideTime / slideDuration);

        if (CanUseAuthoredAnimations())
        {
            ApplyAuthoredMotion(isJumping, speed, false);
            if (isSliding)
                ApplySlidePose(slidePhase);
            else if (!isJumping)
                StabilizeAuthoredRunPose();
            return;
        }

        if (isJumping) ApplyJumpPose(deltaTime);
        else if (isSliding) ApplySlidePose(slidePhase);
        else ApplyRunPose(speed, deltaTime);
    }

    private bool CanUseAuthoredAnimations()
    {
        return useAuthoredAnimations
            && _animator != null
            && _animator.runtimeAnimatorController != null;
    }

    private void ApplyAuthoredMotion(bool isJumping, float speed, bool idle)
    {
        int targetState = idle ? IdleState : isJumping ? JumpState : RunState;
        if (!_animator.HasState(0, targetState)) return;

        if (_activeAuthoredState != targetState)
        {
            if (_activeAuthoredState == 0)
                _animator.Play(targetState, 0, 0f);
            else
                _animator.CrossFade(targetState, 0.12f, 0);
            _activeAuthoredState = targetState;
        }

        _animator.speed = targetState == RunState
            ? Mathf.Clamp(speed / 10f, 0.85f, 1.4f)
            : 1f;
    }

    private void StabilizeAuthoredRunPose()
    {
        if (!stabilizeAuthoredRun) return;
        ShapeAuthoredCoreBone(
            hipsTransform, _hipsBasePos, _hipsBaseRot, 0f, 8f, 0f, 1f);
        ShapeAuthoredCoreBone(
            bodyTransform, _bodyBasePos, _bodyBaseRot,
            runSpineLean, 12f, 0f, 1f);

        float phase = GetAuthoredRunPhase();
        float leftRecovery = 0.5f - 0.5f * Mathf.Cos(phase);
        float rightRecovery = 1f - leftRecovery;
        AddRunningKneeFlex(
            leftLowerLeg, _leftLowerLegBaseRot, leftRecovery);
        AddRunningKneeFlex(
            rightLowerLeg, _rightLowerLegBaseRot, rightRecovery);

        AlignRunningFootForward(leftFoot, _leftToes, 7f);
        AlignRunningFootForward(rightFoot, _rightToes, 7f);
    }

    private float GetAuthoredRunPhase()
    {
        if (_animator == null) return _runPhase;
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        return state.normalizedTime * Mathf.PI * 2f;
    }

    private static void ShapeAuthoredCoreBone(
        Transform bone, Vector3 basePosition, Quaternion baseRotation,
        float targetLean, float yawLimit, float rollLimit,
        float horizontalCentering)
    {
        if (bone == null) return;

        Vector3 position = bone.localPosition;
        position.x = Mathf.Lerp(
            position.x, basePosition.x, horizontalCentering);
        bone.localPosition = position;

        Quaternion relativeRotation =
            Quaternion.Inverse(baseRotation) * bone.localRotation;
        Vector3 relativeEuler = SignedEuler(relativeRotation);
        if (targetLean > 0f)
        {
            relativeEuler.x = Mathf.Lerp(
                relativeEuler.x,
                Mathf.Max(relativeEuler.x, targetLean), 0.55f);
        }
        relativeEuler.y = Mathf.Clamp(
            relativeEuler.y, -yawLimit, yawLimit);
        relativeEuler.z = Mathf.Clamp(
            relativeEuler.z, -rollLimit, rollLimit);
        bone.localRotation = baseRotation * Quaternion.Euler(relativeEuler);
    }

    private static void AddRunningKneeFlex(
        Transform lowerLeg, Quaternion baseRotation, float recovery)
    {
        if (lowerLeg == null) return;

        Vector3 relativeEuler = SignedEuler(
            Quaternion.Inverse(baseRotation) * lowerLeg.localRotation);
        float authoredBend = Mathf.Max(0f, -relativeEuler.x);
        float addedBend = Mathf.Lerp(3f, 11f, recovery);
        relativeEuler.x = -Mathf.Clamp(
            Mathf.Max(authoredBend, 12f) + addedBend, 15f, 105f);
        lowerLeg.localRotation =
            baseRotation * Quaternion.Euler(relativeEuler);
    }

    private void AlignRunningFootForward(
        Transform foot, Transform toes, float allowedSplay)
    {
        if (foot == null || toes == null) return;

        Vector3 currentDirection = toes.position - foot.position;
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(
            currentDirection, Vector3.up);
        Vector3 horizontalForward = Vector3.ProjectOnPlane(
            transform.forward, Vector3.up).normalized;
        if (horizontalDirection.sqrMagnitude < 0.000001f
            || horizontalForward.sqrMagnitude < 0.000001f)
        {
            return;
        }

        float yawCorrection = Vector3.SignedAngle(
            horizontalDirection, horizontalForward, Vector3.up);
        if (Mathf.Abs(yawCorrection) <= allowedSplay) return;

        float appliedCorrection = yawCorrection
            - Mathf.Sign(yawCorrection) * allowedSplay;
        foot.rotation = Quaternion.AngleAxis(
            appliedCorrection, Vector3.up) * foot.rotation;
    }

    private static Vector3 SignedEuler(Quaternion rotation)
    {
        Vector3 angles = rotation.eulerAngles;
        angles.x = Mathf.DeltaAngle(0f, angles.x);
        angles.y = Mathf.DeltaAngle(0f, angles.y);
        angles.z = Mathf.DeltaAngle(0f, angles.z);
        return angles;
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
        float stride = Mathf.Sin(_runPhase);
        float armSwing = stride * armSwingAngle;
        float legSwing = stride * legSwingAngle;
        float elbowBend = elbowBendAngle
            + (0.5f - 0.5f * Mathf.Cos(_runPhase * 2f)) * elbowBendVariation;

        ApplyRunArm(leftUpperArm, leftLowerArm,
            _leftUpperArmBaseRootRot, _leftLowerArmBaseRootRot,
            armSwing, armTuckAngle, elbowBend);
        ApplyRunArm(rightUpperArm, rightLowerArm,
            _rightUpperArmBaseRootRot, _rightLowerArmBaseRootRot,
            -armSwing, -armTuckAngle, elbowBend);
        AimRunningForearm(leftLowerArm, leftHand, true, 0.35f);
        AimRunningForearm(rightLowerArm, rightHand, false, 0.35f);
        SetLocalRotation(leftUpperLeg,
            _leftUpperLegBaseRot * Quaternion.Euler(-legSwing, 0f, 0f));
        SetLocalRotation(rightUpperLeg,
            _rightUpperLegBaseRot * Quaternion.Euler(legSwing, 0f, 0f));
        float leftRecovery = 0.5f - 0.5f * Mathf.Cos(_runPhase);
        float rightRecovery = 1f - leftRecovery;
        float leftKneeBend = Mathf.Lerp(10f, kneeFlexAngle, leftRecovery);
        float rightKneeBend = Mathf.Lerp(10f, kneeFlexAngle, rightRecovery);
        SetLocalRotation(leftLowerLeg,
            _leftLowerLegBaseRot * Quaternion.Euler(-leftKneeBend, 0f, 0f));
        SetLocalRotation(rightLowerLeg,
            _rightLowerLegBaseRot * Quaternion.Euler(-rightKneeBend, 0f, 0f));

        float leftFootPitch = Mathf.Lerp(-14f, 20f, leftRecovery)
            + legSwing * 0.1f;
        float rightFootPitch = Mathf.Lerp(-14f, 20f, rightRecovery)
            - legSwing * 0.1f;
        SetLocalRotation(leftFoot,
            _leftFootBaseRot * Quaternion.Euler(leftFootPitch, 0f, 0f));
        SetLocalRotation(rightFoot,
            _rightFootBaseRot * Quaternion.Euler(rightFootPitch, 0f, 0f));

        float bob = Mathf.Abs(Mathf.Sin(_runPhase * 2f)) * bobAmplitude;
        Quaternion hipsRotation = _hipsBaseRot
            * Quaternion.Euler(0f, stride * 3f, 0f);
        Quaternion bodyRotation = _bodyBaseRot
            * Quaternion.Euler(runSpineLean, -stride * 4f, 0f);
        BlendCorePose(_hipsBasePos, hipsRotation,
            _bodyBasePos + Vector3.up * bob, bodyRotation, deltaTime);
    }

    private void ApplyJumpPose(float deltaTime)
    {
        // Freeze the lead side from the last run stride so the pose stays stable
        // throughout the jump instead of swapping limbs in mid-air.
        bool leftLegLeads = Mathf.Sin(_runPhase) >= 0f;
        float leadingArmSwing = -jumpArmRaiseAngle;
        float trailingArmSwing = jumpArmRaiseAngle * 0.35f;
        ApplyRunArm(leftUpperArm, leftLowerArm,
            _leftUpperArmBaseRootRot, _leftLowerArmBaseRootRot,
            leftLegLeads ? trailingArmSwing : leadingArmSwing,
            armTuckAngle, jumpElbowBendAngle);
        ApplyRunArm(rightUpperArm, rightLowerArm,
            _rightUpperArmBaseRootRot, _rightLowerArmBaseRootRot,
            leftLegLeads ? leadingArmSwing : trailingArmSwing,
            -armTuckAngle, jumpElbowBendAngle);

        float leftThighAngle = leftLegLeads
            ? jumpLegTuckAngle : -jumpLegTuckAngle * 0.3f;
        float rightThighAngle = leftLegLeads
            ? -jumpLegTuckAngle * 0.3f : jumpLegTuckAngle;
        float leftKneeAngle = leftLegLeads
            ? -jumpKneeBendAngle : -jumpKneeBendAngle * 0.55f;
        float rightKneeAngle = leftLegLeads
            ? -jumpKneeBendAngle * 0.55f : -jumpKneeBendAngle;
        BlendLocalRotation(leftUpperLeg,
            _leftUpperLegBaseRot * Quaternion.Euler(leftThighAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(rightUpperLeg,
            _rightUpperLegBaseRot * Quaternion.Euler(rightThighAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(leftLowerLeg,
            _leftLowerLegBaseRot * Quaternion.Euler(leftKneeAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(rightLowerLeg,
            _rightLowerLegBaseRot * Quaternion.Euler(rightKneeAngle, 0f, 0f), deltaTime);
        BlendLocalRotation(leftFoot,
            _leftFootBaseRot * Quaternion.Euler(leftLegLeads ? 22f : -8f, 0f, 0f), deltaTime);
        BlendLocalRotation(rightFoot,
            _rightFootBaseRot * Quaternion.Euler(leftLegLeads ? -8f : 22f, 0f, 0f), deltaTime);
        BlendCorePose(_hipsBasePos, _hipsBaseRot,
            _bodyBasePos,
            _bodyBaseRot * Quaternion.Euler(jumpSpineLean, 0f, 0f), deltaTime);
    }

    private void ApplySlidePose(float phase)
    {
        // Five-stage slide: turn, drop, hand contact, glide, and push-off.
        // Each chain comes in at a different time so the pose reads as forward
        // momentum instead of a single static seated target.
        float recovery = SmoothRange(phase, 0.76f, 1f);
        float hold = 1f - recovery;
        float turnWeight = SmoothRange(phase, 0f, 0.1f) * hold;
        float dropWeight = SmoothRange(phase, 0.05f, 0.22f) * hold;
        float contactWeight = SmoothRange(phase, 0.12f, 0.24f) * hold;
        float glideWeight = SmoothRange(phase, 0.2f, 0.3f) * hold;
        float impactPulse = SmoothRange(phase, 0.12f, 0.2f)
            * (1f - SmoothRange(phase, 0.2f, 0.32f)) * hold;

        const float groundY = 0f;
        Vector3 groundUnderHips = new Vector3(
            _hipsBaseRootPos.x, groundY, _hipsBaseRootPos.z);
        float hipClearance = Mathf.Clamp(
            _hipsBaseRootPos.y - slideHipDrop, 0.15f, 0.25f);
        Vector3 lowHipsRoot = groundUnderHips
            + Vector3.up * (hipClearance - 0.025f * impactPulse)
            + Vector3.forward * (0.05f * glideWeight);

        if (hipsTransform != null)
        {
            hipsTransform.position = Vector3.Lerp(
                hipsTransform.position,
                transform.TransformPoint(lowHipsRoot), dropWeight);
        }
        if (bodyTransform != null)
            bodyTransform.localPosition = Vector3.Lerp(
                bodyTransform.localPosition, _bodyBasePos, dropWeight);

        BlendRootRotation(hipsTransform, _hipsBaseRootRot,
            Quaternion.Euler(12f, slideYawAngle, slideBodyRoll),
            turnWeight);
        BlendRootRotation(bodyTransform, _bodyBaseRootRot,
            Quaternion.Euler(
                slideSpineLean, slideYawAngle * 0.9f, slideBodyRoll),
            dropWeight);
        BlendRootRotation(chestTransform, _chestBaseRootRot,
            Quaternion.Euler(
                slideChestLean + impactPulse * 4f,
                slideYawAngle, slideBodyRoll * 1.15f),
            contactWeight);
        BlendRootRotation(upperChestTransform, _upperChestBaseRootRot,
            Quaternion.Euler(
                slideUpperChestLean + impactPulse * 5f,
                slideYawAngle, slideBodyRoll * 1.15f),
            contactWeight);
        BlendRootRotation(headTransform, _headBaseRootRot,
            Quaternion.Euler(
                slideHeadLean, slideYawAngle * 0.55f,
                slideBodyRoll * 0.25f),
            contactWeight);

        float leftLength = LegLength(
            leftUpperLeg, leftLowerLeg, leftFoot);
        float rightLength = LegLength(
            rightUpperLeg, rightLowerLeg, rightFoot);
        if (leftLength > 0f)
        {
            SolveTwoBoneLimb(leftUpperLeg, leftLowerLeg, leftFoot,
                groundUnderHips
                    + Vector3.forward * (leftLength * 0.75f)
                    + Vector3.left * (leftLength * 0.85f)
                    + Vector3.up * 0.08f,
                _leftFootBaseRootRot, leftLength,
                new Vector3(-0.15f, 1f, 0.6f), dropWeight);
        }
        else
        {
            BlendSlideKneeFlex(
                leftLowerLeg, _leftLowerLegBaseRot, 8f, dropWeight);
        }

        if (rightLength > 0f)
        {
            SolveTwoBoneLimb(rightUpperLeg, rightLowerLeg, rightFoot,
                groundUnderHips
                    - Vector3.forward * (rightLength * 0.2f)
                    + Vector3.right * (rightLength * 0.55f)
                    + Vector3.up * 0.13f,
                _rightFootBaseRootRot, rightLength,
                new Vector3(0.8f, 0.1f, -0.4f), contactWeight);
        }
        else
        {
            BlendSlideKneeFlex(
                rightLowerLeg, _rightLowerLegBaseRot,
                slideRearKneeBend, contactWeight);
        }

        float leftArmLength = LimbLength(
            leftUpperArm, leftLowerArm, leftHand);
        float rightArmLength = LimbLength(
            rightUpperArm, rightLowerArm, rightHand);
        if (leftArmLength > 0f)
        {
            Vector3 balanceHand = lowHipsRoot
                + Vector3.left * (leftArmLength * 0.08f)
                + Vector3.up * (leftArmLength * 0.28f)
                + Vector3.forward * (leftArmLength * 0.16f);
            SolveTwoBoneLimb(leftUpperArm, leftLowerArm, leftHand,
                balanceHand, _leftHandBaseRootRot, leftArmLength,
                new Vector3(-0.2f, 0.35f, 1f), contactWeight);
        }
        if (rightArmLength > 0f)
        {
            Vector3 plantedHand = groundUnderHips
                + Vector3.right * (rightArmLength * 0.55f)
                + Vector3.forward * (rightArmLength * 0.95f)
                + Vector3.up * 0.025f;
            SolveTwoBoneLimb(rightUpperArm, rightLowerArm, rightHand,
                plantedHand, _rightHandBaseRootRot, rightArmLength,
                new Vector3(0.8f, 0.1f, 0.4f), contactWeight);
        }
    }

    private static float SmoothRange(float value, float start, float end)
    {
        return Mathf.SmoothStep(0f, 1f,
            Mathf.InverseLerp(start, end, value));
    }

    private void BlendRootRotation(
        Transform bone, Quaternion baseRootRotation,
        Quaternion offset, float weight)
    {
        if (bone == null) return;
        Quaternion target = transform.rotation * offset * baseRootRotation;
        bone.rotation = Quaternion.Slerp(bone.rotation, target, weight);
    }

    private static float LimbLength(
        Transform upper, Transform lower, Transform end)
    {
        if (upper == null || lower == null || end == null) return 0f;
        return Vector3.Distance(upper.position, lower.position)
            + Vector3.Distance(lower.position, end.position);
    }

    private static float LegLength(
        Transform upperLeg, Transform lowerLeg, Transform foot)
    {
        return LimbLength(upperLeg, lowerLeg, foot);
    }

    private static void BlendSlideKneeFlex(
        Transform lowerLeg, Quaternion baseRotation,
        float minimumBend, float weight)
    {
        if (lowerLeg == null) return;
        Vector3 relativeEuler = SignedEuler(
            Quaternion.Inverse(baseRotation) * lowerLeg.localRotation);
        float authoredBend = Mathf.Max(0f, -relativeEuler.x);
        relativeEuler.x = Mathf.Lerp(relativeEuler.x,
            -Mathf.Max(authoredBend, minimumBend), weight);
        lowerLeg.localRotation =
            baseRotation * Quaternion.Euler(relativeEuler);
    }

    private void SolveTwoBoneLimb(
        Transform upper, Transform lower, Transform end,
        Vector3 targetRootPosition, Quaternion endRootRotation,
        float limbLength, Vector3 poleRootDirection, float weight)
    {
        if (upper == null || lower == null || end == null
            || limbLength <= 0.0001f)
        {
            return;
        }

        Vector3 target = Vector3.Lerp(
            end.position, transform.TransformPoint(targetRootPosition), weight);
        float upperLength = Vector3.Distance(
            upper.position, lower.position);
        float lowerLength = Vector3.Distance(
            lower.position, end.position);
        Vector3 upperToTarget = target - upper.position;
        float distance = Mathf.Clamp(upperToTarget.magnitude,
            Mathf.Abs(upperLength - lowerLength) + 0.001f,
            upperLength + lowerLength - 0.001f);
        Vector3 direction = upperToTarget.sqrMagnitude > 0.000001f
            ? upperToTarget.normalized
            : transform.forward;

        Vector3 pole = Vector3.ProjectOnPlane(
            transform.TransformDirection(poleRootDirection), direction);
        if (pole.sqrMagnitude < 0.000001f)
            pole = Vector3.ProjectOnPlane(transform.right, direction);
        pole.Normalize();

        float along = (upperLength * upperLength
            - lowerLength * lowerLength + distance * distance)
            / (2f * distance);
        float bend = Mathf.Sqrt(Mathf.Max(
            0f, upperLength * upperLength - along * along));
        Vector3 jointTarget = upper.position
            + direction * along + pole * bend;

        Vector3 currentUpper = lower.position - upper.position;
        Vector3 desiredUpper = jointTarget - upper.position;
        if (currentUpper.sqrMagnitude > 0.000001f
            && desiredUpper.sqrMagnitude > 0.000001f)
        {
            upper.rotation = Quaternion.FromToRotation(
                currentUpper, desiredUpper) * upper.rotation;
        }

        Vector3 currentLower = end.position - lower.position;
        Vector3 desiredLower = target - lower.position;
        if (currentLower.sqrMagnitude > 0.000001f
            && desiredLower.sqrMagnitude > 0.000001f)
        {
            lower.rotation = Quaternion.FromToRotation(
                currentLower, desiredLower) * lower.rotation;
        }

        end.rotation = Quaternion.Slerp(
            end.rotation, transform.rotation * endRootRotation, weight);
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

    private void AimRunningForearm(
        Transform lowerArm, Transform hand, bool isLeft,
        float targetVertical)
    {
        if (lowerArm == null || hand == null) return;
        Vector3 currentDirection = hand.position - lowerArm.position;
        if (currentDirection.sqrMagnitude < 0.000001f) return;

        // Solve from the actual elbow-to-wrist vector instead of assuming a
        // local Mixamo axis. Hands travel forward/up and slightly inward.
        Vector3 localTarget = new Vector3(
            isLeft ? 0.08f : -0.08f,
            targetVertical, 0.93f).normalized;
        Vector3 targetDirection = transform.TransformDirection(localTarget);
        lowerArm.rotation = Quaternion.FromToRotation(
            currentDirection.normalized, targetDirection) * lowerArm.rotation;
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

    private Vector3 RootPosition(Transform bone)
    {
        return bone != null
            ? transform.InverseTransformPoint(bone.position)
            : Vector3.zero;
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
