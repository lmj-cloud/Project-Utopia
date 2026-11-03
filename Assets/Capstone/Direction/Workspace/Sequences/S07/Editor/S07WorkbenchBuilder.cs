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

namespace Capstone.Direction.Workspace.S07.Editor
{
    public static class S07WorkbenchBuilder
    {
        public const string Root="Assets/Capstone/Direction/Workspace/Sequences/S07";
        public const string ScenePath=Root+"/Scenes/S07_AwardMemory.unity";
        public const string PrefabPath=Root+"/Prefabs/S07_TestRig.prefab";
        static TMPro.TMP_FontAsset font;
        [MenuItem("Capstone/Direction/S07 Workspace/Open test scene")]
        public static void Open() { Guard(); EditorSceneManager.OpenScene(ScenePath); }
        static void Guard()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Resolve unsaved scene changes yourself; S07 will not save or discard them.");
        }
        [MenuItem("Capstone/Direction/S07 Workspace/Generate first prototype")]
        public static void Build()
        {
            Guard(); if(File.Exists(ScenePath)){Open();return;}
            foreach(var folder in new[]{"Scenes","Prefabs","Timelines","Materials","Audio"})if(!AssetDatabase.IsValidFolder(Root+"/"+folder))AssetDatabase.CreateFolder(Root,folder);
            font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/S03Prototype/Fonts/S03PromptFont.asset");
            if(!font || !Shader.Find("Universal Render Pipeline/Lit"))throw new InvalidOperationException("URP Lit and existing prompt font must be imported.");
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,Application.isBatchMode&&string.IsNullOrEmpty(previous.path)?NewSceneMode.Single:NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            var root=new GameObject("S07_TEST_ONLY");
            var stone=Mat("RuinedConcrete",new Color(.22f,.23f,.22f)); var dark=Mat("DarkStructure",new Color(.055f,.07f,.08f));
            var cloth=Mat("FieldUniform",new Color(.17f,.22f,.14f)); var skin=Mat("BlockoutHands",new Color(.61f,.42f,.29f));
            var silver=Mat("SilverMedalAndTags",new Color(.65f,.69f,.73f),.75f); var wood=Mat("BedroomWood",new Color(.28f,.17f,.1f));
            var linen=Mat("BedLinen",new Color(.36f,.43f,.5f)); var ribbon=Mat("MedalRibbon",new Color(.2f,.28f,.45f));
            var award=Child(root.transform,"Award_Blockout"); Room(award,Vector3.zero,dark,stone,9,9);
            Box(award,"SecondPlacePlatform",new Vector3(0,-.12f,0),new Vector3(2,.24f,2),stone);
            Box(award,"FirstPlace_Empty",new Vector3(-2.4f,.3f,1),new Vector3(1.6f,.6f,1.6f),silver);
            Box(award,"ThirdPlace_Empty",new Vector3(2.4f,.1f,1),new Vector3(1.6f,.2f,1.6f),stone);
            WorldText(award,"AwardSign","BOXING / SILVER / 2nd PLACE",new Vector3(0,2.8f,4),.12f);
            Key(award,new Color(.88f,.93f,1),1.1f);
            var war=Child(root.transform,"Battlefield_Memory_Blockout");
            var warGround=Box(war,"AshGround",new Vector3(80,-.15f,0),new Vector3(42,.3f,44),stone); warGround.gameObject.layer=0; warGround.gameObject.AddComponent<BoxCollider>();
            Box(war,"OvercastMemorySky",new Vector3(80,12,5),new Vector3(44,.4f,45),dark);
            for(int side=-1;side<=1;side+=2)for(int i=0;i<6;i++)
            {
                Box(war,"BrokenWall",new Vector3(80+side*(5+i*.3f),1.1f+i%3*.4f,i*3+1),new Vector3(1.2f,2.2f+i%3,2.2f),stone).rotation=Quaternion.Euler(0,side*12,side*5);
                Box(war,"Rubble",new Vector3(80+side*(3+i*.4f),.2f,i*2+2),new Vector3(1.2f,.4f,1),dark).rotation=Quaternion.Euler(0,i*31,15);
            }
            Box(war,"DistantRuins",new Vector3(80,3.5f,20),new Vector3(32,7,1),dark);
            for(int i=0;i<3;i++)
            {
                var fallen=Primitive(war,"FallenComrade_SymbolicUniform",PrimitiveType.Capsule,new Vector3(78+i*1.7f,.35f,4+i*2),new Vector3(.4f,.85f,.4f),cloth);
                fallen.rotation=Quaternion.Euler(90,0,i*27); Sphere(war,"AbandonedHelmet",new Vector3(77.6f+i*1.7f,.2f,4+i*2),Vector3.one*.3f,cloth);
            }
            Key(war,new Color(.61f,.67f,.54f),.85f);
            var smoke=Child(war,"TEMP_DustAndSmoke"); smoke.position=new Vector3(80,1,8);
            var particles=smoke.gameObject.AddComponent<ParticleSystem>(); var pm=particles.main; pm.startLifetime=8; pm.startSpeed=.2f; pm.startSize=2.4f; pm.startColor=new Color(.22f,.24f,.21f,.18f); pm.maxParticles=100; pm.loop=true; pm.prewarm=true;
            var emission=particles.emission; emission.rateOverTime=8; var shape=particles.shape; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(14,2,14);
            var smokeMat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); smokeMat.SetFloat("_Surface",1); smokeMat.SetFloat("_ZWrite",0); smokeMat.SetFloat("_SrcBlend",5); smokeMat.SetFloat("_DstBlend",10); smokeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); smokeMat.renderQueue=3000; AssetDatabase.CreateAsset(smokeMat,Root+"/Materials/TempSmoke.mat"); smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial=smokeMat;
            var bedroom=Child(root.transform,"Bedroom_Blockout"); Room(bedroom,new Vector3(160,0,0),wood,linen,7,8);
            Box(bedroom,"BedFrame",new Vector3(160,.25f,1.4f),new Vector3(2,.5f,3),wood);
            Box(bedroom,"Mattress",new Vector3(160,.6f,1.4f),new Vector3(1.9f,.25f,2.9f),linen);
            Box(bedroom,"Pillow",new Vector3(160,.85f,2.4f),new Vector3(1,.2f,.6f),silver);
            Box(bedroom,"Nightstand",new Vector3(162,.55f,1.7f),new Vector3(.7f,1.1f,.7f),wood);
            Key(bedroom,new Color(.95f,.76f,.56f),.75f);
            var actor=Child(root.transform,"TestPlayer"); actor.position=new Vector3(0,.05f,0); actor.gameObject.layer=2;
            var cc=actor.gameObject.AddComponent<CharacterController>(); cc.height=1.8f; cc.center=Vector3.up*.9f; cc.radius=.25f;
            var body=Primitive(actor,"ThirdPersonBody",PrimitiveType.Capsule,new Vector3(0,.9f,0),new Vector3(.55f,.9f,.55f),cloth).gameObject;
            var torso=Child(actor,"CinematicTorso"); var head=Child(torso,"HeadPose"); head.localPosition=Vector3.up*1.6f;
            var hands=Child(torso,"FirstPersonHands");
            var left=Child(hands,"LeftGrip_Stable"); left.localPosition=new Vector3(-.28f,1.15f,.48f);
            var right=Child(hands,"RightHand"); right.localPosition=new Vector3(.29f,1.32f,.5f);
            Hand(left,skin); Hand(right,skin);
            var medal=Child(left,"SilverMedal"); var disc=Primitive(medal,"Medal",PrimitiveType.Cylinder,new Vector3(0,-.06f,.07f),new Vector3(.18f,.012f,.18f),silver); disc.localRotation=Quaternion.Euler(90,0,0);
            Box(medal,"Ribbon",new Vector3(0,.05f,.07f),new Vector3(.035f,.18f,.015f),ribbon);
            WorldText(medal,"Rank","2",new Vector3(-.028f,-.045f,.054f),.02f);
            var tags=Child(left,"FallenComradesDogTags");
            for(int i=0;i<2;i++) { var tag=Box(tags,"DogTag",new Vector3(i*.045f-.02f,-.06f-i*.025f,.08f-i*.015f),new Vector3(.065f,.11f,.012f),silver); tag.localRotation=Quaternion.Euler(0,0,i*18-8); }
            for(int i=0;i<10;i++)Sphere(tags,"ChainBead",new Vector3(Mathf.Sin(i*.7f)*.035f,.10f-i*.015f,.075f),Vector3.one*.012f,silver);
            FrameHeldProps(medal,tags);
            var pistol=Child(right,"TEMP_InertPistol"); Box(pistol,"Grip",new Vector3(0,0,.055f),new Vector3(.055f,.13f,.08f),dark); Box(pistol,"SlideAndBarrel",new Vector3(0,.095f,.14f),new Vector3(.065f,.065f,.26f),dark);
            tags.gameObject.SetActive(false); pistol.gameObject.SetActive(false); hands.gameObject.SetActive(false);
            var locomotion=actor.gameObject.AddComponent<S03FirstPersonPlayer>(); var so=new SerializedObject(locomotion); so.FindProperty("view").objectReferenceValue=head; so.ApplyModifiedPropertiesWithoutUndo();
            var cameraRoot=Child(root.transform,"Cameras"); var main=Child(cameraRoot,"Main Camera"); main.gameObject.tag="MainCamera"; var output=main.gameObject.AddComponent<Camera>(); output.clearFlags=CameraClearFlags.SolidColor; output.backgroundColor=new Color(.07f,.08f,.07f); main.gameObject.AddComponent<AudioListener>(); main.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
            var brain=main.gameObject.AddComponent<CinemachineBrain>(); brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,.65f);
            var third=Child(cameraRoot,"CM_ThirdPerson"); var gameCamera=third.gameObject.AddComponent<CinemachineCamera>(); gameCamera.Priority=20; var rig=third.gameObject.AddComponent<S03ThirdPersonRig>(); rig.pivot=head; rig.offset=new Vector3(.6f,.45f,-2.3f); rig.Snap();
            var first=Child(head,"CM_FirstPerson_S07"); var cine=first.gameObject.AddComponent<CinemachineCamera>(); cine.Priority=0;
            foreach(var camera in new[]{gameCamera,cine}) { var lens=camera.Lens; lens.FieldOfView=62; lens.NearClipPlane=.025f; camera.Lens=lens; }
            main.SetPositionAndRotation(third.position,third.rotation);
            var volume=Child(root.transform,"S07_TraumaVolume").gameObject.AddComponent<Volume>(); volume.isGlobal=true; volume.priority=30; volume.weight=0;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,Root+"/Materials/S07_MemoryProfile.asset");
            var colors=profile.Add<ColorAdjustments>(true); colors.saturation.Override(-65); colors.contrast.Override(12); colors.postExposure.Override(-.15f);
            var vignette=profile.Add<Vignette>(true); vignette.intensity.Override(.28f); vignette.smoothness.Override(.65f);
            foreach(var item in profile.components)AssetDatabase.AddObjectToAsset(item,profile); AssetDatabase.SaveAssetIfDirty(profile); volume.sharedProfile=profile;
            var hud=Child(root.transform,"HUD").gameObject.AddComponent<Canvas>(); hud.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=hud.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1280,720);
            var label=new GameObject("PhaseAndHelp",typeof(RectTransform),typeof(TMPro.TextMeshProUGUI)); label.transform.SetParent(hud.transform,false); var rect=(RectTransform)label.transform; rect.anchorMin=new Vector2(0,1); rect.anchorMax=Vector2.one; rect.pivot=new Vector2(.5f,1); rect.sizeDelta=new Vector2(-40,100); rect.anchoredPosition=new Vector2(0,-20); var text=label.GetComponent<TMPro.TextMeshProUGUI>(); text.font=font; text.fontSize=20;
            // Overlay LAST: it also occludes the diagnostic HUD during closed eyes.
            var cover=new GameObject("BlackEyeClosure",typeof(RectTransform),typeof(UnityEngine.UI.Image)); cover.transform.SetParent(hud.transform,false); rect=(RectTransform)cover.transform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; var image=cover.GetComponent<UnityEngine.UI.Image>(); image.color=Color.clear; image.raycastTarget=false;
            var logic=Child(root.transform,"S07_Direction").gameObject; var screen=logic.AddComponent<DirectionScreen>(); screen.overlay=image;
            var sequence=logic.AddComponent<DirectionSequence>(); var director=logic.AddComponent<PlayableDirector>(); var control=logic.AddComponent<S03ControlLockAdapter>(); control.prototypePlayer=locomotion;
            var c=logic.AddComponent<S07AwardSequence>(); c.sequence=sequence; c.screen=screen; c.controlLock=control; c.actor=actor; c.head=head; c.torso=torso; c.leftHand=left; c.rightHand=right; c.hands=hands.gameObject; c.medal=medal.gameObject; c.tags=tags.gameObject; c.pistol=pistol.gameObject; c.thirdPersonBody=body; c.awardEnvironment=award.gameObject; c.warEnvironment=war.gameObject; c.bedroomEnvironment=bedroom.gameObject; c.status=text; c.traumaVolume=volume;
            c.warSpawn=Child(root.transform,"WarSpawn"); c.warSpawn.position=new Vector3(80,.05f,0); c.bedroomSpawn=Child(root.transform,"BedroomSpawn"); c.bedroomSpawn.position=new Vector3(160,.05f,-1);
            c.awardAudio=Audio(root.transform,"TEMP_AwardAmbience",Clip("TEMP_AwardAmbience",2,0),.25f,true);
            c.warAudio=Audio(root.transform,"TEMP_DistantBattleMemory",Clip("TEMP_DistantBattleMemory",4,1),.3f,true);
            c.roomAudio=Audio(root.transform,"TEMP_RoomTone",Clip("TEMP_RoomTone",2,2),.16f,true);
            c.metalAudio=Audio(root.transform,"TEMP_DogTagMetal",Clip("TEMP_DogTagMetal",.5f,3),.25f,false);
            c.tinnitusAudio=Audio(root.transform,"TEMP_Tinnitus",Clip("TEMP_Tinnitus",2,4),.18f,true);
            c.breathAudio=Audio(root.transform,"TEMP_RoughBreathing",Clip("TEMP_RoughBreathing",2.6f,5),.45f,true);
            sequence.director=director; sequence.cinematicCamera=cine; sequence.timelineTimeout=180; sequence.screen=null; sequence.inputComponents=new Behaviour[]{locomotion};
            UnityEventTools.AddPersistentListener(sequence.onStarted,c.SequenceStarted); UnityEventTools.AddPersistentListener(sequence.onCompleted,c.SequenceCompleted); UnityEventTools.AddPersistentListener(sequence.onCancelled,c.SequenceCancelled);
            var timeline=ScriptableObject.CreateInstance<TimelineAsset>(); timeline.durationMode=TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration=29;
            AssetDatabase.CreateAsset(timeline,Root+"/Timelines/S07_AwardMemory.playable"); var receiver=logic.AddComponent<SignalReceiver>(); var track=timeline.CreateTrack<SignalTrack>(null,"S07 narrative events (unscaled Timeline)");
            Cue(track,receiver,"Headache",2.1,c.Headache); Cue(track,receiver,"CloseEyes",6,c.CloseEyes); Cue(track,receiver,"CoveredSwap",7.1,c.CoveredSwap); Cue(track,receiver,"Rise",10,c.Rise); Cue(track,receiver,"WarFront",13,c.WarFront); Cue(track,receiver,"WarEnd",21,c.WarEnd); Cue(track,receiver,"CoveredBedroom",22.3,c.CoveredBedroom); Cue(track,receiver,"Wake",23,c.Wake); Cue(track,receiver,"Finish",28.5,c.FinishCue);
            director.playableAsset=timeline; director.playOnAwake=false; director.extrapolationMode=DirectorWrapMode.Hold; director.timeUpdateMode=DirectorUpdateMode.UnscaledGameTime; director.SetGenericBinding(track,receiver); AssetDatabase.SaveAssetIfDirty(timeline);
            var adapter=logic.AddComponent<S07S06Adapter>(); adapter.target=c;
            war.gameObject.SetActive(false); bedroom.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root,PrefabPath,InteractionMode.AutomatedAction); EditorSceneManager.SaveScene(scene,ScenePath); Debug.Log("S07_BUILD_PASS "+ScenePath);
        }
        public static void UpgradeClock()
        {
            var root=PrefabUtility.LoadPrefabContents(PrefabPath);
            try { root.GetComponentInChildren<PlayableDirector>().timeUpdateMode=DirectorUpdateMode.UnscaledGameTime; root.GetComponentInChildren<DirectionSequence>().inputComponents=new Behaviour[]{root.GetComponentInChildren<S03FirstPersonPlayer>()}; root.GetComponentInChildren<S03ThirdPersonRig>().offset=new Vector3(.6f,.45f,-2.3f); var c=root.GetComponentInChildren<S07AwardSequence>(); c.rightHand.localPosition=new Vector3(.29f,1.32f,.5f); FrameHeldProps(c.medal.transform,c.tags.transform); PrefabUtility.SaveAsPrefabAsset(root,PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var asset=AssetDatabase.LoadAssetAtPath<TimelineAsset>(Root+"/Timelines/S07_AwardMemory.playable"); foreach(var track in asset.GetOutputTracks()) { track.name="S07 narrative events (unscaled Timeline)"; EditorUtility.SetDirty(track); AssetDatabase.SaveAssetIfDirty(track); } AssetDatabase.SaveAssetIfDirty(asset);
        }
        static void FrameHeldProps(Transform medal,Transform tags)
        {
            medal.Find("Medal").localPosition=new Vector3(.12f,-.12f,-.035f);
            medal.Find("Ribbon").localPosition=new Vector3(.06f,-.02f,-.035f); medal.Find("Ribbon").localRotation=Quaternion.Euler(0,0,35);
            var rank=medal.Find("Rank"); rank.localPosition=new Vector3(.12f,-.12f,-.05f); rank.localScale=Vector3.one*.035f;
            var text=rank.GetComponent<TMPro.TextMeshPro>();text.alignment=TMPro.TextAlignmentOptions.Center;text.fontSize=18;text.rectTransform.sizeDelta=new Vector2(4,4);
            int plate=0,bead=0;
            foreach(Transform child in tags)if(child.name=="DogTag") { child.localPosition=new Vector3(.1f+plate*.08f,-.14f-plate*.02f,-.035f-plate*.015f);plate++; }
            else if(child.name=="ChainBead") { child.localPosition=new Vector3(bead/9f*.14f+Mathf.Sin(bead*.7f)*.01f,.02f-bead*.016f,-.035f);bead++; }
        }
        public static void FinalizeAndValidate() { UpgradeClock(); S07Validation.Run(); }
        static void Cue(SignalTrack track,SignalReceiver receiver,string name,double time,UnityAction callback) { var signal=ScriptableObject.CreateInstance<SignalAsset>(); AssetDatabase.CreateAsset(signal,Root+"/Timelines/"+name+".signal"); var marker=track.CreateMarker<SignalEmitter>(time); marker.asset=signal; marker.emitOnce=true; marker.retroactive=true; var reaction=new UnityEvent(); UnityEventTools.AddPersistentListener(reaction,callback); receiver.AddReaction(signal,reaction); }
        static AudioSource Audio(Transform root,string name,AudioClip clip,float volume,bool loop) { var s=Child(root,name).gameObject.AddComponent<AudioSource>(); s.clip=clip; s.playOnAwake=false; s.loop=loop; s.volume=volume; s.spatialBlend=0; return s; }
        static AudioClip Clip(string name,float seconds,int kind)
        {
            int rate=22050; var data=new float[Mathf.CeilToInt(seconds*rate)]; var random=new System.Random(707+kind); float low=0;
            for(int i=0;i<data.Length;i++)
            {
                float t=(float)i/rate,noise=(float)(random.NextDouble()*2-1); low=Mathf.Lerp(low,noise,.045f); float envelope=Mathf.Clamp01(t/.03f)*Mathf.Clamp01((seconds-t)/.03f);
                float sample=kind==0?low*.15f:kind==1?low*.35f+Mathf.Sin(t*110)*.05f*Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*3.2f)),12):kind==2?low*.05f:kind==3?(Mathf.Sin(t*6200)+Mathf.Sin(t*8350))*.12f*Mathf.Exp(-t*9):kind==4?Mathf.Sin(2*Mathf.PI*2600*t)*.12f:low*.6f*Mathf.Pow(Mathf.Abs(Mathf.Sin(t*Mathf.PI*2/1.3f)),2);
                data[i]=sample*envelope;
            }
            string path=Root+"/Audio/"+name+".wav"; using(var w=new BinaryWriter(File.Create(path))) { w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36+data.Length*2); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate*2); w.Write((short)2); w.Write((short)16); w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(data.Length*2); foreach(var sample in data)w.Write((short)(Mathf.Clamp(sample,-1,1)*32767)); }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport); return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        static Transform Child(Transform parent,string name) { var o=new GameObject(name); o.transform.SetParent(parent,false); return o.transform; }
        static Material Mat(string name,Color color,float metallic=0) { var m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color=color; m.SetFloat("_Metallic",metallic); AssetDatabase.CreateAsset(m,Root+"/Materials/"+name+".mat"); return m; }
        static Transform Primitive(Transform parent,string name,PrimitiveType type,Vector3 p,Vector3 scale,Material m) { var o=GameObject.CreatePrimitive(type); o.name=name; o.transform.SetParent(parent,false); o.transform.localPosition=p; o.transform.localScale=scale; o.GetComponent<Renderer>().sharedMaterial=m; if(o.GetComponent<Collider>())UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>()); o.layer=2; return o.transform; }
        static Transform Box(Transform parent,string name,Vector3 p,Vector3 scale,Material m) => Primitive(parent,name,PrimitiveType.Cube,p,scale,m);
        static Transform Sphere(Transform parent,string name,Vector3 p,Vector3 scale,Material m) => Primitive(parent,name,PrimitiveType.Sphere,p,scale,m);
        static void Room(Transform parent,Vector3 center,Material wall,Material floor,float width,float depth) { var ground=Box(parent,"Floor",center+new Vector3(0,-.15f,0),new Vector3(width,.3f,depth),floor); ground.gameObject.layer=0; ground.gameObject.AddComponent<BoxCollider>(); Box(parent,"Ceiling",center+Vector3.up*4,new Vector3(width,.2f,depth),wall); foreach(int sign in new[]{-1,1}) { Box(parent,"Wall",center+new Vector3(sign*width/2,2,0),new Vector3(.2f,4,depth),wall); Box(parent,"Wall",center+new Vector3(0,2,sign*depth/2),new Vector3(width,4,.2f),wall); } }
        static void Key(Transform parent,Color color,float intensity) { var l=Child(parent,"KeyLight").gameObject.AddComponent<Light>(); l.type=LightType.Directional; l.color=color; l.intensity=intensity; l.shadows=LightShadows.None; l.transform.rotation=Quaternion.Euler(48,-25,0); }
        static void Hand(Transform parent,Material m) { Box(parent,"Palm_FixedGrip",Vector3.zero,new Vector3(.11f,.07f,.16f),m); for(int i=0;i<4;i++) { var finger=Box(parent,"CurledFinger",new Vector3(-.039f+i*.026f,-.033f,.052f),new Vector3(.021f,.05f,.06f),m); finger.localRotation=Quaternion.Euler(25,0,0); } Box(parent,"Forearm",new Vector3(0,-.1f,-.13f),new Vector3(.085f,.1f,.3f),m).localRotation=Quaternion.Euler(-25,0,0); }
        static void WorldText(Transform parent,string name,string value,Vector3 p,float scale) { var t=Child(parent,name); t.localPosition=p; t.localScale=Vector3.one*scale; var text=t.gameObject.AddComponent<TMPro.TextMeshPro>(); text.font=font; text.text=value; text.fontSize=24; text.rectTransform.sizeDelta=new Vector2(70,5); }
    }
}
