using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Capstone.Direction.Workspace.S03;

namespace Capstone.Direction.Workspace.S06
{
    [Serializable] public sealed class S06AwardState
    {
        public int placing = 2;
        public string nextSequence = "AwardCeremony";
        public bool intentionalFirstPerson = true;
        public bool firstTraumaExperienced;
        public bool medalTransformationPending = true;
    }

    // Timeline-facing adapter. No combat code or scene loading is performed here.
    public sealed class S06BoutSequence : MonoBehaviour
    {
        public DirectionSequence sequence;
        public DirectionScreen screen;
        public S03ControlLockAdapter controlLock;
        public Transform player, opponent, cameraPivot, subjectiveEmitter;
        public AudioSource crowd, outsideStarter, approach, ringing;
        public AudioLowPassFilter crowdFilter;
        public Volume subjectiveVolume;
        public TMPro.TMP_Text status;
        public S06AwardState awardState = new S06AwardState();
        public UnityEvent onAwardReady = new UnityEvent();
        public string Phase { get; private set; } = "Ready";
        public bool Completed { get; private set; }
        public int CompletionCount { get; private set; }
        public bool Active => sequence && sequence.IsRunning;
        public float EffectStrength { get; private set; }
        public float watchdogSeconds = 20;
        float phaseStart, runStart, crowdVolume, cutoff;
        Vector3 pivotPosition, opponentPosition;
        Quaternion pivotRotation;
        bool owned, disposed;
        void Start() { SetStatus("S06 TEST | E: start bout  X: cancel | WASD / mouse\nSynthetic starter / flyby / ringing = TEMP AUDIO"); }
        void Update()
        {
            var keys = Keyboard.current;
            if (keys != null && keys.eKey.wasPressedThisFrame) BeginBout();
            if (keys != null && keys.xKey.wasPressedThisFrame) CancelBout();
            if (!owned) return;
            try
            {
                if (Time.realtimeSinceStartup - runStart > watchdogSeconds) { CancelBout(); return; }
                float t = Time.realtimeSinceStartup - phaseStart;
                float target = Phase == "Approach" ? Mathf.SmoothStep(0, .7f, t / 2.2f) : Phase == "Hallucination" ? 1 : 0;
                EffectStrength = Mathf.MoveTowards(EffectStrength, target, Time.unscaledDeltaTime * 3);
                subjectiveVolume.weight = EffectStrength;
                crowd.volume = Mathf.Lerp(crowdVolume, crowdVolume * .12f, EffectStrength);
                crowdFilter.cutoffFrequency = Mathf.Lerp(cutoff, 650, EffectStrength);
                approach.volume = Phase == "Approach" ? Mathf.Lerp(.06f, .65f, Mathf.Clamp01(t / 2.2f)) : 0;
                subjectiveEmitter.position = player.position + player.forward * Mathf.Lerp(14, .6f, Mathf.Clamp01(t / 2.2f)) + Vector3.up * 1.5f;
                float shake = EffectStrength * .018f;
                cameraPivot.localPosition = pivotPosition + new Vector3(Mathf.Sin(t * 39), Mathf.Sin(t * 31), 0) * shake;
                cameraPivot.localRotation = pivotRotation * Quaternion.Euler(EffectStrength * Mathf.Sin(t * 12) * .6f, 0, EffectStrength * Mathf.Sin(t * 17) * .5f);
                bool disrupted = Phase == "Disrupted";
                opponent.localPosition = opponentPosition + Vector3.forward * Mathf.Sin(t * (disrupted ? 4 : 2.8f)) * (disrupted ? .28f : .12f);
                if (screen.overlay) screen.overlay.color = new Color(.68f, .74f, .62f, Phase == "Hallucination" ? .14f : 0);
            }
            catch (Exception error) { Debug.LogException(error, this); CancelBout(); }
        }
        public void BeginBout()
        {
            if (!isActiveAndEnabled || Active || Completed) return;
            if (!sequence || !screen || !controlLock || !player || !opponent || !cameraPivot || !subjectiveEmitter || !crowd || !crowdFilter || !outsideStarter || !approach || !ringing || !subjectiveVolume)
            { Debug.LogError("S06 configuration incomplete; start rejected before control lock.", this); return; }
            sequence.Play();
        }
        public void OnStarted()
        {
            disposed = false; owned = true; runStart = Time.realtimeSinceStartup;
            crowdVolume = crowd.volume; cutoff = crowdFilter.cutoffFrequency;
            pivotPosition = cameraPivot.localPosition; pivotRotation = cameraPivot.localRotation;
            opponentPosition = opponent.localPosition;
            controlLock.Acquire(); SetPhase("Boxing", "Exhibition bout: guard / rhythm test (no combat damage)");
        }
        public void StarterShot() { if (!owned || Phase != "Boxing") return; outsideStarter.Play(); SetPhase("StarterShot", "Outside athletics starter pistol — no weapon in arena"); }
        public void BeginApproach() { if (!owned || Phase != "StarterShot") return; approach.Play(); SetPhase("Approach", "Subjective incoming sound grows; crowd becomes distant"); }
        public void BriefHallucination() { if (!owned || Phase != "Approach") return; approach.Stop(); ringing.Play(); SetPhase("Hallucination", "Brief trauma flash — same physical arena, no battlefield travel"); }
        public void DisruptBout() { if (!owned || Phase != "Hallucination") return; ringing.Stop(); SetPhase("Disrupted", "Rhythm broken; scripted opponent pressure (combat placeholder)"); }
        public void ShowSecondPlace() { if (!owned || Phase != "Disrupted") return; SetPhase("SecondPlace", "RESULT: 2nd place / silver — award ceremony pending"); }
        public void OnCompleted()
        {
            bool success = owned && Phase == "SecondPlace";
            Restore();
            if (!success) { SetPhase("Cancelled", "Interrupted before result; no award handoff. E to retry."); return; }
            Completed = true; awardState.firstTraumaExperienced = true; CompletionCount++;
            SetPhase("Completed", "2nd PLACE | AwardReady event sent once\nNext: intentional first-person award ceremony (not built here)");
            try { onAwardReady.Invoke(); } catch (Exception e) { Debug.LogException(e, this); }
        }
        public void CancelBout() { if (sequence && sequence.IsRunning) sequence.Cancel(); else if (owned) OnCancelled(); }
        public void OnCancelled() { Restore(); if (!Completed) SetPhase("Cancelled", "Cancelled / timed out — camera, input and audio restored. E to retry."); }
        void Restore()
        {
            if (!owned || disposed) return;
            disposed = true; owned = false;
            if (outsideStarter) outsideStarter.Stop(); if (approach) approach.Stop(); if (ringing) ringing.Stop();
            if (crowd) crowd.volume = crowdVolume; if (crowdFilter) crowdFilter.cutoffFrequency = cutoff;
            if (subjectiveVolume) subjectiveVolume.weight = 0; EffectStrength = 0; if (screen) screen.Clear();
            if (cameraPivot) { cameraPivot.localPosition = pivotPosition; cameraPivot.localRotation = pivotRotation; }
            if (opponent) opponent.localPosition = opponentPosition; if (controlLock) controlLock.Release();
        }
        void SetPhase(string phase, string message) { Phase = phase; phaseStart = Time.realtimeSinceStartup; SetStatus("S06 TEST | X: cancel\n" + message + "\nTEMP synthetic starter / flyby / tinnitus"); }
        void SetStatus(string message) { if (status) status.text = message; }
        void OnDisable() { CancelBout(); Restore(); }
        void OnDestroy() { Restore(); }
    }
}
