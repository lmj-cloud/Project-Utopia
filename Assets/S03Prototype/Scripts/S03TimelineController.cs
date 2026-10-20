using UnityEngine;
using UnityEngine.Playables;

namespace S03Prototype
{
    public sealed class S03TimelineController : MonoBehaviour
    {
        [SerializeField] private PlayableDirector director;
        [SerializeField] private S03FirstPersonPlayer player;
        [SerializeField] private S03DoorInteraction door;
        [SerializeField] private S03ScreenFade screenFade;
        [SerializeField] private S03AudioCues audioCues;
        [SerializeField] private Transform destination;
        [SerializeField] private Transform playerCamera;
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0.02f, 0.12f);
        private Vector3 cameraStart;
        private bool previousLock;
        private bool travelled;
        private bool controlsReleased;
        public bool IsRunning { get; private set; }
        public bool HasCompleted { get; private set; }
        public bool HasTravelled => travelled;

        private void OnEnable()
        {
            if (director != null) director.stopped += OnDirectorStopped;
        }

        public bool PlaySequence()
        {
            if (!isActiveAndEnabled || director == null || !director.isActiveAndEnabled ||
                director.playableAsset == null || player == null || door == null ||
                screenFade == null || destination == null || playerCamera == null || IsRunning || HasCompleted)
                return false;
            cameraStart = playerCamera.localPosition;
            previousLock = player.ControlsLocked;
            travelled = false;
            controlsReleased = false;
            IsRunning = true;
            player.SetControlsLocked(true);
            director.time = 0;
            director.Play();
            return true;
        }

        public void ApplyCue(S03TimelineCue cue, float progress)
        {
            if (!IsRunning) return;
            switch (cue)
            {
                case S03TimelineCue.DoorOpen:
                    door.SetTimelineOpenProgress(progress);
                    break;
                case S03TimelineCue.CameraMotion:
                    playerCamera.localPosition = cameraStart + cameraOffset * Mathf.Sin(Mathf.PI * progress);
                    break;
                case S03TimelineCue.FadeToWhite:
                    screenFade.SetOpacity(Mathf.SmoothStep(0f,1f,progress));
                    break;
                case S03TimelineCue.HoldWhite:
                    screenFade.SetOpacity(1f);
                    break;
                case S03TimelineCue.FadeFromWhite:
                    screenFade.SetOpacity(1f-Mathf.SmoothStep(0f,1f,progress));
                    break;
                case S03TimelineCue.TravelToArena:
                    if (!travelled)
                    {
                        // The arrival is always hidden, even if a frame skips over the white boundary.
                        screenFade.SetOpacity(1f);
                        player.Teleport(destination.position,destination.rotation);
                        travelled = true;
                    }
                    break;
                case S03TimelineCue.DoorSound:
                    if (audioCues != null) audioCues.PlayDoorSound();
                    break;
                case S03TimelineCue.CrowdFadeIn:
                    if (audioCues != null) audioCues.SetTimelineCrowdProgress(progress);
                    break;
                case S03TimelineCue.RestoreControls:
                    if (!controlsReleased)
                    {
                        screenFade.Clear();
                        playerCamera.localPosition = cameraStart;
                        player.SetControlsLocked(previousLock);
                        controlsReleased = true;
                    }
                    break;
            }
        }

        private void OnDirectorStopped(PlayableDirector stoppedDirector)
        {
            if (!IsRunning) return;
            HasCompleted = controlsReleased;
            Restore(!HasCompleted);
        }

        private void Restore(bool interrupted)
        {
            if (!IsRunning) return;
            IsRunning = false;
            if (player != null && !controlsReleased) player.SetControlsLocked(previousLock);
            if (screenFade != null) screenFade.Clear();
            if (playerCamera != null) playerCamera.localPosition = cameraStart;
            if (door != null) door.SetTimelineOpenProgress(1f);
            if (interrupted && audioCues != null) audioCues.StopCrowdAmbience();
        }

        private void OnDisable()
        {
            if (director != null)
            {
                director.stopped -= OnDirectorStopped;
                if (IsRunning) director.Stop();
            }
            Restore(true);
        }
    }
}
