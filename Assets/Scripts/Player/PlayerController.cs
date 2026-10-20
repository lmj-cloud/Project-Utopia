using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

public class PlayerController : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private PlayerCameraController cameraController;

    [Header("Weapon")]
    [SerializeField] private WeaponAssaultRifle weapon;
    [SerializeField] private PlayerEquipment equipment;
    public WeaponAssaultRifle CurrentWeapon => weapon;

    [Header("Interaction / UI")]
    [SerializeField] private PlayerInteraction playerInteraction;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private StoryProgress storyProgress;
    [SerializeField] private GameplayUI gameplayUI;

    [Header("Animation")]
    [SerializeField] private float animationChangedSecond = 1f;

    [Header("Demo Actions")]
    [SerializeField] private PlayerMeleeCombat melee;
    [SerializeField] private ItemUseController itemUse;
    [SerializeField, HideInInspector] private Key bareHandsKey = Key.None;
    [SerializeField, HideInInspector] private Key glovesKey = Key.None;
    [SerializeField] private Key daggerKey = Key.Digit3;
    [SerializeField, HideInInspector] private Key medicineKey = Key.None;
    [SerializeField] private Key itemUseKey = Key.Digit4;
    [SerializeField, HideInInspector] private Key dodgeKey = Key.None;
    [SerializeField] private Key blockKey = Key.Q;
    [SerializeField] private Key grabKey = Key.G;
    [SerializeField] private Key assassinationKey = Key.H;
    private static bool Pressed(Keyboard keyboard, Key key) => keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
    [SerializeField] private bool allowWeapons = true;
    [SerializeField] private bool allowMelee = true;
    [SerializeField] private bool allowEquipmentChanges = true;
    public bool CanUseWeapons => CanControlPlayer && allowWeapons;
    public bool CanChangeEquipment => CanControlPlayer && allowEquipmentChanges;
    public bool CanUseMelee => CanControlPlayer && allowMelee;
    public void SetWeaponActionsEnabled(bool value) { allowWeapons = value; if (!value && weapon != null) weapon.CancelWeaponActions(); }
    public void SetMeleeActionsEnabled(bool value) { allowMelee = value; if (!value && melee != null) melee.CancelActions(); }
    public void SetEquipmentChangesEnabled(bool value) => allowEquipmentChanges = value;
    public bool IsAlive => playerStatus != null && !playerStatus.IsDead;
    private PlayerStatus playerStatus;
    private PlayerMovement playerMovement;
    private PlayerAnimatorController playerAnimator;
    private bool canMove = true;
    private bool isAiming;
    private Vector2 animationInput;
    private float fireFacingUntil;
    private int lastFiredFrame = -1;
    private readonly HashSet<Object> controlLocks = new();

    private readonly HashSet<Object> movementSimulationLocks = new();
    public bool HasAnimationControlLock => !isActiveAndEnabled || !canMove || controlLocks.Count != 0;
    public bool CanControlAnimations => isActiveAndEnabled && canMove && controlLocks.Count == 0 &&
        playerStatus != null && !playerStatus.IsDead;

    public void AcquireMovementSimulationLock(Object owner)
    {
        if (owner == null) return;
        movementSimulationLocks.Add(owner);
        if (playerMovement != null) playerMovement.SetSimulationEnabled(false);
    }

    public void ReleaseMovementSimulationLock(Object owner)
    {
        if (owner == null || !movementSimulationLocks.Remove(owner)) return;
        if (movementSimulationLocks.Count == 0 && playerMovement != null)
            playerMovement.SetSimulationEnabled(true);
    }

    public bool IsAiming => isAiming;
    public bool CanApplyRifleAim => CanUseWeapons && equipment != null &&
        equipment.CurrentKind == PlayerEquipmentKind.AssaultRifle && weapon != null &&
        equipment.CurrentWeapon == weapon && weapon.isActiveAndEnabled && !weapon.IsReloading;
    public bool WantsRifleAimCorrection => CanApplyRifleAim && (isAiming || Time.time < fireFacingUntil);
    public bool RifleFiredThisFrame => CanApplyRifleAim && lastFiredFrame == Time.frameCount;
    public bool CanControlPlayer => isActiveAndEnabled && canMove && controlLocks.Count == 0 &&
        playerStatus != null && !playerStatus.IsDead &&
        !(cameraController != null && cameraController.IsStoryFirstPerson) &&
        !(dialogueUI != null && (dialogueUI.IsOpen || dialogueUI.WasClosedThisFrame)) &&
        !(inventoryUI != null && (inventoryUI.IsOpen || inventoryUI.WasClosedThisFrame));

    private void Awake()
    {
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        if (melee == null) melee = GetComponent<PlayerMeleeCombat>();
        if (itemUse == null) itemUse = GetComponent<ItemUseController>();
        playerStatus = GetComponent<PlayerStatus>();
        playerMovement = GetComponent<PlayerMovement>();
        playerAnimator = GetComponent<PlayerAnimatorController>();
        if (playerInteraction == null) playerInteraction = GetComponent<PlayerInteraction>();
        if (playerInventory == null) playerInventory = GetComponent<PlayerInventory>();
    }

    private void OnEnable()
    {
        if (equipment != null)
        {
            equipment.Initialize();
            equipment.OnWeaponChanged += SetCurrentWeapon;
            SetCurrentWeapon(equipment.CurrentWeapon);
        }
        else SetCurrentWeapon(weapon);
    }

    private void SetCurrentWeapon(WeaponAssaultRifle next)
    {
        if (weapon != null) weapon.OnFired -= OnWeaponFired;
        weapon = next;
        if (weapon != null) weapon.OnFired += OnWeaponFired;
        fireFacingUntil = 0f;
        lastFiredFrame = -1;
        if (playerAnimator != null && equipment != null) playerAnimator.OnEquipmentChanged(equipment.CurrentKind);
        else if (playerAnimator != null && weapon != null) playerAnimator.OnWeaponChanged(weapon.WeaponName);
    }

    private void OnWeaponFired(Vector3 targetPoint)
    {
        if (!CanControlPlayer) return;
        fireFacingUntil = Time.time + Mathf.Max(0.25f, weapon.AttackInterval + 0.05f);
        lastFiredFrame = Time.frameCount;
        playerMovement.FaceImmediately(cameraController.Forward);
    }

    private void Update()
    {
        if (weapon != null && weapon.IsReloading)
        {
            fireFacingUntil = 0f;
            lastFiredFrame = -1;
        }
        if (playerStatus == null || playerStatus.IsDead)
        {
            if (inventoryUI != null) inventoryUI.Close();
            if (dialogueUI != null) dialogueUI.CancelDialogue();
            BlockControl();
            return;
        }

        if (gameplayUI != null && gameplayUI.IsDemoEndVisible) { BlockControl(); return; }
        Keyboard keyboard = Keyboard.current;

        // Timeline에서 띄운 대사도 진행할 수 있도록, 대사 입력을 먼저 처리한다.
        if (dialogueUI != null && dialogueUI.IsOpen)
        {
            BlockControl();
            dialogueUI.SetAdvanceInput(
                keyboard != null && keyboard.enterKey.wasPressedThisFrame,
                keyboard != null && keyboard.enterKey.isPressed,
                keyboard != null && keyboard.enterKey.wasReleasedThisFrame);
            return;
        }

        if (!canMove || controlLocks.Count > 0 || (cameraController != null && cameraController.IsStoryFirstPerson) ||
            (dialogueUI != null && dialogueUI.WasClosedThisFrame) ||
            (inventoryUI != null && inventoryUI.WasClosedThisFrame))
        {
            BlockControl();
            return;
        }

        if (inventoryUI != null && inventoryUI.IsOpen)
        {
            if (Mouse.current != null) inventoryUI.ScrollStorage(Mouse.current.scroll.ReadValue().y);
            BlockControl();
            if (keyboard != null && (keyboard.iKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                inventoryUI.Close();
            return;
        }

        if (keyboard != null && keyboard.iKey.wasPressedThisFrame && inventoryUI != null)
        {
            inventoryUI.Open();
            BlockControl();
            return;
        }

        if (playerInteraction != null) playerInteraction.SetInteractionEnabled(true);
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame && playerInteraction != null)
        {
            playerInteraction.TryInteract(playerInventory, dialogueUI, storyProgress);
            // 같은 프레임에 시작한 대사/컷씬이 이동·발사보다 우선한다.
            if (!CanControlPlayer)
            {
                BlockControl();
                return;
            }
        }

        if (equipment != null && keyboard != null && CanChangeEquipment)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) equipment.Equip(WeaponName.AssaultRifle);
            else if (keyboard.digit2Key.wasPressedThisFrame) equipment.Equip(WeaponName.Pistol);
            else if (Pressed(keyboard, daggerKey)) equipment.EquipDagger();
        }
        UpdateCameraInput();
        Vector2 input = GetMoveInput();
        playerMovement.SetCrouching(keyboard != null && keyboard.leftCtrlKey.isPressed);
        UpdateSpeed(input);
        UpdateMove(input);
        UpdateJump();
        if (playerInventory != null && Mouse.current != null && (itemUse == null || !itemUse.IsUsing) &&
            (gameplayUI == null || !gameplayUI.IsDemoEndVisible))
        {
            float wheel = Mouse.current.scroll.ReadValue().y;
            if (wheel != 0f) playerInventory.SelectEquipped(wheel > 0f ? -1 : 1);
        }
        if (Pressed(keyboard, itemUseKey) && itemUse != null && (gameplayUI == null || !gameplayUI.IsDemoEndVisible))
            itemUse.UseSelectedItem();
        if (!CanControlPlayer) { BlockControl(); return; }
        if (equipment != null && equipment.IsMelee && melee != null && CanUseMelee)
        {
            isAiming = false;
            bool mma = equipment.CurrentKind == PlayerEquipmentKind.Gloves;
            melee.SetBlocking(mma && keyboard != null && blockKey != Key.None && keyboard[blockKey].isPressed);
            if (Mouse.current != null)
            {
                if (Mouse.current.rightButton.wasPressedThisFrame) melee.TryStrongAttack();
                else if (Mouse.current.leftButton.wasPressedThisFrame) melee.TryAttack();
            }
            if (mma && Pressed(keyboard, grabKey)) melee.TryGrab(false);
            if (keyboard != null && Pressed(keyboard, assassinationKey)) melee.TryGrab(true);
        }
        else
        {
            if (melee != null) melee.SetBlocking(false);
            UpdateWeaponAction();
        }
    }

    private void UpdateCameraInput()
    {
        isAiming = (equipment == null || !equipment.IsMelee) && weapon != null && CanUseWeapons &&
            Mouse.current != null && Mouse.current.rightButton.isPressed;
        if (Mouse.current != null) cameraController.Look(Mouse.current.delta.ReadValue());
    }

    private void UpdateSpeed(Vector2 input)
    {
        playerMovement.TargetSpeed = playerStatus.WalkSpeed;
        bool wantsToSprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        if (wantsToSprint && !playerMovement.IsCrouching && input.sqrMagnitude > 0.01f &&
            playerStatus.TryUseStamina(playerStatus.RunStaminaCostPerSecond * Time.deltaTime))
            playerMovement.TargetSpeed = playerStatus.RunSpeed;
    }

    private void UpdateMove(Vector2 input)
    {
        Vector3 moveDirection = cameraController.GetMoveDirection(input);
        playerMovement.MoveTo(moveDirection);
        if (Time.time < fireFacingUntil)
            playerMovement.SetFacingDirection(cameraController.Forward, true);
        else if (cameraController.IsThirdPerson && !isAiming)
            playerMovement.SetFacingDirection(moveDirection, false);
        else
            playerMovement.SetFacingDirection(cameraController.Forward, true);
    }

    private void UpdateJump()
    {
        if(Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            playerMovement.Jump();
    }

    private Vector2 GetMoveInput()
    {
        float horizontal = 0f;
        float vertical = 0f;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed) vertical += 1f;
        }
        float maxDelta = animationChangedSecond * Time.deltaTime;
        animationInput.x = Mathf.MoveTowards(animationInput.x, horizontal, maxDelta);
        animationInput.y = Mathf.MoveTowards(animationInput.y, vertical, maxDelta);
        if (playerAnimator != null)
        {
            playerAnimator.MoveHorizontal = animationInput.x;
            playerAnimator.MoveVertical = animationInput.y;
        }
        return new Vector2(horizontal, vertical);
    }

    private void UpdateWeaponAction()
    {
        if (weapon == null) return;
        if (!CanUseWeapons) { weapon.CancelWeaponActions(); return; }
        if (Mouse.current != null)
        {
            if (Mouse.current.leftButton.wasPressedThisFrame) weapon.StartWeaponAction();
            else if (Mouse.current.leftButton.wasReleasedThisFrame) weapon.StopWeaponAction();
        }
        else weapon.StopWeaponAction();
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
            weapon.StartReload();
    }

    private void BlockControl()
    {
        StopPlayerActions();
        if (playerInteraction != null) playerInteraction.SetInteractionEnabled(false);
    }

    private void StopPlayerActions()
    {
        isAiming = false;
        fireFacingUntil = 0f;
        lastFiredFrame = -1;
        animationInput = Vector2.zero;
        if (playerMovement != null) playerMovement.Stop();
        if (weapon != null) weapon.CancelWeaponActions();
        if (melee != null) melee.CancelActions();
        // 컷씬이 애니메이터를 제어 중이면 파라미터를 계속 덮어쓰지 않는다.
        if (playerAnimator != null && canMove && controlLocks.Count == 0)
        {
            playerAnimator.MoveHorizontal = 0f;
            playerAnimator.MoveVertical = 0f;
            playerAnimator.CurrentSpeed = 0f;
            playerAnimator.OnAiming(false);
        }
    }

    private void LateUpdate()
    {
        if (playerMovement != null)
        {
            if (cameraController != null) cameraController.SetCrouching(playerMovement.IsCrouching);
            if (playerAnimator != null) playerAnimator.OnCrouching(playerMovement.IsCrouching);
        }
        bool inventoryOpen = (inventoryUI != null && inventoryUI.IsOpen) || (gameplayUI != null && gameplayUI.IsDemoEndVisible);
        Cursor.visible = inventoryOpen;
        Cursor.lockState = inventoryOpen ? CursorLockMode.None : CursorLockMode.Locked;
        if (playerAnimator != null && playerMovement != null && canMove && controlLocks.Count == 0)
        {
            playerAnimator.CurrentSpeed = CanControlPlayer ? playerMovement.HorizontalVelocity.magnitude : 0f;
            playerAnimator.OnAiming(IsAiming);
        }
    }

    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;
        if (!enabled) StopPlayerActions();
        if (!enabled && playerInteraction != null) playerInteraction.SetInteractionEnabled(false);
    }

    public void AcquireControlLock(Object owner)
    {
        if (owner == null) return;
        controlLocks.Add(owner);
        if (itemUse != null && itemUse.IsUsing && owner != itemUse) itemUse.CancelUse();
        StopPlayerActions();
        if (inventoryUI != null) inventoryUI.Close();
        if (playerInteraction != null) playerInteraction.SetInteractionEnabled(false);
    }

    public void ReleaseControlLock(Object owner)
    {
        if (owner != null) controlLocks.Remove(owner);
    }

    private void OnDisable()
    {
        if (equipment != null) equipment.OnWeaponChanged -= SetCurrentWeapon;
        if (weapon != null) weapon.OnFired -= OnWeaponFired;
        BlockControl();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
