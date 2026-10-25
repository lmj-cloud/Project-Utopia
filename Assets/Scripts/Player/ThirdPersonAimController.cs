using UnityEngine;

/// <summary>
/// 유효한 Humanoid와 소총 조준 상태에서 척추의 상하 각도를 보정하고 이전 프레임의 보정을 복원한다.
/// </summary>
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
    [Tooltip("실제 총열의 +Z 방향. 총을 회전시키지 않고 상체 자세의 수평 오차만 계산한다.")]
    [SerializeField] private Transform rifleDirection;
    [SerializeField, Range(0f, 90f)] private float maxYawCorrection = 80f;

    [Header("Spine Rotation Weights")]
    [SerializeField, Min(0f)] private float spineWeight = 0.25f;
    [SerializeField, Min(0f)] private float chestWeight = 0.5f;
    [SerializeField, Min(0f)] private float upperChestWeight = 0.25f;

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

    private bool CanApplyPitch => playerController != null && playerController.CanApplyRifleAim &&
        (weapon == null || weapon == playerController.CurrentWeapon) &&
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

        if (!CanApplyPitch)
        {
            ResetPitchState();
            return;
        }
        if (appliedFrame == Time.frameCount) return;
        appliedFrame = Time.frameCount;
        // 카메라의 pitch는 위를 보면 음수, 아래를 보면 양수다.
        float targetPitch = Mathf.Clamp(cameraController.Pitch, -maxUpAngle, maxDownAngle);
        if (playerController.RifleFiredThisFrame)
        {
            // 발사 확정 프레임은 블렌드 지연 없이, 이 프레임의 최종 IK Pass에 반영한다.
            aimWeight = 1f;
            currentPitch = targetPitch;
        }
        else
        {
            bool requested = playerController.WantsRifleAimCorrection;
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
        float pitchToApply = currentPitch * aimWeight;
        Vector3 worldPitchAxis = playerController.transform.right;
        float yawToApply = 0f;
        if (rifleDirection != null)
        {
            // Root를 제외한 상체 클립은 하체 자세에 따라 총열의 수평 방향이 달라진다.
            Vector3 barrelForward = Vector3.ProjectOnPlane(rifleDirection.forward, Vector3.up);
            Vector3 cameraForward = cameraController.Forward;
            if (barrelForward.sqrMagnitude > 0.001f)
                yawToApply = Mathf.Clamp(Vector3.SignedAngle(barrelForward, cameraForward, Vector3.up),
                    -maxYawCorrection, maxYawCorrection) * aimWeight;
        }

        ApplyPitch(HumanBodyBones.Spine, spine, pitchToApply * spineShare / totalWeight, worldPitchAxis, 0, yawToApply * spineShare / totalWeight);
        ApplyPitch(HumanBodyBones.Chest, chest, pitchToApply * chestShare / totalWeight, worldPitchAxis, 1, yawToApply * chestShare / totalWeight);
        ApplyPitch(HumanBodyBones.UpperChest, upperChest, pitchToApply * upperChestShare / totalWeight, worldPitchAxis, 2, yawToApply * upperChestShare / totalWeight);
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
    private void OnDisable() => ResetPitchState();
}
