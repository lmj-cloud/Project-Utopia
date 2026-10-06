using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

public class PlayerCameraController : MonoBehaviour
{
    [SerializeField] private Transform playerTr;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private CinemachineCamera firstPersonCamera;
    [SerializeField] private CinemachineCamera thirdPersonCamera;

    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool invertY;
    [SerializeField] private bool lockCursorOnStart = true;

    private float pitch;
    private bool canLook = true;
    private bool isThirdPerson;

    private void Awake()
    {
        if(playerTr == null)
        {
            playerTr = transform;
        }

        if(cameraTarget == null || firstPersonCamera == null || thirdPersonCamera == null)
        {
            enabled = false;
            return;
        }

        pitch = cameraTarget.localEulerAngles.x;
        if(pitch > 180f)
        {
            pitch -= 360f;
        }

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        cameraTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        SetView(false);
    }

    private void Start()
    {
        if (lockCursorOnStart)
        {
            SetCursorLocked(true);
        }
    }

    private void Update()
    {
        if (!canLook)
            return;

        if(Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame){
            SetView(!isThirdPerson);
        }

        if (Mouse.current == null)
            return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float yaw = mouseDelta.x * mouseSensitivity;
        float pitchDelta = mouseDelta.y * mouseSensitivity;

        if (!invertY)
        {
            pitchDelta = -pitchDelta;
        }

        playerTr.Rotate(Vector3.up * yaw);

        pitch = Mathf.Clamp(pitch + pitchDelta, minPitch, maxPitch);
        cameraTarget.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void SetView(bool useThirdPerson)
    {
        isThirdPerson = useThirdPerson;

        if (isThirdPerson)
        {
            firstPersonCamera.Priority = 10;
            thirdPersonCamera.Priority = 20;
        }
        else
        {
            firstPersonCamera.Priority = 20;
            thirdPersonCamera.Priority = 10;
        }
    }

    // Timeline Signal 또는 대화 시스템에서 호출한다.
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
