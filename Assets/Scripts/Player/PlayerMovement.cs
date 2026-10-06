using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Windows;

public class PlayerMovement : MonoBehaviour
{
    private Vector3 finalVelocity;

    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -2f;

    [SerializeField] private float jumpForce = 1.3f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string speedParameter = "Speed";
    [SerializeField] private string groundedParameter = "IsGrounded";
    [SerializeField] private float animationDampTime = 0.1f;

    private CharacterController characterController;

    private float currentSpeed;
    private float verticalVelocity;
    private int speedHash;
    private int groundedHash;

    public float CurrentSpeed
    {
        set => currentSpeed = Mathf.Max(0, value);
        get => currentSpeed;
    }

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        if (animator != null)
        {
            animator.applyRootMotion = false;
            speedHash = Animator.StringToHash(speedParameter);
            groundedHash = Animator.StringToHash(groundedParameter);
        }
    }

    void Update()
    {
        if (!characterController.isGrounded)
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        characterController.Move(finalVelocity * Time.deltaTime);

    }

    public void MoveTo(Vector3 direction)
    {
        Vector3 horizontalVelocity = Vector3.zero;
        horizontalVelocity = transform.TransformDirection(direction) * currentSpeed;

        finalVelocity = horizontalVelocity + Vector3.up * verticalVelocity;
        UpdateAnimator(horizontalVelocity.magnitude, characterController.isGrounded);
    }

    public void Jump()
    {
        bool isGrounded = characterController.isGrounded;

        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }

        if (isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
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
}
