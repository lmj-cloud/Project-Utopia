using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using Capstone.Direction.Workspace.S03;
using Capstone.Direction.Workspace.S10;
using Capstone.Direction.Workspace.S13;

namespace Capstone.Direction.Workspace.S07.Editor
{
    // Uses saved scenes without rebuilding, changing timing assets, or restarting
    // the first auto-start. Runtime-only fault setup is discarded on leaving Play.
    [InitializeOnLoad]
    public static class DirectorStartupValidation
    {
        const string Pending="Direction.StartupValidation.Pending",Case="Direction.StartupValidation.Case";
        static readonly string[] Scenes={S07WorkbenchBuilder.ScenePath,Capstone.Direction.Workspace.S10.Editor.S10WorkbenchBuilder.ScenePath,Capstone.Direction.Workspace.S13.Editor.S13WorkbenchBuilder.ScenePath};
        sealed class Context
        {
            public DirectionSequence sequence;public DirectionScreen screen;public S03ControlLockAdapter control;
            public AudioSource[] audio;public Func<bool> active,completed;public Func<int> count;public Func<float> time;public Func<string> phase;
            public Action cancel;public Action<float> watchdog;public UnityEvent started;
        }
        static Context c;static Keyboard keyboard;static float began,doneAt,scale,step;static int starts;static bool moved;static string error;
        static bool[] inputEnabled,audioPlaying;static float[] audioVolumes;static int priority;static bool cameraEnabled;static string lastPhase;static int second;
        static DirectorStartupValidation(){EditorApplication.playModeStateChanged+=State;EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
        [MenuItem("Capstone/Direction/Startup validation/Run first-start checks %#&F8")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first");
            Guard();SessionState.SetInt(Case,0);SessionState.SetBool(Pending,true);LoadCase();
        }
        static void Guard(){for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Resolve unsaved scenes first. No save/discard performed.");}
        static void LoadCase()
        {
            if(!SessionState.GetBool(Pending,false))return;
            int index=SessionState.GetInt(Case,0);
            if(index>=9){SessionState.SetBool(Pending,false);Debug.Log("DIRECTOR_STARTUP_VALIDATION_PASS first auto-start S07/S10; S13 first E; cancel before readiness; zero-clock startup watchdog; camera/input/audio/time restore");if(Application.isBatchMode)EditorApplication.Exit(0);return;}
            Guard();EditorSceneManager.OpenScene(Scenes[index%3]);c=null;
            if(!Application.isBatchMode){var view=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");if(view!=null)EditorWindow.GetWindow(view).Focus();}
            EditorApplication.isPlaying=true;
        }
        static void State(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Pending,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){try{Setup();}catch(Exception e){Fail(e);}}
            if(state==PlayModeStateChange.ExitingPlayMode){if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);keyboard=null;}
            if(state==PlayModeStateChange.EnteredEditMode)EditorApplication.delayCall+=LoadCase;
        }
        static void Setup()
        {
            int index=SessionState.GetInt(Case,0),kind=index%3,mode=index/3;
            starts=0;moved=false;error=null;lastPhase=null;second=-1;doneAt=-1;began=Time.realtimeSinceStartup;scale=Time.timeScale;step=Time.fixedDeltaTime;
            if(kind==0)
            {
                var x=UnityEngine.Object.FindFirstObjectByType<S07AwardSequence>();Check(x&&x.autoStartStandalone,"S07 saved auto-start enabled");
                c=new Context{sequence=x.sequence,screen=x.screen,control=x.controlLock,audio=new[]{x.awardAudio,x.warAudio,x.roomAudio,x.metalAudio,x.tinnitusAudio,x.breathAudio},active=()=>x.Active,completed=()=>x.Completed,count=()=>x.CompletionCount,time=()=>x.Playhead,phase=()=>x.Phase,cancel=x.Cancel,watchdog=v=>x.watchdogSeconds=v,started=x.onStarted};
            }
            else if(kind==1)
            {
                var x=UnityEngine.Object.FindFirstObjectByType<S10CrashSequence>();Check(x&&x.autoStartStandalone&&!x.integrationMode,"S10 saved standalone auto-start enabled");
                c=new Context{sequence=x.sequence,screen=x.screen,control=x.controlLock,audio=x.Sources,active=()=>x.Active,completed=()=>x.Completed,count=()=>x.CompletionCount,time=()=>x.Playhead,phase=()=>x.Phase,cancel=x.Cancel,watchdog=v=>x.watchdogSeconds=v,started=x.onStarted};
            }
            else
            {
                var x=UnityEngine.Object.FindFirstObjectByType<S13EndingSequence>();Check(x&&x.allowKeyboard&&x.InRange&&!x.Active,"S13 saved E interaction ready (no API start)");
                c=new Context{sequence=x.sequence,screen=x.screen,control=x.controlLock,audio=x.Sources,active=()=>x.Active,completed=()=>x.Completed,count=()=>x.CompletionCount,time=()=>x.Playhead,phase=()=>x.Phase,cancel=x.Cancel,watchdog=v=>x.watchdogSeconds=v,started=x.onStarted};
            }
            Check(!c.active()&&c.count()==0,"observe untouched first start before it runs");
            inputEnabled=c.sequence.inputComponents.Select(x=>x&&x.enabled).ToArray();audioPlaying=c.audio.Select(x=>x.isPlaying).ToArray();audioVolumes=c.audio.Select(x=>x.volume).ToArray();priority=c.sequence.cinematicCamera.Priority.Value;cameraEnabled=c.sequence.cinematicCamera.enabled;
            c.started.AddListener(()=>starts++);
            if(mode==1)c.started.AddListener(()=>c.cancel()); // cancellation while still waiting for first evaluation
            if(mode==2){c.sequence.director.timeUpdateMode=DirectorUpdateMode.Manual;c.watchdog(.2f);} // valid graph, no clock advance
            if(kind==2){keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.E));}
            Debug.Log($"STARTUP_CASE_BEGIN S{(kind==0?7:kind==1?10:13):00} mode={mode} frame={Time.frameCount} scale={scale}");
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Pending,false)||!EditorApplication.isPlaying||c==null||EditorApplication.isCompiling)return;
            try
            {
                if(error!=null)throw new Exception("Unexpected runtime error: "+error);
                int index=SessionState.GetInt(Case,0),kind=index%3,mode=index/3;
                float elapsed=Time.realtimeSinceStartup-began,t=c.time();string phase=c.phase();
                Check(elapsed<65,"case deadline");
                if(phase!=lastPhase||(int)t!=second){Debug.Log($"STARTUP_OBSERVE S{(kind==0?7:kind==1?10:13):00} mode={mode} phase={phase} t={t:F3} state={c.sequence.director.state} scale={Time.timeScale} camera={Camera.main?.GetComponent<CinemachineBrain>()?.ActiveVirtualCamera?.Name}");lastPhase=phase;second=(int)t;}
                if(t>.1f)moved=true;
                if(mode==0)
                {
                    Check(phase!="Cancelled","first start must not cancel");
                    if(!c.completed())return;
                    Check(starts==1&&c.count()==1&&moved,"first start completes once without restart");
                }
                else
                {
                    if(starts==0||c.active())return;
                    Check(phase=="Cancelled"&&c.count()==0,"cancel/watchdog does not complete");
                    if(mode==2)Check(t==0&&elapsed<3,"zero-clock initialization wait is bounded by watchdog");
                }
                if(doneAt<0){doneAt=Time.realtimeSinceStartup;return;}if(Time.realtimeSinceStartup-doneAt<.9f)return;
                Check(!c.active()&&!c.control.IsAcquired,"input lock released");
                for(int i=0;i<inputEnabled.Length;i++)Check(c.sequence.inputComponents[i].enabled==inputEnabled[i],"input enabled state restored");
                Check(c.sequence.cinematicCamera.Priority.Value==priority&&c.sequence.cinematicCamera.enabled==cameraEnabled,"camera ownership restored");
                Check(Camera.main.GetComponent<CinemachineBrain>().ActiveVirtualCamera.Name!=c.sequence.cinematicCamera.Name,"actual output camera returned");
                Check(Camera.allCameras.Length==1&&UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(x=>x.isActiveAndEnabled)==1,"single output camera/listener");
                for(int i=0;i<c.audio.Length;i++)Check(c.audio[i].isPlaying==audioPlaying[i]&&Mathf.Abs(c.audio[i].volume-audioVolumes[i])<.001f,"audio snapshot restored");
                Check(Mathf.Abs(Time.timeScale-scale)<.0001f&&Mathf.Abs(Time.fixedDeltaTime-step)<.0001f,"time scale preserved");
                Check(Mathf.Abs(c.screen.overlay.color.a-((mode==0&&kind!=0)?1:0))<.001f,"screen restored or intentionally covered on completion");
                Debug.Log($"STARTUP_CASE_PASS S{(kind==0?7:kind==1?10:13):00} mode={mode} starts={starts} completed={c.count()} t={t:F3}");
                SessionState.SetInt(Case,index+1);c=null;EditorApplication.isPlaying=false;
            }
            catch(Exception e){Fail(e);}
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);}
        static void Log(string message,string trace,LogType type){if(SessionState.GetBool(Pending,false)&&(type==LogType.Exception||type==LogType.Error||type==LogType.Assert))error=message;}
        static void Fail(Exception e){SessionState.SetBool(Pending,false);if(c!=null)c.cancel();if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);keyboard=null;Debug.LogError("DIRECTOR_STARTUP_VALIDATION_FAIL "+e);if(Application.isBatchMode)EditorApplication.Exit(1);else EditorApplication.isPlaying=false;}
    }
}
