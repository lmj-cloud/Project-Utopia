using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

public class PlayerController : MonoBehaviour
{

    private PlayerStatus playerStatus;
    private PlayerMovement playerMovement;
    private bool canMove = true;

    void Start()
    {
        playerStatus = GetComponent<PlayerStatus>();
        playerMovement = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        UpdateMove();
        UpdateJump();
    }

    private void UpdateMove()
    {
        bool canControlPlayer = canMove && !playerStatus.IsDead;

        if (canControlPlayer)
        {
            playerMovement.CurrentSpeed = playerStatus.WalkSpeed;
            bool wantsToSprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

            Vector2 input = GetMoveInput();

            Vector3 localMove = new Vector3(input.x, 0f, input.y);
            localMove = Vector3.ClampMagnitude(localMove, 1f);

            if (wantsToSprint && localMove.sqrMagnitude > 0.01f && playerStatus.TryUseStamina(playerStatus.RunStaminaCostPerSecond * Time.deltaTime))
            {
                playerMovement.CurrentSpeed = playerStatus.RunSpeed;
            }
            playerMovement.MoveTo(localMove);
        }
    }

    private void UpdateJump()
    {
        if(Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            playerMovement.Jump();
        }
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

        return new Vector2(horizontal, vertical);
    }

    // Timeline Signal 또는 대화 시스템에서 호출한다.
    public void SetMovementEnabled(bool enabled)
    {
        canMove = enabled;
    }
}
