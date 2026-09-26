using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraController : MonoBehaviour
{
    [SerializeField] private Transform playerBody;

    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float minPitch = -80f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool invertY;
    [SerializeField] private bool lockCursorOnStart = true;

    private float pitch;
    private bool canLook = true;

    private void Awake()
    {
        pitch = transform.localEulerAngles.x;
        if (pitch > 180f)
        {
            pitch -= 360f;
        }

        if (playerBody == null && transform.parent != null)
        {
            playerBody = transform.parent;
        }
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
        if (!canLook || playerBody == null || Mouse.current == null)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float yaw = mouseDelta.x * mouseSensitivity;
        float pitchDelta = mouseDelta.y * mouseSensitivity;

        if (!invertY)
        {
            pitchDelta = -pitchDelta;
        }

        playerBody.Rotate(Vector3.up * yaw);

        pitch = Mathf.Clamp(pitch + pitchDelta, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
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
