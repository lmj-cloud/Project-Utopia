using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using S03Prototype;

namespace Capstone.Direction.Workspace.S03
{
    // Connects the existing prototype door to the existing common DirectionSequence.
    // Same-scene teleport is intentional: no Build Settings or production scenes are changed.
    public sealed class S03SceneBridge : MonoBehaviour
    {
        public DirectionSequence sequence;
        public S03DoorInteraction door;
        public S03FirstPersonPlayer player;
        public S03ControlLockAdapter controlLock;
        public S03ThirdPersonRig gameplayRig;
        public CinemachineCamera cinematicCamera;
        public Transform destination, arenaShotStart, arenaShotEnd, arenaLookTarget;
        public GameObject homeLights, arenaLights;
        public AudioClip doorClip;
        [Tooltip("Optional real home ambience. No invented home sound is generated.")]
        public AudioClip homeAmbience;
        public CanvasGroup prompt;
        public TMPro.TMP_Text status;
        public bool testOnly = true;

        [Header("S03 cinematic transition (legacy events remain compatible)")]
        public bool cinematicTransition;
        [Min(0.5f)] public float whiteBuildDuration = 3f;
        [Min(0.5f)] public float revealDuration = 1.8f;
        [Min(1)] public float overallTimeout = 14f;
        public Camera outputCamera;
        public Transform doorFocus, doorDollyEnd;
        public S03CinematicLight lightEffects;
        public AnimationCurve whiteCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float CoverageAtTransfer { get; private set; }
        public float SecondsToTransfer { get; private set; }
        public float DoorProgress { get; private set; }
        public string LastFailure { get; private set; }
        public DirectionScreen ActiveScreen => borrowedScreen ? borrowedScreen : sequence ? sequence.screen : null;

        PrioritySettings originalPriority;
        bool originalEnabled, armed, arrived, complete, pan;
        bool originalHomeLights, originalArenaLights;
        float beganAt;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        LensSettings originalLens;
        Vector3 dollyStart;
        Quaternion dollyRotation;
        float dollyFov, revealBegan;
        bool doorReady, revealing;
        Coroutine transition, cameraRestore;
        DirectionScreen borrowedScreen;

        public bool IsRunning => armed;
        public bool HasArrived => arrived;
        public bool HasCompleted => complete;

        void Start()
        {
            if (testOnly && SceneManager.sceneCount != 1)
            {
                Debug.LogError("S03 test must be opened alone. Stop Play and open the generated S03_Workbench scene in Single mode.", this);
                // Disable the whole generated prefab, including its camera and AudioListener.
                transform.root.gameObject.SetActive(false);
                return;
            }
            if (homeAmbience && sequence && sequence.audioSystem)
                sequence.audioSystem.Loop(homeAmbience, DirectionBus.Ambient, 0.25f, 0.5f);
            SetStatus("WASD: move   Mouse: look   Approach the door and press E\nEsc: release cursor / cancel   Click Game: capture cursor");
        }

        void Update()
        {
            if (!armed) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cancel();
                return;
            }
            if (Time.realtimeSinceStartup - beganAt > (cinematicTransition ? overallTimeout : 8f))
            {
                LastFailure = "S03 transition timed out";
                Debug.LogWarning(LastFailure + ". Restart Play to retry.", this);
                Cancel();
                return;
            }
            if (revealing && lightEffects)
                lightEffects.SetAmount(1f - Mathf.SmoothStep(0, 1, (Time.realtimeSinceStartup - revealBegan) / revealDuration));
            if (armed && pan && arrived && sequence.director && sequence.director.state == PlayState.Playing)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)sequence.director.time / 3f));
                cinematicCamera.transform.position = Vector3.Lerp(arenaShotStart.position, arenaShotEnd.position, t);
                cinematicCamera.transform.LookAt(arenaLookTarget.position);
            }
        }

        // S03DoorInteraction.OpeningStarted invokes this before its Opened event.
        public void BeginDoorShot()
        {
            if (armed || complete || !sequence || sequence.IsRunning || !controlLock || !cinematicCamera)
                return;
            armed = true;
            beganAt = Time.realtimeSinceStartup;
            originalPriority = cinematicCamera.Priority;
            originalEnabled = cinematicCamera.enabled;
            cameraPosition = cinematicCamera.transform.position;
            cameraRotation = cinematicCamera.transform.rotation;
            originalLens = cinematicCamera.Lens;
            if (cameraRestore != null) { StopCoroutine(cameraRestore); cameraRestore = null; }
            originalHomeLights = homeLights && homeLights.activeSelf;
            originalArenaLights = arenaLights && arenaLights.activeSelf;
            controlLock.Acquire();
            doorReady = false;
            LastFailure = null;
            if (cinematicTransition && outputCamera)
            {
                // Begin at the rendered camera, never at the old side-on authored transform.
                dollyStart = outputCamera.transform.position;
                dollyRotation = outputCamera.transform.rotation;
                dollyFov = outputCamera.fieldOfView;
                cinematicCamera.transform.SetPositionAndRotation(dollyStart, dollyRotation);
                var lens = cinematicCamera.Lens; lens.FieldOfView = dollyFov; cinematicCamera.Lens = lens;
            }
            cinematicCamera.enabled = true;
            cinematicCamera.Priority = 100;
            if (prompt) prompt.alpha = 0;
            if (sequence.audioSystem)
            {
                sequence.audioSystem.FadeOut(DirectionBus.Ambient, cinematicTransition ? 2.4f : 0.6f);
                sequence.audioSystem.Play(doorClip);
            }
            SetStatus("S03: door opens / controls locked");
            if (cinematicTransition) transition = StartCoroutine(CinematicTransition());
        }

        public void StartTransition()
        {
            if (!armed || complete || arrived || sequence.IsRunning) return;
            doorReady = true;
            if (cinematicTransition) return; // Door Opened is a readiness signal; the three-second build owns timing.
            sequence.Play();
            if (!sequence.IsRunning)
            {
                Debug.LogWarning("S03 could not acquire the shared DirectionSequence. Cancelling.", this);
                Cancel();
            }
        }

        IEnumerator CinematicTransition()
        {
            float elapsed = 0;
            while (armed && elapsed < whiteBuildDuration)
            {
                elapsed = Time.realtimeSinceStartup - beganAt;
                if (elapsed >= whiteBuildDuration) break;
                if (!Step(() => ApplyDoorProgress(elapsed / whiteBuildDuration))) yield break;
                yield return null;
            }
            if (!armed || !Step(() => ApplyDoorProgress(1))) yield break;
            // Scaled prototype door animation can be paused. Keep coverage and let the watchdog cancel safely.
            while (armed && !doorReady) yield return null;
            if (!armed) yield break;
            if (!Step(() =>
            {
                // Temporarily bypass only the fixed 0.5/0.8 fades. Common Timeline, audio and events stay in charge.
                borrowedScreen = sequence.screen;
                sequence.screen = null;
                sequence.Play();
                if (!sequence.IsRunning || !arrived) throw new InvalidOperationException("S03 shared sequence could not begin safely.");
            })) yield break;
            revealBegan = Time.realtimeSinceStartup;
            revealing = true;
            // Reuse DirectionScreen.Fade for the longer reveal; pre-transfer coverage uses the authored ease curve.
            yield return borrowedScreen.Fade(Color.white, 0, revealDuration);
            revealing = false;
            if (lightEffects) lightEffects.ResetLight();
            RestoreScreenReference();
            transition = null;
        }

        bool Step(Action action)
        {
            try { action(); return armed; }
            catch (Exception error)
            {
                LastFailure = error.Message;
                Debug.LogException(error, this);
                Cancel();
                return false;
            }
        }

        void ApplyDoorProgress(float value)
        {
            if (!doorFocus || !doorDollyEnd || !ActiveScreen || !ActiveScreen.overlay)
                throw new InvalidOperationException("S03 Dolly/white-light references are missing.");
            DoorProgress = Mathf.Clamp01(value);
            float t = Mathf.SmoothStep(0, 1, DoorProgress);
            Vector3 end = doorDollyEnd.position;
            Vector3 p1 = Vector3.Lerp(dollyStart, new Vector3(end.x, dollyStart.y, end.z), 0.42f);
            p1.y = Mathf.Max(dollyStart.y + 0.55f, 2.75f);
            Vector3 p2 = Vector3.Lerp(dollyStart, end, 0.78f);
            p2.y = Mathf.Max(p2.y, end.y + 0.7f);
            float u = 1f - t;
            var position = u * u * u * dollyStart + 3 * u * u * t * p1 + 3 * u * t * t * p2 + t * t * t * end;
            var rotation = Quaternion.LookRotation(doorFocus.position + Vector3.up * (0.65f * (1f - t)) - position, Vector3.up);
            cinematicCamera.transform.SetPositionAndRotation(position, Quaternion.Slerp(dollyRotation, rotation, t));
            var lens = cinematicCamera.Lens; lens.FieldOfView = Mathf.Lerp(dollyFov, dollyFov - 5f, t); cinematicCamera.Lens = lens;
            if (lightEffects) lightEffects.SetAmount(t);
            // Bloom and a world-space doorway glow lead the fade; opacity gradually closes the final exposure gap.
            float alpha = whiteCurve.Evaluate(DoorProgress * DoorProgress);
            if (DoorProgress >= 1) alpha = 1;
            ActiveScreen.overlay.color = new Color(1, 1, 1, Mathf.Clamp01(alpha));
        }

        // DirectionSequence guarantees the fade has completed before invoking this event.
        public void TransferWhileCovered()
        {
            if (!armed || arrived) return;
            if (!ActiveScreen || !ActiveScreen.overlay || ActiveScreen.overlay.color.a < 0.999f ||
                !player || !destination || !arenaShotStart || !arenaShotEnd || !arenaLookTarget)
                throw new System.InvalidOperationException("S03 transfer requires full white coverage and valid rig references.");
            CoverageAtTransfer = ActiveScreen.overlay.color.a;
            SecondsToTransfer = Time.realtimeSinceStartup - beganAt;
            player.Teleport(destination.position, destination.rotation);
            gameplayRig.Snap();
            if (homeLights) homeLights.SetActive(false);
            if (arenaLights) arenaLights.SetActive(true);
            cinematicCamera.transform.position = arenaShotStart.position;
            cinematicCamera.transform.LookAt(arenaLookTarget.position);
            arrived = true;
            if (cinematicTransition)
            {
                var lens = cinematicCamera.Lens; lens.FieldOfView = 58; cinematicCamera.Lens = lens;
            }
            SetStatus("S03: arena / temporary crowd chatter ambience");
        }

        // Called by real Timeline Signal emitters; no duplicate screen/audio implementation.
        public void StartArenaShot() { if (armed && arrived) pan = true; }
        public void EndArenaShot()
        {
            pan = false;
            if (armed && arrived)
            {
                cinematicCamera.transform.position = arenaShotEnd.position;
                cinematicCamera.transform.LookAt(arenaLookTarget.position);
            }
        }

        public void Complete()
        {
            if (!armed) return;
            complete = arrived;
            Cleanup();
            SetStatus(complete ? "S03 COMPLETE: third-person control restored\nRestart Play to repeat the door sequence." : "S03 stopped before arrival.");
        }

        public void Cancel()
        {
            if (!armed) return;
            // DirectionSequence invokes Cancelled synchronously. Cleanup is idempotent.
            if (sequence && sequence.IsRunning) sequence.Cancel();
            Cleanup();
            SetStatus("S03 CANCELLED: controls restored. Restart Play to retry.");
        }

        public void OnSequenceCancelled()
        {
            Cleanup();
            SetStatus("S03 CANCELLED: controls restored. Restart Play to retry.");
        }

        void Cleanup()
        {
            if (!armed) return;
            armed = false;
            pan = false;
            revealing = false;
            if (transition != null) { StopCoroutine(transition); transition = null; }
            RestoreScreenReference();
            if (lightEffects) lightEffects.ResetLight();
            if (cinematicCamera)
            {
                cinematicCamera.Priority = originalPriority;
                // Keep the outgoing pose alive for Cinemachine's return blend, then restore the standby camera.
                if (isActiveAndEnabled && gameObject.activeInHierarchy) cameraRestore = StartCoroutine(RestoreCameraAfterBlend());
                else RestoreCameraNow();
            }
            if (!arrived)
            {
                if (homeLights) homeLights.SetActive(originalHomeLights);
                if (arenaLights) arenaLights.SetActive(originalArenaLights);
                if (homeAmbience && sequence && sequence.audioSystem && sequence.audioSystem.isActiveAndEnabled && gameObject.activeInHierarchy)
                    sequence.audioSystem.Loop(homeAmbience, DirectionBus.Ambient, 0.25f, 0.5f);
            }
            if (sequence && sequence.screen) sequence.screen.Clear();
            if (controlLock) controlLock.Release();
        }

        void RestoreScreenReference()
        {
            if (!borrowedScreen) return;
            if (sequence) sequence.screen = borrowedScreen;
            borrowedScreen.Clear();
            borrowedScreen = null;
        }

        IEnumerator RestoreCameraAfterBlend()
        {
            yield return new WaitForSecondsRealtime(1.1f);
            RestoreCameraNow();
            cameraRestore = null;
        }

        void RestoreCameraNow()
        {
            if (!cinematicCamera) return;
            cinematicCamera.enabled = originalEnabled;
            cinematicCamera.Lens = originalLens;
            if (!arrived) cinematicCamera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        }

        void SetStatus(string value) { if (status) status.text = value; }
        void OnDisable()
        {
            Cancel();
            if (cameraRestore != null) { StopCoroutine(cameraRestore); cameraRestore = null; RestoreCameraNow(); }
        }
    }
}
