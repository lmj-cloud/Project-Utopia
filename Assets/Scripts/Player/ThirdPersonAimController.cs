using UnityEngine;

/// <summary>
/// 유효한 Humanoid에서 소총·권총 조준을 상체에 보정하고 이전 프레임의 보정을 복원한다.
/// </summary>
// 최종 상체 보정을 소총의 기본 순서 LateUpdate 사격 판정보다 먼저 끝낸다.
[DefaultExecutionOrder(-10)]
public class ThirdPersonAimController : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerCameraController cameraController;
    [SerializeField] private WeaponAssaultRifle weapon;

    [Header("Animator IK")]
    [Tooltip("Base Layer = 0. UpperLayer도 IK Pass를 켜고, 가중치가 있을 때만 마지막 레이어에서 한 번 보정한다.")]
    [SerializeField, Min(0)] private int ikLayerIndex = 0;

    [Header("Aim Pitch")]
    [SerializeField, Range(0f, 80f)] private float maxUpAngle = 60f;
    [SerializeField, Range(0f, 80f)] private float maxDownAngle = 45f;
    [SerializeField, Min(0.01f)] private float pitchSpeed = 180f;
    [SerializeField, Min(0.01f)] private float aimBlendSpeed = 6f;

    [Header("Rifle Direction")]
    [Tooltip("실제 총열과 같은 +Z 방향. 무기 소켓을 돌리지 않고 상체 조준 오차를 계산한다.")]
    [SerializeField] private Transform rifleDirection;
    [SerializeField] private Transform pistolDirection;
    [SerializeField, Range(0f, 90f)] private float maxYawCorrection = 80f;

    [Header("Spine Rotation Weights")]
    [SerializeField, Min(0f)] private float spineWeight = 0.25f;
    [SerializeField, Min(0f)] private float chestWeight = 0.5f;
    [SerializeField, Min(0f)] private float upperChestWeight = 0.25f;

    [Header("MMA Boxing Idle Look At")]
    [SerializeField] private bool enableMmaIdleLookAt = true;
    [SerializeField, Range(0f, 80f)] private float mmaLookHorizontalLimit = 45f;
    [SerializeField, Range(0f, 60f)] private float mmaLookUpLimit = 25f;
    [SerializeField, Range(0f, 60f)] private float mmaLookDownLimit = 20f;
    [SerializeField, Min(0.01f)] private float mmaLookBlendSeconds = 0.2f;
    [SerializeField, Range(0f, 1f)] private float mmaLookHeadWeight = 0.85f;
    [SerializeField, Range(0f, 1f)] private float mmaLookClampWeight = 0.5f;
    [SerializeField, Min(1f)] private float mmaLookDistance = 10f;
    [SerializeField, Min(1f)] private float mmaLookTurnSpeed = 180f;

    [Header("MMA Punch Look At")]
    [SerializeField] private bool enableMmaAttackLookAt = true;
    [SerializeField, Range(0f, 0.3f)] private float mmaIdleBodyWeight = 0.1f;
    [SerializeField, Range(0f, 1f)] private float mmaAttackHeadWeight = 0.7f;
    [SerializeField, Range(0f, 0.3f)] private float mmaAttackBodyWeight = 0.04f;
    [SerializeField, Range(0f, 60f)] private float mmaAttackHorizontalLimit = 35f;
    [SerializeField, Range(0f, 45f)] private float mmaAttackVerticalLimit = 20f;
    [SerializeField, Min(0.01f)] private float mmaAttackBlendSeconds = 0.15f;

    private PlayerAnimatorController playerAnimator;
    private PlayerMovement playerMovement;
    private PlayerCombatActions combatActions;
    private PlayerEquipment equipment;
    private Transform head;
    private float mmaLookWeight;
    private float mmaLookYaw;
    private float mmaLookPitch;
    private float mmaLookBodyBlend;
    private float mmaLookHeadBlend;
    private int mmaLookFrame = -1;

    private Animator animator;
    private int upperIKLayer = -1;
    private Transform spine;
    private Transform chest;
    private Transform upperChest;
    private float currentPitch;
    private float aimWeight;
    private int appliedFrame = -1;
    private readonly Quaternion[] animatedRotations = new Quaternion[3];
    private readonly Quaternion[] appliedRotations = new Quaternion[3];
    private readonly bool[] hasPitchOffset = new bool[3];

    // 재장전은 조준 Pitch와 분리하고 실제 총구의 수평 방향만 현재 Player Root에 맞춘다.
    private bool CanApplyReloadFacing => playerController != null && playerController.CanUseWeapons &&
        playerController.CurrentWeapon != null && playerController.CurrentWeapon.IsReloading;

    private bool CanApplyPitch => playerController != null &&
        (playerController.CanApplyRifleAim || playerController.CanApplyPistolAim || CanApplyReloadFacing) &&
        (playerController.CurrentWeapon != null && playerController.CurrentWeapon.WeaponName == WeaponName.Pistol ||
            weapon == null || weapon == playerController.CurrentWeapon) &&
        cameraController != null && cameraController.isActiveAndEnabled && cameraController.IsThirdPerson &&
        animator != null && animator.isActiveAndEnabled && animator.runtimeAnimatorController != null;

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (playerController == null || cameraController == null)
        {
            Debug.LogError("ThirdPersonAimController: Player Controller와 Camera Controller를 연결하세요.", this);
            enabled = false;
            return;
        }

        if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
        {
            Debug.LogError("ThirdPersonAimController: 유효한 Humanoid Avatar가 필요합니다.", this);
            enabled = false;
            return;
        }

        if (ikLayerIndex < 0 || ikLayerIndex >= animator.layerCount)
        {
            Debug.LogError("ThirdPersonAimController: IK Layer Index를 Animator 레이어 번호에 맞추세요.", this);
            enabled = false;
            return;
        }

        upperIKLayer = animator.GetLayerIndex("UpperLayer");
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
        head = animator.GetBoneTransform(HumanBodyBones.Head);
        playerAnimator = playerController.GetComponent<PlayerAnimatorController>();
        playerMovement = playerController.GetComponent<PlayerMovement>();
        combatActions = playerController.GetComponent<PlayerCombatActions>();
        equipment = playerController.GetComponent<PlayerEquipment>();
    }

    /// <summary>
    /// 이전 프레임에 이 컴포넌트가 더한 조준 회전을 제거하고 조준 조건이 사라지면 보정 상태를 초기화한다.
    /// </summary>
    private void Update()
    {
        // Remove only our still-present offset before this frame's animation evaluates.
        // An Animator/Timeline pose already written by another owner is left untouched.
        RestorePitchOffsets();
        if (!CanApplyPitch) ResetPitchState();
    }

    /// <summary>
    /// 소총 조준 조건과 적용 Layer를 확인한 뒤 한 프레임에 한 번 척추 각도를 보정한다.
    /// </summary>
    private void OnAnimatorIK(int layerIndex)
    {
        // 다른 레이어에서도 IK Pass가 켜져 있을 때 중복 적용을 막는다.
        // 상체 행동은 마지막 IK Pass에서, 일반 이동은 Base에서 적용해 덮어쓰기와 중복을 피한다.
        int selectedLayer = animator != null && upperIKLayer >= 0 && upperIKLayer < animator.layerCount &&
            animator.GetLayerWeight(upperIKLayer) > 0f ? upperIKLayer : ikLayerIndex;
        if (!isActiveAndEnabled || layerIndex != selectedLayer)
            return;

        ApplyMmaLookAt();

        if (!CanApplyPitch)
        {
            ResetPitchState();
            return;
        }
        if (appliedFrame == Time.frameCount) return;
        appliedFrame = Time.frameCount;
        // 카메라의 pitch는 위를 보면 음수, 아래를 보면 양수다.
        bool isReloading = CanApplyReloadFacing;
        // 재장전 클립이 총을 들어 올리는 자연스러운 동작은 보존한다.
        float targetPitch = isReloading ? 0f : Mathf.Clamp(cameraController.Pitch, -maxUpAngle, maxDownAngle);
        if (playerController.RifleFiredThisFrame || playerController.PistolFiredThisFrame)
        {
            // 발사 확정 프레임은 블렌드 지연 없이, 이 프레임의 최종 IK Pass에 반영한다.
            aimWeight = 1f;
            currentPitch = targetPitch;
        }
        else
        {
            bool requested = isReloading || playerController.WantsRifleAimCorrection || playerController.WantsPistolAimCorrection;
            aimWeight = Mathf.MoveTowards(aimWeight, requested ? 1f : 0f, aimBlendSpeed * Time.deltaTime);
            if (requested) currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, pitchSpeed * Time.deltaTime);
            else if (aimWeight <= 0f) currentPitch = 0f;
        }

        if (aimWeight <= 0f)
            return;

        float spineShare = GetAvailableWeight(spine, spineWeight);
        float chestShare = GetAvailableWeight(chest, chestWeight);
        float upperChestShare = GetAvailableWeight(upperChest, upperChestWeight);
        float totalWeight = spineShare + chestShare + upperChestShare;
        if (totalWeight <= 0f)
            return;

        // UpperChest가 없는 모델도 남은 뼈가 조준 각도를 나누어 담당한다.
        float pitchToApply = isReloading ? 0f : currentPitch * aimWeight;
        if (!isReloading && playerController.CanApplyPistolAim && playerController.IsAiming && pistolDirection != null)
        {
            // 권총 준비 자세에 포함된 총열의 상하 각도까지 반영한다. 공유 Pitch 상태를 바꾸지 않아 소총 전환에 영향을 주지 않는다.
            float barrelElevation = Mathf.Asin(Mathf.Clamp(pistolDirection.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            pitchToApply = Mathf.Clamp(currentPitch + barrelElevation, -maxUpAngle, maxDownAngle) * aimWeight;
        }
        Vector3 worldPitchAxis = playerController.transform.right;
        float yawToApply = 0f;
        // 권총과 소총은 각자의 실제 총구를 기준으로 기존 상체 보정을 공유한다.
        bool isPistol = playerController.CurrentWeapon != null && playerController.CurrentWeapon.WeaponName == WeaponName.Pistol;
        Transform aimDirection = isPistol ? pistolDirection : rifleDirection;
        if (aimDirection != null)
        {
            // Root를 제외한 상체 클립은 하체 자세에 따라 총열의 수평 방향이 달라진다.
            Vector3 barrelForward = Vector3.ProjectOnPlane(aimDirection.forward, Vector3.up);
            // 조준은 카메라 기준, 재장전은 현재 몸 방향 기준이다. Root 자체는 회전시키지 않는다.
            Vector3 cameraForward = isReloading ? playerController.transform.forward : cameraController.Forward;
            if (barrelForward.sqrMagnitude > 0.001f)
                yawToApply = Mathf.Clamp(Vector3.SignedAngle(barrelForward, cameraForward, Vector3.up),
                    -maxYawCorrection, maxYawCorrection) * aimWeight;
        }

        ApplyPitch(HumanBodyBones.Spine, spine, pitchToApply * spineShare / totalWeight, worldPitchAxis, 0, yawToApply * spineShare / totalWeight);
        ApplyPitch(HumanBodyBones.Chest, chest, pitchToApply * chestShare / totalWeight, worldPitchAxis, 1, yawToApply * chestShare / totalWeight);
        ApplyPitch(HumanBodyBones.UpperChest, upperChest, pitchToApply * upperChestShare / totalWeight, worldPitchAxis, 2, yawToApply * upperChestShare / totalWeight);
    }

    /// <summary>기존 IK Pass에서 MMA 시선을 더한다. 공격의 Root·판정은 유지하고 피격·총기·컷씬을 우선한다.</summary>
    private void ApplyMmaLookAt()
    {
        if (mmaLookFrame == Time.frameCount) return;
        mmaLookFrame = Time.frameCount;
        if (head == null || playerController == null || playerAnimator == null || playerMovement == null ||
            cameraController == null || !cameraController.isActiveAndEnabled || !cameraController.IsThirdPerson || !cameraController.LookEnabled ||
            equipment == null || equipment.CurrentKind != PlayerEquipmentKind.Gloves ||
            !playerController.CanUseMma || playerController.HasAnimationControlLock || !playerController.IsAlive ||
            playerAnimator.IsHitReacting || (combatActions != null && combatActions.IsRunning && !combatActions.HasMmaAttackFacing))
        {
            ResetMmaLookAt();
            animator.SetLookAtWeight(0f);
            return;
        }

        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        bool punch = combatActions != null && combatActions.IsRunning && combatActions.HasMmaAttackFacing;
        // CrossFade의 이전·다음 펀치까지 포함해 Jab/Cross/Combo 사이에서 시선 블렌드를 초기화하지 않는다.
        punch = punch || IsMmaPunchPose(state) ||
            (animator.IsInTransition(0) && IsMmaPunchPose(animator.GetNextAnimatorStateInfo(0)));
        bool idle = state.IsName("Base Layer.MMA.Locomotion") && !animator.IsInTransition(0) &&
            playerMovement.IsGrounded && !playerMovement.IsCrouching && playerMovement.HorizontalVelocity.sqrMagnitude < 0.0025f;
        bool requested = enableMmaIdleLookAt && ((idle && playerAnimator.UsesMmaCombatLocomotion) || (punch && enableMmaAttackLookAt));
        float blendSeconds = Mathf.Max(0.01f, punch ? mmaAttackBlendSeconds : mmaLookBlendSeconds);
        float headWeight = punch ? mmaAttackHeadWeight : mmaLookHeadWeight;
        float bodyWeight = punch ? Mathf.Min(mmaAttackBodyWeight, mmaIdleBodyWeight) : mmaIdleBodyWeight;
        if (mmaLookWeight <= 0f)
        {
            mmaLookHeadBlend = headWeight;
            mmaLookBodyBlend = bodyWeight;
        }
        float blend = 1f - Mathf.Exp(-3f * Time.deltaTime / blendSeconds);
        mmaLookHeadBlend = Mathf.Lerp(mmaLookHeadBlend, headWeight, blend);
        mmaLookBodyBlend = Mathf.Lerp(mmaLookBodyBlend, bodyWeight, blend);
        mmaLookWeight = Mathf.MoveTowards(mmaLookWeight, requested ? 1f : 0f, Time.deltaTime / blendSeconds);
        if (mmaLookWeight <= 0f)
        {
            ResetMmaLookAt();
            animator.SetLookAtWeight(0f);
            return;
        }

        // 카메라 위치가 아닌 시선 방향을 머리에서 멀리 연장한다. 몸 기준 yaw/pitch를 제한해 뒤돌아보지 않는다.
        Vector3 viewDirection = Quaternion.AngleAxis(cameraController.Pitch, Vector3.Cross(Vector3.up, cameraController.Forward)) * cameraController.Forward;
        Vector3 localDirection = playerController.transform.InverseTransformDirection(viewDirection);
        float yaw = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(localDirection.y, new Vector2(localDirection.x, localDirection.z).magnitude) * Mathf.Rad2Deg;
        float yawLimit = punch ? mmaAttackHorizontalLimit : mmaLookHorizontalLimit;
        float upLimit = punch ? mmaAttackVerticalLimit : mmaLookUpLimit;
        float downLimit = punch ? mmaAttackVerticalLimit : mmaLookDownLimit;
        yaw = Mathf.Clamp(yaw, -yawLimit, yawLimit);
        pitch = Mathf.Clamp(pitch, -upLimit, downLimit);
        mmaLookYaw = Mathf.MoveTowardsAngle(mmaLookYaw, yaw, mmaLookTurnSpeed * Time.deltaTime);
        mmaLookPitch = Mathf.MoveTowards(mmaLookPitch, pitch, mmaLookTurnSpeed * Time.deltaTime);
        Vector3 lookDirection = playerController.transform.rotation * Quaternion.Euler(mmaLookPitch, mmaLookYaw, 0f) * Vector3.forward;
        animator.SetLookAtPosition(head.position + lookDirection * mmaLookDistance);
        // 공격의 골반·어깨 회전을 지우지 않도록 상체 영향은 Idle보다 낮춘다. 눈 IK는 사용하지 않는다.
        animator.SetLookAtWeight(mmaLookWeight, mmaLookBodyBlend, mmaLookHeadBlend, 0f, mmaLookClampWeight);
    }

    private static bool IsMmaPunchPose(AnimatorStateInfo state)
    {
        return state.IsName("Base Layer.MMA.LeftPunch") || state.IsName("Base Layer.MMA.RightPunch") ||
            state.IsName("Base Layer.MMA.ComboPunch");
    }

    private void ResetMmaLookAt()
    {
        mmaLookWeight = 0f;
        mmaLookYaw = 0f;
        mmaLookPitch = 0f;
        mmaLookBodyBlend = 0f;
        mmaLookHeadBlend = 0f;
    }

    /// <summary>
    /// Humanoid 평가 뒤 실제 총구에서 사격 목표로 향하는 잔여 오차를 상체에 나누어 적용한다.
    /// </summary>
    private void LateUpdate()
    {
        if (!CanApplyPitch || !playerController.IsAiming || CanApplyReloadFacing ||
            aimWeight <= 0f || appliedFrame != Time.frameCount) return;

        WeaponAssaultRifle currentWeapon = playerController.CurrentWeapon;
        Transform aimDirection = currentWeapon != null && currentWeapon.WeaponName == WeaponName.Pistol
            ? pistolDirection : rifleDirection;
        if (aimDirection == null || currentWeapon == null ||
            !currentWeapon.TryGetAimTarget(out Vector3 targetPoint)) return;

        float spineShare = GetAvailableWeight(spine, spineWeight);
        float chestShare = GetAvailableWeight(chest, chestWeight);
        float upperChestShare = GetAvailableWeight(upperChest, upperChestWeight);
        float totalWeight = spineShare + chestShare + upperChestShare;
        if (totalWeight <= 0f) return;

        // 카메라와 몸 사이의 자기 Collider 등 뒤쪽 목표로 상체를 뒤집지 않는다.
        if (Vector3.Dot(targetPoint - playerController.transform.position, playerController.transform.forward) <= 0f)
        {
            ApplyPistolCameraAim();
            return;
        }

        Vector3 muzzleToTarget = targetPoint - aimDirection.position;
        float horizontalDistance = Vector3.ProjectOnPlane(muzzleToTarget, Vector3.up).magnitude;
        float requiredPitch = Mathf.Atan2(-muzzleToTarget.y, horizontalDistance) * Mathf.Rad2Deg;
        // 발밑처럼 기존 조준 범위를 넘는 목표는 원래 IK 자세를 유지한다. 정렬을 위해 허리를 과하게 꺾지 않는다.
        if (requiredPitch < -maxUpAngle || requiredPitch > maxDownAngle)
        {
            ApplyPistolCameraAim();
            return;
        }
        // 제한 각도의 마지막 10도에서는 잔여 보정을 서서히 줄여 경계에서 자세가 튀지 않게 한다.
        float pitchMargin = Mathf.Min(requiredPitch + maxUpAngle, maxDownAngle - requiredPitch);
        float correctionWeight = aimWeight * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(pitchMargin / 10f));

        ApplyAimCorrection(spine, 0, aimDirection, targetPoint, correctionWeight * spineShare / totalWeight);
        ApplyAimCorrection(chest, 1, aimDirection, targetPoint, correctionWeight * chestShare / totalWeight);
        ApplyAimCorrection(upperChest, 2, aimDirection, targetPoint, correctionWeight * upperChestShare / totalWeight);

        // 총구도 뼈를 중심으로 이동하므로 마지막 잔여 오차는 실제 회전 중심을 기준으로 해결한다.
        if (upperChest != null) ApplyAimCorrection(upperChest, 2, aimDirection, targetPoint, correctionWeight);
        else if (chest != null) ApplyAimCorrection(chest, 1, aimDirection, targetPoint, correctionWeight);
        else ApplyAimCorrection(spine, 0, aimDirection, targetPoint, correctionWeight);
    }

    /// <summary>상체 뼈에만 보정을 더하고 다음 프레임에 제거할 회전을 기록한다. 무기와 손 소켓은 그대로 둔다.</summary>
    private void ApplyAimCorrection(Transform bone, int index, Transform aimDirection, Vector3 targetPoint, float weight)
    {
        if (bone == null || weight <= 0f) return;
        Vector3 muzzleOffset = aimDirection.position - bone.position;
        Vector3 targetOffset = targetPoint - bone.position;
        Vector3 barrelForward = aimDirection.forward;
        float alongBarrel = Vector3.Dot(muzzleOffset, barrelForward);
        float discriminant = alongBarrel * alongBarrel + targetOffset.sqrMagnitude - muzzleOffset.sqrMagnitude;
        // 목표가 팔·총구의 회전 반경 안에 있으면 해당 자세로는 도달할 수 없다. 억지로 몸을 꺾지 않는다.
        if (discriminant < 0f) return;
        float barrelDistance = Mathf.Sqrt(discriminant) - alongBarrel;
        if (barrelDistance <= 0f) return;

        // 회전 뒤에도 총열 선 위에 남을 점을 목표와 같은 반경에서 구한다.
        // 단순 FromToRotation(총열, 목표-총구)와 달리 총구 위치가 함께 움직이는 근거리 시차를 포함한다.
        Vector3 pointOnBarrel = muzzleOffset + barrelForward * barrelDistance;
        if (pointOnBarrel.sqrMagnitude < 0.0001f || targetOffset.sqrMagnitude < 0.0001f) return;
        Quaternion correction = Quaternion.FromToRotation(pointOnBarrel, targetOffset);
        if (!hasPitchOffset[index]) animatedRotations[index] = bone.localRotation;
        bone.rotation = Quaternion.Slerp(Quaternion.identity, correction, weight) * bone.rotation;
        appliedRotations[index] = bone.localRotation;
        hasPitchOffset[index] = true;
    }

    /// <summary>근거리 목표가 관절 범위를 넘으면 권총의 기존 카메라 방향 보정을 그대로 사용한다.</summary>
    private void ApplyPistolCameraAim()
    {
        if (!playerController.CanApplyPistolAim || pistolDirection == null) return;
        Transform targetBone = upperChest;
        int index = 2;
        if (targetBone == null) { targetBone = chest; index = 1; }
        if (targetBone == null) { targetBone = spine; index = 0; }
        if (targetBone == null) return;
        float pitch = Mathf.Clamp(cameraController.Pitch, -maxUpAngle, maxDownAngle);
        Quaternion viewRotation = Quaternion.LookRotation(cameraController.Forward, Vector3.up);
        Vector3 targetForward = viewRotation * Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;
        Quaternion correction = Quaternion.FromToRotation(pistolDirection.forward, targetForward);
        if (!hasPitchOffset[index]) animatedRotations[index] = targetBone.localRotation;
        targetBone.rotation = Quaternion.Slerp(Quaternion.identity, correction, aimWeight) * targetBone.rotation;
        appliedRotations[index] = targetBone.localRotation;
        hasPitchOffset[index] = true;
    }

    /// <summary>
    /// 참조 뼈가 유효한지에 따라 적용 가능한 조준 보정 가중치를 반환한다.
    /// </summary>
    private static float GetAvailableWeight(Transform bone, float weight)
    {
        return bone != null && bone.parent != null ? Mathf.Max(0f, weight) : 0f;
    }

    /// <summary>
    /// 현재 애니메이션 자세 위에 지정한 뼈의 조준 상하 회전을 더한다.
    /// </summary>
    private void ApplyPitch(HumanBodyBones boneId, Transform bone, float angle, Vector3 worldAxis, int index, float yaw)
    {
        if (bone == null || bone.parent == null || (Mathf.Approximately(angle, 0f) && Mathf.Approximately(yaw, 0f)))
            return;

        // 뼈마다 로컬 축이 다르므로 Player의 오른쪽 방향을 부모 공간으로 변환한다.
        Vector3 localAxis = Quaternion.Inverse(bone.parent.rotation) * worldAxis;
        Quaternion pitchOffset = Quaternion.AngleAxis(angle, localAxis);

        // 현재 프레임의 애니메이션 자세에 회전을 더한다. 시작 자세로 덮어쓰지 않는다.
        Quaternion animatedRotation = bone.localRotation;
        Vector3 localUp = Quaternion.Inverse(bone.parent.rotation) * Vector3.up;
        Quaternion correctedRotation = Quaternion.AngleAxis(yaw, localUp) * pitchOffset * animatedRotation;
        animatedRotations[index] = animatedRotation;
        appliedRotations[index] = correctedRotation;
        hasPitchOffset[index] = true;
        animator.SetBoneLocalRotation(boneId, correctedRotation);
    }

    /// <summary>
    /// 이전 프레임에 적용한 각 뼈의 조준 보정을 먼저 제거한다.
    /// </summary>
    private void RestorePitchOffsets()
    {
        RestorePitchOffset(spine, 0);
        RestorePitchOffset(chest, 1);
        RestorePitchOffset(upperChest, 2);
    }

    /// <summary>
    /// 해당 뼈에 마지막으로 적용한 보정만 복원하여 누적 회전을 막는다.
    /// </summary>
    private void RestorePitchOffset(Transform bone, int index)
    {
        if (!hasPitchOffset[index]) return;
        if (bone != null && Quaternion.Angle(bone.localRotation, appliedRotations[index]) < 0.01f)
            bone.localRotation = animatedRotations[index];
        hasPitchOffset[index] = false;
    }

    /// <summary>
    /// 저장된 조준 각도와 보정 상태를 초기화한다.
    /// </summary>
    private void ResetPitchState()
    {
        RestorePitchOffsets();
        currentPitch = 0f;
        aimWeight = 0f;
        appliedFrame = -1;
    }

    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        ResetMmaLookAt();
        mmaLookFrame = -1;
        ResetPitchState();
    }
}
