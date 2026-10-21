using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace S03Prototype
{
    public sealed class S03DoorInteraction : MonoBehaviour
    {
        [SerializeField] private S03FirstPersonPlayer player;
        [SerializeField] private Transform playerView;
        [SerializeField] private CanvasGroup prompt;
        [SerializeField] private S03TimelineController timelineSequence;
        [SerializeField] private Vector3 localInteractionPoint = new Vector3(0.78f, 1.2f, 0f);
        [SerializeField, Min(0.1f)] private float interactionDistance = 2.2f;
        [SerializeField, Min(0.01f)] private float openDuration = 0.65f;
        [SerializeField] private float openAngle = -100f;
        [SerializeField] private UnityEvent openingStarted = new UnityEvent();
        [SerializeField] private UnityEvent opened = new UnityEvent();

        private Quaternion closedRotation;
        public bool HasOpened { get; private set; }
        public bool IsOpening { get; private set; }
        public UnityEvent OpeningStarted => openingStarted;
        public UnityEvent Opened => opened;

        private void Awake()
        {
            closedRotation = transform.localRotation;
            SetPrompt(false);
            if (player == null || playerView == null || prompt == null)
            {
                Debug.LogError("S03 door needs player, camera and prompt references.", this);
                enabled = false;
            }
        }

        public bool CanInteract()
        {
            if (HasOpened || player == null || player.ControlsLocked || playerView == null) return false;
            Vector3 direction = transform.TransformPoint(localInteractionPoint) - playerView.position;
            if (direction.sqrMagnitude > interactionDistance * interactionDistance) return false;
            // A wall must not allow interaction with the door through it.
            if (!Physics.Raycast(playerView.position, direction.normalized, out RaycastHit hit,
                                 direction.magnitude + 0.15f, Physics.DefaultRaycastLayers,
                                 QueryTriggerInteraction.Ignore)) return false;
            return hit.transform == transform || hit.transform.IsChildOf(transform);
        }

        private void Update()
        {
            bool available = Application.isFocused && Cursor.lockState == CursorLockMode.Locked && CanInteract();
            SetPrompt(available);
            if (available && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                TryOpen();
        }

        public bool TryOpen()
        {
            if (!isActiveAndEnabled || !CanInteract()) return false;
            // Latch before invoking events: even reentrant or repeated input cannot start twice.
            HasOpened = true;
            IsOpening = true;
            SetPrompt(false);
            if (timelineSequence != null && timelineSequence.PlaySequence()) return true;
            StartCoroutine(OpenDoor());
            openingStarted.Invoke();
            return true;
        }

        public void SetTimelineOpenProgress(float progress)
        {
            float fraction = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            transform.localRotation = closedRotation * Quaternion.Euler(0f, openAngle * fraction, 0f);
            if (progress >= 1f && IsOpening)
            {
                IsOpening = false;
                opened.Invoke();
            }
        }

        private IEnumerator OpenDoor()
        {
            Quaternion target = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
            float elapsed = 0f;
            while (elapsed < openDuration)
            {
                elapsed += Time.deltaTime;
                float fraction = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / openDuration));
                transform.localRotation = Quaternion.Slerp(closedRotation, target, fraction);
                yield return null;
            }
            transform.localRotation = target;
            IsOpening = false;
            opened.Invoke();
        }

        private void SetPrompt(bool visible)
        {
            if (prompt != null) prompt.alpha = visible ? 1f : 0f;
        }

        private void OnDisable() => SetPrompt(false);
    }
}
