using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Capstone.Direction.Workspace.S03;
using Capstone.Direction.Workspace.S06;

namespace Capstone.Direction.Workspace.S07
{
    [Serializable] public sealed class S07WakeState
    {
        public int previousPlacing = 2;
        public bool rememberedFallenComrades;
        public string nextStoryPoint = "BedroomAfterNightmare";
    }

    // Only S07-owned objects are animated. No scene loading, global lighting or mixer edits.
    public sealed class S07AwardSequence : MonoBehaviour
    {
        public DirectionSequence sequence;
        public DirectionScreen screen;
        public S03ControlLockAdapter controlLock;
        public Transform actor, head, leftHand, rightHand, torso;
        public GameObject hands, medal, tags, pistol, thirdPersonBody;
        public GameObject awardEnvironment, warEnvironment, bedroomEnvironment;
        public Transform warSpawn, bedroomSpawn;
        public AudioSource awardAudio, warAudio, roomAudio, metalAudio, tinnitusAudio, breathAudio;
        public Volume traumaVolume;
        public TMPro.TMP_Text status;
        public UnityEvent onStarted = new UnityEvent(), onCompleted = new UnityEvent(), onCancelled = new UnityEvent();
        public S07WakeState wakeState = new S07WakeState();
        public float watchdogSeconds = 50;
        public bool autoStartStandalone = true;
        public bool Active => sequence && sequence.IsRunning;
        public bool Completed { get; private set; }
        public string Phase { get; private set; } = "Ready";
        public float Playhead { get; private set; }
        public float CoverAlpha => screen && screen.overlay ? screen.overlay.color.a : 0;
        public bool Swapped { get; private set; }
        public float SwapAlpha { get; private set; }
        public float WarSeconds { get; private set; }
        public int CompletionCount { get; private set; }
        public S06AwardState InputState { get; private set; }
        public float HeadYaw { get; private set; }
        public float HeadRoll { get; private set; }
        public bool InjectFaultOnSwap { get; set; } // Diagnostic hook, false in saved prefab.
        bool owned, finishRequested, directorReady;
        int startedFrame;
        float runStart, previousTick, warStart, savedTimeScale, savedFixedStep;
        Vector3 savedActor, savedHead, savedLeft, savedRight, savedTorso;
        Quaternion savedActorRotation, savedHeadRotation, savedRightRotation, savedTorsoRotation;
        bool[] savedActive;
        bool savedMedal, savedTags, savedPistol, savedHands, savedBody;
        float savedVolumeWeight;
        struct AudioSnapshot { public AudioSource source; public float volume; public int sample; public bool playing; }
        AudioSnapshot[] audioSnapshots;
        AudioSource[] Sources => new[] { awardAudio, warAudio, roomAudio, metalAudio, tinnitusAudio, breathAudio };

        System.Collections.IEnumerator Start() { yield return null; if (autoStartStandalone) StartStandalone(); else Message("Ready: E to inject 2nd-place test state"); }
        public void StartStandalone() => AcceptAward(new S06AwardState { placing = 2, firstTraumaExperienced = true });
        public bool AcceptAward(S06AwardState state)
        {
            if (!isActiveAndEnabled || Active || Completed || state == null || state.placing != 2 || !state.intentionalFirstPerson || !state.firstTraumaExperienced || !state.medalTransformationPending) return false;
            if (!sequence || !sequence.director || !sequence.director.playableAsset || !screen || !screen.overlay || !controlLock || !actor || !head || !leftHand || !rightHand || !torso || !hands || !medal || !tags || !pistol || !thirdPersonBody || !awardEnvironment || !warEnvironment || !bedroomEnvironment || !warSpawn || !bedroomSpawn || !traumaVolume) return false;
            foreach (var source in Sources) if (!source || !source.clip) return false;
            InputState = new S06AwardState { placing=state.placing, nextSequence=state.nextSequence, intentionalFirstPerson=state.intentionalFirstPerson, firstTraumaExperienced=state.firstTraumaExperienced, medalTransformationPending=state.medalTransformationPending };
            sequence.Play(); return Active;
        }
        public void SequenceStarted()
        {
            savedTimeScale = Time.timeScale; savedFixedStep = Time.fixedDeltaTime;
            savedActor = actor.position; savedActorRotation = actor.rotation;
            savedHead = head.localPosition; savedHeadRotation = head.localRotation;
            savedLeft = leftHand.localPosition; savedRight = rightHand.localPosition; savedRightRotation = rightHand.localRotation;
            savedTorso = torso.localPosition; savedTorsoRotation = torso.localRotation;
            savedActive = new[] { awardEnvironment.activeSelf, warEnvironment.activeSelf, bedroomEnvironment.activeSelf };
            savedMedal=medal.activeSelf; savedTags=tags.activeSelf; savedPistol=pistol.activeSelf; savedHands=hands.activeSelf; savedBody=thirdPersonBody.activeSelf;
            savedVolumeWeight = traumaVolume.weight;
            var sources = Sources; audioSnapshots = new AudioSnapshot[sources.Length];
            for(int i=0;i<sources.Length;i++) { audioSnapshots[i]=new AudioSnapshot { source=sources[i], volume=sources[i].volume, playing=sources[i].isPlaying, sample=sources[i].timeSamples }; sources[i].Stop(); }
            owned = true; finishRequested=false; directorReady=false; startedFrame=Time.frameCount; Swapped=false; SwapAlpha=0; WarSeconds=0; Playhead=0;
            runStart=previousTick=Time.realtimeSinceStartup;
            controlLock.Acquire(); hands.SetActive(true); thirdPersonBody.SetActive(false);
            medal.SetActive(true); tags.SetActive(false); pistol.SetActive(false);
            awardEnvironment.SetActive(true); warEnvironment.SetActive(false); bedroomEnvironment.SetActive(false);
            awardAudio.Play(); SetPhase("Award"); onStarted.Invoke();
        }
        void Update()
        {
            var k=Keyboard.current;
            if(k!=null && k.eKey.wasPressedThisFrame) StartStandalone();
            if(k!=null && k.xKey.wasPressedThisFrame) Cancel();
            if(!owned) return;
            try
            {
                float now=Time.realtimeSinceStartup;
                if(now-runStart>watchdogSeconds) { Debug.LogWarning("S07 realtime watchdog cancelled sequence.",this); Cancel(); return; }
                // Play() may precede the Director's first graph evaluation. The existing
                // realtime watchdog still bounds this wait; later interruptions still cancel.
                if(!directorReady)
                {
                    if(Time.frameCount<=startedFrame||sequence.director.state!=PlayState.Playing||sequence.director.time<=0)return;
                    directorReady=true;
                }
                // Read the actual playing Timeline. Scrub/Evaluate intentionally suppresses Signal notifications.
                Playhead=(float)sequence.director.time;
                ApplyPoseAndCover();
                if(sequence.director.state!=PlayState.Playing && !finishRequested) { Cancel(); return; }
                if(finishRequested && owned) sequence.director.Stop();
            }
            catch(Exception e) { Debug.LogException(e,this); Cancel(); }
        }
        static float Ease(float a,float b,float t) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        void ApplyPoseAndCover()
        {
            float t=Playhead;
            float pitch=40, yaw=-27, roll=0;
            if(t<2.5f) { pitch=Mathf.Lerp(8,40,Ease(0,1.5f,t)); yaw=Mathf.Lerp(0,-27,Ease(0,1.5f,t)); }
            else if(t<6) {
                float target=t<3.5f?10:t<4.5f?-12:11;
                float from=t<3.5f?-27:t<4.5f?10:-12;
                float a=t<3.5f?2.5f:t<4.5f?3.5f:4.5f;
                yaw=Mathf.Lerp(from,target,Ease(a,a+.8f,t)); roll=-yaw*.55f; pitch=40+Mathf.Sin((t-2.5f)*2)*3;
            }
            else if(t<7) { pitch=43; yaw=11; roll=-6; } // Keep the last right drop until fully opaque.
            else if(t<10) { pitch=43; yaw=-27; roll=-3; }
            else if(t<13) { pitch=Mathf.Lerp(43,0,Ease(10,13,t)); yaw=Mathf.Lerp(-27,0,Ease(10,13,t)); roll=Mathf.Lerp(-3,0,Ease(10,13,t)); }
            else if(t<23) { pitch=0; yaw=0; roll=0; }
            else if(t>=23) { pitch=Mathf.Lerp(28,0,Ease(23,25,t)); yaw=0; }
            float breath=t>=23 ? Mathf.Sin((t-23)*Mathf.PI*2*1.15f)*.015f : 0;
            float shock=t>=21&&t<22.3f ? Mathf.Sin(t*45)*.65f : 0;
            HeadYaw=yaw; HeadRoll=roll;
            head.localRotation=Quaternion.Euler(pitch+shock,yaw,roll+shock*.6f);
            head.localPosition=savedHead+Vector3.up*breath;
            torso.localRotation=Quaternion.Euler(t>=10&&t<13?Mathf.Lerp(10,0,Ease(10,13,t)):t>=6&&t<10?Mathf.Lerp(0,10,Ease(6,7,t)):0,0,0);
            torso.localPosition=savedTorso+Vector3.up*breath*.5f;
            // Left grip and both props share this unchanged local anchor throughout the swap.
            leftHand.localPosition=savedLeft;
            float pain=Ease(2.1f,2.8f,t)*(1-Ease(6.7f,7.1f,t));
            rightHand.localPosition=Vector3.Lerp(savedRight,new Vector3(.18f,1.62f,.10f),pain);
            rightHand.localRotation=Quaternion.Slerp(savedRightRotation,Quaternion.Euler(-55,0,25),pain);
            float alpha=t<6?0:t<7?Ease(6,7,t):t<7.8f?1:t<8.6f?1-Ease(7.8f,8.6f,t):t<21?0:t<22?Ease(21,22,t):t<23?1:t<24?1-Ease(23,24,t):0;
            SetBlack(alpha);
            traumaVolume.weight=t>=7.1f&&t<22.3f?.8f:0;
            awardAudio.volume=Mathf.Lerp(.25f,0,Ease(6,7,t));
            warAudio.volume=t<7.1f?0:t<13?Mathf.Lerp(.06f,.3f,Ease(10,13,t)):Mathf.Lerp(.3f,0,Ease(21,22.3f,t));
            roomAudio.volume=t>=23?Mathf.Lerp(0,.16f,Ease(23,25,t)):0;
            breathAudio.volume=t>=23?Mathf.Lerp(.45f,.22f,Ease(24,28,t)):0;
            if(status) status.text="S07 TEST | X: cancel | "+Phase+"\n1st-person cinematic / blockout hands / TEMP synthetic audio";
        }
        void SetBlack(float alpha) { if(screen && screen.overlay) screen.overlay.color=new Color(0,0,0,alpha); }
        void SetPhase(string phase) { Phase=phase; Message(phase); Debug.Log("S07_PHASE "+phase+" t="+Playhead.ToString("F3"),this); }
        public void Headache() { if(owned)SetPhase("Headache"); }
        public void CloseEyes() { if(owned)SetPhase("EyesClosing"); }
        public void CoveredSwap()
        {
            if(!owned || Swapped)return;
            try
            {
            if(CoverAlpha<.999f)throw new InvalidOperationException("S07 blocked uncovered prop/background swap.");
            SwapAlpha=CoverAlpha;
            medal.SetActive(false); tags.SetActive(true); pistol.SetActive(true);
            if(InjectFaultOnSwap)throw new InvalidOperationException("S07_EXPECTED_SWAP_FAULT");
            MoveActor(warSpawn.position,warSpawn.rotation);
            awardEnvironment.SetActive(false); warEnvironment.SetActive(true);
            awardAudio.Stop(); warAudio.Play(); metalAudio.Play(); Swapped=true; SetPhase("DogTags");
            }
            catch(Exception e){Debug.LogException(e,this);Cancel();}
        }
        public void Rise() { if(owned)SetPhase("Rising"); }
        public void WarFront() { if(!owned)return; warStart=Time.realtimeSinceStartup; SetPhase("WarFront"); }
        public void WarEnd() { if(!owned)return; WarSeconds=Time.realtimeSinceStartup-warStart; tinnitusAudio.Play(); SetPhase("Tinnitus"); }
        public void CoveredBedroom()
        {
            if(!owned)return;
            try
            {
            if(CoverAlpha<.999f)throw new InvalidOperationException("S07 blocked uncovered bedroom transfer.");
            MoveActor(bedroomSpawn.position,bedroomSpawn.rotation);
            warEnvironment.SetActive(false); bedroomEnvironment.SetActive(true); warAudio.Stop(); metalAudio.Stop(); pistol.SetActive(false); tags.SetActive(false);
            SetPhase("BedroomCovered");
            }
            catch(Exception e){Debug.LogException(e,this);Cancel();}
        }
        public void Wake() { if(!owned)return; tinnitusAudio.Stop(); roomAudio.Play(); breathAudio.Play(); SetPhase("Waking"); }
        public void FinishCue() { if(owned) { finishRequested=true; SetPhase("Finish"); } }
        public void SequenceCompleted()
        {
            bool success=owned && finishRequested && Swapped && WarSeconds>=7.9f;
            Restore(success);
            if(!success) { SetPhase("Cancelled"); onCancelled.Invoke(); return; }
            Completed=true; CompletionCount++; wakeState.previousPlacing=InputState.placing; wakeState.rememberedFallenComrades=true;
            SetPhase("Completed");
            try { onCompleted.Invoke(); } catch(Exception e) { Debug.LogException(e,this); }
        }
        public void Cancel() { if(sequence && sequence.IsRunning)sequence.Cancel(); else if(owned)SequenceCancelled(); }
        public void SequenceCancelled() { Restore(false); SetPhase("Cancelled"); try{onCancelled.Invoke();}catch(Exception e){Debug.LogException(e,this);} }
        void Restore(bool success)
        {
            if(!owned)return; owned=false;
            Time.timeScale=savedTimeScale; Time.fixedDeltaTime=savedFixedStep;
            if(audioSnapshots!=null)foreach(var a in audioSnapshots)if(a.source) { a.source.Stop(); a.source.volume=a.volume; if(a.playing) { a.source.timeSamples=Mathf.Clamp(a.sample,0,Mathf.Max(0,a.source.clip.samples-1)); a.source.Play(); } }
            if(screen)screen.Clear(); if(traumaVolume)traumaVolume.weight=savedVolumeWeight;
            if(!success && actor)MoveActor(savedActor,savedActorRotation);
            if(head) { head.localPosition=savedHead; head.localRotation=savedHeadRotation; }
            if(leftHand)leftHand.localPosition=savedLeft;
            if(rightHand) { rightHand.localPosition=savedRight; rightHand.localRotation=savedRightRotation; }
            if(torso) { torso.localPosition=savedTorso; torso.localRotation=savedTorsoRotation; }
            if(hands)hands.SetActive(savedHands); if(medal)medal.SetActive(savedMedal); if(tags)tags.SetActive(savedTags); if(pistol)pistol.SetActive(savedPistol); if(thirdPersonBody)thirdPersonBody.SetActive(savedBody);
            if(awardEnvironment)awardEnvironment.SetActive(success?false:savedActive[0]); if(warEnvironment)warEnvironment.SetActive(success?false:savedActive[1]); if(bedroomEnvironment)bedroomEnvironment.SetActive(success?true:savedActive[2]);
            if(controlLock)controlLock.Release();
        }
        void MoveActor(Vector3 position,Quaternion rotation)
        {
            if(controlLock && controlLock.prototypePlayer && controlLock.prototypePlayer.transform==actor)controlLock.prototypePlayer.Teleport(position,rotation);
            else actor.SetPositionAndRotation(position,rotation);
        }
        void Message(string text) { if(status)status.text="S07 | "+text; }
        void OnDisable(){Cancel();Restore(false);}
        void OnDestroy(){Restore(false);}
    }
}
