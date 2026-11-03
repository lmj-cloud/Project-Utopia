using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using Capstone.Direction.Workspace.S06;

namespace Capstone.Direction.Workspace.S07.Editor
{
    [InitializeOnLoad]
    public static class S07Validation
    {
        const string Key="S07.Validation.Pending";
        static S07AwardSequence c;
        static float began,stageAt,completedAt=-1;
        static int stage,events,starts;
        static bool swapped,war,bedroom,right1,left,right2;
        static bool initialized;
        static bool medalShot,tagShot,wakeShot;
        static bool warPoseHeld;
        static Vector3 grip;
        static float originalScale,originalFixed;
        static readonly HashSet<string> phases=new HashSet<string>();
        static S07Validation(){EditorApplication.update+=Tick;}
        [MenuItem("Capstone/Direction/S07 Workspace/Run Play validation")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Resolve unsaved scenes first.");
            initialized=false; c=null; EditorSceneManager.OpenScene(S07WorkbenchBuilder.ScenePath); SessionState.SetBool(Key,true); EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                if(!initialized)
                {
                    c=UnityEngine.Object.FindFirstObjectByType<S07AwardSequence>(); if(!c||Time.frameCount<3)return;
                    if(c.Active)c.Cancel(); c.autoStartStandalone=false;
                    originalScale=Time.timeScale; originalFixed=Time.fixedDeltaTime;
                    Time.timeScale=.65f; Time.fixedDeltaTime=.013f;
                    began=stageAt=Time.realtimeSinceStartup; stage=0; starts=events=0; phases.Clear(); swapped=war=bedroom=right1=left=right2=false; completedAt=-1;
                    medalShot=tagShot=wakeShot=warPoseHeld=false;
                    c.onStarted.AddListener(()=>starts++); c.onCompleted.AddListener(()=>events++);
                    Check(!c.AcceptAward(new S06AwardState{placing=1,firstTraumaExperienced=true}),"invalid placing rejected");
                    Check(!c.AcceptAward(new S06AwardState{placing=2,firstTraumaExperienced=false}),"incomplete S06 state rejected");
                    var state=new S06AwardState{placing=2,firstTraumaExperienced=true}; Check(c.AcceptAward(state),"valid S06 input accepted"); state.placing=1;
                    Check(c.InputState.placing==2,"input state copied"); c.StartStandalone(); Check(starts==1,"start and duplicate guard");
                    Check(c.controlLock.IsAcquired,"control lock acquired"); grip=c.leftHand.localPosition;
                    Check(c.medal.activeSelf&&!c.tags.activeSelf&&!c.pistol.activeSelf,"initial silver medal only");
                    Check(c.medal.transform.parent==c.leftHand && c.tags.transform.parent==c.leftHand && c.pistol.transform.parent==c.rightHand,"hand prop anchor ownership");
                    foreach(var source in new[]{c.awardAudio,c.warAudio,c.roomAudio,c.metalAudio,c.tinnitusAudio,c.breathAudio})
                    { var clip=source.clip; Check(clip&&clip.length>.2f,"imported test clip duration"); var data=new float[clip.samples*clip.channels]; Check(clip.GetData(data,0)&&Array.Exists(data,v=>Mathf.Abs(v)>.002f),"non-silent PCM samples"); }
                    initialized=true;
                }
                if(Time.realtimeSinceStartup-began>130)throw new TimeoutException("S07 validation deadline");
                phases.Add(c.Phase);
                if(stage==0)
                {
                    float t=c.Playhead;
                    if(t>=13.2f&&t<20.9f) { if(Mathf.Abs(c.HeadYaw)>1||Quaternion.Angle(c.head.localRotation,Quaternion.identity)>1)throw new Exception("war front pose drifted during eight-second interval"); warPoseHeld=true; }
                    if(t>=6.1f&&t<6.9f&&c.HeadYaw<8)throw new Exception("last right-head drop changed before full eye closure");
                    if(!medalShot&&t>=1.8f){Capture("S07-medal.png");Check(InView(c.medal.transform.GetChild(0).position),"left medal within camera view");medalShot=true;}
                    if(!tagShot&&t>=9){Capture("S07-dogtags.png");Check(InView(c.tags.transform.GetChild(0).position),"left dog tags within camera view");tagShot=true;}
                    if(!wakeShot&&t>=25){Capture("S07-waking.png");wakeShot=true;}
                    if(t>3.2f&&t<3.5f)right1|=c.HeadYaw>4;
                    if(t>4.2f&&t<4.5f)left|=c.HeadYaw< -5;
                    if(t>5.2f&&t<5.5f)right2|=c.HeadYaw>4;
                    if(!swapped&&c.Swapped)
                    {
                        Check(c.SwapAlpha>=.999f && c.CoverAlpha>=.999f,"prop and background swap under full black");
                        Check(Vector3.Distance(grip,c.leftHand.localPosition)<.0001f,"left grip continuity");
                        Check(!c.medal.activeSelf&&c.tags.activeSelf&&c.pistol.activeSelf,"dog tags and inert right-hand pistol");
                        Check(c.warEnvironment.activeSelf&&!c.awardEnvironment.activeSelf,"war background swap"); swapped=true;
                    }
                    if(!war&&c.Phase=="WarFront"&&t>=13.2f) { Check(Mathf.Abs(c.HeadYaw)<1,"front-facing war pose"); Check(InView(c.pistol.transform.position+Vector3.up*.095f+c.actor.forward*.14f),"right pistol within camera view"); Capture("S07-war.png"); war=true; }
                    if(!bedroom&&c.Phase=="BedroomCovered") { Check(c.CoverAlpha>=.999f&&c.bedroomEnvironment.activeSelf&&!c.warEnvironment.activeSelf,"bedroom transfer fully covered"); bedroom=true; }
                    if(!c.Completed) { if(!c.Active)throw new Exception("normal sequence stopped early: "+c.Phase); return; }
                    if(completedAt<0){completedAt=Time.realtimeSinceStartup;return;}
                    if(Time.realtimeSinceStartup-completedAt<1)return;
                    Check(swapped&&war&&bedroom&&right1&&left&&right2,"full flow and right-left-right head order");
                    Check(c.WarSeconds>=7.9f&&c.WarSeconds<9.5f,"war front duration approximately eight seconds: "+c.WarSeconds.ToString("F3"));
                    Check(warPoseHeld,"front pose maintained throughout war interval");
                    Check(events==1&&c.CompletionCount==1&&c.wakeState.rememberedFallenComrades,"bedroom completion event once");
                    Check(Vector3.Distance(c.actor.position,c.bedroomSpawn.position)<.2f,"actor arrived in bedroom: "+c.actor.position);
                    Check(Camera.main.GetComponent<CinemachineBrain>().ActiveVirtualCamera.Name=="CM_ThirdPerson","brain returned to third person");
                    Restored(); Capture("S07-bedroom.png"); c.StartStandalone(); Check(!c.Active&&events==1,"completed replay blocked");
                    Reload(); stage=1;return;
                }
                if(stage==1&&c.Playhead>=7.4f)
                {
                    Check(c.Swapped&&c.CoverAlpha>=.999f,"cancel during closed-eye swap interval"); c.Cancel(); Restored(); Check(c.CompletionCount==0,"cancel emits no completion");
                    Check(c.awardEnvironment.activeSelf&&!c.warEnvironment.activeSelf&&c.medal.activeSelf&&!c.tags.activeSelf,"cancel restores initial props and world");
                    c.StartStandalone(); stage=2;stageAt=Time.realtimeSinceStartup;return;
                }
                if(stage==2&&c.Playhead>=14)
                {
                    c.Cancel(); Restored(); Check(c.CompletionCount==0,"war cancellation emits no completion"); c.InjectFaultOnSwap=true; c.StartStandalone(); stage=3;stageAt=Time.realtimeSinceStartup;return;
                }
                if(stage==3&&!c.Active)
                {
                    Check(c.Phase=="Cancelled"&&!c.Swapped,"injected swap exception recovered");Check(c.medal.activeSelf&&!c.tags.activeSelf&&!c.pistol.activeSelf,"partial prop mutation rolled back after exception");Restored(); c.InjectFaultOnSwap=false;c.watchdogSeconds=.2f;c.StartStandalone();stage=4;stageAt=Time.realtimeSinceStartup;return;
                }
                if(stage==4&&!c.Active)
                {
                    Check(c.Playhead<1,"watchdog timeout");Restored(); c.watchdogSeconds=50;c.StartStandalone();c.enabled=false;Restored();Check(!c.Active&&c.CompletionCount==0,"disable recovery");
                    Finish(true,"normal / S06 data / full-black swap / stable grips / R-L-R / 8s war / bedroom / once-only event / closed-eye and war cancel / timeout / exception / disable / camera-input-audio-timeScale restore");
                }
            }
            catch(Exception e){Finish(false,e.ToString());}
        }
        static void Reload()
        {
            UnityEngine.Object.DestroyImmediate(c.transform.root.gameObject);
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(S07WorkbenchBuilder.PrefabPath)); c=root.GetComponentInChildren<S07AwardSequence>();c.autoStartStandalone=false;c.StartStandalone();stageAt=Time.realtimeSinceStartup;
        }
        static void Restored()
        {
            Check(!c.controlLock.IsAcquired&&!c.controlLock.prototypePlayer.ControlsLocked&&c.controlLock.prototypePlayer.enabled,"input lock and test locomotion restored");
            Check(c.CoverAlpha==0&&c.traumaVolume.weight==0,"screen and Volume restored");
            foreach(var s in new[]{c.awardAudio,c.warAudio,c.roomAudio,c.metalAudio,c.tinnitusAudio,c.breathAudio})Check(!s.isPlaying,"owned audio stopped/restored");
            Check(Mathf.Abs(Time.timeScale-.65f)<.001f&&Mathf.Abs(Time.fixedDeltaTime-.013f)<.00001f,"non-default time scale and fixed step restored");
            Check(c.sequence.cinematicCamera.Priority.Value==0&&!c.hands.activeSelf&&c.thirdPersonBody.activeSelf,"camera priority and body visibility restored");
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);Debug.Log("S07_CHECK_PASS "+message);}
        static bool InView(Vector3 point) { var p=Camera.main.WorldToViewportPoint(point); return p.z>0&&p.x>0&&p.x<1&&p.y>0&&p.y<1; }
        static void Capture(string name)
        {
            var folder=Environment.GetEnvironmentVariable("S07_VALIDATION_DIR");if(string.IsNullOrEmpty(folder))return;
            // Camera capture excludes screen-space overlay; eye-cover is separately asserted from its actual Image alpha.
            var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,name),image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        static void Finish(bool success,string text)
        {
            SessionState.SetBool(Key,false);if(c)c.Cancel();Time.timeScale=originalScale;Time.fixedDeltaTime=originalFixed;
            Debug.Log((success?"S07_VALIDATION_PASS ":"S07_VALIDATION_FAIL ")+text);
            if(Application.isBatchMode)EditorApplication.Exit(success?0:1);else EditorApplication.isPlaying=false;
        }
    }
}
