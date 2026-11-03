using UnityEngine;

/// <summary>
/// 대화 중에만 Humanoid Animator의 Look At IK로 플레이어를 바라본다.
/// 루트와 본 Transform을 직접 회전시키지 않고 기존 애니메이션 위에 시선을 더한다.
/// Animator와 같은 GameObject에 연결하고 Base Layer의 IK Pass를 켜야 한다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class NPCLookAtPlayer : MonoBehaviour
{
    [Header("Look At Target")]
    [SerializeField] private bool enableLookAt = true;
    [Tooltip("플레이어의 Head 또는 얼굴 높이 Transform을 연결한다.")]
    [SerializeField] private Transform playerTarget;
    [Tooltip("시선 방향의 영향을 받지 않는 NPC 루트를 연결한다.")]
    [SerializeField] private Transform bodyForwardReference;

    [Header("Detection")]
    [SerializeField, Min(0.1f)] private float detectionDistance = 5f;
    [SerializeField, Range(0f, 89f)] private float maxHorizontalAngle = 70f;
    [SerializeField, Range(0f, 89f)] private float maxVerticalAngle = 25f;
    [SerializeField, Min(0.01f)] private float lookSpeed = 5f;

    [Header("Humanoid Look At Weights")]
    [SerializeField, Range(0f, 1f)] private float bodyWeight = 0.08f;
    [SerializeField, Range(0f, 1f)] private float headWeight = 0.85f;
    [SerializeField, Range(0f, 1f)] private float clampWeight = 0.5f;

    private Animator targetAnimator;
    private Transform head;
    private Vector3 smoothedLookPosition;
    private float currentLookWeight;
    private bool hasLookPosition;
    private bool isTargetInView;
    private bool isDialogueLookActive;

    public float CurrentLookWeight { get { return currentLookWeight; } }
    public bool IsTargetInView { get { return isTargetInView; } }
    public bool IsDialogueLookActive { get { return isDialogueLookActive; } }

    /// <summary>대화창이 실제로 열린 후 호출한다. 재요청 시 기존 보간 상태를 유지한다.</summary>
    public void BeginDialogueLookAt()
    {
        isDialogueLookActive = true;
    }

    /// <summary>종료·취소의 중복 요청은 안전하게 처리하고 IK 콜백에서 서서히 복귀한다.</summary>
    public void EndDialogueLookAt()
    {
        isDialogueLookActive = false;
        isTargetInView = false;
    }


    /// <summary>모델 자신의 Humanoid Animator와 Head 본을 한 번 준비한다.</summary>
    private void Awake()
    {
        targetAnimator = GetComponent<Animator>();
        if (targetAnimator.avatar == null || !targetAnimator.isHuman || !targetAnimator.avatar.isValid)
        {
            Debug.LogWarning("NPCLookAtPlayer: 유효한 Humanoid Avatar가 필요합니다.", this);
            enabled = false;
            return;
        }
        head = targetAnimator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null)
        {
            Debug.LogWarning("NPCLookAtPlayer: Head 본이 연결되어 있지 않습니다.", this);
            enabled = false;
        }
    }

    /// <summary>애니메이션 평가 뒤 IK 전용 콜백에서만 Look At API를 호출한다.</summary>
    private void OnAnimatorIK(int layerIndex)
    {
        if (layerIndex != 0 || targetAnimator == null || head == null)
        {
            return;
        }

        Transform reference = bodyForwardReference;
        if (reference == null)
        {
            reference = transform;
        }
        Vector3 forward = reference.forward;
        Vector3 up = reference.up;
        isTargetInView = CanLookAtTarget(forward, up);

        float deltaTime = Time.deltaTime;
        if (targetAnimator.updateMode == AnimatorUpdateMode.UnscaledTime)
        {
            deltaTime = Time.unscaledDeltaTime;
        }
        float speed = Mathf.Max(0.01f, lookSpeed);
        float desiredWeight = 0f;
        if (isTargetInView)
        {
            desiredWeight = 1f;
        }
        currentLookWeight = Mathf.MoveTowards(currentLookWeight, desiredWeight, speed * deltaTime);

        if (!hasLookPosition)
        {
            smoothedLookPosition = head.position + forward * 2f;
            hasLookPosition = true;
        }
        // 범위 밖 목표는 추적하지 않는다. 마지막 유효 지점을 유지한 채 가중치만 줄여 뒤로 꺾이지 않게 한다.
        if (isTargetInView)
        {
            float blend = 1f - Mathf.Exp(-speed * deltaTime);
            smoothedLookPosition = Vector3.Lerp(smoothedLookPosition, playerTarget.position, blend);
        }
        targetAnimator.SetLookAtPosition(smoothedLookPosition);
        targetAnimator.SetLookAtWeight(currentLookWeight, bodyWeight, headWeight, 0f, clampWeight);
    }

    /// <summary>NPC 루트 정면을 기준으로 거리와 수평·수직 각도를 각각 검사한다.</summary>
    private bool CanLookAtTarget(Vector3 forward, Vector3 up)
    {
        if (!enableLookAt || !isDialogueLookActive || playerTarget == null)
        {
            return false;
        }
        Vector3 direction = playerTarget.position - head.position;
        if (direction.sqrMagnitude < 0.0001f || direction.sqrMagnitude > detectionDistance * detectionDistance)
        {
            return false;
        }
        Vector3 horizontalDirection = Vector3.ProjectOnPlane(direction, up);
        if (horizontalDirection.sqrMagnitude < 0.0001f)
        {
            return false;
        }
        float horizontalAngle = Vector3.Angle(forward, horizontalDirection);
        float verticalAngle = Mathf.Abs(Mathf.Atan2(Vector3.Dot(direction, up), horizontalDirection.magnitude) * Mathf.Rad2Deg);
        // 90도 미만으로 제한하여 Inspector 범위를 코드로 바꾸더라도 뒤쪽을 추적하지 않는다.
        return horizontalAngle <= Mathf.Clamp(maxHorizontalAngle, 0f, 89f) + 0.01f
            && verticalAngle <= Mathf.Clamp(maxVerticalAngle, 0f, 89f) + 0.01f;
    }

    /// <summary>다시 활성화될 때 이전 대상 위치와 가중치를 갑자기 이어 쓰지 않는다.</summary>
    private void OnDisable()
    {
        isDialogueLookActive = false;
        currentLookWeight = 0f;
        isTargetInView = false;
        hasLookPosition = false;
    }
}
