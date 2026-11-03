using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

/// <summary>
/// 플레이어 입력을 받아 이동, 공격, 장비 변경 및 상호작용을 각 담당 컴포넌트에 요청한다.
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Camera")]
    [SerializeField] private PlayerCameraController cameraController;

    [Header("Weapon")]
    [SerializeField] private WeaponAssaultRifle weapon;
    [SerializeField] private PlayerEquipment equipment;
    public WeaponAssaultRifle CurrentWeapon => weapon;
    [Header("Interaction Input")]
    [SerializeField] private Key interactionKey = Key.F;
    public Key InteractionKey => interactionKey;

    [Header("Interaction / UI")]
    [SerializeField] private PlayerInteraction playerInteraction;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private StoryProgress storyProgress;
    [SerializeField] private GameplayUI gameplayUI;

    [Header("Quick Slot Wheel")]
    [SerializeField] private QuickSlotWheelUI quickSlotWheel;
    [SerializeField] private Key quickSlotKey = Key.E;
    private bool suppressQuickSlotMouse;

    [Header("Temporary Tutorial Display Input")]
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private bool enableTutorialDisplayKey = true;
    [SerializeField] private Key tutorialDisplayKey = Key.T;

    [Header("Animation")]
    [SerializeField] private float animationChangedSecond = 1f;

    [Header("Demo Actions")]
    [SerializeField] private PlayerMeleeCombat melee;
    [SerializeField] private ItemUseController itemUse;
    // 맨손 선택 키. None이면 이 키 경로를 사용하지 않는다.
    [SerializeField] private Key bareHandsKey = Key.None;
    // 글러브 장비 선택 키. None이면 이 키 경로를 사용하지 않는다.
    [SerializeField] private Key glovesKey = Key.None;
    [SerializeField] private Key daggerKey = Key.Digit3;
    // 기존 직렬화 값의 호환성을 위해 남아 있는 이전 입력 필드.
    [SerializeField, HideInInspector] private Key medicineKey = Key.None;
    [SerializeField] private Key itemUseKey = Key.Digit4;
    // 회피 입력 키. None이면 별도 키 입력을 사용하지 않는다.
    [SerializeField] private Key dodgeKey = Key.None;
    // 글러브 장비의 방어 입력 키.
    [SerializeField] private Key blockKey = Key.Q;
    // 기존 잡기 행동을 요청하는 키.
    [SerializeField] private Key grabKey = Key.G;
    // 암살 조건 검사를 요청하는 키.
    [SerializeField] private Key assassinationKey = Key.H;
    // 일반 마무리 입력 키. None이면 별도 키 입력을 사용하지 않는다.
    [SerializeField] private Key finisherKey = Key.None;
    private PlayerCombatActions combatActions;
    /// <summary>
    /// 유효한 키보드와 지정 키의 이번 프레임 눌림을 확인한다.
    /// </summary>
    private static bool Pressed(Keyboard keyboard, Key key) => keyboard != null && key != Key.None && keyboard[key].wasPressedThisFrame;
    [SerializeField] private bool allowWeapons = true;
    [SerializeField] private bool allowMelee = true;
    [SerializeField] private bool allowEquipmentChanges = true;
    public bool CanUseWeapons => CanControlPlayer && allowWeapons;
    public bool CanChangeEquipment => CanControlPlayer && allowEquipmentChanges && mmaCombatOwner == null &&
        (playerAnimator == null || !playerAnimator.IsHitReacting);
    [Header("MMA 행동 권한 (다른 장비에는 적용하지 않음)")]
    [SerializeField] private bool allowMmaOutsideCombat;
    private Object mmaCombatOwner;
    private PlayerEquipmentKind equipmentBeforeMma;
    public bool IsMmaCombatActive { get { return mmaCombatOwner != null; } }
    public bool CanUseMma { get { return CanControlPlayer && allowMelee && (mmaCombatOwner != null || allowMmaOutsideCombat); } }
    public bool CanUseMelee => CanControlPlayer && allowMelee &&
        (playerAnimator == null || !playerAnimator.IsHitReacting) &&
        (equipment == null || (equipment.CurrentKind != PlayerEquipmentKind.Gloves && equipment.CurrentKind != PlayerEquipmentKind.BareHands) || mmaCombatOwner != null || allowMmaOutsideCombat);

    /// <summary>안전지대의 MMA 권한만 바꾼다. 소총·권총·단검의 권한은 유지한다.</summary>
    public void SetMmaActionsEnabled(bool value)
    {
        allowMmaOutsideCombat = value;
        if (!CanUseMma && equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Gloves && melee != null)
        {
            melee.CancelActions();
        }
    }

    /// <summary>현재 장비를 기억하고 기존 행동을 취소한 뒤 소유자 전용 MMA 전투를 시작한다.</summary>
    public bool TryBeginMmaCombat(Object owner)
    {
        if (owner == null || !CanControlPlayer || !allowMelee || equipment == null) return false;
        if (mmaCombatOwner != null) return mmaCombatOwner == owner;
        PlayerEquipmentKind previous = equipment.CurrentKind;
        if (weapon != null) weapon.CancelWeaponActions();
        if (melee != null) melee.CancelActions();
        if (!equipment.RestoreEquipment(PlayerEquipmentKind.Gloves)) return false;
        equipmentBeforeMma = previous;
        mmaCombatOwner = owner;
        if (combatActions != null) combatActions.ResetMmaCombo();
        isAiming = false;
        return true;
    }

    /// <summary>자기 전투만 종료하고 이전 장비를 복구한다. 다른 컷신의 입력 잠금은 해제하지 않는다.</summary>
    public void EndMmaCombat(Object owner)
    {
        if (owner == null || mmaCombatOwner != owner) return;
        if (melee != null) melee.CancelActions();
        mmaCombatOwner = null;
        if (combatActions != null) combatActions.ResetMmaCombo();
        if (equipment != null && !equipment.RestoreEquipment(equipmentBeforeMma))
        {
            equipment.RestoreEquipment(PlayerEquipmentKind.BareHands);
        }
    }
    /// <summary>
    /// 총기 행동 허용 여부를 변경하고 금지 시 진행 중 무기 동작을 취소한다.
    /// </summary>
    public void SetWeaponActionsEnabled(bool value) { allowWeapons = value; if (!value && weapon != null) weapon.CancelWeaponActions(); }
    /// <summary>
    /// 근접 행동 허용 여부를 변경하고 금지 시 진행 중 동작을 정리한다.
    /// </summary>
    public void SetMeleeActionsEnabled(bool value) { allowMelee = value; if (!value && melee != null) melee.CancelActions(); }
    /// <summary>
    /// 플레이어의 장비 변경 허용 여부를 설정한다.
    /// </summary>
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
    // 입력을 잠근 소유자 집합. 자기 소유자만 제거해야 다른 컷신 잠금이 유지된다.
    private readonly HashSet<Object> controlLocks = new();

    // 이동 시뮬레이션을 중지한 소유자 집합. 마지막 잠금 해제 시에만 이동을 복구한다.
    private readonly HashSet<Object> movementSimulationLocks = new();
    public bool HasAnimationControlLock => !isActiveAndEnabled || !canMove || controlLocks.Count != 0;
    public bool CanControlAnimations => isActiveAndEnabled && canMove && controlLocks.Count == 0 &&
        playerStatus != null && !playerStatus.IsDead;

    /// <summary>
    /// 소유자를 이동 잠금에 등록하고 CharacterController 이동 시뮬레이션을 중지한다.
    /// </summary>
    public void AcquireMovementSimulationLock(Object owner)
    {
        if (owner == null) return;
        movementSimulationLocks.Add(owner);
        if (playerMovement != null) playerMovement.SetSimulationEnabled(false);
    }

    /// <summary>
    /// 자기 이동 잠금만 제거하고 남은 소유자가 없을 때 이동 시뮬레이션을 복구한다.
    /// </summary>
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
    public bool CanApplyPistolAim => CanUseWeapons && equipment != null &&
        equipment.CurrentKind == PlayerEquipmentKind.Pistol && weapon != null &&
        equipment.CurrentWeapon == weapon && weapon.isActiveAndEnabled && !weapon.IsReloading;
    public bool WantsPistolAimCorrection => CanApplyPistolAim && isAiming;
    public bool PistolFiredThisFrame => CanApplyPistolAim && isAiming && lastFiredFrame == Time.frameCount;
    public bool WantsRifleAimCorrection => CanApplyRifleAim && isAiming;
    public bool RifleFiredThisFrame => CanApplyRifleAim && isAiming && lastFiredFrame == Time.frameCount;
    public bool CanControlPlayer => isActiveAndEnabled && canMove && controlLocks.Count == 0 &&
        playerStatus != null && !playerStatus.IsDead &&
        !(cameraController != null && cameraController.IsStoryFirstPerson) &&
        !(dialogueUI != null && (dialogueUI.IsOpen || dialogueUI.WasClosedThisFrame)) &&
        !(inventoryUI != null && (inventoryUI.IsOpen || inventoryUI.WasClosedThisFrame));

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        combatActions = GetComponent<PlayerCombatActions>();
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        if (melee == null) melee = GetComponent<PlayerMeleeCombat>();
        if (itemUse == null) itemUse = GetComponent<ItemUseController>();
        playerStatus = GetComponent<PlayerStatus>();
        playerMovement = GetComponent<PlayerMovement>();
        playerAnimator = GetComponent<PlayerAnimatorController>();
        if (playerInteraction == null) playerInteraction = GetComponent<PlayerInteraction>();
        if (playerInventory == null) playerInventory = GetComponent<PlayerInventory>();
    }

    /// <summary>
    /// 선택 무기 변경 이벤트를 구독하고 현재 장비의 발사와 Animator 연결을 준비한다.
    /// </summary>
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

    /// <summary>
    /// 이전 무기의 발사 구독을 해제하고 새 무기의 구독과 Animator 상태를 연결한다.
    /// </summary>
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

    /// <summary>
    /// 이번 발사에 맞추어 플레이어의 방향과 조준 보정 유지 시간을 갱신한다.
    /// </summary>
    private void OnWeaponFired(Vector3 targetPoint)
    {
        if (!CanControlPlayer) return;
        fireFacingUntil = Time.time + Mathf.Max(0.25f, weapon.AttackInterval + 0.05f);
        lastFiredFrame = Time.frameCount;
        // 비조준 발사는 기존 이동 방향을 유지하고, 우클릭 조준 중에만 카메라를 향한다.
        if (isAiming) playerMovement.FaceImmediately(cameraController.Forward);
    }

    /// <summary>
    /// 생존·UI·잠금을 우선 검사한 뒤 시선, 이동, 장비별 전투와 상호작용 입력을 분배한다.
    /// </summary>
    private void Update()
    {
        // 휠이 가진 잠금 때문에 일반 입력 검사에서 조기 반환되기 전에 해제·취소를 처리한다.
        if (HandleQuickSlotInput())
        {
            BlockControl();
            return;
        }
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
            BlockControl();
            if (keyboard != null && (keyboard.tabKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                inventoryUI.Close();
            return;
        }

        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame && inventoryUI != null)
        {
            inventoryUI.Open();
            BlockControl();
            return;
        }

        if (playerInteraction != null) playerInteraction.SetInteractionEnabled(true);
        if (keyboard != null && interactionKey != Key.None && keyboard[interactionKey].wasPressedThisFrame && playerInteraction != null)
        {
            playerInteraction.TryInteract(playerInventory, dialogueUI, storyProgress);
            // 같은 프레임에 시작한 대사/컷씬이 이동·발사보다 우선한다.
            if (!CanControlPlayer)
            {
                BlockControl();
                return;
            }
        }

        if (enableTutorialDisplayKey && CanControlPlayer && Pressed(keyboard, tutorialDisplayKey) && tutorial != null)
            tutorial.RequestShowCurrentTutorial();

        if (equipment != null && keyboard != null && CanChangeEquipment)
        {
            if (keyboard.digit1Key.wasPressedThisFrame) equipment.Equip(WeaponName.AssaultRifle);
            else if (keyboard.digit2Key.wasPressedThisFrame) equipment.Equip(WeaponName.Pistol);
            else if (Pressed(keyboard, daggerKey)) equipment.EquipDagger();
            else if (Pressed(keyboard, glovesKey)) equipment.EquipGloves();
            else if (Pressed(keyboard, bareHandsKey)) equipment.EquipBareHands();
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
                if (mma)
                {
                    // 누르고 있는 상태가 아니라 새 클릭만 전달한다. 예약과 손 순서는 전투 행동이 관리한다.
                    if (combatActions != null && combatActions.UsesDirectionalInput && Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        combatActions.TryMmaComboAttack();
                    }
                }
                else
                {
                    if (Mouse.current.rightButton.wasPressedThisFrame) melee.TryStrongAttack();
                    else if (Mouse.current.leftButton.wasPressedThisFrame) melee.TryAttack();
                }
            }
            if (mma && Pressed(keyboard, grabKey)) melee.TryGrab(false);
            if (Pressed(keyboard, assassinationKey))
            {
                if (mma && combatActions != null && combatActions.UsesDirectionalInput) combatActions.TryExecution(true);
                else melee.TryGrab(true);
            }
            if (Pressed(keyboard, finisherKey) && combatActions != null && combatActions.UsesDirectionalInput)
                combatActions.TryExecution(false);
            if (mma && Pressed(keyboard, dodgeKey)) melee.TryDodge(cameraController.GetMoveDirection(input));
        }
        else
        {
            if (melee != null) melee.SetBlocking(false);
            UpdateWeaponAction();
        }
    }

    /// <summary>
    /// 휠 소유 잠금 이외의 UI·컷신·차량·사망 상태가 생기면 선택을 취소한다.
    /// </summary>
    public bool CanKeepQuickSlotOpen(QuickSlotWheelUI owner)
    {
        return owner != null && isActiveAndEnabled && canMove && IsAlive && !HasOtherControlLock(owner) &&
            !(cameraController != null && cameraController.IsStoryFirstPerson) &&
            !(dialogueUI != null && (dialogueUI.IsOpen || dialogueUI.WasClosedThisFrame)) &&
            !(inventoryUI != null && (inventoryUI.IsOpen || inventoryUI.WasClosedThisFrame)) &&
            !(gameplayUI != null && gameplayUI.IsDemoEndVisible) &&
            !(itemUse != null && itemUse.IsUsing) && !(playerInventory != null && playerInventory.IsUseLocked);
    }

    /// <summary>
    /// 키 누름 한 번당 휠 세션 하나만 처리한다. 닫힌 프레임과 확인 클릭은 총기 입력으로 넘기지 않는다.
    /// </summary>
    private bool HandleQuickSlotInput()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (quickSlotWheel != null && quickSlotWheel.IsOpen)
        {
            if (!CanKeepQuickSlotOpen(quickSlotWheel) || keyboard == null || mouse == null ||
                quickSlotKey == Key.None || keyboard.escapeKey.wasPressedThisFrame)
            {
                quickSlotWheel.Cancel();
            }
            else if (keyboard.tabKey.wasPressedThisFrame)
            {
                quickSlotWheel.Cancel();
                if (inventoryUI != null) inventoryUI.Open();
            }
            else
            {
                quickSlotWheel.UpdatePointer(mouse.position.ReadValue());
                if (mouse.leftButton.wasPressedThisFrame || !keyboard[quickSlotKey].isPressed)
                    quickSlotWheel.ConfirmAndClose(itemUse);
            }
            suppressQuickSlotMouse = mouse != null && mouse.leftButton.isPressed;
            return true;
        }
        if (quickSlotWheel != null && quickSlotWheel.WasClosedThisFrame) return true;
        if (suppressQuickSlotMouse)
        {
            suppressQuickSlotMouse = mouse != null && mouse.leftButton.isPressed;
            if (suppressQuickSlotMouse) return true;
        }
        if (quickSlotWheel == null || !quickSlotWheel.isActiveAndEnabled || !CanControlPlayer ||
            !CanKeepQuickSlotOpen(quickSlotWheel) || !Pressed(keyboard, quickSlotKey) || mouse == null) return false;
        return quickSlotWheel.TryOpen(this, playerInventory);
    }

    /// <summary>
    /// 총기와 제어 조건에 맞는 조준 상태를 계산하고 마우스 시선을 전달한다.
    /// </summary>
    private void UpdateCameraInput()
    {
        isAiming = (equipment == null || !equipment.IsMelee) && weapon != null && CanUseWeapons &&
            Mouse.current != null && Mouse.current.rightButton.isPressed;
        if (Mouse.current != null) cameraController.Look(Mouse.current.delta.ReadValue());
    }

    /// <summary>
    /// 현재 이동 상태에 맞는 속도를 계산하여 적용한다.
    /// </summary>
    private void UpdateSpeed(Vector2 input)
    {
        playerMovement.TargetSpeed = playerStatus.WalkSpeed;
        if (isAiming)
        {
            // 조준은 기존 걷기 속도를 사용한다. Shift의 스태미나 검사도 실행하지 않는다.
            playerMovement.LimitHorizontalSpeed(playerStatus.WalkSpeed);
            return;
        }
        bool wantsToSprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        if (wantsToSprint && !playerMovement.IsCrouching && input.sqrMagnitude > 0.01f &&
            playerStatus.TryUseStamina(playerStatus.RunStaminaCostPerSecond * Time.deltaTime))
            playerMovement.TargetSpeed = playerStatus.RunSpeed;
    }

    /// <summary>
    /// 카메라 기준 입력 방향으로 이동을 요청하고 이동·조준 상태에 맞는 몸 방향을 전달한다.
    /// </summary>
    private void UpdateMove(Vector2 input)
    {
        Vector3 moveDirection = cameraController.GetMoveDirection(input);
        playerMovement.MoveTo(moveDirection);
        // 장비·재장전 여부와 무관하게 우클릭 조준 중일 때만 카메라 기준 방향을 사용한다.
        // 비조준은 맨손과 같은 이동 방향 회전을 사용하며, 정지 중에는 현재 방향을 유지한다.
        if (playerAnimator != null && playerAnimator.UsesMmaCombatLocomotion)
        {
            // 복싱은 뒤로 걷기와 옆 스텝을 위해 카메라 정면을 유지한다. 실제 회전은 이동 담당자가 적용한다.
            Vector3 forward = cameraController.Forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
            playerMovement.SetFacingDirection(forward, true);
            // 월드 이동을 이번 프레임의 몸 기준 방향으로 변환한다. 대각선 입력의 크기는 1로 제한한다.
            Vector3 localMove = Quaternion.Inverse(Quaternion.LookRotation(forward)) * Vector3.ClampMagnitude(moveDirection, 1f);
            float maxDelta = animationChangedSecond * Time.deltaTime;
            animationInput.x = Mathf.MoveTowards(animationInput.x, localMove.x, maxDelta);
            animationInput.y = Mathf.MoveTowards(animationInput.y, localMove.z, maxDelta);
            playerAnimator.MoveHorizontal = animationInput.x;
            playerAnimator.MoveVertical = animationInput.y;
        }
        else if (cameraController.IsThirdPerson && !isAiming)
            playerMovement.SetFacingDirection(moveDirection, false);
        else
            playerMovement.SetFacingDirection(cameraController.Forward, true);
    }

    /// <summary>
    /// 점프 키의 이번 프레임 입력을 이동 담당자에게 전달한다.
    /// </summary>
    private void UpdateJump()
    {
        if(Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            playerMovement.Jump();
    }

    /// <summary>
    /// 키보드의 방향 입력을 모으고 Animator용 이동 입력 값을 갱신한다.
    /// </summary>
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
        // 복싱 이동은 UpdateMove에서 몸의 기준 방향으로 변환한 뒤 한 번만 보간한다.
        if (playerAnimator != null && playerAnimator.UsesMmaCombatLocomotion)
            return new Vector2(horizontal, vertical);
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

    /// <summary>
    /// 마우스와 재장전 키 입력을 현재 총기의 발사·중단·재장전에 전달한다.
    /// </summary>
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
        {
            weapon.StartReload();
            // 재장전 시작만으로 Root를 카메라 방향으로 돌리지 않는다.
            // UpdateMove가 현재 조준 여부에 맞는 기존 회전 정책을 계속 담당한다.
        }
    }

    /// <summary>
    /// 제어할 수 없는 동안 남은 입력과 플레이어 행동을 중단한다.
    /// </summary>
    private void BlockControl()
    {
        StopPlayerActions();
        if (playerInteraction != null) playerInteraction.SetInteractionEnabled(false);
    }

    /// <summary>
    /// 이동 입력과 발사·근접 행동을 정리하여 잠금 중 동작이 이어지지 않게 한다.
    /// </summary>
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

    /// <summary>
    /// 일반 Update 이후 최종 상태에 맞추어 표시나 자세를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (playerMovement != null)
        {
            if (cameraController != null) cameraController.SetCrouching(playerMovement.IsCrouching);
            if (playerAnimator != null) playerAnimator.OnCrouching(playerMovement.IsCrouching);
        }
        bool inventoryOpen = (inventoryUI != null && inventoryUI.IsOpen) || (gameplayUI != null && gameplayUI.IsDemoEndVisible);
        bool wheelOpen = quickSlotWheel != null && quickSlotWheel.IsOpen;
        Cursor.visible = inventoryOpen || wheelOpen;
        Cursor.lockState = inventoryOpen || wheelOpen ? CursorLockMode.None : CursorLockMode.Locked;
        if (playerAnimator != null && playerMovement != null && canMove && controlLocks.Count == 0)
        {
            playerAnimator.CurrentSpeed = CanControlPlayer ? playerMovement.HorizontalVelocity.magnitude : 0f;
            playerAnimator.OnAiming(IsAiming);
        }
    }

    /// <summary>
    /// 이동 허용 상태를 변경하고 중단 시 남은 입력과 동작을 정리한다.
    /// </summary>
    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;
        if (!enabled && quickSlotWheel != null) quickSlotWheel.Cancel();
        if (!enabled) StopPlayerActions();
        if (!enabled && playerInteraction != null) playerInteraction.SetInteractionEnabled(false);
    }

    /// <summary>
    /// 요청 소유자를 입력 잠금에 등록하고 플레이어의 진행 중 일반 동작을 정리한다.
    /// </summary>
    public void AcquireControlLock(Object owner)
    {
        if (owner == null) return;
        controlLocks.Add(owner);
        if (quickSlotWheel != null && quickSlotWheel.IsOpen && owner != quickSlotWheel) quickSlotWheel.Cancel();
        if (itemUse != null && itemUse.IsUsing && owner != itemUse) itemUse.CancelUse();
        StopPlayerActions();
        if (inventoryUI != null) inventoryUI.Close();
        if (playerInteraction != null) playerInteraction.SetInteractionEnabled(false);
    }

    /// <summary>
    /// 지정 소유자의 입력 잠금만 해제하여 다른 시스템의 잠금은 유지한다.
    /// </summary>
    public void ReleaseControlLock(Object owner)
    {
        if (owner != null) controlLocks.Remove(owner);
    }

    /// <summary>
    /// 차량처럼 자기 입력 잠금을 보유한 동작이 다른 소유자의 잠금과 충돌하는지 확인한다.
    /// </summary>
    public bool HasOtherControlLock(Object owner)
    {
        foreach (Object lockOwner in controlLocks)
        {
            if (lockOwner != owner) return true;
        }
        return false;
    }

    /// <summary>
    /// 자기 전투 잠금 이외의 방해 요인과 생존·장비 제어 조건을 검사한다.
    /// </summary>
    public bool CanContinueOwnedCombat(Object owner)
    {
        if (owner == null || !isActiveAndEnabled || !canMove || !IsAlive || !allowMelee ||
            (cameraController != null && cameraController.IsStoryFirstPerson)) return false;
        foreach (Object lockOwner in controlLocks)
        {
            if (lockOwner != owner) return false;
        }
        return true;
    }

    /// <summary>
    /// 장비·발사 이벤트를 해제하고 남은 동작과 커서 상태를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (quickSlotWheel != null) quickSlotWheel.Cancel();
        if (equipment != null) equipment.OnWeaponChanged -= SetCurrentWeapon;
        if (weapon != null) weapon.OnFired -= OnWeaponFired;
        BlockControl();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
