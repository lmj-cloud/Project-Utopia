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

    [Header("Animation")]
    [SerializeField] private float animationChangedSecond = 1f;

    private PlayerStatus playerStatus;
    private PlayerMovement playerMovement;
    private PlayerAnimatorController playerAnimator;
    private bool canMove = true;
    private bool isAiming;

    private Vector2 animationInput;
    private float fireFacingUntil;

    // 총기 스크립트에서 조준 이동 상태를 확인할 때 사용할 수 있다.
    public bool IsAiming => isAiming;

    void Start()
    {
        playerStatus = GetComponent<PlayerStatus>();
        playerMovement = GetComponent<PlayerMovement>();
        playerAnimator = GetComponent<PlayerAnimatorController>();
    }

    private void OnEnable()
    {
        if (weapon != null)
            weapon.OnFired += OnWeaponFired;
    }

    private void OnWeaponFired(Vector3 targetPoint)
    {
        fireFacingUntil = Time.time + Mathf.Max(0.25f, weapon.AttackInterval + 0.05f);

        playerMovement.FaceImmediately(cameraController.Forward);
    }

    void Update()
    {
        UpdateCameraInput();

        bool canControlPlayer = canMove && !playerStatus.IsDead;
        if (!canControlPlayer)
        {
            isAiming = false;
            playerMovement.Stop();
            if (weapon != null)
                weapon.StopWeaponAction();
            return;
        }

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

        if (Mouse.current != null)
            cameraController.Look(Mouse.current.delta.ReadValue());
    }

    private void UpdateSpeed(Vector2 input)
    {
        playerMovement.TargetSpeed = playerStatus.WalkSpeed;

        bool wantsToSprint = Keyboard.current != null &&
            Keyboard.current.leftShiftKey.isPressed;

        if (wantsToSprint && input.sqrMagnitude > 0.01f &&
            playerStatus.TryUseStamina(playerStatus.RunStaminaCostPerSecond * Time.deltaTime))
        {
            playerMovement.TargetSpeed = playerStatus.RunSpeed;
        }
    }

    private void UpdateMove(Vector2 input)
    {
        Vector3 moveDirection = cameraController.GetMoveDirection(input);
        playerMovement.MoveTo(moveDirection);

        if (Time.time < fireFacingUntil)
        {
            playerMovement.SetFacingDirection(cameraController.Forward, true);
        }
        else if (cameraController.IsThirdPerson && !isAiming)
        {
            // 3인칭: 이동하는 방향을 바라본다. 멈추면 현재 방향을 유지한다.
            playerMovement.SetFacingDirection(moveDirection, false);
        }
        else
        {
            // 1인칭: 이동 여부와 관계없이 카메라 방향을 바라본다.
            playerMovement.SetFacingDirection(cameraController.Forward, true);
        }
    }

    private void UpdateJump()
    {
        if(Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            playerMovement.Jump();
    }

    private Vector2 GetMoveInput()
    {
        if (Keyboard.current == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (Keyboard.current.aKey.isPressed) horizontal -= 1f;
        if (Keyboard.current.dKey.isPressed) horizontal += 1f;
        if (Keyboard.current.sKey.isPressed) vertical -= 1f;
        if (Keyboard.current.wKey.isPressed) vertical += 1f;

        float maxDelta = animationChangedSecond * Time.deltaTime;

        animationInput.x = Mathf.MoveTowards(animationInput.x, horizontal, maxDelta);
        animationInput.y = Mathf.MoveTowards(animationInput.y, vertical, maxDelta);

        playerAnimator.MoveHorizontal = animationInput.x;
        playerAnimator.MoveVertical = animationInput.y;

        return new Vector2(horizontal, vertical);
    }

    private void UpdateWeaponAction()
    {
        if (weapon == null) return;

        if (Mouse.current != null)
        {
            // 좌클릭을 누르는 순간 발사 시작
            if (Mouse.current.leftButton.wasPressedThisFrame)
                weapon.StartWeaponAction();

            // 좌클릭을 떼는 순간 발사 중지
            else if (Mouse.current.leftButton.wasReleasedThisFrame)
                weapon.StopWeaponAction();
        }
        else
        {
            weapon.StopWeaponAction();
        }

        // R키를 누르는 순간 재장전
        if (Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            weapon.StartReload();
        }
    }

    private void LateUpdate()
    {
        // 모든 Update가 끝난 뒤, 이번 프레임의 실제 이동 속도를 전달한다.
        if (playerAnimator != null)
        {
            playerAnimator.CurrentSpeed = playerMovement.HorizontalVelocity.magnitude;
            playerAnimator.OnAiming(IsAiming);
        }
    }

    private void OnDisable()
    {
        isAiming = false;
        fireFacingUntil = 0f;

        if (weapon != null)
            weapon.OnFired -= OnWeaponFired;

        if (weapon != null)
            weapon.StopWeaponAction();

        if (playerMovement != null)
            playerMovement.Stop();

        if (playerAnimator != null)
            playerAnimator.CurrentSpeed = 0f;
    }

    // Timeline Signal 또는 대화 시스템에서 호출한다.
    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;
        if (!enabled)
        {
            fireFacingUntil = 0f;
            if (playerMovement != null)
                playerMovement.Stop();

            if (weapon != null)
                weapon.StopWeaponAction();
        }
    }
}
