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

    private float yaw;
    private float pitch;
    private bool canLook = true;
    private bool isThirdPerson;

    public float Pitch => pitch;
    public bool IsThirdPerson => isThirdPerson;
    public Vector3 Forward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

    private void Awake()
    {
        if(playerTr == null)
            playerTr = transform;

        if(cameraTarget == null || cameraTarget == playerTr || firstPersonCamera == null || thirdPersonCamera == null)
        {
            enabled = false;
            return;
        }

        yaw = playerTr.eulerAngles.y;
        pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, cameraTarget.eulerAngles.x), minPitch, maxPitch);
        cameraTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
        SetView(false);
    }

    private void Start()
    {
        if (lockCursorOnStart)
            SetCursorLocked(true);
    }

    public void Look(Vector2 mouseDelta)
    {
        if (!canLook || !isActiveAndEnabled)
            return;

        yaw = Mathf.Repeat(yaw + mouseDelta.x * mouseSensitivity, 360f);
        float pitchDelta = mouseDelta.y * mouseSensitivity;
        if (!invertY)
            pitchDelta = -pitchDelta;

        pitch = Mathf.Clamp(pitch + pitchDelta, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (!canLook)
            return;

        // 부모 Player가 회전해도 카메라 방향은 독립적인 월드 회전으로 유지한다.
        Quaternion targetRotation = Quaternion.Euler(pitch, yaw, 0f);

        if (cameraTarget != null)
            cameraTarget.rotation = targetRotation;

        if (thirdPersonCameraTarget != null)
            thirdPersonCameraTarget.rotation = targetRotation;
    }

    public Vector3 GetMoveDirection(Vector2 input)
    {
        Vector3 localDirection = new Vector3(input.x, 0f, input.y);
        localDirection = Vector3.ClampMagnitude(localDirection, 1f);
        return Quaternion.Euler(0f, yaw, 0f) * localDirection;
    }

    public void ToggleView()
    {
        if (canLook && isActiveAndEnabled)
            SetView(!isThirdPerson);
    }

    public void SetView(bool useThirdPerson)
    {
        if (firstPersonCamera == null || thirdPersonCamera == null)
            return;

        isThirdPerson = useThirdPerson;
        firstPersonCamera.Priority = isThirdPerson ? 10 : 20;
        thirdPersonCamera.Priority = isThirdPerson ? 20 : 10;

        SetModelVisible(firstPersonRenderers, !isThirdPerson);
        SetModelVisible(thirdPersonRenderers, isThirdPerson);
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
