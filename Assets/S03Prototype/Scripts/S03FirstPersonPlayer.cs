using UnityEngine;
using UnityEngine.InputSystem;

namespace S03Prototype
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class S03FirstPersonPlayer : MonoBehaviour
    {
        [SerializeField] private Transform view;
        [SerializeField, Min(0f)] private float moveSpeed = 3.5f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
        [SerializeField] private float gravity = -20f;
        [SerializeField, Range(1f, 89f)] private float pitchLimit = 80f;

        private CharacterController controller;
        private float verticalSpeed;
        private float pitch;
        public bool ControlsLocked { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (view == null)
            {
                Camera childCamera = GetComponentInChildren<Camera>();
                if (childCamera != null) view = childCamera.transform;
            }
            if (view == null)
            {
                Debug.LogError("S03 player needs a child camera.", this);
                enabled = false;
            }
        }

        private void Start() => CaptureCursor();

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                ReleaseCursor();
            // Click inside Game view to resume after Escape or losing focus.
            else if (!ControlsLocked && Application.isFocused && mouse != null &&
                     mouse.leftButton.wasPressedThisFrame)
                CaptureCursor();

            bool acceptsInput = !ControlsLocked && Application.isFocused &&
                                Cursor.lockState == CursorLockMode.Locked;
            Vector2 movement = Vector2.zero;
            if (acceptsInput && keyboard != null)
            {
                movement.x = (keyboard.dKey.isPressed ? 1f : 0f) -
                             (keyboard.aKey.isPressed ? 1f : 0f);
                movement.y = (keyboard.wKey.isPressed ? 1f : 0f) -
                             (keyboard.sKey.isPressed ? 1f : 0f);
                movement = Vector2.ClampMagnitude(movement, 1f);
            }
            if (acceptsInput && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;
                transform.Rotate(0f, delta.x, 0f);
                pitch = Mathf.Clamp(pitch - delta.y, -pitchLimit, pitchLimit);
                view.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += gravity * Time.deltaTime;
            Vector3 velocity = (transform.right * movement.x + transform.forward * movement.y) * moveSpeed;
            velocity.y = verticalSpeed;
            controller.Move(velocity * Time.deltaTime);
        }

        // A cutscene can stop both walking and looking, then restore them.
        public void SetControlsLocked(bool locked) => ControlsLocked = locked;

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            verticalSpeed = 0f;
            pitch = 0f;
            view.localRotation = Quaternion.identity;
            controller.enabled = wasEnabled;
        }

        private void CaptureCursor()
        {
            if (ControlsLocked || !Application.isFocused) return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static void ReleaseCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) ReleaseCursor();
        }

        private void OnDisable() => ReleaseCursor();
    }
}
