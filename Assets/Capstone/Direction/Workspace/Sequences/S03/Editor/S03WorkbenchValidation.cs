using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

namespace Capstone.Direction.Workspace.S03.Editor
{
    // Explicit batch entry points only. Does not run on ordinary imports or save scene changes.
    public static class S03WorkbenchValidation
    {
        const string Pending = "S03.Workbench.Validation.Pending";
        const string Mode = "S03.Workbench.Validation.Mode";
        static S03SceneBridge bridge;
        static double startedAt;
        static float beganAt, completedAt;
        static int startFrame;
        static bool triggered, cancelled;
        static float lastAlpha;
        static bool midSample, arenaSample, whiteSample;
        static string captureDirectory;
        static float initialDoorDistance;

        public static void RunCompletionBatch() => Run("completion");
        public static void RunCancellationBatch() => Run("cancellation");
        public static void RunExceptionBatch() => Run("exception");
        public static void RunTimeoutBatch() => Run("timeout");
        public static void RunRevealCancellationBatch() => Run("reveal-cancellation");
        public static void RunDisableBatch() => Run("disable");

        static void Run(string mode)
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch validation only.");
            var path = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Capstone/Direction/Workspace/Sequences/S03/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.Contains("S03_Workbench_"))
                .OrderByDescending(p => p).FirstOrDefault();
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Generate the S03 workbench first.");
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var args = Environment.GetCommandLineArgs();
            int captureArg = Array.IndexOf(args, "-s03CaptureDir");
            SessionState.SetString("S03.Validation.CaptureDir", captureArg >= 0 && captureArg + 1 < args.Length ? args[captureArg + 1] : "");
            SessionState.SetString(Mode, mode);
            SessionState.SetBool(Pending, true);
            Attach();
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        static void Resume() { if (SessionState.GetBool(Pending, false)) Attach(); }

        static void Attach()
        {
            startedAt = EditorApplication.timeSinceStartup;
            bridge = null; triggered = false; cancelled = false; completedAt = 0;
            lastAlpha = 0; midSample = false; arenaSample = false; whiteSample = false;
            captureDirectory = SessionState.GetString("S03.Validation.CaptureDir", "");
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup - startedAt > 45)
                    throw new TimeoutException("S03 validation did not complete within 45 editor seconds.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                if (!bridge) bridge = UnityEngine.Object.FindFirstObjectByType<S03SceneBridge>();
                if (!bridge || Time.frameCount < 3) return;
                if (!triggered)
                {
                    Require(bridge.cinematicTransition, "Existing scene was not upgraded to the cinematic configuration.");
                    Require(bridge.lightEffects && bridge.lightEffects.volume && bridge.lightEffects.volume.sharedProfile, "Missing dedicated URP Volume.");
                    Capture("00-home");
                    bridge.player.Teleport(new Vector3(0, 0.05f, 2.2f), Quaternion.identity);
                    bridge.gameplayRig.Snap();
                    Physics.SyncTransforms();
                    Require(bridge.door.TryOpen(), "Door interaction raycast/start failed.");
                    Require(!bridge.door.TryOpen(), "Duplicate door interaction was not rejected.");
                    Require(bridge.controlLock.IsAcquired && bridge.player.ControlsLocked, "Control lock not acquired.");
                    Require(Vector3.Distance(bridge.cinematicCamera.transform.position, bridge.outputCamera.transform.position) < 0.02f, "Door camera jumped from rendered pose.");
                    initialDoorDistance = Vector3.Distance(bridge.cinematicCamera.transform.position, bridge.doorDollyEnd.position);
                    if (SessionState.GetString(Mode, "") == "timeout") bridge.overallTimeout = 1.8f;
                    startFrame = Time.frameCount;
                    beganAt = Time.realtimeSinceStartup;
                    triggered = true;
                    Debug.Log("S03_VALIDATION_STARTED mode=" + SessionState.GetString(Mode, "") + " frame=" + startFrame);
                    return;
                }
                float elapsed = Time.realtimeSinceStartup - beganAt;
                string mode = SessionState.GetString(Mode, "");
                if (bridge.IsRunning && !bridge.HasArrived && bridge.DoorProgress < 0.99f)
                {
                    Require(bridge.ActiveScreen.overlay.color.a + 0.002f >= lastAlpha, "White build was not monotonic.");
                    lastAlpha = bridge.ActiveScreen.overlay.color.a;
                    if (elapsed < 2.9f) Require(!bridge.HasArrived, "Arrival happened before the three-second light build.");
                }
                if (!midSample && elapsed >= 1.5f && bridge.IsRunning)
                {
                    midSample = true;
                    Require(!bridge.HasArrived && bridge.DoorProgress > 0.35f && bridge.DoorProgress < 0.85f, "Dolly timing is not gradual: progress=" + bridge.DoorProgress.ToString("F3") + ", wall elapsed=" + elapsed.ToString("F3"));
                    Require(bridge.ActiveScreen.overlay.color.a > 0.05f && bridge.ActiveScreen.overlay.color.a < 0.75f, "Midpoint white opacity should be partial.");
                    Require(bridge.lightEffects.Amount > 0.2f && bridge.lightEffects.Exposure > 0.1f, "World light/Bloom/exposure did not rise.");
                    Require(Vector3.Distance(bridge.cinematicCamera.transform.position, bridge.doorDollyEnd.position) < initialDoorDistance * 0.9f, "Camera did not advance toward the door.");
                    Capture("01-door-dolly-light");
                    if (mode == "cancellation") { bridge.Cancel(); cancelled = true; }
                    if (mode == "disable") { bridge.enabled = false; cancelled = true; }
                    if (mode == "exception") bridge.destination = null; // Expected covered-transfer exception, then recovery.
                }
                if (!whiteSample && bridge.IsRunning && bridge.DoorProgress >= 0.985f && !bridge.HasArrived)
                {
                    whiteSample = true;
                    Capture("02-white-coverage");
                }
                if (mode == "reveal-cancellation" && !cancelled && bridge.HasArrived && elapsed > 3.5f)
                {
                    bridge.Cancel(); cancelled = true;
                }
                if (mode != "completion")
                {
                    bool recoveryExpected = cancelled || mode == "timeout" || mode == "exception";
                    if (recoveryExpected && !bridge.IsRunning && elapsed > 4.5f)
                    {
                        Require(!bridge.sequence.IsRunning, "Cancel leaked transition.");
                        Require(bridge.HasArrived == (mode == "reveal-cancellation"), "Recovery changed arrival state unexpectedly.");
                        Require(!bridge.controlLock.IsAcquired && !bridge.player.ControlsLocked, "Cancel left controls locked.");
                        Require(bridge.sequence.screen != null, "Borrowed DirectionScreen reference was not restored.");
                        Require(bridge.sequence.screen.overlay.color.a < 0.001f, "Cancel left overlay visible.");
                        Require(bridge.lightEffects.Amount < 0.001f && Mathf.Abs(bridge.lightEffects.Exposure) < 0.001f, "Recovery leaked exposure/door light.");
                        Require(bridge.homeLights.activeSelf == !bridge.HasArrived && bridge.arenaLights.activeSelf == bridge.HasArrived, "Recovery left inconsistent stage lights.");
                        var recoveryBrain = bridge.outputCamera.GetComponent<CinemachineBrain>();
                        Require(recoveryBrain.ActiveVirtualCamera != null && recoveryBrain.ActiveVirtualCamera.Name == "CM_ThirdPerson", "Recovery did not return camera.");
                        Require(Time.frameCount - startFrame > 5, "Play frames did not advance.");
                        Finish(true, mode + " controls/camera/overlay/exposure/screen-reference restored; frames=" + (Time.frameCount - startFrame));
                    }
                    return;
                }
                if (bridge.HasArrived)
                {
                    Require(bridge.CoverageAtTransfer >= 0.999f, "Transfer exposed the world switch.");
                    Require(bridge.SecondsToTransfer >= 2.95f && bridge.SecondsToTransfer < (string.IsNullOrEmpty(captureDirectory) ? 4f : 12f), "White build was not approximately three seconds: " + bridge.SecondsToTransfer.ToString("F3") + " wall seconds; capture instrumentation=" + !string.IsNullOrEmpty(captureDirectory));
                }
                if (!arenaSample && bridge.HasArrived && elapsed > 5)
                {
                    arenaSample = true;
                    Capture("03-arena-reveal");
                    var eye = bridge.destination.position + Vector3.up * 1.6f;
                    foreach (var direction in new[] { Vector3.up, Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                        Require(Physics.Raycast(eye, direction, 60, Physics.DefaultRaycastLayers), "Indoor boundary is missing in direction " + direction);
                }
                if (!bridge.HasCompleted) return;
                if (completedAt <= 0) { completedAt = Time.realtimeSinceStartup; return; }
                if (Time.realtimeSinceStartup - completedAt < 1.2f) return; // Allow the 0.85-second return blend and standby restore.
                Require(bridge.HasArrived && !bridge.IsRunning, "Sequence did not arrive/finish.");
                Require(!bridge.controlLock.IsAcquired && !bridge.player.ControlsLocked, "Controls not restored.");
                Require(Vector3.Distance(bridge.player.transform.position, bridge.destination.position) < 0.25f, "Player not at arena destination.");
                Require(!bridge.homeLights.activeSelf && bridge.arenaLights.activeSelf, "Stage lights not switched.");
                Require(bridge.sequence.screen.overlay.color.a < 0.001f, "White overlay not cleared.");
                Require(bridge.lightEffects.Amount < 0.001f && Mathf.Abs(bridge.lightEffects.Exposure) < 0.001f, "Final exposure did not return to baseline.");
                Require(Vector3.Distance(bridge.cinematicCamera.transform.position, bridge.arenaShotEnd.position) < 0.15f, "Timeline camera signals/pan failed.");
                var brain = UnityEngine.Object.FindFirstObjectByType<CinemachineBrain>();
                Require(brain && brain.ActiveVirtualCamera != null && brain.ActiveVirtualCamera.Name == "CM_ThirdPerson", "Third-person camera did not return.");
                Require(bridge.sequence.audioSystem.GetComponentsInChildren<AudioSource>()
                    .Any(s => s.clip == bridge.sequence.crowdLoop && s.isPlaying && s.volume > 0.1f), "Crowd audio did not play/fade in.");
                Require(Time.frameCount - startFrame > 10, "Play frames did not advance.");
                Capture("04-third-person-arena");
                Finish(true, "3-second Dolly/light curve + full-white transfer + indoor boundary rays + Timeline + crowd + restore; transferSeconds=" + bridge.SecondsToTransfer.ToString("F3") + "; frames=" + (Time.frameCount - startFrame));
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }

        static void Capture(string name)
        {
            if (string.IsNullOrEmpty(captureDirectory)) return;
            Directory.CreateDirectory(captureDirectory);
            var camera = bridge.outputCamera;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(captureDirectory, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        static void Finish(bool success, string details)
        {
            if (bridge && bridge.IsRunning) bridge.Cancel(); // Clean up while objects are active, before Editor shutdown.
            SessionState.SetBool(Pending, false);
            EditorApplication.update -= Tick;
            if (success) Debug.Log("S03_RUNTIME_VALIDATION_PASS " + details);
            else Debug.LogError("S03_RUNTIME_VALIDATION_FAIL " + details);
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
