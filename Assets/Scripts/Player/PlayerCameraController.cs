using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

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
    [SerializeField, HideInInspector] private float thirdPersonCrouchHeight = 1f; // Legacy serialized value; use Third Person Crouch Drop.
    [SerializeField, Min(0f)] private float crouchCameraSpeed = 5f;
    private float firstPersonStandingHeight;
    [SerializeField, Min(0f)] private float thirdPersonCrouchDrop = 0.5f;
    private Vector3 thirdPersonStandingPosition;
    private float thirdPersonHeightOffset;
    private bool isCrouching;

    public void SetCrouching(bool value) => isCrouching = value;

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
    internal bool CanStartStoryView(UnityEngine.Object cutsceneOwner) => storyViewOwner == null &&
        (targetControlOwners.Count == 0 || (targetControlOwners.Count == 1 && targetControlOwners.Contains(cutsceneOwner)));

    private readonly System.Collections.Generic.HashSet<UnityEngine.Object> targetControlOwners = new();
    public Transform FirstPersonTarget => cameraTarget;
    public Transform ThirdPersonTarget => thirdPersonCameraTarget;
    public void AcquireTargetControl(UnityEngine.Object owner) { if (owner != null) targetControlOwners.Add(owner); }
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

    private void Start()
    {
        if (lockCursorOnStart)
            SetCursorLocked(true);
    }

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

    public Vector3 GetMoveDirection(Vector2 input)
    {
        Vector3 localDirection = new Vector3(input.x, 0f, input.y);
        localDirection = Vector3.ClampMagnitude(localDirection, 1f);
        return Quaternion.Euler(0f, yaw, 0f) * localDirection;
    }

    public void SynchronizeDirection(Quaternion rotation)
    {
        yaw = rotation.eulerAngles.y;
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, rotation.eulerAngles.x), minPitch, maxPitch);
        Quaternion view = Quaternion.Euler(pitch, yaw, 0f);
        if (thirdPersonCameraTarget != null) thirdPersonCameraTarget.rotation = view;
    }

    // Legacy public entry points cannot switch gameplay into first person.
    public void ToggleView() => SetView(true);
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

    public void SetLookEnabled(bool enabled)
    {
        canLook = enabled;
    }

    public void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
