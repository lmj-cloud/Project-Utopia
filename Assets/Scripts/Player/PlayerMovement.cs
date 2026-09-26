using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("이동")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 5.5f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -2f;

    [Header("점프")]
    [SerializeField] private float jumpHeight = 1.3f;

    [Header("스태미나")]
    [SerializeField] private float sprintStaminaCostPerSecond = 20f;

    [Header("애니메이션")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string groundedParameter = "IsGrounded";
    [SerializeField] private float animationDampTime = 0.1f;

    private CharacterController characterController;
    private PlayerStatus playerStatus;

    private float verticalVelocity;
    private bool canMove = true;
    private int speedHash;
    private int groundedHash;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        playerStatus = GetComponent<PlayerStatus>();

        if (animator != null)
        {
            Debug.Log("애니메이터 아직 없음");

            animator.applyRootMotion = false;
            speedHash = Animator.StringToHash(speedParameter);
            groundedHash = Animator.StringToHash(groundedParameter);
        }
    }

    void Update()
    {
        bool isGrounded = characterController.isGrounded;

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }

        Vector3 horizontalVelocity = Vector3.zero;
        bool canControlPlayer = canMove && !playerStatus.IsDead;

        if (canControlPlayer)
        {
            Vector2 input = GetMoveInput();
            Vector3 localMove = new Vector3(input.x, 0f, input.y);
            localMove = Vector3.ClampMagnitude(localMove, 1f);

            float currentSpeed = walkSpeed;
            bool wantsToSprint = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

            if (wantsToSprint && localMove.sqrMagnitude > 0.01f &&
                playerStatus.TryUseStamina(sprintStaminaCostPerSecond * Time.deltaTime))
            {
                currentSpeed = sprintSpeed;
            }

            horizontalVelocity = transform.TransformDirection(localMove) * currentSpeed;

            if (isGrounded && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 finalVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
        characterController.Move(finalVelocity * Time.deltaTime);

        UpdateAnimator(horizontalVelocity.magnitude, isGrounded);
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

    private void UpdateAnimator(float moveSpeed, bool isGrounded)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetFloat(speedHash, moveSpeed, animationDampTime, Time.deltaTime);
        animator.SetBool(groundedHash, isGrounded);
    }

    // Timeline Signal 또는 대화 시스템에서 호출한다.
    public void SetMovementEnabled(bool enabled)
    {
        canMove = false;
    }
}
