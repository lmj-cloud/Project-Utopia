using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Unity.Cinemachine;
namespace Capstone.Direction.Workspace.S10.Editor
{
    [InitializeOnLoad]
    public static class S10Validation
    {
        const string Key="S10.Validation.Pending";
        static S10CrashSequence c;
        static bool initialized,driveShot,approachShot,impactSeen,impactShot,redShot,coveredShot;
        static bool[] eyes;
        static int stage,starts,events,forwarded,transferFrame;
        static float began,completedAt=-1,waitAt,originalScale,originalStep,tone1,tone2,tone3;
        static Vector3 carStart,truckStart,cameraStart;
        static Quaternion cameraRotation;
        static Quaternion outputEarly;
        static bool outputMoved;
        static float keyStart;
        static UnityEngine.UI.Image destination;
        static readonly List<UnityEngine.Object> transient=new List<UnityEngine.Object>();
        static S10Validation(){EditorApplication.update+=Tick;}
        [MenuItem("Capstone/Direction/S10 Workspace/Run Play validation")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Resolve unsaved scenes first.");
            initialized=false;c=null;EditorSceneManager.OpenScene(S10WorkbenchBuilder.ScenePath);TimingRoundTrip();SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
        }
        static void TimingRoundTrip()
        {
            var s=AssetDatabase.LoadAssetAtPath<S10Timing>(S10WorkbenchBuilder.SettingsPath);float before=s.normalDrive;string guid=AssetDatabase.AssetPathToGUID(S10WorkbenchBuilder.TimelinePath);
            try{s.normalDrive=before+.2f;S10WorkbenchBuilder.ApplyTiming();var t=AssetDatabase.LoadAssetAtPath<TimelineAsset>(S10WorkbenchBuilder.TimelinePath);Check(Math.Abs(t.duration-(s.FinishAt+.1f))<.03,"timing duration applied");Check(guid==AssetDatabase.AssetPathToGUID(S10WorkbenchBuilder.TimelinePath),"Timeline GUID retained");}
            finally{s.normalDrive=before;S10WorkbenchBuilder.ApplyTiming();}
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            try
            {
                if(!initialized)
                {
                    c=UnityEngine.Object.FindFirstObjectByType<S10CrashSequence>();if(!c||Time.frameCount<3)return;if(c.Active)c.Cancel();c.autoStartStandalone=false;
                    originalScale=Time.timeScale;originalStep=Time.fixedDeltaTime;Time.timeScale=.7f;Time.fixedDeltaTime=.014f;began=Time.realtimeSinceStartup;stage=starts=events=forwarded=0;completedAt=-1;eyes=new bool[5];driveShot=approachShot=impactSeen=impactShot=redShot=coveredShot=false;tone1=tone2=tone3=0;
                    outputMoved=false;Snapshot();c.onStarted.AddListener(()=>starts++);c.onS11Ready.AddListener(()=>{events++;Check(c.screen.overlay.color.a>=.999f,"S11 callback is fully covered");});c.GetComponent<S10StoryAdapter>().onHospitalRequested.AddListener(()=>forwarded++);
                    c.onImpact.AddListener(()=>{Check(c.crash.isPlaying&&c.impactLight.intensity>0&&Array.Exists(c.fragments,f=>f.gameObject.activeSelf),"actual collision audio/light/fragments active");impactSeen=true;});
                    Check(c.driver&&c.driver.realTruckUsed,"actual imported truck selected");Check(c.truck.GetComponentsInChildren<MeshFilter>().Any(m=>m.sharedMesh&&m.sharedMesh.vertexCount>1000&&AssetDatabase.GetAssetPath(m.sharedMesh).Contains("S10/Resources/TruckFromLocalCache")),"real FBX meshes referenced, not blockout");
                    Check(c.truck.GetComponentsInChildren<Camera>(true).All(camera=>!camera.isActiveAndEnabled),"FBX authoring cameras cannot overwrite Game view");
                    Check(Camera.allCameras.Where(camera=>camera.targetTexture==null&&camera.targetDisplay==c.outputCamera.targetDisplay).SequenceEqual(new[]{c.outputCamera}),"Game display rendered only by intended output camera");
                    Check(c.driver.seatEye.localPosition.x<0&&c.driver.wifeSeat.localPosition.x>0&&c.driver.rearLeft.localPosition.z<0&&c.driver.rearRight.localPosition.z<0,"driver / wife passenger / two rear child stand-ins");
                    Check(!c.BeginFromS09(new S10DriveState{vehicleBoarded=false}),"invalid S09 input rejected");var old=c.timing;var stale=UnityEngine.Object.Instantiate(old);stale.normalDrive+=.25f;stale.truckApproach-=.25f;c.timing=stale;Check(!c.BeginFromS09(new S10DriveState()),"unapplied equal-total timing rejected");c.timing=old;UnityEngine.Object.DestroyImmediate(stale);
                    var input=new S10DriveState();Check(c.BeginFromS09(input),"start accepted");input.departureReady=false;Check(c.InputState.departureReady,"input copied");c.StartStandalone();Check(starts==1,"duplicate start blocked");
                    foreach(var a in c.Sources){var data=new float[a.clip.samples*a.clip.channels];Check(a.clip.GetData(data,0)&&Array.Exists(data,v=>Mathf.Abs(v)>.002f),"non-silent temporary PCM");}initialized=true;
                }
                if(Time.realtimeSinceStartup-began>150)throw new TimeoutException("S10 driver validation deadline");
                if(stage==6)
                {
                    CheckSilent(S10BlackoutRelay.Current&&S10BlackoutRelay.Current.Overlay.color.a==1,"blackout lost after source destruction");
                    if(Time.realtimeSinceStartup-waitAt<.3f)return;
                    Check(S10BlackoutRelay.Current.TimedOut,"timeout after source destruction still holds black");destination.color=Color.black;transferFrame=Time.frameCount;Check(S10BlackoutRelay.TransferToReadyDestination(destination),"opaque ready destination accepted");stage=7;return;
                }
                if(stage==7)
                {
                    if(S10BlackoutRelay.Current)return;Check(Time.frameCount-transferFrame>=2&&destination.color.a==1,"two-frame no-gap ownership transfer");UnityEngine.Object.DestroyImmediate(destination.canvas.gameObject);destination=null;Reload(false);FastTimeline();c.integrationMode=true;c.handoffTimeout=.2f;c.StartStandalone();stage=8;return;
                }
                var s=c.timing;float t=c.Playhead;
                if(stage==0)
                {
                    if(c.Active&&t>=.2f){CheckSilent(Vector3.Distance(c.cinematicPose.position,c.driver.seatEye.position)<.27f,"camera escaped driver seat");CheckSilent(c.tinnitus.volume<=Mathf.Min(.25f,s.tinnitusMaximum)+.001f,"tinnitus volume cap exceeded");}
                    if(!driveShot&&t>=.65f){Check(Camera.main.GetComponent<CinemachineBrain>().ActiveVirtualCamera.Name=="CM_S10_DriverFirstPerson","actual driver POV camera active");outputEarly=c.outputCamera.transform.rotation;Capture("S10-driver-normal.png");driveShot=true;}
                    if(!outputMoved&&t>=1.5f){Check(Quaternion.Angle(outputEarly,c.outputCamera.transform.rotation)>5,"rendering camera actually rotates across frames");Check(Quaternion.Angle(c.outputCamera.transform.rotation,c.cinematicPose.rotation)<3&&Vector3.Distance(c.outputCamera.transform.position,c.cinematicPose.position)<.05f,"rendering camera follows procedural pose");outputMoved=true;}
                    if(!approachShot&&t>=s.ApproachAt+.6f){Check(c.truck.localPosition.z<truckStart.z&&InView(c.truck.position+Vector3.up*1.6f),"truck approaches through forward view");Capture("S10-driver-approach.png");approachShot=true;}
                    if(!impactShot&&c.ImpactCount==1&&t>=s.ImpactAt+.04f){Capture("S10-driver-impact.png");impactShot=true;}
                    if(!redShot&&t>=s.ConfusionAt+.7f){Check(c.driver.RedStrength>0&&c.driver.subjectiveRed.color.r>c.driver.subjectiveRed.color.g&&c.confusionVolume.weight>0,"red view and confusion volume active");Check(c.driver.windshieldCracks.activeSelf&&c.roadFilter.cutoffFrequency<2000,"cracks and muffled environment");Capture("S10-driver-red.png");redShot=true;}
                    if(t>=s.FirstClosedAt+.01f&&t<s.FirstOpenAt){CheckSilent(c.screen.overlay.color.a>=.999f,"first eye closure not opaque");eyes[0]=true;}
                    if(t>=s.FirstReopenedAt+.04f&&t<s.SecondAt){CheckSilent(c.screen.overlay.color.a<.01f,"first reopening absent");eyes[1]=true;tone1=c.tinnitus.volume;}
                    if(t>=s.SecondClosedAt+.01f&&t<s.SecondOpenAt){CheckSilent(c.screen.overlay.color.a>=.999f,"second eye closure not opaque");eyes[2]=true;}
                    if(t>=s.SecondReopenedAt+.04f&&t<s.ThirdAt){CheckSilent(c.screen.overlay.color.a<.01f,"second reopening absent");eyes[3]=true;tone2=c.tinnitus.volume;}
                    if(t>=s.CoveredAt-.15f&&t<s.CoveredAt)tone3=Mathf.Max(tone3,c.tinnitus.volume);
                    if(c.Active&&t>=s.CoveredAt+.02f){CheckSilent(c.screen.overlay.color.a>=.999f,"final closure reopened");eyes[4]=true;if(!coveredShot){Capture("S10-driver-final-black.png");coveredShot=true;}}
                    if(!c.Completed){if(!c.Active)throw new Exception("normal sequence stopped: "+c.Phase);return;}
                    if(completedAt<0){completedAt=Time.realtimeSinceStartup;return;}if(Time.realtimeSinceStartup-completedAt<.8f)return;
                    Check(driveShot&&approachShot&&impactSeen&&impactShot&&redShot&&coveredShot&&eyes.All(x=>x),"full POV crash and three closures / only two reopenings");Check(tone1>0&&tone2>tone1&&tone3>tone2,"tinnitus increases across cycles: "+tone1+" / "+tone2+" / "+tone3);
                    Check(s.blink2Hold>s.blink1Hold&&s.blackHold>s.blink2Hold&&s.blink2Open>s.blink1Open&&s.fadeToBlack>s.blink2Close,"progressively longer holds and slower reopening/final close");
                    Check(events==1&&forwarded==1&&c.hospitalState.unconscious&&c.hospitalState.requiresBlackStart,"existing S11 contract forwarded once with unconscious state");Check(c.IsBlackHeld&&c.screen.overlay.color.a==1,"no automatic reopening after completion");c.StartStandalone();Check(c.CompletionCount==1&&!c.Active,"completed duplicate blocked");
                    c.ResetStandalonePreview();Restored();Reload(true);stage=1;return;
                }
                if(stage==1&&t>=s.ApproachAt+.5f){c.Cancel();Restored();c.StartStandalone();stage=2;return;}
                if(stage==2&&t>=s.ImpactAt+.3f){c.Cancel();Restored();c.InjectImpactFault=true;c.StartStandalone();stage=3;return;}
                if(stage==3&&!c.Active){Check(c.Phase=="Cancelled"&&c.CompletionCount==0,"impact exception restores without completion");Restored();c.InjectImpactFault=false;c.watchdogSeconds=.2f;c.StartStandalone();stage=4;return;}
                if(stage==4&&!c.Active){Check(c.Playhead<1,"sequence watchdog");Restored();c.watchdogSeconds=40;c.StartStandalone();c.enabled=false;Restored();Check(!c.Active,"disabled component recovered");Reload(false);FastTimeline();c.integrationMode=true;c.StartStandalone();stage=5;return;}
                if(stage==5&&c.Completed)
                {
                    var relay=S10BlackoutRelay.Current;Check(relay&&relay.Overlay.color.a==1&&relay.gameObject.scene.name=="DontDestroyOnLoad","persistent opaque integration relay");Check(!S10BlackoutRelay.TransferToReadyDestination(null),"unready destination cannot release black");Check(!S10BlackoutRelay.TransferToReadyDestination(relay.Overlay)&&!S10BlackoutRelay.TransferToReadyDestination(c.screen.overlay),"relay or source overlay cannot receive ownership");destination=DestinationImage();Check(!S10BlackoutRelay.TransferToReadyDestination(destination),"transparent destination rejected");destination.color=Color.black;destination.rectTransform.localScale=Vector3.one*.5f;Check(!S10BlackoutRelay.TransferToReadyDestination(destination),"partial-screen destination rejected");destination.rectTransform.localScale=Vector3.one;destination.color=Color.clear;UnityEngine.Object.DestroyImmediate(c.transform.root.gameObject);Check(S10BlackoutRelay.Current&&S10BlackoutRelay.Current.Overlay.color.a==1,"source destruction keeps black");relay.timeoutSeconds=.15f;waitAt=Time.realtimeSinceStartup;stage=6;return;
                }
                if(stage==8&&c.Completed){stage=9;return;}
                if(stage==9&&S10BlackoutRelay.Current&&S10BlackoutRelay.Current.TimedOut){Check(c.HandoffPending&&c.IsBlackHeld&&S10BlackoutRelay.Current.Overlay.color.a==1,"handoff timeout does not reveal unready S11");c.Cancel();Check(!c.HandoffPending&&!c.IsBlackHeld&&c.Phase=="HandoffCancelled","explicit abort restores handoff ownership");Restored();Finish(true,"driver POV / family seats / real FBX / impact-red-blur / 3 closures 2 reopenings / bounded growing tinnitus / final black / standalone reset / cancel-timeout-exception-disable / persistent relay survives source destruction / opaque two-frame transfer / handoff timeout");}
            }
            catch(Exception e){Finish(false,e.ToString());}
        }
        static void Snapshot(){carStart=c.car.localPosition;truckStart=c.truck.localPosition;cameraStart=c.cinematicPose.position;cameraRotation=c.cinematicPose.rotation;keyStart=c.keyLight.intensity;}
        static void Reload(bool start){if(c)UnityEngine.Object.DestroyImmediate(c.transform.root.gameObject);var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(S10WorkbenchBuilder.PrefabPath));c=root.GetComponentInChildren<S10CrashSequence>();c.autoStartStandalone=false;Snapshot();if(start)c.StartStandalone();}
        static void FastTimeline()
        {
            var s=ScriptableObject.CreateInstance<S10Timing>();s.normalDrive=.3f;s.truckApproach=.35f;s.danger=.2f;s.impact=.05f;s.confusion=.25f;s.blink1Close=.1f;s.blink1Hold=.15f;s.blink1Open=.15f;s.blink1Rest=.1f;s.blink2Close=.2f;s.blink2Hold=.25f;s.blink2Open=.3f;s.blink2Rest=.15f;s.fadeToBlack=.4f;s.blackHold=.35f;s.appliedTimingSignature=s.TimingSignature;c.timing=s;transient.Add(s);
            var timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=s.FinishAt+.1;var track=timeline.CreateTrack<SignalTrack>(null,"FAST IN-MEMORY INTEGRATION TEST");string[] names={"Approach","Danger","Impact","Confusion","Blackout","Covered","Finish"};double[] times={s.ApproachAt,s.DangerAt,s.ImpactAt,s.ConfusionAt,s.BlackAt,s.CoveredAt,s.FinishAt};for(int i=0;i<names.Length;i++){var marker=track.CreateMarker<SignalEmitter>(times[i]);marker.asset=AssetDatabase.LoadAssetAtPath<SignalAsset>(S10WorkbenchBuilder.Root+"/Timelines/"+names[i]+".signal");marker.emitOnce=true;marker.retroactive=true;}c.sequence.director.playableAsset=timeline;c.sequence.director.SetGenericBinding(track,c.GetComponent<SignalReceiver>());transient.Add(timeline);
        }
        static UnityEngine.UI.Image DestinationImage(){var go=new GameObject("S10_TEST_ReceiverOverlay",typeof(Canvas));go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var child=new GameObject("ReadyBlack",typeof(RectTransform),typeof(UnityEngine.UI.Image));child.transform.SetParent(go.transform,false);var r=(RectTransform)child.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;var image=child.GetComponent<UnityEngine.UI.Image>();image.color=Color.clear;return image;}
        static void Restored()
        {
            Check(!c.controlLock.IsAcquired&&!c.controlLock.prototypePlayer.ControlsLocked&&c.controlLock.prototypePlayer.enabled,"input restored");Check(Vector3.Distance(c.car.localPosition,carStart)<.001f&&Vector3.Distance(c.truck.localPosition,truckStart)<.001f,"vehicles restored");Check(Vector3.Distance(c.cinematicPose.position,cameraStart)<.001f&&Quaternion.Angle(c.cinematicPose.rotation,cameraRotation)<.1f&&c.sequence.cinematicCamera.Priority.Value==0,"camera pose and priority restored");Check(c.screen.overlay.color.a==0&&c.driver.subjectiveRed.color.a==0&&!c.driver.windshieldCracks.activeSelf&&c.confusionVolume.weight==0,"black/red/cracks/Volume restored");Check(c.keyLight.intensity==keyStart&&c.impactLight.intensity==0&&c.roadFilter.cutoffFrequency>21000,"lights and filter restored");Check(c.Sources.All(a=>!a.isPlaying)&&c.fragments.All(f=>!f.gameObject.activeSelf),"audio and fixed fragments restored");Check(Mathf.Abs(Time.timeScale-.7f)<.001f&&Mathf.Abs(Time.fixedDeltaTime-.014f)<.0001f,"time scale restored");
        }
        static bool InView(Vector3 point){var p=Camera.main.WorldToViewportPoint(point);return p.z>0&&p.x>0&&p.x<1&&p.y>0&&p.y<1;}
        static void CheckSilent(bool ok,string label){if(!ok)throw new Exception(label);}
        static void Check(bool ok,string label){CheckSilent(ok,label);Debug.Log("S10_CHECK_PASS "+label);}
        static void Capture(string name)
        {
            var folder=Environment.GetEnvironmentVariable("S10_VALIDATION_DIR");if(string.IsNullOrEmpty(folder))return;var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var canvases=UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(v=>v.isRootCanvas&&v.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();var cameras=canvases.Select(v=>v.worldCamera).ToArray();var distances=canvases.Select(v=>v.planeDistance).ToArray();
            try{camera.targetTexture=rt;foreach(var v in canvases){v.renderMode=RenderMode.ScreenSpaceCamera;v.worldCamera=camera;v.planeDistance=.2f;}Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();if(name.Contains("final-black"))Check(image.GetPixels32().All(p=>p.r<=2&&p.g<=2&&p.b<=2),"full composite capture is black at every pixel");System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,name),image.EncodeToPNG());}
            finally{for(int i=0;i<canvases.Length;i++){canvases[i].renderMode=RenderMode.ScreenSpaceOverlay;canvases[i].worldCamera=cameras[i];canvases[i].planeDistance=distances[i];}camera.targetTexture=old;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        static void Finish(bool success,string message)
        {
            SessionState.SetBool(Key,false);S10BlackoutRelay.CancelPending();if(c)c.Cancel();if(destination)UnityEngine.Object.DestroyImmediate(destination.canvas.gameObject);foreach(var o in transient)if(o)UnityEngine.Object.DestroyImmediate(o);transient.Clear();Time.timeScale=originalScale;Time.fixedDeltaTime=originalStep;Debug.Log((success?"S10_DRIVER_VALIDATION_PASS ":"S10_DRIVER_VALIDATION_FAIL ")+message);if(Application.isBatchMode)EditorApplication.Exit(success?0:1);else EditorApplication.isPlaying=false;
        }
    }
}
