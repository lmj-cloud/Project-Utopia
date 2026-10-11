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

    [Header("Interaction / UI")]
    [SerializeField] private PlayerInteraction playerInteraction;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private StoryProgress storyProgress;

    [Header("Animation")]
    [SerializeField] private float animationChangedSecond = 1f;

    private PlayerStatus playerStatus;
    private PlayerMovement playerMovement;
    private PlayerAnimatorController playerAnimator;
    private bool canMove = true;
    private bool isAiming;
    private Vector2 animationInput;
    private float fireFacingUntil;
    private readonly HashSet<Object> controlLocks = new();

    public bool IsAiming => isAiming;
    public bool CanControlPlayer => isActiveAndEnabled && canMove && controlLocks.Count == 0 &&
        playerStatus != null && !playerStatus.IsDead &&
        !(dialogueUI != null && (dialogueUI.IsOpen || dialogueUI.WasClosedThisFrame)) &&
        !(inventoryUI != null && (inventoryUI.IsOpen || inventoryUI.WasClosedThisFrame));

    private void Awake()
    {
        playerStatus = GetComponent<PlayerStatus>();
        playerMovement = GetComponent<PlayerMovement>();
        playerAnimator = GetComponent<PlayerAnimatorController>();
        if (playerInteraction == null) playerInteraction = GetComponent<PlayerInteraction>();
        if (playerInventory == null) playerInventory = GetComponent<PlayerInventory>();
    }

    private void OnEnable()
    {
        if (weapon != null) weapon.OnFired += OnWeaponFired;
    }

    private void OnWeaponFired(Vector3 targetPoint)
    {
        if (!CanControlPlayer) return;
        fireFacingUntil = Time.time + Mathf.Max(0.25f, weapon.AttackInterval + 0.05f);
        playerMovement.FaceImmediately(cameraController.Forward);
    }

    private void Update()
    {
        if (playerStatus == null || playerStatus.IsDead)
        {
            if (inventoryUI != null) inventoryUI.Close();
            if (dialogueUI != null) dialogueUI.CancelDialogue();
            BlockControl();
            return;
        }

        Keyboard keyboard = Keyboard.current;

        // Timeline에서 띄운 대사도 진행할 수 있도록, 대사 입력을 먼저 처리한다.
        if (dialogueUI != null && dialogueUI.IsOpen)
        {
            BlockControl();
            bool advance = keyboard != null && (keyboard.fKey.wasPressedThisFrame ||
                keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
            if (advance) dialogueUI.AdvanceDialogue();
            return;
        }

        if (!canMove || controlLocks.Count > 0 ||
            (dialogueUI != null && dialogueUI.WasClosedThisFrame) ||
            (inventoryUI != null && inventoryUI.WasClosedThisFrame))
        {
            BlockControl();
            return;
        }

        if (inventoryUI != null && inventoryUI.IsOpen)
        {
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

        UpdateCameraInput();
        Vector2 input = GetMoveInput();
        UpdateSpeed(input);
        UpdateMove(input);
        UpdateJump();
        UpdateWeaponAction();
    }

    private void UpdateCameraInput()
    {
        isAiming = Mouse.current != null && Mouse.current.rightButton.isPressed;
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
            cameraController.ToggleView();
        if (Mouse.current != null) cameraController.Look(Mouse.current.delta.ReadValue());
    }

    private void UpdateSpeed(Vector2 input)
    {
        playerMovement.TargetSpeed = playerStatus.WalkSpeed;
        bool wantsToSprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        if (wantsToSprint && input.sqrMagnitude > 0.01f &&
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
        animationInput = Vector2.zero;
        if (playerMovement != null) playerMovement.Stop();
        if (weapon != null) weapon.StopWeaponAction();
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
        bool inventoryOpen = inventoryUI != null && inventoryUI.IsOpen;
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
        if (!enabled) StopPlayerActions();
        canMove = enabled;
        if (!enabled && playerInteraction != null) playerInteraction.SetInteractionEnabled(false);
    }

    public void AcquireControlLock(Object owner)
    {
        if (owner == null) return;
        StopPlayerActions();
        controlLocks.Add(owner);
        if (inventoryUI != null) inventoryUI.Close();
        if (playerInteraction != null) playerInteraction.SetInteractionEnabled(false);
    }

    public void ReleaseControlLock(Object owner)
    {
        if (owner != null) controlLocks.Remove(owner);
    }

    private void OnDisable()
    {
        if (weapon != null) weapon.OnFired -= OnWeaponFired;
        BlockControl();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
