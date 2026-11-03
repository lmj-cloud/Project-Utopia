using UnityEngine;

/// <summary>
/// 기존 장비 상태를 읽어 Equipment 미리보기의 무기와 Idle만 표시한다. 실제 장비는 변경하지 않는다.
/// </summary>
public class EquipmentPreviewController : MonoBehaviour
{
    [Header("Existing References")]
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private Animator previewAnimator;
    [Header("Preview Weapon Renderers")]
    [SerializeField] private Renderer[] rifleRenderers = new Renderer[0];
    [SerializeField] private Renderer[] pistolRenderers = new Renderer[0];
    [SerializeField] private Renderer[] meleeRenderers = new Renderer[0];
    [Header("Existing Loop Clips / Matching Preview States")]
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip rifleIdleClip;
    [SerializeField] private AnimationClip pistolIdleClip;
    [SerializeField] private AnimationClip meleeIdleClip;
    [SerializeField, Min(0f)] private float transitionDuration = 0.15f;

    private bool wasVisible;
    private bool needsRefresh = true;
    private string currentState;

    private void Awake()
    {
        // 전시 모델은 이동하지 않으며, 인벤토리의 시간 정지 정책과 관계없이 Idle을 재생한다.
        if (previewAnimator != null)
        {
            previewAnimator.applyRootMotion = false;
            previewAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
    }

    private void OnEnable()
    {
        // 기존 장착/소유 이벤트를 구독한다. 매 프레임 모델 생성이나 상태 재시작은 하지 않는다.
        if (equipment != null) equipment.OnWeaponChanged += HandleWeaponChanged;
        if (inventory != null) inventory.Changed += HandleInventoryChanged;
        needsRefresh = true;
    }

    private void OnDisable()
    {
        if (equipment != null) equipment.OnWeaponChanged -= HandleWeaponChanged;
        if (inventory != null) inventory.Changed -= HandleInventoryChanged;
        if (previewAnimator != null) previewAnimator.enabled = false;
        wasVisible = false;
        SetRenderers(rifleRenderers, false);
        SetRenderers(pistolRenderers, false);
        SetRenderers(meleeRenderers, false);
    }

    private void HandleWeaponChanged(WeaponAssaultRifle weapon)
    {
        needsRefresh = true;
    }

    private void HandleInventoryChanged()
    {
        needsRefresh = true;
    }

    private void LateUpdate()
    {
        bool isVisible = inventoryUI != null && inventoryUI.IsOpen && inventoryUI.CurrentCategory == 0;
        if (!isVisible)
        {
            if (previewAnimator != null) previewAnimator.enabled = false;
            wasVisible = false;
            return;
        }
        if (!wasVisible || needsRefresh) RefreshPreview(!wasVisible);
        wasVisible = true;
    }

    /// <summary>
    /// 현재 장비와 소유 상태를 함께 검사한다. 클립/모델이 준비되지 않은 장비는 기본 Idle로 표시한다.
    /// </summary>
    private void RefreshPreview(bool isOpening)
    {
        needsRefresh = false;
        if (previewAnimator == null || equipment == null || previewAnimator.avatar == null ||
            !previewAnimator.avatar.isValid || !previewAnimator.isHuman) return;

        string state = "Idle";
        AnimationClip clip = idleClip;
        PlayerEquipmentKind kind = equipment.CurrentKind;
        if (kind == PlayerEquipmentKind.AssaultRifle && equipment.HasOwnership(kind) &&
            rifleRenderers.Length > 0 && rifleIdleClip != null)
        {
            state = "RifleIdle";
            clip = rifleIdleClip;
        }
        else if (kind == PlayerEquipmentKind.Pistol && equipment.HasOwnership(kind) &&
            pistolRenderers.Length > 0 && pistolIdleClip != null)
        {
            state = "PistolIdle";
            clip = pistolIdleClip;
        }
        else if (kind == PlayerEquipmentKind.Dagger && equipment.GetRequiredItem(kind) != null &&
            equipment.HasOwnership(kind) && meleeRenderers.Length > 0 && meleeIdleClip != null)
        {
            // 단검 데이터·모델·클립이 연결된 경우에만 향후 전용 상태를 사용할 수 있다.
            state = "MeleeIdle";
            clip = meleeIdleClip;
        }

        int stateHash = Animator.StringToHash("Base Layer." + state);
        if (clip == null || !clip.isLooping || !previewAnimator.HasState(0, stateHash))
        {
            state = "Idle";
            stateHash = Animator.StringToHash("Base Layer.Idle");
            if (idleClip == null || !idleClip.isLooping || !previewAnimator.HasState(0, stateHash)) return;
        }
        SetRenderers(rifleRenderers, state == "RifleIdle");
        SetRenderers(pistolRenderers, state == "PistolIdle");
        SetRenderers(meleeRenderers, state == "MeleeIdle");
        previewAnimator.enabled = true;
        if (isOpening)
        {
            previewAnimator.Play(stateHash, 0, 0f);
        }
        else if (currentState != state)
        {
            previewAnimator.CrossFadeInFixedTime(stateHash, transitionDuration, 0);
        }
        currentState = state;
    }

    private void SetRenderers(Renderer[] renderers, bool isVisible)
    {
        foreach (Renderer target in renderers)
        {
            if (target != null) target.enabled = isVisible;
        }
    }
}
