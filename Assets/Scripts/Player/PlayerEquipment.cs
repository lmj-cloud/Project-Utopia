using UnityEngine;

public enum PlayerEquipmentKind { AssaultRifle = 0, Pistol = 1, BareHands = 2, Gloves = 3, Dagger = 4 }

public class PlayerEquipment : MonoBehaviour
{
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
    [SerializeField] private PlayerEquipmentKind initialKind = PlayerEquipmentKind.AssaultRifle;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private ItemData requiredRifleItem;
    [SerializeField] private ItemData requiredPistolItem;
    [SerializeField] private ItemData requiredGlovesItem;
    [SerializeField] private ItemData requiredDaggerItem;
    [SerializeField] private PlayerMeleeCombat melee;
    public PlayerEquipmentKind CurrentKind { get; private set; }
    public bool IsMelee => CurrentKind == PlayerEquipmentKind.BareHands || CurrentKind == PlayerEquipmentKind.Gloves || CurrentKind == PlayerEquipmentKind.Dagger;
    public event System.Action<DemoAction, UnityEngine.Object> Succeeded;
    [SerializeField] private bool allowUnmodelledMeleeForTests;
    private PlayerController playerController;
    private EquipmentSlot current;
    private UnityEngine.Object storyDisplayOwner;
    private EquipmentSlot storySlot;
    private bool initialized;
    public WeaponAssaultRifle CurrentWeapon => !IsMelee && current != null ? current.weapon : null;
    public event System.Action<WeaponAssaultRifle> OnWeaponChanged;

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

    private void Start() => Initialize();

    public void Equip(WeaponName name) => TryEquip(name == WeaponName.Pistol ? PlayerEquipmentKind.Pistol : PlayerEquipmentKind.AssaultRifle);
    public void EquipBareHands() => TryEquip(PlayerEquipmentKind.BareHands);
    public void EquipGloves() => TryEquip(PlayerEquipmentKind.Gloves);
    public void EquipDagger() => TryEquip(PlayerEquipmentKind.Dagger);
    public bool TryEquip(PlayerEquipmentKind kind)
    {
        if (!isActiveAndEnabled || playerController == null || !playerController.CanChangeEquipment || kind == CurrentKind) return false;
        ItemData required = kind == PlayerEquipmentKind.AssaultRifle ? requiredRifleItem : kind == PlayerEquipmentKind.Pistol ? requiredPistolItem :
            kind == PlayerEquipmentKind.Gloves ? requiredGlovesItem : kind == PlayerEquipmentKind.Dagger ? requiredDaggerItem : null;
        if (required != null && (inventory == null || !inventory.HasItem(required))) return false;
        EquipmentSlot slot = kind == PlayerEquipmentKind.AssaultRifle ? assaultRifle : kind == PlayerEquipmentKind.Pistol ? pistol :
            kind == PlayerEquipmentKind.Gloves ? gloves : kind == PlayerEquipmentKind.Dagger ? dagger : null;
        if (!IsSlotReady(kind, slot)) return false;
        if (melee != null) melee.CancelActions();
        CurrentKind = kind;
        Select(slot);
        Succeeded?.Invoke(DemoAction.Equip, this);
        return true;
    }
    public void ResetEquipment()
    {
        if (melee != null) melee.CancelActions();
        CurrentKind = initialKind;
        if (initialKind == PlayerEquipmentKind.AssaultRifle && initialWeapon == WeaponName.Pistol) CurrentKind = PlayerEquipmentKind.Pistol;
        EquipmentSlot slot = CurrentKind == PlayerEquipmentKind.AssaultRifle ? assaultRifle : CurrentKind == PlayerEquipmentKind.Pistol ? pistol :
            CurrentKind == PlayerEquipmentKind.Gloves ? gloves : CurrentKind == PlayerEquipmentKind.Dagger ? dagger : null;
        if (!IsSlotReady(CurrentKind, slot))
        {
            CurrentKind = PlayerEquipmentKind.BareHands;
            slot = null;
        }
        Select(slot);
    }

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

    private static void SetEquipped(EquipmentSlot slot, bool equipped)
    {
        if (slot == null || slot.weapon == null) return;
        slot.weapon.SetEquipped(equipped);
    }

    private void LateUpdate() => UpdateDisplays();

    public bool CanShowStoryEquipment(PlayerEquipmentKind kind)
    {
        EquipmentSlot slot = kind == PlayerEquipmentKind.AssaultRifle ? assaultRifle : kind == PlayerEquipmentKind.Pistol ? pistol :
            kind == PlayerEquipmentKind.Gloves ? gloves : kind == PlayerEquipmentKind.Dagger ? dagger : null;
        return kind == PlayerEquipmentKind.BareHands || (slot != null && slot.firstPersonDisplay != null);
    }
    internal bool BeginStoryDisplay(UnityEngine.Object owner, PlayerEquipmentKind kind)
    {
        if (owner == null || storyDisplayOwner != null || !CanShowStoryEquipment(kind)) return false;
        storyDisplayOwner = owner;
        storySlot = kind == PlayerEquipmentKind.AssaultRifle ? assaultRifle : kind == PlayerEquipmentKind.Pistol ? pistol :
            kind == PlayerEquipmentKind.Gloves ? gloves : kind == PlayerEquipmentKind.Dagger ? dagger : null;
        RefreshDisplays();
        return true;
    }
    internal void EndStoryDisplay(UnityEngine.Object owner)
    {
        if (owner != storyDisplayOwner) return;
        storyDisplayOwner = null;
        storySlot = null;
        RefreshDisplays();
    }
    public void RefreshDisplays() => UpdateDisplays();
    private void UpdateDisplays()
    {
        SetDisplay(assaultRifle, assaultRifle == current);
        SetDisplay(pistol, pistol == current);
        SetDisplay(gloves, gloves == current);
        SetDisplay(dagger, dagger == current);
    }
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
    private static void SetRenderers(Renderer[] renderers, bool visible)
    {
        foreach (Renderer renderer in renderers) if (renderer != null) renderer.enabled = visible;
    }

    private void OnEnable()
    {
        if (initialized) SetEquipped(current, true);
    }

    private void OnDisable()
    {
        if (storyDisplayOwner != null) EndStoryDisplay(storyDisplayOwner);
        if (melee != null) melee.CancelActions();
        SetEquipped(current, false);
        UpdateDisplays();
    }
}
