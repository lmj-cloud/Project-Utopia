using UnityEngine;

/// <summary>
/// 플레이어의 소총·권총·맨손·글러브·단검 장비를 구분하는 기존 식별 값이다.
/// </summary>
public enum PlayerEquipmentKind { AssaultRifle = 0, Pistol = 1, BareHands = 2, Gloves = 3, Dagger = 4 }

/// <summary>
/// 소유 조건과 장비 슬롯을 검사하여 무기와 근접 장비를 전환하고 표시와 장착 상태를 맞춘다.
/// </summary>
public class PlayerEquipment : MonoBehaviour
{
    /// <summary>
    /// 소유·장비 조건을 확인하고 입력 잠금 중에도 확정된 저장 장비 선택을 복원한다.
    /// </summary>
    public bool RestoreEquipment(PlayerEquipmentKind kind)
    {
        // 입력 잠금 중에도 세션 복원은 가능하지만, 없는 모델이나 소유하지 않은 장비는 만들지 않는다.
        Initialize();
        EquipmentSlot slot;
        switch (kind)
        {
            case PlayerEquipmentKind.AssaultRifle: slot = assaultRifle; break;
            case PlayerEquipmentKind.Pistol: slot = pistol; break;
            case PlayerEquipmentKind.BareHands: slot = null; break;
            case PlayerEquipmentKind.Gloves: slot = gloves; break;
            case PlayerEquipmentKind.Dagger: slot = dagger; break;
            default: return false;
        }
        if (!HasOwnership(kind))
        {
            return false;
        }
        if (!IsSlotReady(kind, slot))
        {
            return false;
        }
        if (melee != null) melee.CancelActions();
        CurrentKind = kind;
        Select(slot);
        return true;
    }

    /// <summary>
    /// 장비의 무기 컴포넌트와 1·3인칭 표시 오브젝트 및 Renderer를 연결한다.
    /// </summary>
    [System.Serializable]
    private class EquipmentSlot
    {
        public WeaponAssaultRifle weapon;
        public GameObject firstPersonDisplay;
        public GameObject thirdPersonDisplay;
        public Renderer[] thirdPersonWeaponRenderers = new Renderer[0];
    }

    [SerializeField] private EquipmentSlot assaultRifle = new EquipmentSlot();
    [SerializeField] private EquipmentSlot pistol = new EquipmentSlot();
    [SerializeField] private WeaponName initialWeapon = WeaponName.AssaultRifle;
    [SerializeField] private PlayerCameraController cameraController;
    [SerializeField] private EquipmentSlot gloves = new EquipmentSlot();
    [SerializeField] private EquipmentSlot dagger = new EquipmentSlot();
    // 시작 시 선택할 장비 종류. 준비되지 않으면 기존 대체 정책을 적용한다.
    [SerializeField] private PlayerEquipmentKind initialKind = PlayerEquipmentKind.AssaultRifle;
    [SerializeField] private PlayerInventory inventory;
    // 소총 장착에 필요한 소유 아이템. 연결되어 있을 때만 보유 조건을 검사한다.
    [SerializeField] private ItemData requiredRifleItem;
    // 권총 장착에 필요한 소유 아이템. 연결되어 있을 때만 보유 조건을 검사한다.
    [SerializeField] private ItemData requiredPistolItem;
    // 글러브 장착에 필요한 소유 아이템. 연결되어 있을 때만 보유 조건을 검사한다.
    [SerializeField] private ItemData requiredGlovesItem;
    // 단검 장착에 필요한 소유 아이템. 연결되어 있을 때만 보유 조건을 검사한다.
    [SerializeField] private ItemData requiredDaggerItem;
    [SerializeField] private PlayerMeleeCombat melee;
    public PlayerEquipmentKind CurrentKind { get; private set; }
    public bool IsMelee => CurrentKind == PlayerEquipmentKind.BareHands || CurrentKind == PlayerEquipmentKind.Gloves || CurrentKind == PlayerEquipmentKind.Dagger;
    public event System.Action<DemoAction, UnityEngine.Object> Succeeded;
    // 모델 없는 글러브를 시험용으로만 허용하는 기존 옵션.
    [SerializeField] private bool allowUnmodelledMeleeForTests;
    private PlayerController playerController;
    private EquipmentSlot current;
    private UnityEngine.Object storyDisplayOwner;
    private EquipmentSlot storySlot;
    private bool initialized;
    public WeaponAssaultRifle CurrentWeapon => !IsMelee && current != null ? current.weapon : null;
    public event System.Action<WeaponAssaultRifle> OnWeaponChanged;

    /// <summary>
    /// 장비 참조를 준비하고 중복되지 않게 초기 장착 상태를 적용한다.
    /// </summary>
    public void Initialize()
    {
        if (initialized) return;
        initialized = true;
        playerController = GetComponent<PlayerController>();
        if (cameraController == null) cameraController = GetComponent<PlayerCameraController>();
        SetEquipped(assaultRifle, false);
        SetEquipped(pistol, false);
        ResetEquipment();
    }

    /// <summary>
    /// 컴포넌트의 초기 실행 시점에 설정된 시작 동작을 요청한다.
    /// </summary>
    private void Start() => Initialize();

    /// <summary>
    /// 총기 종류를 기존 장비 식별 값으로 변환하여 장착을 요청한다.
    /// </summary>
    public void Equip(WeaponName name) => TryEquip(name == WeaponName.Pistol ? PlayerEquipmentKind.Pistol : PlayerEquipmentKind.AssaultRifle);
    /// <summary>
    /// 맨손 장비로 전환을 요청한다.
    /// </summary>
    public void EquipBareHands() => TryEquip(PlayerEquipmentKind.BareHands);
    /// <summary>
    /// 글러브 장비로 전환을 요청한다.
    /// </summary>
    public void EquipGloves() => TryEquip(PlayerEquipmentKind.Gloves);
    /// <summary>
    /// 단검 장비로 전환을 요청한다.
    /// </summary>
    public void EquipDagger() => TryEquip(PlayerEquipmentKind.Dagger);
    /// <summary>
    /// 제어·소유·실제 슬롯 준비 여부를 검사하고 이전 행동을 취소한 뒤 장비를 선택한다.
    /// </summary>
    public bool TryEquip(PlayerEquipmentKind kind)
    {
        if (!isActiveAndEnabled || playerController == null || !playerController.CanChangeEquipment || kind == CurrentKind) return false;
        if (!HasOwnership(kind)) return false;
        EquipmentSlot slot = kind == PlayerEquipmentKind.AssaultRifle ? assaultRifle : kind == PlayerEquipmentKind.Pistol ? pistol :
            kind == PlayerEquipmentKind.Gloves ? gloves : kind == PlayerEquipmentKind.Dagger ? dagger : null;
        if (!IsSlotReady(kind, slot)) return false;
        if (melee != null) melee.CancelActions();
        CurrentKind = kind;
        Select(slot);
        Succeeded?.Invoke(DemoAction.Equip, this);
        return true;
    }
    /// <summary>
    /// 초기 장비를 적용하며 준비되지 않은 슬롯은 기존 맨손 대체 정책을 따른다.
    /// </summary>
    public void ResetEquipment()
    {
        if (melee != null) melee.CancelActions();
        CurrentKind = initialKind;
        if (initialKind == PlayerEquipmentKind.AssaultRifle && initialWeapon == WeaponName.Pistol) CurrentKind = PlayerEquipmentKind.Pistol;
        EquipmentSlot slot = CurrentKind == PlayerEquipmentKind.AssaultRifle ? assaultRifle : CurrentKind == PlayerEquipmentKind.Pistol ? pistol :
            CurrentKind == PlayerEquipmentKind.Gloves ? gloves : CurrentKind == PlayerEquipmentKind.Dagger ? dagger : null;
        if (!HasOwnership(CurrentKind) || !IsSlotReady(CurrentKind, slot))
        {
            CurrentKind = PlayerEquipmentKind.BareHands;
            slot = null;
        }
        Select(slot);
    }

    /// <summary>
    /// 장비 종류에 연결된 필수 소유 아이템 데이터를 반환한다.
    /// </summary>
    public ItemData GetRequiredItem(PlayerEquipmentKind kind)
    {
        switch (kind)
        {
            case PlayerEquipmentKind.AssaultRifle: return requiredRifleItem;
            case PlayerEquipmentKind.Pistol: return requiredPistolItem;
            case PlayerEquipmentKind.Gloves: return requiredGlovesItem;
            case PlayerEquipmentKind.Dagger: return requiredDaggerItem;
            default: return null;
        }
    }

    /// <summary>
    /// 소총과 권총은 필수 소유 데이터가 연결되어야 한다. 근접 장비의 기존 조건은 유지한다.
    /// </summary>
    public bool HasOwnership(PlayerEquipmentKind kind)
    {
        ItemData item = GetRequiredItem(kind);
        if (kind == PlayerEquipmentKind.AssaultRifle || kind == PlayerEquipmentKind.Pistol)
        {
            return item != null && item.IsFixedWeapon && item.EquipmentKind == kind &&
                inventory != null && inventory.HasItem(item);
        }
        return item == null || (inventory != null && inventory.HasItem(item));
    }

    private void OwnershipChanged()
    {
        if (initialized && !HasOwnership(CurrentKind))
        {
            CurrentKind = PlayerEquipmentKind.BareHands;
            Select(null);
        }
    }

    public bool IsAssigned(PlayerEquipmentKind kind)
    {
        EquipmentSlot slot = kind == PlayerEquipmentKind.AssaultRifle ? assaultRifle : kind == PlayerEquipmentKind.Pistol ? pistol :
            kind == PlayerEquipmentKind.Dagger ? dagger : kind == PlayerEquipmentKind.Gloves ? gloves : null;
        return IsSlotReady(kind, slot);
    }

    /// <summary>
    /// 무기 종류와 모델·컴포넌트의 연결 및 시험용 허용 설정을 검사한다.
    /// </summary>
    private bool IsSlotReady(PlayerEquipmentKind kind, EquipmentSlot slot)
    {
        if (kind == PlayerEquipmentKind.BareHands) return true;
        if (kind == PlayerEquipmentKind.AssaultRifle || kind == PlayerEquipmentKind.Pistol)
        {
            WeaponName expected = kind == PlayerEquipmentKind.Pistol ? WeaponName.Pistol : WeaponName.AssaultRifle;
            if (slot == null || slot.weapon == null || !slot.weapon.isActiveAndEnabled || slot.weapon.WeaponName != expected) return false;
            // A second slot must not borrow the rifle's ammo and firing state.
            return kind != PlayerEquipmentKind.Pistol || assaultRifle == null || slot.weapon != assaultRifle.weapon;
        }
        bool hasDisplay = slot != null && (slot.thirdPersonDisplay != null ||
            (slot.thirdPersonWeaponRenderers != null && System.Array.Exists(slot.thirdPersonWeaponRenderers, renderer => renderer != null)));
        return hasDisplay || (kind == PlayerEquipmentKind.Gloves && allowUnmodelledMeleeForTests);
    }

    /// <summary>
    /// 이전 장비의 동작과 장착을 정리하고 선택 알림 및 새 장비 표시를 적용한다.
    /// </summary>
    private void Select(EquipmentSlot slot)
    {
        if (melee != null) melee.CancelActions();
        SetEquipped(current, false);
        current = slot;
        // Update subscribers before enabling the new weapon and its presentation.
        OnWeaponChanged?.Invoke(CurrentWeapon);
        SetEquipped(current, isActiveAndEnabled);
        UpdateDisplays();
    }

    /// <summary>
    /// 무기의 장착 상태를 바꾸고 남은 발사·재장전과 표시를 정리한다.
    /// </summary>
    private static void SetEquipped(EquipmentSlot slot, bool equipped)
    {
        if (slot == null || slot.weapon == null) return;
        slot.weapon.SetEquipped(equipped);
    }

    /// <summary>
    /// 일반 Update 이후 최종 상태에 맞추어 표시나 자세를 갱신한다.
    /// </summary>
    private void LateUpdate() => UpdateDisplays();

    /// <summary>
    /// 컷신에서 표시할 장비의 1인칭 모델 참조가 준비됐는지 확인한다.
    /// </summary>
    public bool CanShowStoryEquipment(PlayerEquipmentKind kind)
    {
        EquipmentSlot slot = kind == PlayerEquipmentKind.AssaultRifle ? assaultRifle : kind == PlayerEquipmentKind.Pistol ? pistol :
            kind == PlayerEquipmentKind.Gloves ? gloves : kind == PlayerEquipmentKind.Dagger ? dagger : null;
        return HasOwnership(kind) && (kind == PlayerEquipmentKind.BareHands || (slot != null && slot.firstPersonDisplay != null));
    }
    /// <summary>
    /// 다른 표시 소유자가 없을 때 해당 요청의 컷신 장비 표시를 예약한다.
    /// </summary>
    internal bool BeginStoryDisplay(UnityEngine.Object owner, PlayerEquipmentKind kind)
    {
        if (owner == null || storyDisplayOwner != null || !CanShowStoryEquipment(kind)) return false;
        storyDisplayOwner = owner;
        storySlot = kind == PlayerEquipmentKind.AssaultRifle ? assaultRifle : kind == PlayerEquipmentKind.Pistol ? pistol :
            kind == PlayerEquipmentKind.Gloves ? gloves : kind == PlayerEquipmentKind.Dagger ? dagger : null;
        RefreshDisplays();
        return true;
    }
    /// <summary>
    /// 해당 소유자의 컷신 표시 예약만 해제하고 일반 장비 표시를 복구한다.
    /// </summary>
    internal void EndStoryDisplay(UnityEngine.Object owner)
    {
        if (owner != storyDisplayOwner) return;
        storyDisplayOwner = null;
        storySlot = null;
        RefreshDisplays();
    }
    /// <summary>
    /// 현재 장비와 시점에 맞추어 장비 표시 갱신을 요청한다.
    /// </summary>
    public void RefreshDisplays() => UpdateDisplays();
    /// <summary>
    /// 일반 장착 상태와 컷신 표시 소유권에 따라 장비 오브젝트와 Renderer를 갱신한다.
    /// </summary>
    private void UpdateDisplays()
    {
        SetDisplay(assaultRifle, assaultRifle == current);
        SetDisplay(pistol, pistol == current);
        SetDisplay(gloves, gloves == current);
        SetDisplay(dagger, dagger == current);
    }
    /// <summary>
    /// 장비 표시 오브젝트의 활성 상태를 설정한다.
    /// </summary>
    private void SetDisplay(EquipmentSlot slot, bool equipped)
    {
        if (slot == null) return;
        bool story = storyDisplayOwner != null;
        if (slot.firstPersonDisplay != null)
            SetRenderers(slot.firstPersonDisplay.GetComponentsInChildren<Renderer>(true), story && slot == storySlot);
        bool visible = equipped && isActiveAndEnabled && !story;
        if (slot.thirdPersonWeaponRenderers != null && slot.thirdPersonWeaponRenderers.Length > 0)
            SetRenderers(slot.thirdPersonWeaponRenderers, visible);
        else if (slot.thirdPersonDisplay != null && slot.thirdPersonDisplay.GetComponent<Animator>() == null &&
            slot.thirdPersonDisplay.GetComponent<WeaponAssaultRifle>() == null && slot.thirdPersonDisplay != gameObject)
            SetRenderers(slot.thirdPersonDisplay.GetComponentsInChildren<Renderer>(true), visible);
        // Character roots with gameplay components stay active; their weapon Renderers must be connected explicitly.
    }
    /// <summary>
    /// 연결된 장비 Renderer의 표시 상태를 일괄 적용한다.
    /// </summary>
    private static void SetRenderers(Renderer[] renderers, bool visible)
    {
        foreach (Renderer renderer in renderers) if (renderer != null) renderer.enabled = visible;
    }

    /// <summary>
    /// 컴포넌트 활성화 시 필요한 알림과 초기 표시를 연결한다.
    /// </summary>
    private void OnEnable()
    {
        if (inventory == null) inventory = GetComponent<PlayerInventory>();
        if (inventory != null) inventory.Changed += OwnershipChanged;
        OwnershipChanged();
        if (initialized) SetEquipped(current, true);
    }

    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (inventory != null) inventory.Changed -= OwnershipChanged;
        if (storyDisplayOwner != null) EndStoryDisplay(storyDisplayOwner);
        if (melee != null) melee.CancelActions();
        SetEquipped(current, false);
        UpdateDisplays();
    }
}
