using UnityEngine;

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

    private void Update()
    {
        // Remove only our still-present offset before this frame's animation evaluates.
        // An Animator/Timeline pose already written by another owner is left untouched.
        RestorePitchOffsets();
        if (!CanApplyPitch) ResetPitchState();
    }

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

        ApplyPitch(HumanBodyBones.Spine, spine, pitchToApply * spineShare / totalWeight, worldPitchAxis, 0);
        ApplyPitch(HumanBodyBones.Chest, chest, pitchToApply * chestShare / totalWeight, worldPitchAxis, 1);
        ApplyPitch(HumanBodyBones.UpperChest, upperChest, pitchToApply * upperChestShare / totalWeight, worldPitchAxis, 2);
    }

    private static float GetAvailableWeight(Transform bone, float weight)
    {
        return bone != null && bone.parent != null ? Mathf.Max(0f, weight) : 0f;
    }

    private void ApplyPitch(HumanBodyBones boneId, Transform bone, float angle, Vector3 worldAxis, int index)
    {
        if (bone == null || bone.parent == null || Mathf.Approximately(angle, 0f))
            return;

        // 뼈마다 로컬 축이 다르므로 Player의 오른쪽 방향을 부모 공간으로 변환한다.
        Vector3 localAxis = Quaternion.Inverse(bone.parent.rotation) * worldAxis;
        Quaternion pitchOffset = Quaternion.AngleAxis(angle, localAxis);

        // 현재 프레임의 애니메이션 자세에 회전을 더한다. 시작 자세로 덮어쓰지 않는다.
        Quaternion animatedRotation = bone.localRotation;
        Quaternion correctedRotation = pitchOffset * animatedRotation;
        animatedRotations[index] = animatedRotation;
        appliedRotations[index] = correctedRotation;
        hasPitchOffset[index] = true;
        animator.SetBoneLocalRotation(boneId, correctedRotation);
    }

    private void RestorePitchOffsets()
    {
        RestorePitchOffset(spine, 0);
        RestorePitchOffset(chest, 1);
        RestorePitchOffset(upperChest, 2);
    }

    private void RestorePitchOffset(Transform bone, int index)
    {
        if (!hasPitchOffset[index]) return;
        if (bone != null && Quaternion.Angle(bone.localRotation, appliedRotations[index]) < 0.01f)
            bone.localRotation = animatedRotations[index];
        hasPitchOffset[index] = false;
    }

    private void ResetPitchState()
    {
        RestorePitchOffsets();
        currentPitch = 0f;
        aimWeight = 0f;
        appliedFrame = -1;
    }

    private void OnDisable() => ResetPitchState();
}
