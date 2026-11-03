using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Capstone.Direction.Workspace.S03;
using Unity.Cinemachine;

namespace Capstone.Direction.Workspace.S10
{
    [Serializable] public sealed class S10DriveState
    {
        public bool vehicleBoarded=true, departureReady=true;
        public string sourceSequence="S09";
    }
    [Serializable] public sealed class S10HospitalState
    {
        public string nextSequence="S11";
        public bool accidentOccurred;
        public bool causeConfirmed=false; // No cause or injury diagnosis is authored here.
        public bool unconscious,requiresBlackStart;
    }
    public sealed class S10CrashSequence : MonoBehaviour
    {
        public DirectionSequence sequence;
        public DirectionScreen screen;
        public S03ControlLockAdapter controlLock;
        public S10Timing timing;
        public S10DriverEffects driver;
        public bool integrationMode;
        public float handoffTimeout=30;
        public bool HandoffPending {get;private set;}
        public bool IsBlackHeld {get;private set;}
        public Transform car,truck,roadMotion,cinematicPose;
        public Camera outputCamera;
        public Transform[] fragments;
        public Light keyLight,impactLight;
        public Volume confusionVolume;
        public AudioSource engine,road,cabin,truckAudio,horn,brake,crash,glass,tinnitus;
        public AudioLowPassFilter roadFilter;
        public TMPro.TMP_Text status;
        public UnityEvent onStarted=new UnityEvent(),onS11Ready=new UnityEvent(),onCancelled=new UnityEvent();
        public UnityEvent onImpact=new UnityEvent();
        public UnityEvent onHandoffTimeout=new UnityEvent();
        public S10HospitalState hospitalState=new S10HospitalState();
        public bool autoStartStandalone=true;
        public float watchdogSeconds=40;
        public bool Active=>sequence&&sequence.IsRunning;
        public bool Completed {get;private set;}
        public int CompletionCount {get;private set;}
        public int ImpactCount {get;private set;}
        public string Phase {get;private set;}="Ready";
        public float Playhead {get;private set;}
        public float HandoffAlpha {get;private set;}
        public bool InjectImpactFault {get;set;}
        public S10DriveState InputState {get;private set;}
        public AudioSource[] Sources=>new[]{engine,road,cabin,truckAudio,horn,brake,crash,glass,tinnitus};
        bool owned,finishRequested,covered,directorReady;
        int startedFrame;
        float startTime,savedScale,savedStep,savedKey,savedFlash,savedWeight,savedCutoff;
        Vector3 carP,truckP,roadP,cameraP;
        Quaternion carR,truckR,cameraR;
        LensSettings savedLens;
        Color savedOverlay;
        Vector3[] fragmentP; Quaternion[] fragmentR; bool[] fragmentActive;
        struct SoundState {public float volume;public int sample;public bool playing;}
        SoundState[] soundStates;
        System.Collections.IEnumerator Start(){yield return null;if(autoStartStandalone)StartStandalone();}
        public void StartStandalone()=>BeginFromS09(new S10DriveState());
        public bool BeginFromS09(S10DriveState state)
        {
            if(!isActiveAndEnabled||Active||Completed||S10BlackoutRelay.Current||state==null||!state.vehicleBoarded||!state.departureReady)return false;
            if(!sequence||!sequence.director||!sequence.director.playableAsset||!timing||!timing.Valid||!screen||!screen.overlay||!controlLock||!car||!truck||!roadMotion||!cinematicPose||!outputCamera||!keyLight||!impactLight||!confusionVolume||!roadFilter)return false;
            foreach(var source in Sources)if(!source||!source.clip)return false;
            if(!driver)return false;
            if(timing.appliedTimingSignature!=timing.TimingSignature||Math.Abs(sequence.director.playableAsset.duration-(timing.FinishAt+.1f))>.03)
            {Debug.LogWarning("S10 timing changed. Run Apply timing to Timeline from the S10 menu.",this);return false;}
            InputState=new S10DriveState{vehicleBoarded=state.vehicleBoarded,departureReady=state.departureReady,sourceSequence=state.sourceSequence};
            sequence.Play();return Active;
        }
        public void SequenceStarted()
        {
            carP=car.localPosition;carR=car.localRotation;truckP=truck.localPosition;truckR=truck.localRotation;roadP=roadMotion.localPosition;
            cameraP=cinematicPose.position;cameraR=cinematicPose.rotation;savedLens=sequence.cinematicCamera.Lens;
            savedScale=Time.timeScale;savedStep=Time.fixedDeltaTime;savedKey=keyLight.intensity;savedFlash=impactLight.intensity;savedWeight=confusionVolume.weight;savedCutoff=roadFilter.cutoffFrequency;savedOverlay=screen.overlay.color;
            fragmentP=new Vector3[fragments.Length];fragmentR=new Quaternion[fragments.Length];fragmentActive=new bool[fragments.Length];
            for(int i=0;i<fragments.Length;i++){fragmentP[i]=fragments[i].localPosition;fragmentR[i]=fragments[i].localRotation;fragmentActive[i]=fragments[i].gameObject.activeSelf;}
            var sounds=Sources;soundStates=new SoundState[sounds.Length];
            for(int i=0;i<sounds.Length;i++){soundStates[i]=new SoundState{volume=sounds[i].volume,sample=sounds[i].timeSamples,playing=sounds[i].isPlaying};sounds[i].Stop();}
            owned=true;finishRequested=covered=directorReady=false;startedFrame=Time.frameCount;ImpactCount=0;Playhead=0;startTime=Time.realtimeSinceStartup;
            controlLock.Acquire();cinematicPose.SetPositionAndRotation(outputCamera.transform.position,outputCamera.transform.rotation);
            engine.Play();road.Play();cabin.Play();SetPhase("Driving");onStarted.Invoke();
        }
        void Update()
        {
            var k=Keyboard.current;if(k!=null&&k.eKey.wasPressedThisFrame)StartStandalone();if(k!=null&&k.xKey.wasPressedThisFrame)Cancel();
            if(!owned)return;
            try
            {
                if(Time.realtimeSinceStartup-startTime>watchdogSeconds){Debug.LogWarning("S10 realtime watchdog; restoring.",this);Cancel();return;}
                if(!directorReady)
                {
                    if(Time.frameCount<=startedFrame||sequence.director.state!=PlayState.Playing||sequence.director.time<=0)return;
                    directorReady=true;
                }
                Playhead=(float)sequence.director.time;ApplyFrame();
                if(sequence.director.state!=PlayState.Playing&&!finishRequested){Debug.LogWarning($"S10_DIRECTOR_INTERRUPTED time={sequence.director.time:F3} duration={sequence.director.duration:F3} assetDuration={sequence.director.playableAsset.duration:F3} state={sequence.director.state} phase={Phase}",this);Cancel();return;}
                if(finishRequested)sequence.director.Stop();
            }catch(Exception e){Debug.LogException(e,this);Cancel();}
        }
        static float Ease(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        void ApplyFrame()
        {
            float t=Playhead;var s=timing;
            float approach=Ease(s.ApproachAt,s.DangerAt,t),danger=Ease(s.DangerAt,s.ImpactAt,t);
            float z=t<s.ApproachAt?48:t<s.DangerAt?Mathf.Lerp(48,15,approach):Mathf.Lerp(15,6,danger);
            truck.localPosition=new Vector3(0,0,z);
            float drive=1-Ease(s.ImpactAt,s.ConfusionAt+.6f,t);roadMotion.localPosition=roadP+Vector3.back*(t*9%12)*drive;
            float hit=t>=s.ImpactAt?Mathf.Exp(-(t-s.ImpactAt)*11):0;
            float after=t>=s.ConfusionAt?Mathf.Exp(-(t-s.ConfusionAt)*.9f):0;
            car.localPosition=carP+new Vector3(.06f*hit,0,-.22f*hit);car.localRotation=carR*Quaternion.Euler(0,0,.8f*hit);
            impactLight.intensity=savedFlash+7*hit;
            keyLight.intensity=Mathf.Lerp(savedKey,savedKey*.65f,Ease(s.ImpactAt,s.ConfusionAt+.5f,t));
            confusionVolume.weight=Mathf.Clamp01(after*.8f+hit*.6f);
            float black=Ease(s.BlackAt,s.CoveredAt,t);
            screen.overlay.color=t>=s.BlackAt?new Color(0,0,0,black):new Color(1,1,1,hit*.42f);
            roadFilter.cutoffFrequency=Mathf.Lerp(savedCutoff,700,Ease(s.ImpactAt,s.ConfusionAt+.5f,t));
            engine.volume=Mathf.Lerp(.24f,.03f,Ease(s.ImpactAt,s.ConfusionAt,t))*(1-black);
            road.volume=Mathf.Lerp(.2f,.04f,Ease(s.ImpactAt,s.ConfusionAt,t))*(1-black);cabin.volume=.10f*(1-black);
            truckAudio.volume=Mathf.Lerp(.03f,.35f,Mathf.Clamp01(approach+danger*.5f))*(1-.8f*Ease(s.ImpactAt,s.ConfusionAt+.4f,t))*(1-black);
            tinnitus.volume=.16f*(1-black);
            for(int i=0;i<fragments.Length;i++)if(fragments[i].gameObject.activeSelf)
            {
                float age=Mathf.Clamp(t-s.ImpactAt,0,.8f);float a=i*2.39996f;
                fragments[i].localPosition=fragmentP[i]+new Vector3(Mathf.Cos(a)*age*2.5f,age*.9f-age*age,Mathf.Sin(a)*age*1.7f);
                fragments[i].localRotation=fragmentR[i]*Quaternion.Euler(age*150,age*i*9,0);
                if(t>s.ImpactAt+.8f)fragments[i].gameObject.SetActive(false);
            }
            driver.Apply(t);
        }
        public void Approach(){if(!owned)return;truckAudio.Play();SetPhase("Approach");}
        public void Danger(){if(!owned)return;if(timing.proposedHorn)horn.Play();if(timing.proposedBrake)brake.Play();SetPhase("Danger");}
        public void Impact()
        {
            if(!owned||ImpactCount>0)return;
            try{ImpactCount++;crash.Play();if(timing.proposedGlass){glass.Play();foreach(var f in fragments)f.gameObject.SetActive(true);}impactLight.intensity=7;SetPhase("Impact");onImpact.Invoke();if(InjectImpactFault)throw new InvalidOperationException("S10_EXPECTED_IMPACT_FAULT");}
            catch(Exception e){Debug.LogException(e,this);Cancel();}
        }
        public void Confusion(){if(!owned)return;if(timing.proposedTinnitus)tinnitus.Play();SetPhase("Confusion");}
        public void Blackout(){if(owned)SetPhase("Blackout");}
        public void Covered(){if(!owned)return;screen.overlay.color=Color.black;covered=true;SetPhase("Covered");}
        public void Finish(){if(owned){finishRequested=true;}}
        public void SequenceCompleted()
        {
            bool success=owned&&finishRequested&&covered&&ImpactCount==1&&screen.overlay.color.a>=.999f;
            if(!success){SequenceCancelled();return;}
            if(integrationMode){try{S10BlackoutRelay.Acquire(this,handoffTimeout);}catch(Exception e){Debug.LogException(e,this);SequenceCancelled();return;}}
            HandoffAlpha=screen.overlay.color.a;Completed=true;CompletionCount++;hospitalState.accidentOccurred=true;hospitalState.unconscious=true;hospitalState.requiresBlackStart=true;
            Restore(true);SetPhase("Completed");
            IsBlackHeld=true;HandoffPending=integrationMode;
            try{onS11Ready.Invoke();}catch(Exception e){Debug.LogException(e,this);if(integrationMode)S10BlackoutRelay.CancelPending();else ClearHeldBlack();}
        }
        public void Cancel(){if(sequence&&sequence.IsRunning)sequence.Cancel();else if(owned)SequenceCancelled();else if(HandoffPending)S10BlackoutRelay.CancelPending();}
        public void SequenceCancelled(){if(!owned)return;Restore(false);SetPhase("Cancelled");try{onCancelled.Invoke();}catch(Exception e){Debug.LogException(e,this);}}
        void Restore(bool holdBlack)
        {
            if(!owned)return;owned=false;
            Time.timeScale=savedScale;Time.fixedDeltaTime=savedStep;
            if(car){car.localPosition=carP;car.localRotation=carR;}if(truck){truck.localPosition=truckP;truck.localRotation=truckR;}if(roadMotion)roadMotion.localPosition=roadP;
            if(cinematicPose)cinematicPose.SetPositionAndRotation(cameraP,cameraR);if(sequence&&sequence.cinematicCamera)sequence.cinematicCamera.Lens=savedLens;
            if(keyLight)keyLight.intensity=savedKey;if(impactLight)impactLight.intensity=savedFlash;if(confusionVolume)confusionVolume.weight=savedWeight;if(roadFilter)roadFilter.cutoffFrequency=savedCutoff;
            for(int i=0;i<fragments.Length;i++)if(fragments[i]){fragments[i].localPosition=fragmentP[i];fragments[i].localRotation=fragmentR[i];fragments[i].gameObject.SetActive(fragmentActive[i]);}
            var sounds=Sources;for(int i=0;i<sounds.Length;i++)if(sounds[i]){sounds[i].Stop();sounds[i].volume=soundStates[i].volume;if(soundStates[i].playing){sounds[i].timeSamples=Mathf.Clamp(soundStates[i].sample,0,sounds[i].clip.samples-1);sounds[i].Play();}}
            if(screen&&screen.overlay)screen.overlay.color=holdBlack?Color.black:savedOverlay;
            if(driver)driver.Restore();
            if(controlLock)controlLock.Release();
        }
        void SetPhase(string phase){Phase=phase;Debug.Log("S10_PHASE "+phase,this);}
        public void HandoffDestinationReady(){HandoffPending=false;ClearHeldBlack();SetPhase("BlackTransferred");}
        public void HandoffWaitingTimedOut(){SetPhase("AwaitingS11Timeout");try{onHandoffTimeout.Invoke();}catch(Exception e){Debug.LogException(e,this);}}
        public void HandoffAborted(){HandoffPending=false;ClearHeldBlack();SetPhase("HandoffCancelled");try{onCancelled.Invoke();}catch(Exception e){Debug.LogException(e,this);}}
        void ClearHeldBlack(){IsBlackHeld=false;if(screen&&screen.overlay)screen.overlay.color=savedOverlay;}
        [ContextMenu("Reset standalone preview (explicit operator action)")]
        public void ResetStandalonePreview(){if(Active||integrationMode)return;ClearHeldBlack();Completed=false;SetPhase("Ready");}
        void OnDisable(){if(owned)Cancel();else if(!integrationMode)ClearHeldBlack();}void OnDestroy(){if(owned)Restore(false);}
    }
}
