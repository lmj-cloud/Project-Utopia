using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

/// <summary>
/// 일반 3인칭 카메라와 컷신용 1인칭 시점, 시선 입력 및 카메라 제어 소유권을 관리한다.
/// </summary>
public class PlayerCameraController : MonoBehaviour
{
    [SerializeField] private Transform playerTr;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform thirdPersonCameraTarget;
    [SerializeField] private CinemachineCamera firstPersonCamera;
    [SerializeField] private CinemachineCamera thirdPersonCamera;

    [SerializeField] private Renderer[] firstPersonRenderers = new Renderer[0];
    [SerializeField] private Renderer[] thirdPersonRenderers = new Renderer[0];

    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool invertY;
    [SerializeField] private bool lockCursorOnStart = true;

    [Header("Crouch Camera (local Y)")]
    [SerializeField] private float firstPersonCrouchHeight = 1f;
    // 이전 직렬화 높이 값의 호환용 필드. 현재 카메라 앉기 보정은 별도 Drop 설정을 사용한다.
    [SerializeField, HideInInspector] private float thirdPersonCrouchHeight = 1f; // Legacy serialized value; use Third Person Crouch Drop.
    [SerializeField, Min(0f)] private float crouchCameraSpeed = 5f;
    private float firstPersonStandingHeight;
    [SerializeField, Min(0f)] private float thirdPersonCrouchDrop = 0.5f;
    private Vector3 thirdPersonStandingPosition;
    private float thirdPersonHeightOffset;
    private bool isCrouching;

    /// <summary>
    /// 앉기 요청과 천장 공간을 검사하고 발 위치를 유지하며 캡슐 높이를 변경한다.
    /// </summary>
    public void SetCrouching(bool value) => isCrouching = value;

    /// <summary>
    /// 카메라 기준점의 높이를 현재 서기·앉기 목표값으로 부드럽게 이동한다.
    /// </summary>
    private void UpdateTargetHeight(Transform target, float standing, float crouching)
    {
        if (target == null || target == playerTr) return;
        Vector3 position = target.localPosition;
        position.y = Mathf.MoveTowards(position.y, isCrouching ? crouching : standing, crouchCameraSpeed * Time.deltaTime);
        target.localPosition = position;
    }

    private float yaw;
    private float pitch;
    private bool canLook = true;
    private bool isThirdPerson = true;
    private UnityEngine.Object storyViewOwner;
    private bool storyTimelineControlsCamera;
    private bool gameplayViewPending;
    public bool IsStoryFirstPerson => storyViewOwner != null;
    public bool StoryReferencesReady => cameraTarget != null && cameraTarget != playerTr && firstPersonCamera != null && firstPersonCamera.isActiveAndEnabled;
    public bool HasTargetControl => targetControlOwners.Count > 0;
    /// <summary>
    /// 기존 시점 소유권과 목표 제어 소유자가 새 컷신 시점을 허용하는지 검사한다.
    /// </summary>
    internal bool CanStartStoryView(UnityEngine.Object cutsceneOwner) => storyViewOwner == null &&
        (targetControlOwners.Count == 0 || (targetControlOwners.Count == 1 && targetControlOwners.Contains(cutsceneOwner)));

    private readonly System.Collections.Generic.HashSet<UnityEngine.Object> targetControlOwners = new();
    public Transform FirstPersonTarget => cameraTarget;
    public Transform ThirdPersonTarget => thirdPersonCameraTarget;
    /// <summary>
    /// 카메라 기준점 제어 소유자를 등록한다.
    /// </summary>
    public void AcquireTargetControl(UnityEngine.Object owner) { if (owner != null) targetControlOwners.Add(owner); }
    /// <summary>
    /// 자기 기준점 잠금만 해제하고 마지막 해제 시 필요한 시선 동기화와 일반 시점 복구를 처리한다.
    /// </summary>
    public void ReleaseTargetControl(UnityEngine.Object owner, bool synchronize)
    {
        if (owner == null || !targetControlOwners.Remove(owner) || targetControlOwners.Count > 0) return;
        if (synchronize && thirdPersonCameraTarget != null)
        {
            yaw = thirdPersonCameraTarget.eulerAngles.y;
            pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, thirdPersonCameraTarget.eulerAngles.x), minPitch, maxPitch);
            thirdPersonStandingPosition = playerTr.InverseTransformPoint(thirdPersonCameraTarget.position) - Vector3.up * thirdPersonHeightOffset;
        }
        if (storyViewOwner == null && gameplayViewPending) SetView(true);
    }
    public bool LookEnabled => canLook && targetControlOwners.Count == 0;
    public float Pitch => pitch;
    public bool IsThirdPerson => isThirdPerson;
    public Vector3 Forward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        if(playerTr == null)
            playerTr = transform;

        if(thirdPersonCameraTarget == null || thirdPersonCameraTarget == playerTr || thirdPersonCamera == null)
        {
            enabled = false;
            return;
        }

        if (cameraTarget != null) firstPersonStandingHeight = cameraTarget.localPosition.y;
        if (thirdPersonCameraTarget != null)
            thirdPersonStandingPosition = playerTr.InverseTransformPoint(thirdPersonCameraTarget.position);
        yaw = playerTr.eulerAngles.y;
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, thirdPersonCameraTarget.eulerAngles.x), minPitch, maxPitch);
        thirdPersonCameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
        SetView(true);
    }

    /// <summary>
    /// 컴포넌트의 초기 실행 시점에 설정된 시작 동작을 요청한다.
    /// </summary>
    private void Start()
    {
        if (lockCursorOnStart)
            SetCursorLocked(true);
    }

    /// <summary>
    /// 시선 제어가 허용될 때 마우스 입력으로 yaw와 pitch를 갱신한다.
    /// </summary>
    public void Look(Vector2 mouseDelta)
    {
        if (!LookEnabled || !isActiveAndEnabled || IsStoryFirstPerson)
            return;

        yaw = Mathf.Repeat(yaw + mouseDelta.x * mouseSensitivity, 360f);
        float pitchDelta = mouseDelta.y * mouseSensitivity;
        if (!invertY)
            pitchDelta = -pitchDelta;

        pitch = Mathf.Clamp(pitch + pitchDelta, minPitch, maxPitch);
    }

    /// <summary>
    /// 외부 기준점 소유권이 없을 때 앉기 높이와 시선 회전을 카메라 기준점에 적용한다.
    /// </summary>
    private void LateUpdate()
    {
        if (targetControlOwners.Count > 0) return;

        if (thirdPersonCameraTarget != null && thirdPersonCameraTarget != playerTr)
        {
            // Keep the initial world pose relative to Player, independent of animated bone axes.
            thirdPersonHeightOffset = Mathf.MoveTowards(thirdPersonHeightOffset,
                isCrouching ? -thirdPersonCrouchDrop : 0f, crouchCameraSpeed * Time.deltaTime);
            thirdPersonCameraTarget.position = playerTr.TransformPoint(
                thirdPersonStandingPosition + Vector3.up * thirdPersonHeightOffset);
        }
        if (!canLook)
            return;

        // 부모 Player가 회전해도 카메라 방향은 독립적인 월드 회전으로 유지한다.
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);

        if (thirdPersonCameraTarget != null)
            thirdPersonCameraTarget.rotation = targetRotation;
    }

    /// <summary>
    /// 입력 벡터를 카메라 기준 수평 월드 이동 방향으로 변환한다.
    /// </summary>
    public Vector3 GetMoveDirection(Vector2 input)
    {
        Vector3 localDirection = new Vector3(input.x, 0f, input.y);
        localDirection = Vector3.ClampMagnitude(localDirection, 1f);
        return Quaternion.Euler(0f, yaw, 0f) * localDirection;
    }

    /// <summary>
    /// 이동 또는 컷신 복구 후 카메라의 내부 yaw·pitch를 현재 방향과 맞춘다.
    /// </summary>
    public void SynchronizeDirection(Quaternion rotation)
    {
        yaw = rotation.eulerAngles.y;
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, rotation.eulerAngles.x), minPitch, maxPitch);
        Quaternion view = Quaternion.Euler(pitch, yaw, 0f);
        if (thirdPersonCameraTarget != null) thirdPersonCameraTarget.rotation = view;
    }

    // Legacy public entry points cannot switch gameplay into first person.
    /// <summary>
    /// 기존 시점 전환 요청을 전달한다.
    /// </summary>
    public void ToggleView() => SetView(true);
    /// <summary>
    /// 일반 시점 정책과 소유권에 따라 카메라 우선순위와 모델 표시를 적용한다.
    /// </summary>
    public void SetView(bool useThirdPerson)
    {
        if (!useThirdPerson || storyViewOwner != null) return;
        isThirdPerson = true;
        SetModelVisible(firstPersonRenderers, false);
        SetModelVisible(thirdPersonRenderers, true);
        gameplayViewPending = targetControlOwners.Count > 0;
        if (gameplayViewPending) return;
        if (firstPersonCamera != null) firstPersonCamera.Priority = 10;
        if (thirdPersonCamera != null) { thirdPersonCamera.enabled = true; thirdPersonCamera.Priority = 20; }
    }
    /// <summary>
    /// 컷신 소유권과 필수 참조를 검사하여 1인칭 연출 시점을 시작한다.
    /// </summary>
    internal bool TryBeginStoryView(UnityEngine.Object owner, bool timelineControlsCamera)
    {
        if (owner == null || storyViewOwner != null || !isActiveAndEnabled || !StoryReferencesReady || thirdPersonCamera == null) return false;
        storyViewOwner = owner;
        storyTimelineControlsCamera = timelineControlsCamera;
        isThirdPerson = false;
        AcquireTargetControl(owner);
        SetModelVisible(firstPersonRenderers, true);
        SetModelVisible(thirdPersonRenderers, false);
        if (!timelineControlsCamera)
        {
            cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 storyPosition = cameraTarget.localPosition;
            storyPosition.y = isCrouching ? firstPersonCrouchHeight : firstPersonStandingHeight;
            cameraTarget.localPosition = storyPosition;
            firstPersonCamera.Priority = 20;
            thirdPersonCamera.Priority = 10;
        }
        return true;
    }
    /// <summary>
    /// 해당 소유자의 컷신 시점을 종료하고 일반 플레이 시점으로 복구한다.
    /// </summary>
    internal void EndStoryView(UnityEngine.Object owner)
    {
        if (owner == null || storyViewOwner != owner) return;
        storyViewOwner = null;
        isThirdPerson = true;
        ReleaseTargetControl(owner, true);
        SetModelVisible(firstPersonRenderers, false);
        SetModelVisible(thirdPersonRenderers, true);
        // Timeline-owned camera priorities are untouched until the cutscene releases its target lease.
        gameplayViewPending = storyTimelineControlsCamera && targetControlOwners.Count > 0;
        if (!gameplayViewPending)
        {
            if (firstPersonCamera != null) firstPersonCamera.Priority = 10;
            if (thirdPersonCamera != null) { thirdPersonCamera.enabled = true; thirdPersonCamera.Priority = 20; }
        }
    }

    /// <summary>
    /// 지정한 모델 Renderer들의 표시 상태를 설정한다.
    /// </summary>
    private void SetModelVisible(Renderer[] renderers, bool visible)
    {
        if (renderers == null)
            return;

        foreach (Renderer modelRenderer in renderers)
        {
            if (modelRenderer != null)
                modelRenderer.enabled = visible;
        }
    }

    /// <summary>
    /// 플레이어의 마우스 시선 제어 허용 여부를 변경한다.
    /// </summary>
    public void SetLookEnabled(bool enabled)
    {
        canLook = enabled;
    }

    /// <summary>
    /// 플레이 상황에 맞추어 커서 잠금과 표시 상태를 설정한다.
    /// </summary>
    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
