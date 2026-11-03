using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
using Unity.Cinemachine;
using Capstone.Direction.Workspace.S03;
using S03Prototype;
namespace Capstone.Direction.Workspace.S13.Editor
{
    public static class S13WorkbenchBuilder
    {
        public const string Root="Assets/Capstone/Direction/Workspace/Sequences/S13";
        public const string ScenePath=Root+"/Scenes/S13_BunkerExit.unity",PrefabPath=Root+"/Prefabs/S13_TestRig.prefab",SequencePrefabPath=Root+"/Prefabs/S13_SequenceRig.prefab",TimelinePath=Root+"/Timelines/S13_BunkerEnding.playable",TimingPath=Root+"/S13Timing.asset";
        [InitializeOnLoadMethod]
        static void AuditAfterReload(){EditorApplication.delayCall+=()=>{if(!EditorApplication.isPlayingOrWillChangePlaymode&&SceneManager.GetActiveScene().path==ScenePath&&AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))AuditSavedRig();};}
        [MenuItem("Capstone/Direction/S13 Workspace/Audit saved prefab scripts")]
        public static void AuditSavedRig()
        {
            int missing=0;
            foreach(string path in new[]{SequencePrefabPath,PrefabPath})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!prefab)throw new InvalidOperationException("Missing prefab: "+path);
                foreach(var node in prefab.GetComponentsInChildren<Transform>(true)){int count=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);missing+=count;if(count>0)Debug.LogError("S13_MISSING_SCRIPT "+path+" / "+node.name);}
            }
            if(missing>0)throw new InvalidOperationException("S13 missing script count: "+missing);
            Debug.Log("S13_PREFAB_SCRIPT_AUDIT_PASS missing=0");
        }
        static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Resolve unsaved scenes first. No automatic save/discard.");}
        [MenuItem("Capstone/Direction/S13 Workspace/Open test scene")]
        public static void Open(){Guard();EditorSceneManager.OpenScene(ScenePath);}
        [MenuItem("Capstone/Direction/S13 Workspace/Play test in Game view")]
        public static void Play(){Open();var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");if(type!=null)EditorWindow.GetWindow(type).Focus();EditorApplication.isPlaying=true;}
        [MenuItem("Capstone/Direction/S13 Workspace/Generate independent prototype")]
        public static void Build()
        {
            Guard();if(File.Exists(ScenePath)){Open();return;}
            foreach(var folder in new[]{"Scenes","Prefabs","Timelines","Materials","Audio"})if(!AssetDatabase.IsValidFolder(Root+"/"+folder))AssetDatabase.CreateFolder(Root,folder);
            var prior=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,string.IsNullOrEmpty(prior.path)?NewSceneMode.Single:NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var timing=AssetDatabase.LoadAssetAtPath<S13Timing>(TimingPath);if(!timing){timing=ScriptableObject.CreateInstance<S13Timing>();AssetDatabase.CreateAsset(timing,TimingPath);}
            var concrete=Mat("BunkerConcrete",new Color(.19f,.22f,.23f));var metal=Mat("DoorSteel",new Color(.15f,.21f,.22f));var edge=Mat("RibsAndPipes",new Color(.075f,.095f,.10f));var warm=Mat("WarmLamp",new Color(1,.78f,.44f),true);var outside=Mat("ExitLight",Color.white,true);var avatarMat=Mat("TestAvatar",new Color(.18f,.40f,.48f));
            var root=new GameObject("S13_TEST_ONLY");var env=Child(root.transform,"BunkerBlockout");
            Box(env,"Floor",new Vector3(0,-.2f,2),new Vector3(6,.4f,18),concrete);Box(env,"Ceiling",new Vector3(0,3.7f,2),new Vector3(6,.3f,18),concrete);
            Box(env,"LeftWall",new Vector3(-3,1.8f,2),new Vector3(.35f,3.8f,18),concrete);Box(env,"RightWall",new Vector3(3,1.8f,2),new Vector3(.35f,3.8f,18),concrete);Box(env,"RearWall",new Vector3(0,1.8f,-6.8f),new Vector3(6,3.8f,.35f),concrete);
            for(int i=0;i<6;i++)foreach(int side in new[]{-1,1}){Box(env,"SupportRib",new Vector3(side*2.75f,1.8f,-5+i*2.3f),new Vector3(.2f,3.5f,.2f),edge);Box(env,"ServicePipe",new Vector3(side*2.78f,2.8f,-5+i*2.3f),new Vector3(.12f,.12f,2.3f),metal);}
            Box(env,"ExitWallLeft",new Vector3(-2.05f,1.8f,6),new Vector3(1.9f,3.8f,.35f),concrete);Box(env,"ExitWallRight",new Vector3(2.05f,1.8f,6),new Vector3(1.9f,3.8f,.35f),concrete);Box(env,"DoorLintel",new Vector3(0,3.2f,6),new Vector3(2.5f,.9f,.4f),edge);
            var door=Child(env,"ExitDoorHinge");door.localPosition=new Vector3(-1.1f,0,6);Box(door,"DoorLeaf",new Vector3(1.1f,1.4f,0),new Vector3(2.2f,2.8f,.18f),metal);Box(door,"Handle",new Vector3(1.9f,1.3f,-.17f),new Vector3(.08f,.28f,.08f),warm);
            Box(env,"UnspecifiedWhiteBeyondExit",new Vector3(0,1.8f,11),new Vector3(5.8f,3.8f,.2f),outside);
            for(int i=0;i<4;i++){Box(env,"CeilingLamp",new Vector3(0,3.45f,-4+i*3),new Vector3(.9f,.06f,.25f),warm);var lamp=Child(env,"InteriorLamp").gameObject.AddComponent<Light>();lamp.type=LightType.Point;lamp.transform.position=new Vector3(0,3,-4+i*3);lamp.range=6;lamp.intensity=1.6f;lamp.color=new Color(1,.83f,.60f);lamp.shadows=LightShadows.None;}
            var exitLight=Child(env,"ExitSpillLight").gameObject.AddComponent<Light>();exitLight.transform.position=new Vector3(0,2.4f,7);exitLight.type=LightType.Point;exitLight.range=10;exitLight.intensity=1.2f;exitLight.color=new Color(.86f,.94f,1);exitLight.shadows=LightShadows.None;
            var anchor=Child(env,"ExitInteraction");anchor.position=new Vector3(0,0,5.9f);var doorView=Child(env,"CameraDoorPose");doorView.position=new Vector3(0,1.8f,3.5f);doorView.rotation=Quaternion.LookRotation(new Vector3(0,1.65f,6.5f)-doorView.position);var exitView=Child(env,"CameraExitPose");exitView.position=new Vector3(0,1.7f,6.6f);exitView.rotation=Quaternion.identity;
            var player=Child(root.transform,"TEST_ThirdPersonPlayer");player.position=new Vector3(0,.03f,3.1f);var cc=player.gameObject.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.25f;
            var body=GameObject.CreatePrimitive(PrimitiveType.Capsule);body.name="TestAvatarVisual";body.transform.SetParent(player,false);body.transform.localPosition=Vector3.up*.9f;body.transform.localScale=new Vector3(.5f,.9f,.5f);body.GetComponent<Renderer>().sharedMaterial=avatarMat;UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            var pivot=Child(player,"ViewPivot");pivot.localPosition=Vector3.up*1.4f;var mover=player.gameObject.AddComponent<S03FirstPersonPlayer>();var serialized=new SerializedObject(mover);serialized.FindProperty("view").objectReferenceValue=pivot;serialized.ApplyModifiedPropertiesWithoutUndo();
            var cameras=Child(root.transform,"TEST_Cameras");var output=Child(cameras,"Main Camera").gameObject.AddComponent<Camera>();output.tag="MainCamera";output.nearClipPlane=.05f;output.farClipPlane=100;output.allowHDR=true;output.clearFlags=CameraClearFlags.SolidColor;output.backgroundColor=new Color(.05f,.06f,.07f);output.gameObject.AddComponent<AudioListener>();var urp=output.gameObject.AddComponent<UniversalAdditionalCameraData>();urp.renderPostProcessing=true;
            var brain=output.gameObject.AddComponent<CinemachineBrain>();brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,.65f);brain.UpdateMethod=CinemachineBrain.UpdateMethods.LateUpdate;brain.IgnoreTimeScale=true;
            var gameplay=Child(cameras,"CM_ThirdPerson").gameObject.AddComponent<CinemachineCamera>();gameplay.Priority=20;var follow=gameplay.gameObject.AddComponent<S03ThirdPersonRig>();follow.pivot=pivot;follow.Snap();output.transform.SetPositionAndRotation(gameplay.transform.position,gameplay.transform.rotation);
            var logic=Child(root.transform,"S13_SequenceRig");var director=logic.gameObject.AddComponent<PlayableDirector>();director.playOnAwake=false;director.timeUpdateMode=DirectorUpdateMode.UnscaledGameTime;director.extrapolationMode=DirectorWrapMode.Hold;var sequence=logic.gameObject.AddComponent<DirectionSequence>();var c=logic.gameObject.AddComponent<S13EndingSequence>();var adapter=logic.gameObject.AddComponent<S13EndingAdapter>();adapter.target=c;
            var cine=Child(logic,"CM_S13_Ending").gameObject.AddComponent<CinemachineCamera>();cine.Priority=0;var lens=cine.Lens;lens.FieldOfView=55;lens.NearClipPlane=.05f;cine.Lens=lens;
            var screen=logic.gameObject.AddComponent<DirectionScreen>();var canvas=Child(logic,"S13_FadeCanvas").gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;var scaler=canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            var textGo=new GameObject("TestStatus",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));textGo.transform.SetParent(canvas.transform,false);var text=textGo.GetComponent<TMPro.TextMeshProUGUI>();text.font=TMPro.TMP_Settings.defaultFontAsset;text.fontSize=22;text.color=Color.white;text.raycastTarget=false;var tr=(RectTransform)textGo.transform;tr.anchorMin=new Vector2(0,1);tr.anchorMax=new Vector2(1,1);tr.pivot=new Vector2(0,1);tr.anchoredPosition=new Vector2(20,-20);tr.sizeDelta=new Vector2(-40,75);
            var imageGo=new GameObject("WhiteCoverage",typeof(RectTransform),typeof(UnityEngine.UI.Image));imageGo.transform.SetParent(canvas.transform,false);var rect=(RectTransform)imageGo.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;screen.overlay=imageGo.GetComponent<UnityEngine.UI.Image>();screen.overlay.color=Color.clear;screen.overlay.raycastTarget=false;
            var volume=Child(logic,"S13_WhiteVolume").gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=40;volume.weight=0;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Root+"/Materials/S13_WhiteProfile.asset");var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(1.1f);bloom.threshold.Override(.8f);bloom.scatter.Override(.65f);var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(1.2f);color.saturation.Override(-20);foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);volume.sharedProfile=profile;AssetDatabase.SaveAssetIfDirty(profile);
            var audio=Child(logic,"S13_OwnedAudio");var hum=Sound(audio,"TEMP_BunkerHum",0,2,.16f,true);var wind=Sound(audio,"TEMP_ExitWind",1,2,0,true);var doorClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Gamemaster Audio - Pro Sound Collection/Doors/door_creak_short_01.wav");
            var doorSource=Child(audio,"DoorSound").gameObject.AddComponent<AudioSource>();doorSource.playOnAwake=false;doorSource.volume=.35f;doorSource.clip=doorClip?doorClip:Sound(audio,"TEMP_DoorCue",2,1,.2f,false).clip;
            c.sequence=sequence;c.screen=screen;c.controlLock=logic.gameObject.AddComponent<S03ControlLockAdapter>();c.timing=timing;c.endingVolume=volume;c.ambient=hum;c.wind=wind;c.doorSound=doorSource;c.status=text;
            sequence.director=director;sequence.cinematicCamera=cine;sequence.screen=null;sequence.timelineTimeout=130;
            UnityEventTools.AddPersistentListener(sequence.onStarted,c.SequenceStarted);UnityEventTools.AddPersistentListener(sequence.onCompleted,c.SequenceCompleted);UnityEventTools.AddPersistentListener(sequence.onCancelled,c.SequenceCancelled);
            var timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=timing.FinishAt+.1f;AssetDatabase.CreateAsset(timeline,TimelinePath);var track=timeline.CreateTrack<SignalTrack>(null,"S13 Exit and ending cues");var receiver=logic.gameObject.AddComponent<SignalReceiver>();
            string[] names={"OpenDoor","WhiteRise","Covered","Finish"};UnityAction[] actions={c.OpenDoor,c.WhiteRise,c.Covered,c.Finish};float[] times={timing.OpenAt,timing.WhiteAt,timing.CoveredAt,timing.FinishAt};for(int i=0;i<4;i++){var signal=ScriptableObject.CreateInstance<SignalAsset>();AssetDatabase.CreateAsset(signal,Root+"/Timelines/"+names[i]+".signal");var marker=track.CreateMarker<SignalEmitter>(times[i]);marker.asset=signal;marker.emitOnce=true;marker.retroactive=true;var reaction=new UnityEvent();UnityEventTools.AddPersistentListener(reaction,actions[i]);receiver.AddReaction(signal,reaction);}
            director.playableAsset=timeline;director.SetGenericBinding(track,receiver);AssetDatabase.SaveAssetIfDirty(timeline);
            // Portable prefab deliberately has no reference to the test player, camera or bunker.
            PrefabUtility.SaveAsPrefabAssetAndConnect(logic.gameObject,SequencePrefabPath,InteractionMode.AutomatedAction);
            c.player=player;c.outputCamera=output;c.door=door;c.doorView=doorView;c.exitView=exitView;c.exitAnchor=anchor;c.exitLight=exitLight;c.controlLock.prototypePlayer=mover;sequence.inputComponents=new Behaviour[]{mover};PrefabUtility.RecordPrefabInstancePropertyModifications(c);PrefabUtility.RecordPrefabInstancePropertyModifications(c.controlLock);PrefabUtility.RecordPrefabInstancePropertyModifications(sequence);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root,PrefabPath,InteractionMode.AutomatedAction);EditorSceneManager.SaveScene(scene,ScenePath);Debug.Log("S13_BUILD_PASS "+ScenePath);
        }
        [MenuItem("Capstone/Direction/S13 Workspace/Apply timing to Timeline")]
        public static void ApplyTiming()
        {
            Guard();var timing=AssetDatabase.LoadAssetAtPath<S13Timing>(TimingPath);if(!timing||!timing.Valid)throw new InvalidOperationException("Invalid S13 timing");var timeline=AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);float[] times={timing.OpenAt,timing.WhiteAt,timing.CoveredAt,timing.FinishAt};string[] names={"OpenDoor","WhiteRise","Covered","Finish"};foreach(var track in timeline.GetOutputTracks())foreach(var marker in track.GetMarkers())if(marker is SignalEmitter signal&&signal.asset){int i=Array.IndexOf(names,signal.asset.name);if(i>=0){signal.time=times[i];EditorUtility.SetDirty(signal);AssetDatabase.SaveAssetIfDirty(signal);}}timeline.fixedDuration=timing.FinishAt+.1f;EditorUtility.SetDirty(timeline);AssetDatabase.SaveAssetIfDirty(timeline);EditorUtility.SetDirty(timing);AssetDatabase.SaveAssetIfDirty(timing);
        }
        static Transform Child(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static Transform Box(Transform parent,string name,Vector3 position,Vector3 size,Material mat){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;}
        static Material Mat(string name,Color color,bool emission=false){string path=Root+"/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat)return mat;mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;if(emission){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*3);}AssetDatabase.CreateAsset(mat,path);return mat;}
        static AudioSource Sound(Transform parent,string name,int kind,float seconds,float gain,bool loop)
        {
            string path=Root+"/Audio/"+name+".wav";int rate=22050,count=(int)(seconds*rate);var random=new System.Random(1300+kind);float low=0;
            using(var w=new BinaryWriter(File.Create(path))){w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);for(int i=0;i<count;i++){float t=(float)i/rate;low=Mathf.Lerp(low,(float)random.NextDouble()*2-1,.04f);float envelope=Mathf.Clamp01(t/.08f)*Mathf.Clamp01((seconds-t)/.08f);float value=kind==0?Mathf.Sin(t*2*Mathf.PI*65)*.13f+low*.15f:low*.45f;w.Write((short)(Mathf.Clamp(value*envelope,-1,1)*short.MaxValue));}}
            AssetDatabase.ImportAsset(path);var source=Child(parent,name).gameObject.AddComponent<AudioSource>();source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);source.loop=loop;source.volume=gain;source.playOnAwake=false;return source;
        }
        public static void BuildAndValidate(){Build();S13Validation.Run();}
    }
}
