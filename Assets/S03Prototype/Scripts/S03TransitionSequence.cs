using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace S03Prototype
{
    public sealed class S03TransitionSequence : MonoBehaviour
    {
        [SerializeField] private S03FirstPersonPlayer player;
        [SerializeField] private S03ScreenFade screenFade;
        [Tooltip("Optional. Leave empty to test the fade without changing location.")]
        [SerializeField] private Transform destination;
        [SerializeField, Min(0f)] private float doorLeadIn = 0.2f;
        [SerializeField, Min(0f)] private float fadeToWhiteDuration = 0.8f;
        [SerializeField, Min(0f)] private float whiteHoldDuration = 0.15f;
        [SerializeField, Min(0f)] private float fadeFromWhiteDuration = 0.8f;
        [SerializeField] private UnityEvent fullyWhiteReached = new UnityEvent();
        [SerializeField] private UnityEvent sequenceCompleted = new UnityEvent();

        private Coroutine routine;
        private bool previousControlsLocked;
        public bool IsRunning { get; private set; }
        public bool HasCompleted { get; private set; }
        public UnityEvent FullyWhiteReached => fullyWhiteReached;
        public UnityEvent SequenceCompleted => sequenceCompleted;

        public void BeginSequence()
        {
            if (!isActiveAndEnabled || IsRunning || HasCompleted) return;
            if (player == null || screenFade == null || !screenFade.isActiveAndEnabled)
            {
                Debug.LogError("S03 transition needs an active player and screen fade.", this);
                return;
            }
            previousControlsLocked = player.ControlsLocked;
            IsRunning = true;
            player.SetControlsLocked(true);
            routine = StartCoroutine(RunSequence());
        }

        private IEnumerator RunSequence()
        {
            try
            {
                if (doorLeadIn > 0f) yield return new WaitForSecondsRealtime(doorLeadIn);
                yield return screenFade.FadeTo(1f, fadeToWhiteDuration);
                if (destination != null) player.Teleport(destination.position, destination.rotation);
                fullyWhiteReached.Invoke();
                if (whiteHoldDuration > 0f) yield return new WaitForSecondsRealtime(whiteHoldDuration);
                yield return screenFade.FadeTo(0f, fadeFromWhiteDuration);
                HasCompleted = true;
            }
            finally
            {
                RestoreControlsAndScreen();
                routine = null;
            }
            sequenceCompleted.Invoke();
        }

        private void RestoreControlsAndScreen()
        {
            if (!IsRunning) return;
            if (player != null) player.SetControlsLocked(previousControlsLocked);
            if (screenFade != null) screenFade.Clear();
            IsRunning = false;
        }

        private void OnDisable()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            RestoreControlsAndScreen();
        }
    }
}
