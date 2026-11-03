using UnityEngine;

/// <summary>
/// CharacterController를 통해 수평 이동, 회전, 중력, 점프, 앉기 및 회피 이동을 처리한다.
/// </summary>
// 입력 담당자가 조준 속도 제한을 전달한 뒤 이 프레임의 실제 이동을 계산한다.
[DefaultExecutionOrder(10)]
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
    [Header("Crouch")]
    [SerializeField, Min(0.1f)] private float crouchHeight = 1f;
    [SerializeField, Min(0f)] private float crouchSpeed = 1.5f;
    [SerializeField] private LayerMask standingObstructionMask = ~0;
    [SerializeField, Min(0.001f)] private float standingClearance = 0.02f;
    private float standingHeight;
    private Vector3 standingCenter;
    public event System.Action<DemoAction, UnityEngine.Object> Succeeded;
    private float dodgeUntil;
    private Vector3 dodgeVelocity;
    private bool moveReported;
    public bool CanDodge => IsSimulationEnabled && characterController != null && characterController.isGrounded && Time.time >= dodgeUntil;
    /// <summary>
    /// 접지와 이동 시뮬레이션 조건을 확인하여 회피 방향·속도·지속 시간을 기록한다. 스태미나는 호출자가 검사한다.
    /// </summary>
    public bool TryDodge(Vector3 direction, float speed, float duration)
    {
        if (!CanDodge || duration <= 0f) return false;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = -transform.forward;
        dodgeVelocity = direction.normalized * Mathf.Max(0f, speed);
        dodgeUntil = Time.time + duration;
        jumpRequested = false;
        return true;
    }
    /// <summary>
    /// 회피 시간과 회피 속도를 비워 일반 이동 상태로 되돌린다.
    /// </summary>
    public void CancelDodge() { dodgeUntil = 0f; dodgeVelocity = Vector3.zero; }
    /// <summary>
    /// 기존 이동과 수직 속도를 정리한 뒤 CharacterController를 보호하며 위치·회전을 옮긴다.
    /// </summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        Stop();
        verticalVelocity = 0f;
        bool wasEnabled = characterController != null && characterController.enabled;
        if (wasEnabled) characterController.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        if (wasEnabled) characterController.enabled = true;
    }
    public bool IsGrounded => characterController != null && characterController.isGrounded;
    public bool IsCrouching { get; private set; }

    /// <summary>
    /// 앉기 요청과 천장 공간을 검사하고 발 위치를 유지하며 캡슐 높이를 변경한다.
    /// </summary>
    public void SetCrouching(bool requested)
    {
        if (!IsSimulationEnabled) return;
        if (characterController == null || requested == IsCrouching) return;
        if (!requested && IsCrouching && !CanStand()) return;
        IsCrouching = requested;
        float height = requested
            ? Mathf.Clamp(crouchHeight, characterController.radius * 2f, standingHeight)
            : standingHeight;
        // Preserve the capsule bottom in local space.
        characterController.height = height;
        characterController.center = standingCenter + Vector3.up * ((height - standingHeight) * 0.5f);
        if (requested) { jumpRequested = false; Succeeded?.Invoke(DemoAction.Crouch, this); }
    }

    /// <summary>
    /// 앉은 캡슐 위쪽에 서 있을 공간이 있는지 물리 검사한다.
    /// </summary>
    private bool CanStand()
    {
        Vector3 scale = transform.lossyScale;
        float radius = characterController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = standingHeight * Mathf.Abs(scale.y);
        Vector3 center = transform.TransformPoint(standingCenter);
        // Check the head's expansion volume, with full radius so ceiling edges cannot be missed.
        Vector3 top = center + transform.up * Mathf.Max(0f, height * 0.5f - radius);
        Vector3 bottom = top - transform.up * ((standingHeight - characterController.height) * Mathf.Abs(scale.y));
        foreach (Collider hit in Physics.OverlapCapsule(bottom, top + transform.up * standingClearance,
            radius, standingObstructionMask, QueryTriggerInteraction.Ignore))
        {
            if (hit == characterController || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }


    private Vector3 moveDirection;
    private Vector3 facingDirection;
    private Vector3 travelDirection;
    private Vector3 horizontalVelocity;

    private float targetSpeed;
    private float currentSpeed;
    private float verticalVelocity;

    private bool rotateImmediately;
    private bool jumpRequested;
    public bool IsSimulationEnabled { get; private set; } = true;

    /// <summary>
    /// 이동 시뮬레이션의 허용 여부를 바꾸고 중지 시 남은 이동을 정리한다.
    /// </summary>
    public void SetSimulationEnabled(bool value)
    {
        if (IsSimulationEnabled == value) return;
        Stop();
        verticalVelocity = 0f;
        IsSimulationEnabled = value;
    }

    // Controller가 걷기/달리기 목표 속도를 전달한다.
    public float TargetSpeed
    {
        get => targetSpeed;
        set => targetSpeed = Mathf.Max(0f, value);
    }

    /// <summary>
    /// 달리기 도중 조준을 시작해도 수평 속도가 제한을 넘지 않도록 현재 속도만 낮춘다. 수직 속도는 유지한다.
    /// </summary>
    public void LimitHorizontalSpeed(float maximumSpeed)
    {
        currentSpeed = Mathf.Min(currentSpeed, Mathf.Max(0f, maximumSpeed));
    }

    // 가속·감속으로 계산한 속도. 충돌 후 실제 속도와는 구분한다.
    public float CurrentSpeed => currentSpeed;

    // 실제 이동 후 측정한 수평 속도. 애니메이션에 전달한다.
    public Vector3 HorizontalVelocity => horizontalVelocity;

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        standingHeight = characterController.height;
        standingCenter = characterController.center;
    }

    /// <summary>
    /// 시뮬레이션이 허용될 때 회전·수평 속도·수직 속도를 갱신하여 실제 이동을 적용한다.
    /// </summary>
    private void Update()
    {
        if (!IsSimulationEnabled) return;
        UpdateRotation();
        UpdateSpeed();
        UpdateTravelDirection();
        UpdateVerticalVelocity();
        UpdateMovement();
    }

    // Controller가 전달한 월드 기준 수평 방향을 저장한다.
    /// <summary>
    /// 수평 이동 요청 방향을 기록하여 실제 이동 갱신에서 사용한다.
    /// </summary>
    public void MoveTo(Vector3 direction)
    {
        if (!IsSimulationEnabled) return;
        direction.y = 0f;
        moveDirection = Vector3.ClampMagnitude(direction, 1f);
    }

    // immediately: 일반 3인칭은 false, 1인칭/조준은 true.
    /// <summary>
    /// 플레이어가 바라볼 방향과 회전 방식의 요청을 기록한다.
    /// </summary>
    public void SetFacingDirection(Vector3 direction, bool immediately)
    {
        if (!IsSimulationEnabled) return;
        direction.y = 0f;
        facingDirection = direction;
        rotateImmediately = immediately;
    }

    /// <summary>
    /// 수평 기준 방향으로 플레이어를 즉시 회전시킨다.
    /// </summary>
    public void FaceImmediately(Vector3 direction)
    {
        if (!IsSimulationEnabled) return;
        SetFacingDirection(direction, true);
        UpdateRotation();
    }

    /// <summary>
    /// 지면과 현재 이동 상태를 검사하여 점프를 예약한다.
    /// </summary>
    public void Jump()
    {
        if (!IsSimulationEnabled) return;
        if (!IsCrouching) jumpRequested = true;
    }

    // 사망·조작 금지 때 즉시 수평 이동을 멈춘다. 중력은 유지한다.
    // 일반적인 키 해제는 이 메서드를 호출하지 않고 MoveTo(Vector3.zero)를 사용한다.
    /// <summary>
    /// 수평 이동·회피·점프 요청을 정리하여 움직임을 멈춘다.
    /// </summary>
    public void Stop()
    {
        CancelDodge();
        moveDirection = Vector3.zero;
        facingDirection = Vector3.zero;
        travelDirection = Vector3.zero;
        horizontalVelocity = Vector3.zero;
        targetSpeed = 0f;
        currentSpeed = 0f;
        jumpRequested = false;
    }

    /// <summary>
    /// 지정된 시선 방향에 맞추어 즉시 또는 제한된 속도로 회전한다.
    /// </summary>
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

    /// <summary>
    /// 현재 속도를 목표 속도로 부드럽게 변경한다.
    /// </summary>
    private void UpdateSpeed()
    {
        // 입력이 없으면 0, 있으면 걷기/달리기 목표 속도까지 변화한다.
        float desiredSpeed = (IsCrouching ? Mathf.Min(targetSpeed, crouchSpeed) : targetSpeed) * moveDirection.magnitude;
        if (IsCrouching) currentSpeed = Mathf.Min(currentSpeed, crouchSpeed);
        float changeRate = desiredSpeed > currentSpeed ? acceleration : deceleration;

        currentSpeed = Mathf.MoveTowards(
            currentSpeed, desiredSpeed, changeRate * Time.deltaTime);
    }

    /// <summary>
    /// 현재 이동 요청에 가속·감속을 적용하여 수평 이동 방향과 속도를 갱신한다.
    /// </summary>
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

    /// <summary>
    /// 접지 여부와 점프 요청을 처리하고 중력을 수직 속도에 누적한다.
    /// </summary>
    private void UpdateVerticalVelocity()
    {
        bool isGrounded = characterController.isGrounded;

        if (jumpRequested && !IsCrouching && isGrounded && verticalVelocity <= 0f && gravity < 0f)
        {
            verticalVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
            Succeeded?.Invoke(DemoAction.Jump, this);
        }

        // 공중에서 누른 점프가 착지 후 실행되지 않도록 요청을 소비한다.
        jumpRequested = false;

        if (isGrounded && verticalVelocity < 0f)
            verticalVelocity = groundedStickForce;
        else
            verticalVelocity += gravity * Time.deltaTime;
    }

    /// <summary>
    /// 수평·수직·회피 속도를 합쳐 기존 CharacterController.Move 경로로 실제 이동한다.
    /// </summary>
    private void UpdateMovement()
    {
        Vector3 requestedHorizontalVelocity = Time.time < dodgeUntil ? dodgeVelocity : travelDirection * currentSpeed;
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
        bool moving = horizontalVelocity.sqrMagnitude > 0.01f;
        if (moving && !moveReported) Succeeded?.Invoke(DemoAction.Move, this);
        moveReported = moving;
    }
}