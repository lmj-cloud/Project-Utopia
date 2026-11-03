using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using Capstone.Direction.Workspace.S03;
using S03Prototype;

namespace Capstone.Direction.Workspace.S10.Editor
{
    public static partial class S10WorkbenchBuilder
    {
        public const string Root="Assets/Capstone/Direction/Workspace/Sequences/S10";
        public const string ScenePath=Root+"/Scenes/S10_FamilyAccident.unity";
        public const string PrefabPath=Root+"/Prefabs/S10_TestRig.prefab";
        public const string TimelinePath=Root+"/Timelines/S10_FamilyAccident.playable";
        public const string SettingsPath=Root+"/S10Timing.asset";
        static readonly string[] CueNames={"Approach","Danger","Impact","Confusion","Blackout","Covered","Finish"};
        [MenuItem("Capstone/Direction/S10 Workspace/Open test scene")]
        public static void Open(){Guard();EditorSceneManager.OpenScene(ScenePath);}
        static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Resolve unsaved scenes yourself; no automatic saving/discarding.");}
        [MenuItem("Capstone/Direction/S10 Workspace/Generate first prototype")]
        public static void Build()
        {
            Guard();if(File.Exists(ScenePath)){UpgradeDriver();Open();return;}
            foreach(var folder in new[]{"Scenes","Prefabs","Timelines","Materials","Audio"})if(!AssetDatabase.IsValidFolder(Root+"/"+folder))AssetDatabase.CreateFolder(Root,folder);
            var settings=ScriptableObject.CreateInstance<S10Timing>();AssetDatabase.CreateAsset(settings,SettingsPath);
            var prior=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode&&string.IsNullOrEmpty(prior.path)?NewSceneMode.Single:NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var root=new GameObject("S10_TEST_ONLY");
            var asphalt=Mat("Road",new Color(.075f,.09f,.10f));var white=Mat("LaneAndLamps",new Color(.8f,.83f,.77f));var grass=Mat("Roadside",new Color(.18f,.24f,.17f));
            var carMat=Mat("FamilyCarBlue",new Color(.08f,.3f,.48f),.35f);var dark=Mat("RubberAndCabin",new Color(.035f,.045f,.055f));var truckMat=Mat("TruckOrange",new Color(.7f,.28f,.09f));var metal=Mat("TruckCargo",new Color(.39f,.43f,.43f),.25f);
            var skin=Mat("NeutralStandins",new Color(.53f,.43f,.34f));var clothes=Mat("StandinClothes",new Color(.28f,.35f,.4f));var glassMat=Mat("TEMP_GlassFragments",new Color(.55f,.75f,.8f),.5f);
            var environment=Child(root.transform,"Road_Blockout");Box(environment,"Ground",new Vector3(0,-.2f,50),new Vector3(120,.3f,240),grass);
            var roadPlane=Box(environment,"Road",new Vector3(0,-.02f,35),new Vector3(12,.08f,180),asphalt);roadPlane.gameObject.layer=0;roadPlane.gameObject.AddComponent<BoxCollider>();
            var motion=Child(environment,"MovingRoadMarkers");for(int i=-4;i<17;i++)Box(motion,"LaneDash",new Vector3(-3,.035f,i*6),new Vector3(.12f,.015f,3),white);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<14;i++){Box(environment,"RoadsidePost",new Vector3(side*8,.7f,i*8-24),new Vector3(.18f,1.4f,.18f),white);Box(environment,"DistantBuilding",new Vector3(side*(20+i%3*6),2+i%4,i*10-10),new Vector3(6,4+i%4*2,5),metal);}
            var car=Child(root.transform,"FamilyCar_Cutaway_TEST");Box(car,"Chassis",new Vector3(0,.6f,0),new Vector3(1.9f,.55f,4.3f),carMat);Box(car,"Hood",new Vector3(0,1.0f,1.55f),new Vector3(1.85f,.32f,1.2f),carMat);Box(car,"Dashboard",new Vector3(0,1.12f,.87f),new Vector3(1.75f,.23f,.25f),dark);
            foreach(int side in new[]{-1,1}){Box(car,"LowDoor",new Vector3(side*.94f,.95f,-.15f),new Vector3(.13f,.46f,2.6f),carMat);Box(car,"WindshieldPillar",new Vector3(side*.86f,1.4f,.95f),new Vector3(.08f,.9f,.08f),metal);for(int axle=-1;axle<=1;axle+=2)Wheel(car,new Vector3(side*.95f,.43f,axle*1.4f),.55f,dark);}
            Box(car,"WindshieldHeader",new Vector3(0,1.87f,.95f),new Vector3(1.8f,.08f,.08f),metal);
            // Open roof and no opaque windshield are deliberate readability choices for a cinematic blockout.
            var occupants=Child(car,"Occupants_TEST_CountNotCanon");Occupant(occupants,"Protagonist_Standin",new Vector3(-.43f,.98f,.03f),clothes,skin);Occupant(occupants,"Family_Standin_A",new Vector3(.43f,.98f,.03f),clothes,skin);Occupant(occupants,"Family_Standin_B",new Vector3(.35f,.98f,-1.1f),clothes,skin);
            var truck=Child(root.transform,"ApproachingTruck_TEST");truck.position=new Vector3(0,0,48);truck.rotation=Quaternion.Euler(0,180,0);
            Box(truck,"Cab",new Vector3(0,1.7f,2.3f),new Vector3(2.6f,2.4f,2.6f),truckMat);Box(truck,"Windshield",new Vector3(0,2.15f,3.615f),new Vector3(2.15f,.8f,.025f),dark);Box(truck,"Grille",new Vector3(0,1.05f,3.64f),new Vector3(1.6f,.5f,.06f),metal);
            Box(truck,"CargoBox",new Vector3(0,2.1f,-1.8f),new Vector3(2.8f,3.3f,5.6f),metal);foreach(int side in new[]{-1,1}){Box(truck,"Headlamp",new Vector3(side*.96f,1.05f,3.66f),new Vector3(.4f,.25f,.05f),white);foreach(float axle in new[]{2.5f,-1.2f,-3.6f})Wheel(truck,new Vector3(side*1.32f,.6f,axle),.95f,dark);}
            var fragmentsRoot=Child(car,"BoundedGlassVisuals_NoPhysics");var fragments=new Transform[16];for(int i=0;i<fragments.Length;i++){fragments[i]=Box(fragmentsRoot,"Shard_"+i,new Vector3((i%4-1.5f)*.22f,1.35f+i/4*.1f,.98f),new Vector3(.07f,.10f,.018f),glassMat);fragments[i].gameObject.SetActive(false);}
            var key=Child(root.transform,"SceneKey").gameObject.AddComponent<Light>();key.type=LightType.Directional;key.transform.rotation=Quaternion.Euler(42,-25,0);key.intensity=1.35f;key.color=new Color(.97f,.92f,.83f);key.shadows=LightShadows.None;
            var flash=Child(root.transform,"ImpactLight").gameObject.AddComponent<Light>();flash.type=LightType.Point;flash.transform.position=new Vector3(0,2.6f,1.7f);flash.range=12;flash.intensity=0;
            var proxy=Child(root.transform,"TestInputProxy_NotAnOccupant");proxy.position=new Vector3(4,.05f,-4);var cc=proxy.gameObject.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;
            var view=Child(proxy,"ViewPivot");view.localPosition=Vector3.up*1.6f;var locomotion=proxy.gameObject.AddComponent<S03FirstPersonPlayer>();var serialized=new SerializedObject(locomotion);serialized.FindProperty("view").objectReferenceValue=view;serialized.ApplyModifiedPropertiesWithoutUndo();
            var cameras=Child(root.transform,"Cameras");var main=Child(cameras,"Main Camera");main.gameObject.tag="MainCamera";var output=main.gameObject.AddComponent<Camera>();output.clearFlags=CameraClearFlags.SolidColor;output.backgroundColor=new Color(.37f,.43f,.46f);main.gameObject.AddComponent<AudioListener>();main.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
            var brain=main.gameObject.AddComponent<CinemachineBrain>();brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,.55f);
            var game=Child(cameras,"CM_ThirdPersonOverview");game.position=new Vector3(6,3.6f,-7);game.rotation=Quaternion.LookRotation(new Vector3(0,1,1)-game.position);var gameCM=game.gameObject.AddComponent<CinemachineCamera>();gameCM.Priority=20;
            var cine=Child(cameras,"CM_S10_ExteriorCinematic");cine.SetPositionAndRotation(game.position,game.rotation);var cm=cine.gameObject.AddComponent<CinemachineCamera>();cm.Priority=0;
            foreach(var camera in new[]{gameCM,cm}){var lens=camera.Lens;lens.FieldOfView=58;camera.Lens=lens;}main.SetPositionAndRotation(game.position,game.rotation);
            var volume=Child(root.transform,"S10_ConfusionVolume").gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=35;volume.weight=0;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Root+"/Materials/S10_ConfusionProfile.asset");var color=profile.Add<ColorAdjustments>(true);color.saturation.Override(-65);color.contrast.Override(12);var vig=profile.Add<Vignette>(true);vig.intensity.Override(.3f);vig.smoothness.Override(.65f);foreach(var item in profile.components)AssetDatabase.AddObjectToAsset(item,profile);AssetDatabase.SaveAssetIfDirty(profile);volume.sharedProfile=profile;
            var canvas=Child(root.transform,"HUD").gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            var label=new GameObject("Instructions",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI));label.transform.SetParent(canvas.transform,false);var rt=(RectTransform)label.transform;rt.anchorMin=new Vector2(0,1);rt.anchorMax=Vector2.one;rt.pivot=new Vector2(.5f,1);rt.sizeDelta=new Vector2(-40,100);rt.anchoredPosition=new Vector2(0,-20);var text=label.GetComponent<TMPro.TextMeshProUGUI>();text.font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/S03Prototype/Fonts/S03PromptFont.asset");text.fontSize=18;
            var overlay=new GameObject("FlashAndBlackout",typeof(RectTransform),typeof(UnityEngine.UI.Image));overlay.transform.SetParent(canvas.transform,false);rt=(RectTransform)overlay.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;var image=overlay.GetComponent<UnityEngine.UI.Image>();image.color=Color.clear;image.raycastTarget=false;
            var logic=Child(root.transform,"S10_Direction").gameObject;var sequence=logic.AddComponent<DirectionSequence>();var screen=logic.AddComponent<DirectionScreen>();screen.overlay=image;var director=logic.AddComponent<PlayableDirector>();var control=logic.AddComponent<S03ControlLockAdapter>();control.prototypePlayer=locomotion;
            var c=logic.AddComponent<S10CrashSequence>();c.sequence=sequence;c.screen=screen;c.controlLock=control;c.timing=settings;c.car=car;c.truck=truck;c.roadMotion=motion;c.cinematicPose=cine;c.outputCamera=output;c.fragments=fragments;c.keyLight=key;c.impactLight=flash;c.confusionVolume=volume;c.status=text;
            c.engine=Sound(root.transform,"TEMP_Engine",0,2,.24f,true);c.road=Sound(root.transform,"TEMP_Road",1,2,.2f,true);c.cabin=Sound(root.transform,"TEMP_CabinAir_NoDialogue",2,2,.1f,true);c.truckAudio=Sound(root.transform,"TEMP_TruckApproach",3,2,.03f,true);c.horn=Sound(root.transform,"TEMP_ProposedHorn",4,.8f,.3f,false);c.brake=Sound(root.transform,"TEMP_ProposedBrake",5,1,.25f,false);c.crash=Sound(root.transform,"TEMP_Collision",6,1,.65f,false);c.glass=Sound(root.transform,"TEMP_ProposedGlass",7,.65f,.35f,false);c.tinnitus=Sound(root.transform,"TEMP_ProposedTinnitus",8,2,.16f,true);
            c.roadFilter=c.road.gameObject.AddComponent<AudioLowPassFilter>();c.roadFilter.cutoffFrequency=22000;
            sequence.director=director;sequence.screen=null;sequence.cinematicCamera=cm;sequence.inputComponents=new Behaviour[]{locomotion};sequence.timelineTimeout=180;
            UnityEventTools.AddPersistentListener(sequence.onStarted,c.SequenceStarted);UnityEventTools.AddPersistentListener(sequence.onCompleted,c.SequenceCompleted);UnityEventTools.AddPersistentListener(sequence.onCancelled,c.SequenceCancelled);
            var timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=settings.FinishAt+.1;AssetDatabase.CreateAsset(timeline,TimelinePath);var track=timeline.CreateTrack<SignalTrack>(null,"S10 cinematic event cues");var receiver=logic.AddComponent<SignalReceiver>();
            UnityAction[] actions={c.Approach,c.Danger,c.Impact,c.Confusion,c.Blackout,c.Covered,c.Finish};double[] times=Times(settings);
            for(int i=0;i<CueNames.Length;i++){var signal=ScriptableObject.CreateInstance<SignalAsset>();AssetDatabase.CreateAsset(signal,Root+"/Timelines/"+CueNames[i]+".signal");var marker=track.CreateMarker<SignalEmitter>(times[i]);marker.asset=signal;marker.emitOnce=true;marker.retroactive=true;var reaction=new UnityEvent();UnityEventTools.AddPersistentListener(reaction,actions[i]);receiver.AddReaction(signal,reaction);}
            director.playableAsset=timeline;director.playOnAwake=false;director.extrapolationMode=DirectorWrapMode.Hold;director.timeUpdateMode=DirectorUpdateMode.UnscaledGameTime;director.SetGenericBinding(track,receiver);AssetDatabase.SaveAssetIfDirty(timeline);settings.appliedTimingSignature=settings.TimingSignature;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);
            var adapter=logic.AddComponent<S10StoryAdapter>();adapter.target=c;
            PrefabUtility.SaveAsPrefabAssetAndConnect(root,PrefabPath,InteractionMode.AutomatedAction);EditorSceneManager.SaveScene(scene,ScenePath);UpgradeDriver();Debug.Log("S10_BUILD_PASS "+ScenePath);
        }
        static double[] Times(S10Timing s)=>new double[]{s.ApproachAt,s.DangerAt,s.ImpactAt,s.ConfusionAt,s.BlackAt,s.CoveredAt,s.FinishAt};
        [MenuItem("Capstone/Direction/S10 Workspace/Apply timing to Timeline")]
        public static void ApplyTiming()
        {
            Guard();var settings=AssetDatabase.LoadAssetAtPath<S10Timing>(SettingsPath);var timeline=AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);if(!settings||!settings.Valid||!timeline)throw new InvalidOperationException("Invalid S10 timing or missing generated Timeline.");
            var times=Times(settings);foreach(var track in timeline.GetOutputTracks())foreach(var marker in track.GetMarkers())if(marker is SignalEmitter e&&e.asset){int i=Array.IndexOf(CueNames,e.asset.name);if(i>=0){e.time=times[i];EditorUtility.SetDirty(e);AssetDatabase.SaveAssetIfDirty(e);}}
            timeline.fixedDuration=settings.FinishAt+.1;EditorUtility.SetDirty(timeline);AssetDatabase.SaveAssetIfDirty(timeline);settings.appliedTimingSignature=settings.TimingSignature;EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);Debug.Log("S10_TIMING_APPLIED");
        }
        static AudioSource Sound(Transform root,string name,int kind,float duration,float volume,bool loop)
        {
            int rate=22050;int count=Mathf.CeilToInt(rate*duration);var random=new System.Random(1000+kind);float low=0;string path=Root+"/Audio/"+name+".wav";
            using(var w=new BinaryWriter(File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));w.Write(36+count*2);w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));w.Write(16);w.Write((short)1);w.Write((short)1);w.Write(rate);w.Write(rate*2);w.Write((short)2);w.Write((short)16);w.Write(System.Text.Encoding.ASCII.GetBytes("data"));w.Write(count*2);
                for(int i=0;i<count;i++){float t=(float)i/rate,noise=(float)(random.NextDouble()*2-1);low=Mathf.Lerp(low,noise,.08f);float envelope=Mathf.Clamp01(t/.01f)*Mathf.Clamp01((duration-t)/.03f);float value=kind==0?(Mathf.Sin(t*2*Mathf.PI*90)*.14f+low*.2f):kind==1?low*.3f:kind==2?low*.1f:kind==3?(Mathf.Sin(t*2*Mathf.PI*60)*.16f+low*.25f):kind==4?(Mathf.Sin(t*2*Mathf.PI*330)+Mathf.Sin(t*2*Mathf.PI*430))*.15f:kind==5?noise*.18f*Mathf.Sin(t*6000):kind==6?(noise*.7f+Mathf.Sin(t*500)*.3f)*Mathf.Exp(-t*7):kind==7?noise*.5f*Mathf.Exp(-t*10):Mathf.Sin(t*2*Mathf.PI*2700)*.10f;w.Write((short)(Mathf.Clamp(value*envelope,-1,1)*32767));}
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);var source=Child(root,name).gameObject.AddComponent<AudioSource>();source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);source.playOnAwake=false;source.loop=loop;source.volume=volume;return source;
        }
        static Transform Child(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        static Material Mat(string name,Color color,float metallic=0){var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;mat.SetFloat("_Metallic",metallic);AssetDatabase.CreateAsset(mat,Root+"/Materials/"+name+".mat");return mat;}
        static Transform Primitive(Transform p,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material mat){var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(p,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.layer=2;return go.transform;}
        static Transform Box(Transform p,string name,Vector3 position,Vector3 scale,Material mat)=>Primitive(p,name,PrimitiveType.Cube,position,scale,mat);
        static void Wheel(Transform p,Vector3 position,float size,Material mat){var wheel=Primitive(p,"Wheel",PrimitiveType.Cylinder,position,new Vector3(size,.13f,size),mat);wheel.localRotation=Quaternion.Euler(0,0,90);}
        static void Occupant(Transform parent,string name,Vector3 position,Material clothes,Material skin){var pivot=Child(parent,name);pivot.localPosition=position;Box(pivot,"SeatedTorso",Vector3.zero,new Vector3(.4f,.5f,.28f),clothes);Primitive(pivot,"Head",PrimitiveType.Sphere,new Vector3(0,.47f,0),Vector3.one*.32f,skin);Box(pivot,"Seat",new Vector3(0,-.18f,-.15f),new Vector3(.55f,.65f,.25f),clothes);}
    }
}
