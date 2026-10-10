using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("가속, 감속, 회전")]
    [SerializeField, Min(0.01f)] private float acceleration = 12f;
    [SerializeField, Min(0.01f)] private float deceleration = 18f;
    [SerializeField, Min(0f)] private float turnSpeed = 360f;

    [Header("중력, 점프")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -2f;
    // 기존 Inspector 값을 유지한다. 실제 의미는 점프 높이다.
    [SerializeField, Min(0f)] private float jumpForce = 1.3f;

    private CharacterController characterController;

    private Vector3 moveDirection;
    private Vector3 facingDirection;
    private Vector3 travelDirection;
    private Vector3 horizontalVelocity;

    private float targetSpeed;
    private float currentSpeed;
    private float verticalVelocity;

    private bool rotateImmediately;
    private bool jumpRequested;

    // Controller가 걷기/달리기 목표 속도를 전달한다.
    public float TargetSpeed
    {
        get => targetSpeed;
        set => targetSpeed = Mathf.Max(0f, value);
    }

    // 가속·감속으로 계산한 속도. 충돌 후 실제 속도와는 구분한다.
    public float CurrentSpeed => currentSpeed;

    // 실제 이동 후 측정한 수평 속도. 애니메이션에 전달한다.
    public Vector3 HorizontalVelocity => horizontalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        UpdateRotation();
        UpdateSpeed();
        UpdateTravelDirection();
        UpdateVerticalVelocity();
        UpdateMovement();
    }

    // Controller가 전달한 월드 기준 수평 방향을 저장한다.
    public void MoveTo(Vector3 direction)
    {
        direction.y = 0f;
        moveDirection = Vector3.ClampMagnitude(direction, 1f);
    }

    // immediately: 일반 3인칭은 false, 1인칭/조준은 true.
    public void SetFacingDirection(Vector3 direction, bool immediately)
    {
        direction.y = 0f;
        facingDirection = direction;
        rotateImmediately = immediately;
    }

    public void FaceImmediately(Vector3 direction)
    {
        SetFacingDirection(direction, true);
        UpdateRotation();
    }

    public void Jump()
    {
        jumpRequested = true;
    }

    // 사망·조작 금지 때 즉시 수평 이동을 멈춘다. 중력은 유지한다.
    // 일반적인 키 해제는 이 메서드를 호출하지 않고 MoveTo(Vector3.zero)를 사용한다.
    public void Stop()
    {
        moveDirection = Vector3.zero;
        facingDirection = Vector3.zero;
        travelDirection = Vector3.zero;
        horizontalVelocity = Vector3.zero;
        targetSpeed = 0f;
        currentSpeed = 0f;
        jumpRequested = false;
    }

    private void UpdateRotation()
    {
        if (facingDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(facingDirection);

        if (rotateImmediately)
        {
            transform.rotation = targetRotation;
        }
        else
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }
    }

    private void UpdateSpeed()
    {
        // 입력이 없으면 0, 있으면 걷기/달리기 목표 속도까지 변화한다.
        float desiredSpeed = targetSpeed * moveDirection.magnitude;
        float changeRate = desiredSpeed > currentSpeed ? acceleration : deceleration;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed, desiredSpeed, changeRate * Time.deltaTime);
    }

    private void UpdateTravelDirection()
    {
        if (rotateImmediately)
        {
            // 1인칭/조준: 카메라 기준 전후좌우로 이동한다.
            // 키를 떼면 마지막 이동 방향을 유지하며 감속한다.
            if (moveDirection.sqrMagnitude > 0.0001f)
                travelDirection = moveDirection.normalized;
        }
        else
        {
            // 일반 3인칭: 몸이 회전하는 방향을 따라 이동한다.
            travelDirection = transform.forward;
            travelDirection.y = 0f;
            travelDirection.Normalize();
        }
    }

    private void UpdateVerticalVelocity()
    {
        bool isGrounded = characterController.isGrounded;

        if (jumpRequested && isGrounded && verticalVelocity <= 0f && gravity < 0f)
            verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);

        // 공중에서 누른 점프가 착지 후 실행되지 않도록 요청을 소비한다.
        jumpRequested = false;

        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedStickForce;
        else
            verticalVelocity += gravity * Time.deltaTime;
    }

    private void UpdateMovement()
    {
        Vector3 requestedHorizontalVelocity = travelDirection * currentSpeed;
        Vector3 finalVelocity = requestedHorizontalVelocity + Vector3.up * verticalVelocity;

        // 실제 이동은 이 한 곳에서만 처리한다.
        characterController.Move(finalVelocity * Time.deltaTime);

        // CollisionFlags는 CharacterController가 이동하면서 어느 방향에서 충돌했는지 알려주는 enum이다.
        if ((characterController.collisionFlags & CollisionFlags.Above) != 0 &&
            verticalVelocity > 0f)
        {
            verticalVelocity = 0f;
        }

        Vector3 actualVelocity = characterController.velocity;
        horizontalVelocity = new Vector3(actualVelocity.x, 0f, actualVelocity.z);
    }
}