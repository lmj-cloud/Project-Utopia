using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using Unity.Cinemachine;
using Capstone.Direction.Workspace.S03;
namespace Capstone.Direction.Workspace.S13
{
    public sealed class S13EndingSequence : MonoBehaviour
    {
        public DirectionSequence sequence;
        public DirectionScreen screen;
        public S03ControlLockAdapter controlLock;
        public S13Timing timing;
        public Transform player,exitAnchor,door,doorView,exitView;
        public Camera outputCamera;
        public Light exitLight;
        public Volume endingVolume;
        public AudioSource ambient,wind,doorSound;
        public TMPro.TMP_Text status;
        [Min(.1f)] public float interactionDistance=3.2f,watchdogSeconds=20;
        public bool allowKeyboard=true;
        public UnityEvent onStarted=new UnityEvent(),onEndingReady=new UnityEvent(),onCancelled=new UnityEvent();
        public bool Active=>sequence&&sequence.IsRunning;
        public bool Completed {get;private set;}
        public bool WhiteHeld {get;private set;}
        public string Phase {get;private set;}="Ready";
        public float Playhead {get;private set;}
        public int CompletionCount {get;private set;}
        public bool InjectFault {get;set;}
        bool owned,finishRequested,covered,openSignalled;
        float began,lightBefore,weightBefore,scaleBefore,stepBefore;
        Vector3 cameraPosition;
        Quaternion cameraRotation,doorRotation;
        LensSettings lensBefore;
        Color overlayBefore;
        SoundState[] sounds;
        struct SoundState {public bool playing;public float volume;public int sample;}
        public AudioSource[] Sources=>new[]{ambient,wind,doorSound};
        public bool InRange=>player&&exitAnchor&&Vector3.Distance(player.position,exitAnchor.position)<=interactionDistance;
        public bool BeginFromExit()
        {
            if(!isActiveAndEnabled||Active||owned||Completed||WhiteHeld||!sequence||!sequence.director||!timing||!timing.Valid||!sequence.cinematicCamera||!screen||!screen.overlay||!controlLock||!player||!door||!doorView||!exitView||!outputCamera||!outputCamera.GetComponent<CinemachineBrain>()||!exitLight||!endingVolume)return false;
            if(!sequence.director.playableAsset||Math.Abs(sequence.director.playableAsset.duration-(timing.FinishAt+.1f))>.025)return false;
            foreach(var a in Sources)if(!a||!a.clip)return false;
            sequence.Play();return Active;
        }
        void Update()
        {
            var k=Keyboard.current;
            if(allowKeyboard&&k!=null){if(k.eKey.wasPressedThisFrame&&InRange)BeginFromExit();if(k.xKey.wasPressedThisFrame)Cancel();}
            if(!owned){if(status)status.text=Completed?"S13 ENDING READY | white held":"S13 TEST | WASD + mouse | E at exit | X cancel";return;}
            try
            {
                if(Time.realtimeSinceStartup-began>watchdogSeconds){Debug.LogWarning("S13_WATCHDOG",this);Cancel();return;}
                Playhead=(float)sequence.director.time;Apply(Playhead);
                if(finishRequested)sequence.director.Stop();
                else if(sequence.director.state!=PlayState.Playing){Debug.LogWarning("S13_DIRECTOR_INTERRUPTED",this);Cancel();}
            }
            catch(Exception e){Debug.LogException(e,this);Cancel();}
        }
        public void SequenceStarted()
        {
            // DirectionSequence already owns input and camera priority at this point.
            doorRotation=door.localRotation;cameraPosition=sequence.cinematicCamera.transform.position;cameraRotation=sequence.cinematicCamera.transform.rotation;lensBefore=sequence.cinematicCamera.Lens;
            overlayBefore=screen.overlay.color;lightBefore=exitLight.intensity;weightBefore=endingVolume.weight;scaleBefore=Time.timeScale;stepBefore=Time.fixedDeltaTime;
            sounds=new SoundState[3];var sources=Sources;
            for(int i=0;i<3;i++)sounds[i]=new SoundState{playing=sources[i].isPlaying,volume=sources[i].volume,sample=sources[i].timeSamples};
            owned=true;finishRequested=covered=openSignalled=false;began=Time.realtimeSinceStartup;Playhead=0;controlLock.Acquire();
            sequence.cinematicCamera.transform.SetPositionAndRotation(outputCamera.transform.position,outputCamera.transform.rotation);
            // Blend from the actual gameplay pose, rather than a pre-positioned camera.
            startPosition=outputCamera.transform.position;startRotation=outputCamera.transform.rotation;
            ambient.Play();wind.volume=0;wind.Play();Phase="CameraApproach";onStarted.Invoke();
        }
        Vector3 startPosition;Quaternion startRotation;
        static float Ease(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        void Apply(float t)
        {
            float blend=Ease(0,timing.OpenAt,t),opening=Ease(timing.OpenAt,timing.WhiteAt,t),white=Ease(timing.WhiteAt,timing.CoveredAt,t);
            var pose=sequence.cinematicCamera.transform;
            pose.SetPositionAndRotation(Vector3.Lerp(Vector3.Lerp(startPosition,doorView.position,blend),exitView.position,white),Quaternion.Slerp(Quaternion.Slerp(startRotation,doorView.rotation,blend),exitView.rotation,white));
            door.localRotation=doorRotation*Quaternion.Euler(0,-timing.doorAngle*opening,0);
            exitLight.intensity=lightBefore+timing.lightBoost*Mathf.Clamp01(opening*.25f+white*.75f);endingVolume.weight=Mathf.Lerp(weightBefore,1,white);
            screen.overlay.color=new Color(1,1,1,white);
            ambient.volume=sounds[0].volume*(1-white);wind.volume=timing.windGain*opening*(1-white);doorSound.volume=sounds[2].volume*(1-white);
            if(InjectFault&&white>.25f)throw new InvalidOperationException("S13_EXPECTED_FAULT");
            if(status)status.text=$"S13 {Phase} | {t:F1}s | X cancel | TEMP sound";
        }
        public void OpenDoor(){if(!owned||openSignalled)return;openSignalled=true;doorSound.Play();Phase="DoorOpening";}
        public void WhiteRise(){if(owned)Phase="WhiteRise";}
        public void Covered(){if(!owned)return;covered=true;screen.overlay.color=Color.white;Phase="Covered";}
        public void Finish(){if(owned)finishRequested=true;}
        public void SequenceCompleted()
        {
            if(!owned)return;
            if(!finishRequested||!covered||!openSignalled||screen.overlay.color.a<.999f){SequenceCancelled();return;}
            Restore(true);Completed=true;WhiteHeld=true;CompletionCount++;Phase="EndingReady";
            try{onEndingReady.Invoke();}catch(Exception e){Debug.LogException(e,this);ReleaseWhite();Phase="CompletionCallbackFailed";}
        }
        public void Cancel(){if(Active)sequence.Cancel();else if(owned)SequenceCancelled();}
        public void SequenceCancelled(){if(!owned)return;Restore(false);Phase="Cancelled";try{onCancelled.Invoke();}catch(Exception e){Debug.LogException(e,this);}}
        void Restore(bool keepWhite)
        {
            if(!owned)return;owned=false;
            if(door)door.localRotation=doorRotation;
            if(sequence&&sequence.cinematicCamera){sequence.cinematicCamera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);sequence.cinematicCamera.Lens=lensBefore;}
            if(exitLight)exitLight.intensity=lightBefore;if(endingVolume)endingVolume.weight=weightBefore;
            var sources=Sources;for(int i=0;i<3;i++)if(sources[i]){sources[i].Stop();sources[i].volume=sounds[i].volume;if(sounds[i].playing){sources[i].timeSamples=Mathf.Clamp(sounds[i].sample,0,sources[i].clip.samples-1);sources[i].Play();}}
            if(screen&&screen.overlay)screen.overlay.color=keepWhite?Color.white:overlayBefore;
            Time.timeScale=scaleBefore;Time.fixedDeltaTime=stepBefore;
            if(controlLock)controlLock.Release();
        }
        // Call only after the ending host has taken responsibility for the covered frame.
        public void ReleaseWhite(){if(Active)return;if(screen&&screen.overlay)screen.overlay.color=overlayBefore;WhiteHeld=false;}
        [ContextMenu("Reset standalone preview")]
        public void ResetPreview(){if(Active)return;ReleaseWhite();Completed=false;Phase="Ready";}
        void OnDisable(){if(owned)Cancel();if(WhiteHeld)ReleaseWhite();}
        void OnDestroy(){if(owned)Restore(false);}
    }
}
