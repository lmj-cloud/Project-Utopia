using UnityEngine;

public class ThirdPersonAimController : MonoBehaviour
{
    [Header("Player References")]
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerCameraController cameraController;
    [SerializeField] private WeaponAssaultRifle weapon;

    [Header("Animator IK")]
    [Tooltip("현재 구성: Base Layer = 0, Rifle Layer = 1. Rifle Layer의 IK Pass를 켠다.")]
    [SerializeField, Min(0)] private int ikLayerIndex = 1;

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
    private Transform spine;
    private Transform chest;
    private Transform upperChest;
    private float currentPitch;
    private float aimWeight;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (playerController == null || cameraController == null)
        {
            Debug.LogError("ThirdPersonAimController: Player Controller와 Camera Controller를 연결하세요.", this);
            enabled = false;
            return;
        }

        if (animator.avatar == null || !animator.avatar.isValid || !animator.isHuman)
        {
            Debug.LogError("ThirdPersonAimController: 유효한 Humanoid Avatar가 필요합니다.", this);
            enabled = false;
            return;
        }

        if (ikLayerIndex >= animator.layerCount)
        {
            Debug.LogError("ThirdPersonAimController: IK Layer Index를 Animator 레이어 번호에 맞추세요.", this);
            enabled = false;
            return;
        }

        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
    }

    private void OnAnimatorIK(int layerIndex)
    {
        // 다른 레이어에서도 IK Pass가 켜져 있을 때 중복 적용을 막는다.
        if (!isActiveAndEnabled || layerIndex != ikLayerIndex)
            return;

        bool isAiming = playerController.isActiveAndEnabled &&
            cameraController.isActiveAndEnabled &&
            cameraController.IsThirdPerson &&
            playerController.IsAiming &&
            (weapon == null || !weapon.IsReloading);

        float targetWeight = isAiming ? 1f : 0f;
        aimWeight = Mathf.MoveTowards(aimWeight, targetWeight, aimBlendSpeed * Time.deltaTime);

        // 카메라의 pitch는 위를 보면 음수, 아래를 보면 양수다.
        float targetPitch = Mathf.Clamp(cameraController.Pitch, -maxUpAngle, maxDownAngle);
        currentPitch = Mathf.MoveTowards(currentPitch, targetPitch, pitchSpeed * Time.deltaTime);

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

        ApplyPitch(HumanBodyBones.Spine, spine, pitchToApply * spineShare / totalWeight, worldPitchAxis);
        ApplyPitch(HumanBodyBones.Chest, chest, pitchToApply * chestShare / totalWeight, worldPitchAxis);
        ApplyPitch(HumanBodyBones.UpperChest, upperChest, pitchToApply * upperChestShare / totalWeight, worldPitchAxis);
    }

    private static float GetAvailableWeight(Transform bone, float weight)
    {
        return bone != null && bone.parent != null ? Mathf.Max(0f, weight) : 0f;
    }

    private void ApplyPitch(HumanBodyBones boneId, Transform bone, float angle, Vector3 worldAxis)
    {
        if (bone == null || bone.parent == null || Mathf.Approximately(angle, 0f))
            return;

        // 뼈마다 로컬 축이 다르므로 Player의 오른쪽 방향을 부모 공간으로 변환한다.
        Vector3 localAxis = Quaternion.Inverse(bone.parent.rotation) * worldAxis;
        Quaternion pitchOffset = Quaternion.AngleAxis(angle, localAxis);

        // 현재 프레임의 애니메이션 자세에 회전을 더한다. 시작 자세로 덮어쓰지 않는다.
        animator.SetBoneLocalRotation(boneId, pitchOffset * bone.localRotation);
    }

    private void OnDisable()
    {
        currentPitch = 0f;
        aimWeight = 0f;
    }
}
