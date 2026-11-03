using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;

namespace Capstone.Direction.Workspace.S06.Editor
{
    [InitializeOnLoad]
    public static class S06Validation
    {
        const string Key = "S06.Validation.Pending";
        static S06BoutSequence subject;
        static float start, stageStart;
        static int stage;
        static Vector3 position;
        static readonly HashSet<string> phases = new HashSet<string>();
        static bool checkedEffect;
        static int lastSecond = -1;
        static int awardEvents;
        static float completedAt = -1;
        static S06Validation() { EditorApplication.update += Tick; }
        [MenuItem("Capstone/Direction/S06 Workspace/Run Play validation (exits Play when done)")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene; validation will not save or discard it.");
            EditorSceneManager.OpenScene(S06WorkbenchBuilder.ScenePath);
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            try
            {
                if (!subject)
                {
                    subject = UnityEngine.Object.FindFirstObjectByType<S06BoutSequence>();
                    if (!subject) return;
                    start = stageStart = Time.realtimeSinceStartup; stage = 0; phases.Clear(); checkedEffect = false;
                    position = subject.player.position; awardEvents = 0; completedAt = -1; subject.onAwardReady.AddListener(() => awardEvents++); subject.BeginBout(); subject.BeginBout();
                    Check(subject.Active && subject.controlLock.IsAcquired, "start and duplicate guard");
                    foreach (var clip in new[] { subject.outsideStarter.clip, subject.approach.clip, subject.ringing.clip })
                    {
                        Check(clip && clip.length > .2f, "imported PCM test clip duration");
                        var data = new float[clip.samples * clip.channels]; Check(clip.GetData(data, 0) && Array.Exists(data, sample => Mathf.Abs(sample) > .01f), "non-silent test audio samples");
                    }
                    Capture("S06-start.png");
                }
                float elapsed = Time.realtimeSinceStartup - stageStart;
                if ((int)elapsed != lastSecond) { lastSecond = (int)elapsed; Debug.Log("S06_CLOCK stage=" + stage + " realtime=" + elapsed + " director=" + subject.sequence.director.time + " phase=" + subject.Phase + " state=" + subject.sequence.director.state); }
                if (Time.realtimeSinceStartup - start > 35) throw new Exception("Validation deadline exceeded");
                if (stage == 0)
                {
                    phases.Add(subject.Phase);
                    if (!checkedEffect && subject.Phase == "Hallucination" && subject.EffectStrength > .75f)
                    {
                        Check(subject.crowd.volume < .2f && subject.crowdFilter.cutoffFrequency < 6000, "subjective crowd duck and filter");
                        checkedEffect = true;
                    }
                    if (!subject.Completed) return;
                    if (completedAt < 0) { completedAt = Time.realtimeSinceStartup; return; }
                    if (Time.realtimeSinceStartup - completedAt < 1) return;
                    foreach (var phase in new[] { "Boxing", "StarterShot", "Approach", "Hallucination", "Disrupted", "SecondPlace" }) Check(phases.Contains(phase), "Timeline signal: " + phase);
                    Check(checkedEffect, "brief hallucination effect sampled");
                    Check(awardEvents == 1 && subject.CompletionCount == 1 && subject.awardState.placing == 2 && subject.awardState.intentionalFirstPerson, "one award handoff and state");
                    Check(Camera.main.GetComponent<CinemachineBrain>().ActiveVirtualCamera.Name == "CM_ThirdPerson", "rendered brain returned to third person");
                    Check(Vector2.Distance(new Vector2(position.x, position.z), new Vector2(subject.player.position.x, subject.player.position.z)) < .01f, "no battlefield teleport");
                    Restored(); Capture("S06-result.png"); subject.BeginBout(); Check(subject.CompletionCount == 1 && !subject.Active, "completed sequence cannot replay");
                    Reload(); stage = 1; return;
                }
                if (stage == 1 && subject.Phase == "Approach" && elapsed > 4.2f)
                {
                    subject.CancelBout(); Restored(); Check(subject.CompletionCount == 0, "cancel emits no award"); subject.BeginBout(); stage = 2; stageStart = Time.realtimeSinceStartup; return;
                }
                if (stage == 2 && subject.Phase == "Hallucination")
                {
                    subject.CancelBout(); Restored(); Check(subject.CompletionCount == 0, "hallucination cancellation");
                    subject.sequence.timelineTimeout = .2f; subject.BeginBout(); stage = 3; stageStart = Time.realtimeSinceStartup; return;
                }
                if (stage == 3 && elapsed > .7f)
                {
                    Check(!subject.Active, "common timeline timeout"); Restored();
                    subject.sequence.timelineTimeout = 60; subject.watchdogSeconds = .2f; subject.BeginBout(); stage = 4; stageStart = Time.realtimeSinceStartup; return;
                }
                if (stage == 4 && elapsed > .7f)
                {
                    Check(!subject.Active, "independent watchdog"); Restored();
                    subject.watchdogSeconds = 20; subject.BeginBout(); subject.enabled = false; Restored();
                    Check(subject.CompletionCount == 0, "disable recovery emits no award");
                    subject.enabled = true;
                    UnityEngine.Events.UnityAction fault = () => { throw new InvalidOperationException("S06_EXPECTED_FAULT"); };
                    subject.sequence.onStarted.AddListener(fault); subject.BeginBout(); subject.sequence.onStarted.RemoveListener(fault);
                    Check(!subject.Active, "callback exception cancels"); Restored();
                    Finish(true, "normal / duplicate / Timeline / perception / rank2 / cancel approach / cancel flash / timeout / watchdog / disable / callback exception recovery");
                }
            }
            catch (Exception error) { Finish(false, error.ToString()); }
        }
        static void Reload()
        {
            // Reload only the disposable test root during Play; never save this runtime change.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(S06WorkbenchBuilder.Root + "/Prefabs/S06_TestRig.prefab");
            UnityEngine.Object.DestroyImmediate(subject.transform.root.gameObject);
            var root = UnityEngine.Object.Instantiate(prefab); subject = root.GetComponentInChildren<S06BoutSequence>();
            stageStart = Time.realtimeSinceStartup; subject.BeginBout();
        }
        static void Restored()
        {
            Check(!subject.controlLock.IsAcquired && !subject.controlLock.prototypePlayer.ControlsLocked, "control restored");
            Check(subject.subjectiveVolume.weight == 0 && subject.screen.overlay.color.a == 0, "screen restored");
            Check(!subject.approach.isPlaying && !subject.ringing.isPlaying && !subject.outsideStarter.isPlaying, "subjective audio stopped");
            Check(Mathf.Abs(subject.crowd.volume - .38f) < .001f && subject.crowdFilter.cutoffFrequency > 21000, "ambient restored");
            Check(subject.sequence.cinematicCamera.Priority.Value == 0, "camera priority restored");
        }
        static void Check(bool value, string label) { if (!value) throw new Exception(label); Debug.Log("S06_CHECK_PASS " + label); }
        static void Capture(string name)
        {
            string folder = Environment.GetEnvironmentVariable("S06_VALIDATION_DIR"); if (string.IsNullOrEmpty(folder)) return;
            var camera = Camera.main; var old = camera.targetTexture; var active = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24); var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try { camera.targetTexture = target; camera.Render(); RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name), pixels.EncodeToPNG()); }
            finally { camera.targetTexture = old; RenderTexture.active = active; UnityEngine.Object.DestroyImmediate(pixels); target.Release(); UnityEngine.Object.DestroyImmediate(target); }
        }
        static void Finish(bool success, string message)
        {
            SessionState.SetBool(Key, false);
            if (subject) subject.CancelBout();
            Debug.Log((success ? "S06_VALIDATION_PASS " : "S06_VALIDATION_FAIL ") + message);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1); else EditorApplication.isPlaying = false;
        }
    }
}
