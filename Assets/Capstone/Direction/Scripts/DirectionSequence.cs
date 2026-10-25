using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
namespace Capstone.Direction
{
    /// <summary>
    /// 연출 중 입력과 카메라 상태를 보관하고 화면·음향·Timeline 재생 후 원래 상태로 복구한다.
    /// </summary>
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
        /// <summary>
        /// 매 프레임 현재 상태와 입력을 확인하여 이 컴포넌트의 동작을 갱신한다.
        /// </summary>
        void Update(){if(enableF8Test && Keyboard.current!=null && Keyboard.current.f8Key.wasPressedThisFrame) Play();}
        /// <summary>
        /// 현재 실행 조건에 맞는 연출 또는 오디오 재생을 요청한다.
        /// </summary>
        public void Play()
        {
            if(!isActiveAndEnabled || IsRunning || owner || (director && director.state==PlayState.Playing)) return;
            owner=this; IsRunning=true;
            enabledStates=new bool[inputComponents.Length];
            for(int i=0;i<inputComponents.Length;i++) if(inputComponents[i]){enabledStates[i]=inputComponents[i].enabled;inputComponents[i].enabled=false;}
            if(cinematicCamera){oldPriority=cinematicCamera.Priority;oldCameraEnabled=cinematicCamera.enabled;cinematicCamera.enabled=true;cinematicCamera.Priority=100;}
            routine=StartCoroutine(Run());
        }
        /// <summary>
        /// 화면·음향·Timeline을 순서대로 처리하고 완료 또는 취소 시 입력·카메라 상태를 복구한다.
        /// </summary>
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
        /// <summary>
        /// 연결 이벤트의 예외를 기록하고 실패 시 연출 취소·복구를 요청한다.
        /// </summary>
        bool InvokeSafely(UnityEvent callback)
        {
            try { callback.Invoke(); return true; }
            catch(System.Exception error) { Debug.LogException(error,this); Cancel(); return false; }
        }
        /// <summary>
        /// 연결된 Timeline이 정지했음을 대기 상태에 기록한다.
        /// </summary>
        void Stopped(PlayableDirector value){finished=true;}
        /// <summary>
        /// 진행 중 연출 Coroutine과 관련 동작을 취소하고 보관한 상태를 복원한다.
        /// </summary>
        public void Cancel()
        {
            if(!IsRunning)return;
            if(routine!=null) StopCoroutine(routine);
            if(audioSystem) audioSystem.FadeOut(DirectionBus.Ambient,0.2f);
            Restore(); onCancelled.Invoke();
        }
        /// <summary>
        /// 연출이 보관했던 입력·카메라·화면 상태를 원래대로 되돌린다.
        /// </summary>
        void Restore()
        {
            if(director && ownsDirector){director.stopped-=Stopped;director.Stop();ownsDirector=false;}
            if(cinematicCamera){cinematicCamera.Priority=oldPriority;cinematicCamera.enabled=oldCameraEnabled;}
            for(int i=0;i<inputComponents.Length;i++) if(inputComponents[i])inputComponents[i].enabled=enabledStates[i];
            if(screen)screen.Clear();
            routine=null; IsRunning=false; if(owner==this)owner=null;
        }
        /// <summary>
        /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
        /// </summary>
        void OnDisable(){Cancel();}
    }
}

