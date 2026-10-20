using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
namespace Capstone.Direction
{
    public sealed class DirectionSequence : MonoBehaviour
    {
        public DirectionScreen screen;
        public DirectionAudio audioSystem;
        public PlayableDirector director;
        public CinemachineCamera cinematicCamera;
        [Tooltip("Existing PlayerController and PlayerInteraction only. Disabled states are restored exactly.")]
        public Behaviour[] inputComponents = new Behaviour[0];
        public AudioClip transitionSound, crowdLoop;
        public UnityEvent onStarted=new UnityEvent(), onTransitionCovered=new UnityEvent(), onCompleted=new UnityEvent(), onCancelled=new UnityEvent();
        public bool enableF8Test;
        [Min(0.1f)] public float timelineTimeout=30;
        public bool IsRunning {get;private set;}
        bool[] enabledStates;
        PrioritySettings oldPriority;
        bool oldCameraEnabled, finished, ownsDirector;
        Coroutine routine;
        static DirectionSequence owner;
        void Update(){if(enableF8Test && Keyboard.current!=null && Keyboard.current.f8Key.wasPressedThisFrame) Play();}
        public void Play()
        {
            if(!isActiveAndEnabled || IsRunning || owner || (director && director.state==PlayState.Playing)) return;
            owner=this; IsRunning=true;
            enabledStates=new bool[inputComponents.Length];
            for(int i=0;i<inputComponents.Length;i++) if(inputComponents[i]){enabledStates[i]=inputComponents[i].enabled;inputComponents[i].enabled=false;}
            if(cinematicCamera){oldPriority=cinematicCamera.Priority;oldCameraEnabled=cinematicCamera.enabled;cinematicCamera.enabled=true;cinematicCamera.Priority=100;}
            routine=StartCoroutine(Run());
        }
        IEnumerator Run()
        {
            if(!InvokeSafely(onStarted)) yield break;
            if(!IsRunning) yield break;
            if(audioSystem) audioSystem.Play(transitionSound);
            if(screen) yield return screen.Fade(Color.white,1,0.5f);
            if(!InvokeSafely(onTransitionCovered)) yield break; // Teleport or scene handoff belongs to the main developer.
            if(!IsRunning) yield break;
            if(audioSystem) audioSystem.Loop(crowdLoop,DirectionBus.Ambient,0.7f,1.5f);
            if(director && director.playableAsset)
            {
                finished=false; ownsDirector=true; director.stopped+=Stopped;
                director.time=0; director.Play();
            }
            if(screen) yield return screen.Fade(Color.white,0,0.8f);
            if(ownsDirector)
            {
                float t=0;
                while(!finished && t<timelineTimeout){t+=Time.unscaledDeltaTime;yield return null;}
                if(!finished){Debug.LogWarning("Direction Timeline timed out; cancelling and restoring input.",this);Cancel();yield break;}
            }
            else yield return new WaitForSecondsRealtime(0.5f);
            Restore(); Debug.Log("Direction sequence completed: "+name,this); onCompleted.Invoke();
        }
        bool InvokeSafely(UnityEvent callback)
        {
            try { callback.Invoke(); return true; }
            catch(System.Exception error) { Debug.LogException(error,this); Cancel(); return false; }
        }
        void Stopped(PlayableDirector value){finished=true;}
        public void Cancel()
        {
            if(!IsRunning)return;
            if(routine!=null) StopCoroutine(routine);
            if(audioSystem) audioSystem.FadeOut(DirectionBus.Ambient,0.2f);
            Restore(); onCancelled.Invoke();
        }
        void Restore()
        {
            if(director && ownsDirector){director.stopped-=Stopped;director.Stop();ownsDirector=false;}
            if(cinematicCamera){cinematicCamera.Priority=oldPriority;cinematicCamera.enabled=oldCameraEnabled;}
            for(int i=0;i<inputComponents.Length;i++) if(inputComponents[i])inputComponents[i].enabled=enabledStates[i];
            if(screen)screen.Clear();
            routine=null; IsRunning=false; if(owner==this)owner=null;
        }
        void OnDisable(){Cancel();}
    }
}

