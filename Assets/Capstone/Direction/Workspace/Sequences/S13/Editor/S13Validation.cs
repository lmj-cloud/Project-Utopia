using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Capstone.Direction.Workspace.S13.Editor
{
    [InitializeOnLoad]
    public static class S13Validation
    {
        const string Pending="S13.Validation.Pending";
        static S13EndingSequence c;
        static bool initialized,motion,opening,white,covered;
        static int stage,started,completed,forwarded;
        static float began,waitAt,priorScale,priorStep,previousAlpha;
        static Vector3 poseBefore,outputEarly;
        static Quaternion rotationBefore,doorBefore;
        static float lightBefore,weightBefore,ambientBefore;
        static Keyboard injectedKeyboard;
        static S13Validation(){EditorApplication.update+=Tick;}
        [MenuItem("Capstone/Direction/S13 Workspace/Run Play validation %#F11")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before validation");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Resolve unsaved scenes first");
            EditorSceneManager.OpenScene(S13WorkbenchBuilder.ScenePath);initialized=false;c=null;
            if(!Application.isBatchMode){var gameView=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");if(gameView!=null)EditorWindow.GetWindow(gameView).Focus();}
            SessionState.SetBool(Pending,true);EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                if(!initialized)
                {
                    c=UnityEngine.Object.FindFirstObjectByType<S13EndingSequence>();if(!c||Time.frameCount<4)return;
                    priorScale=Time.timeScale;priorStep=Time.fixedDeltaTime;Time.timeScale=.65f;Time.fixedDeltaTime=.013f;began=Time.realtimeSinceStartup;
                    initialized=true;stage=started=completed=forwarded=0;motion=opening=white=covered=false;previousAlpha=0;
                    Check(!c.Active&&c.InRange,"standalone starts in third person within exit interaction range");
                    Check(Camera.allCameras.Length==1&&Camera.allCameras[0]==c.outputCamera,"only intended Game output Camera active");
                    Check(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x=>x.isActiveAndEnabled)==1,"one active AudioListener");
                    var portable=AssetDatabase.LoadAssetAtPath<GameObject>(S13WorkbenchBuilder.SequencePrefabPath);Check(portable.GetComponentsInChildren<Camera>(true).Length==0&&portable.GetComponentsInChildren<AudioListener>(true).Length==0&&portable.GetComponent<S13EndingSequence>().player==null,"portable prefab has no test player/output camera/listener binding");
                    var urp=c.outputCamera.GetComponent<UniversalAdditionalCameraData>();Check(UniversalRenderPipeline.asset&&urp.renderPostProcessing&&(urp.volumeLayerMask.value&(1<<c.endingVolume.gameObject.layer))!=0,"URP and camera post-processing/Volume layer configured");
                    foreach(var source in c.Sources){var data=new float[source.clip.samples*source.clip.channels];Check(source.clip.GetData(data,0)&&data.Any(v=>Mathf.Abs(v)>.001f),"actual non-silent audio clip data available");}
                    poseBefore=c.sequence.cinematicCamera.transform.position;rotationBefore=c.sequence.cinematicCamera.transform.rotation;doorBefore=c.door.localRotation;lightBefore=c.exitLight.intensity;weightBefore=c.endingVolume.weight;ambientBefore=c.ambient.volume;
                    c.ambient.Play();c.controlLock.prototypePlayer.SetControlsLocked(true);
                    c.onStarted.AddListener(()=>started++);c.onEndingReady.AddListener(()=>{completed++;Check(c.WhiteHeld&&c.screen.overlay.color==Color.white,"ending callback occurs only under opaque white");});c.GetComponent<S13EndingAdapter>().onEndingRequested.AddListener(()=>forwarded++);
                    Check(c.GetComponent<S13EndingAdapter>().TryStartFromExit(),"external exit adapter starts sequence");Check(!c.BeginFromExit()&&started==1,"duplicate start blocked");outputEarly=c.outputCamera.transform.position;
                }
                if(Time.realtimeSinceStartup-began>90)throw new TimeoutException("S13 validation deadline");
                float t=c.Playhead;var s=c.timing;
                if(stage==0)
                {
                    if(t>=s.OpenAt+.4f&&!opening){Check(Quaternion.Angle(c.door.localRotation,doorBefore)>5&&c.controlLock.IsAcquired&&!c.controlLock.prototypePlayer.enabled,"door opens while controls locked");Capture("S13-door.png",false);opening=true;}
                    if(t>=s.WhiteAt+.5f&&!motion){Check(Vector3.Distance(c.outputCamera.transform.position,outputEarly)>.4f&&c.outputCamera.GetComponent<CinemachineBrain>().ActiveVirtualCamera.Name=="CM_S13_Ending","actual rendered camera moves to exit and owns shot");Capture("S13-exit-camera.png",false);motion=true;}
                    if(c.Active&&t>=s.WhiteAt&&t<s.CoveredAt){Check(c.screen.overlay.color.a+.01f>=previousAlpha,"white fade monotonic");previousAlpha=c.screen.overlay.color.a;white=true;}
                    if(c.Active&&t>s.CoveredAt+.02f){Check(c.screen.overlay.color==Color.white,"fully white during hold");covered=true;}
                    if(!c.Completed){Check(c.Active,"normal sequence still running");return;}
                    Check(motion&&opening&&white&&covered&&completed==1&&forwarded==1,"all Timeline cues and single ending event executed");Check(c.GetComponent<S13EndingAdapter>().endingState.whiteCovered,"ending adapter state is covered");
                    Check(!c.BeginFromExit(),"completed duplicate blocked");waitAt=Time.realtimeSinceStartup;stage=1;return;
                }
                if(stage==1)
                {
                    if(Time.realtimeSinceStartup-waitAt<.8f)return;
                    CaptureGameWhite();Restored(true,true);Check(c.screen.overlay.color==Color.white,"white remains until explicit host release");c.ResetPreview();Check(!c.WhiteHeld&&c.screen.overlay.color.a==0,"explicit preview reset clears white");c.controlLock.prototypePlayer.SetControlsLocked(false);c.BeginFromExit();stage=2;return;
                }
                if(stage==2&&t>s.WhiteAt+.4f){c.Cancel();Restored(false,false);waitAt=Time.realtimeSinceStartup;stage=3;return;}
                if(stage==3){if(Time.realtimeSinceStartup-waitAt<.8f)return;Check(c.outputCamera.GetComponent<CinemachineBrain>().ActiveVirtualCamera.Name=="CM_ThirdPerson","actual camera returned after cancel blend");c.InjectFault=true;c.BeginFromExit();stage=4;return;}
                if(stage==4&&!c.Active){Check(c.Phase=="Cancelled"&&completed==1,"injected white-transition exception does not complete");Restored(false,false);c.InjectFault=false;c.watchdogSeconds=.15f;c.BeginFromExit();stage=5;return;}
                if(stage==5&&!c.Active)
                {
                    Check(c.Playhead<.5f,"realtime watchdog recovered");Restored(false,false);c.watchdogSeconds=20;c.BeginFromExit();c.enabled=false;Restored(false,false);Check(!c.Active,"component disable cancels sequence");c.enabled=true;
                    c.sequence.enabled=false;Check(!c.BeginFromExit(),"disabled common sequence cannot acquire ownership");c.sequence.enabled=true;
                    UnityEngine.Events.UnityAction fault=()=>throw new InvalidOperationException("S13_EXPECTED_START_FAULT");c.onStarted.AddListener(fault);Check(!c.BeginFromExit(),"start callback exception releases shared ownership");c.onStarted.RemoveListener(fault);Restored(false,false);
                    var game= new GameObject("S13_TEST_OtherSequence");var other=game.AddComponent<DirectionSequence>();other.Play();Check(!c.BeginFromExit(),"other DirectionSequence ownership respected");other.Cancel();UnityEngine.Object.DestroyImmediate(game);
                    var saved=c.outputCamera;c.outputCamera=null;Check(!c.BeginFromExit(),"missing integration camera rejected without state changes");c.outputCamera=saved;
                    waitAt=Time.realtimeSinceStartup;stage=6;return;
                }
                if(stage==6)
                {
                    if(Time.realtimeSinceStartup-waitAt<.8f)return;
                    Check(c.outputCamera.GetComponent<CinemachineBrain>().ActiveVirtualCamera.Name=="CM_ThirdPerson","actual camera recovers after failure paths");
                    injectedKeyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(injectedKeyboard,new KeyboardState(Key.E));waitAt=Time.realtimeSinceStartup;stage=7;return;
                }
                if(stage==7)
                {
                    if(!c.Active){Check(Time.realtimeSinceStartup-waitAt<2,"E input starts within deadline");return;}
                    Check(true,"E keyboard input starts through runtime Update");InputSystem.QueueStateEvent(injectedKeyboard,new KeyboardState(Key.X));waitAt=Time.realtimeSinceStartup;stage=8;return;
                }
                if(stage==8)
                {
                    if(c.Active){Check(Time.realtimeSinceStartup-waitAt<2,"X input cancels within deadline");return;}
                    Check(c.Phase=="Cancelled","X keyboard input cancels through runtime Update");Restored(false,false);InputSystem.QueueStateEvent(injectedKeyboard,new KeyboardState(Key.E));stage=9;return;
                }
                if(stage==9&&c.Completed){Check(completed==2&&forwarded==2,"keyboard retry reaches normal covered ending once");waitAt=Time.realtimeSinceStartup;stage=10;return;}
                if(stage==10)
                {
                    if(Time.realtimeSinceStartup-waitAt<.8f)return;Restored(true,false);c.ResetPreview();
                    Finish(true,"normal exit / third-person blend and dolly / door / monotonic white / covered event / duplicate guard / persistent white until release / cancel / fault / timeout / disable / shared ownership / camera-input-audio-time restore / InputSystem E-X-E path");
                }
            }
            catch(Exception e){Finish(false,e.ToString());}
        }
        static void Restored(bool held,bool previousLock)
        {
            Check(!c.Active&&!c.controlLock.IsAcquired&&c.controlLock.prototypePlayer.ControlsLocked==previousLock&&c.controlLock.prototypePlayer.enabled,"input enabled and prior lock restored");
            Check(Quaternion.Angle(c.door.localRotation,doorBefore)<.01f&&Vector3.Distance(c.sequence.cinematicCamera.transform.position,poseBefore)<.001f&&Quaternion.Angle(c.sequence.cinematicCamera.transform.rotation,rotationBefore)<.01f&&c.sequence.cinematicCamera.Priority.Value==0,"door and virtual-camera snapshot restored");
            Check(c.exitLight.intensity==lightBefore&&c.endingVolume.weight==weightBefore&&Mathf.Abs(c.screen.overlay.color.a-(held?1:0))<.001f,"light Volume and screen restored");
            Check(c.ambient.isPlaying&&Mathf.Abs(c.ambient.volume-ambientBefore)<.001f&&!c.wind.isPlaying&&!c.doorSound.isPlaying,"pre-existing ambience resumed; owned wind and door stopped");
            Check(Mathf.Abs(Time.timeScale-.65f)<.001f&&Mathf.Abs(Time.fixedDeltaTime-.013f)<.0001f,"time scale and fixed step preserved");
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);if(!label.Contains("monotonic")&&label!="normal sequence still running"&&label!="fully white during hold")Debug.Log("S13_CHECK_PASS "+label);}
        static void CaptureGameWhite()
        {
            if(Application.isBatchMode){Debug.Log("S13_CHECK_SKIPPED actual Game pixel capture requires a visible Game view; overlay colour/coverage assertions still run");return;}
            // ScreenSpaceCamera conversion runs UI through URP processing and is not
            // the final Overlay result. Read the actual Game backbuffer after held frames.
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Check(image&&image.width>0&&image.height>0,"actual Game backbuffer captured");
                Debug.Log($"S13_GAME_CAPTURE_SIZE image={image.width}x{image.height} screen={Screen.width}x{Screen.height} camera={c.outputCamera.pixelRect} canvas={c.screen.overlay.canvas.pixelRect}");
                var folder=Environment.GetEnvironmentVariable("S13_VALIDATION_DIR");
                if(!string.IsNullOrEmpty(folder))System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"S13-final-white.png"),image.EncodeToPNG());
                var pixels=image.GetPixels32();int width=image.width,height=image.height;
                // At fractional Editor DPI the readback contains up to four dark
                // padding rows outside the Game render (pixelRect has fractional size).
                // Reject any non-white interior pixel, and any non-dark border leak.
                Check(pixels.Select((p,i)=>new{p,x=i%width,y=i/width}).All(v=>
                    (v.p.r>=253&&v.p.g>=253&&v.p.b>=253)||
                    ((v.x<4||v.y<4||v.x>=width-4||v.y>=height-4)&&v.p.r<=20&&v.p.g<=20&&v.p.b<=20)),
                    "actual Game viewport is opaque white (only <=4px dark Editor readback border allowed)");
            }
            finally{if(image)UnityEngine.Object.DestroyImmediate(image);}
        }
        static void Capture(string file,bool white)
        {
            string folder=Environment.GetEnvironmentVariable("S13_VALIDATION_DIR");if(string.IsNullOrEmpty(folder))return;
            var camera=c.outputCamera;var canvas=c.screen.overlay.canvas;var mode=canvas.renderMode;var world=canvas.worldCamera;float distance=canvas.planeDistance;var target=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=Mathf.Max(.2f,camera.nearClipPlane+.1f);camera.targetTexture=rt;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,file),image.EncodeToPNG());if(white){var pixels=image.GetPixels32();Debug.Log("S13_WHITE_CAPTURE_MIN "+pixels.Min(p=>Math.Min(p.r,Math.Min(p.g,p.b))));Check(pixels.All(p=>p.r>=253&&p.g>=253&&p.b>=253),"composite final frame fully white at every pixel");}}
            finally{canvas.renderMode=mode;canvas.worldCamera=world;canvas.planeDistance=distance;camera.targetTexture=target;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        static void Finish(bool success,string message)
        {
            SessionState.SetBool(Pending,false);if(c)c.Cancel();if(injectedKeyboard!=null&&injectedKeyboard.added)InputSystem.RemoveDevice(injectedKeyboard);injectedKeyboard=null;Time.timeScale=priorScale;Time.fixedDeltaTime=priorStep;Debug.Log((success?"S13_VALIDATION_PASS ":"S13_VALIDATION_FAIL ")+message);if(Application.isBatchMode)EditorApplication.Exit(success?0:1);else EditorApplication.isPlaying=false;
        }
    }
}
