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

namespace Capstone.Direction.Workspace.S06.Editor
{
    public static class S06WorkbenchBuilder
    {
        public const string Root = "Assets/Capstone/Direction/Workspace/Sequences/S06";
        public const string ScenePath = Root + "/Scenes/S06_FirstTrauma.unity";
        const string RingPath = "Assets/3D Boxing Arena Pack/Prefabs/Complete Props/Boxing Ring .prefab";
        [MenuItem("Capstone/Direction/S06 Workspace/Open test scene")]
        public static void Open()
        {
            Guard(); EditorSceneManager.OpenScene(ScenePath);
        }
        static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Resolve unsaved scenes first; no automatic saving or discarding.");
        }
        [MenuItem("Capstone/Direction/S06 Workspace/Generate first prototype")]
        public static void Build()
        {
            Guard();
            if (File.Exists(ScenePath)) { Open(); return; } // Never overwrite an existing workbench.
            foreach (var folder in new[] { "Scenes", "Prefabs", "Timelines", "Materials", "Audio" })
                if (!AssetDatabase.IsValidFolder(Root + "/" + folder)) AssetDatabase.CreateFolder(Root, folder);
            var external = AssetDatabase.LoadAssetAtPath<GameObject>(RingPath);
            if (!external) throw new InvalidOperationException("Restored boxing ring must import first.");
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode && string.IsNullOrEmpty(previous.path) ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root = new GameObject("S06_TEST_ONLY");
            var floor = Mat("Interior", new Color(.08f, .105f, .14f));
            var trim = Mat("Structure", new Color(.19f, .23f, .28f));
            var blue = Mat("Player", new Color(.04f, .36f, .65f));
            var red = Mat("Opponent", new Color(.55f, .08f, .07f));
            var environment = Child(root.transform, "Arena");
            Box(environment, "Floor", new Vector3(0, -.25f, 0), new Vector3(40, .5f, 36), floor);
            Box(environment, "Ceiling", new Vector3(0, 11, 0), new Vector3(40, .4f, 36), floor);
            foreach (int sign in new[] { -1, 1 })
            {
                Box(environment, "SideWall", new Vector3(sign * 20, 5.5f, 0), new Vector3(.4f, 11, 36), trim);
                Box(environment, "EndWall", new Vector3(0, 5.5f, sign * 18), new Vector3(40, 11, .4f), trim);
                for (int tier = 0; tier < 4; tier++) Box(environment, "SeatingTier", new Vector3(0, tier * .6f + .3f, sign * (11 + tier * 1.4f)), new Vector3(30, .6f, 1.2f), trim);
            }
            var ring = (GameObject)PrefabUtility.InstantiatePrefab(external, environment);
            ring.name = "VendorRing_PrivateMaterials";
            Bounds bounds = BoundsOf(ring);
            ring.transform.localScale *= 12 / Mathf.Max(bounds.size.x, bounds.size.z);
            bounds = BoundsOf(ring); ring.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            var clones = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (var renderer in ring.GetComponentsInChildren<Renderer>())
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var source = mats[i]; if (!source) continue;
                    if (!clones.TryGetValue(source, out var clone))
                    {
                        clone = new Material(source); clone.name = "S06_" + source.name;
                        if (clone.HasProperty("_BaseMap") && !clone.GetTexture("_BaseMap") && clone.HasProperty("_MainTex")) clone.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
                        AssetDatabase.CreateAsset(clone, AssetDatabase.GenerateUniqueAssetPath(Root + "/Materials/" + clone.name + ".mat")); clones.Add(source, clone);
                    }
                    mats[i] = clone;
                }
                renderer.sharedMaterials = mats;
            }
            // A private invisible collider defines a stable ring test surface, independent of vendor colliders.
            Box(environment, "TestRingSurface", new Vector3(0, 1.05f, 0), new Vector3(8, .2f, 8), floor).GetComponent<Renderer>().enabled = false;
            var actor = Child(root.transform, "TestPlayer"); actor.position = new Vector3(0, 1.2f, -1.7f); actor.gameObject.layer = 2;
            var cc = actor.gameObject.AddComponent<CharacterController>(); cc.height = 1.8f; cc.center = Vector3.up * .9f; cc.radius = .3f;
            Body(actor, blue);
            var pivot = Child(actor, "ViewPivot"); pivot.localPosition = Vector3.up * 1.5f;
            var player = actor.gameObject.AddComponent<S03FirstPersonPlayer>();
            var serialized = new SerializedObject(player); serialized.FindProperty("view").objectReferenceValue = pivot; serialized.ApplyModifiedPropertiesWithoutUndo();
            var opponent = Child(root.transform, "Opponent_TestRhythm_NoCombat"); opponent.position = new Vector3(0, 1.2f, 1.2f); opponent.rotation = Quaternion.Euler(0, 180, 0); Body(opponent, red);
            var cameras = Child(root.transform, "Cameras");
            var main = Child(cameras, "Main Camera"); main.gameObject.tag = "MainCamera";
            var camera = main.gameObject.AddComponent<Camera>(); camera.backgroundColor = new Color(.02f, .025f, .04f); camera.clearFlags = CameraClearFlags.SolidColor;
            main.gameObject.AddComponent<AudioListener>();
            main.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var brain = main.gameObject.AddComponent<CinemachineBrain>(); brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .7f);
            var follow = Child(cameras, "CM_ThirdPerson"); var gameplay = follow.gameObject.AddComponent<CinemachineCamera>(); gameplay.Priority = 20;
            var rig = follow.gameObject.AddComponent<S03ThirdPersonRig>(); rig.pivot = pivot; rig.offset = new Vector3(.65f, .45f, -4);
            var cinematic = Child(cameras, "CM_BoutSubjective"); var cine = cinematic.gameObject.AddComponent<CinemachineCamera>(); cine.Priority = 0;
            var cineRig = cinematic.gameObject.AddComponent<S03ThirdPersonRig>(); cineRig.pivot = pivot; cineRig.offset = new Vector3(.45f, .2f, -2.8f);
            foreach (var cm in new[] { gameplay, cine }) { var lens = cm.Lens; lens.FieldOfView = 55; cm.Lens = lens; }
            var light = Child(root.transform, "ArenaKey").gameObject.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f; light.color = new Color(.85f, .92f, 1); light.transform.rotation = Quaternion.Euler(55, 20, 0); light.shadows = LightShadows.None;
            foreach (int sign in new[] { -1, 1 }) { var fill = Child(root.transform, "RingLight").gameObject.AddComponent<Light>(); fill.type = LightType.Point; fill.transform.position = new Vector3(sign * 3, 6, 0); fill.range = 20; fill.intensity = 14; }
            var volume = Child(root.transform, "SubjectivePerception").gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 25; volume.weight = 0;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, Root + "/Materials/S06_SubjectiveProfile.asset");
            var color = profile.Add<ColorAdjustments>(true); color.saturation.Override(-85); color.contrast.Override(15); color.postExposure.Override(-.35f);
            var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(.35f); vignette.smoothness.Override(.6f);
            foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile); AssetDatabase.SaveAssetIfDirty(profile); volume.sharedProfile = profile;
            var canvas = Child(root.transform, "HUD").gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var fade = new GameObject("TraumaOverlay", typeof(RectTransform), typeof(UnityEngine.UI.Image)); fade.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)fade.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = fade.GetComponent<UnityEngine.UI.Image>(); image.color = Color.clear; image.raycastTarget = false;
            var label = new GameObject("InstructionsAndResult", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI)); label.transform.SetParent(canvas.transform, false);
            rect = (RectTransform)label.transform; rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1); rect.sizeDelta = new Vector2(-40, 160); rect.anchoredPosition = new Vector2(0, -20);
            var text = label.GetComponent<TMPro.TextMeshProUGUI>(); text.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/S03Prototype/Fonts/S03PromptFont.asset"); text.fontSize = 23; text.color = Color.white;
            var logic = Child(root.transform, "S06_Direction").gameObject;
            var screen = logic.AddComponent<DirectionScreen>(); screen.overlay = image;
            var sequence = logic.AddComponent<DirectionSequence>(); var director = logic.AddComponent<PlayableDirector>();
            var lockAdapter = logic.AddComponent<S03ControlLockAdapter>(); lockAdapter.prototypePlayer = player;
            var controller = logic.AddComponent<S06BoutSequence>(); controller.sequence = sequence; controller.screen = screen; controller.controlLock = lockAdapter;
            controller.player = actor; controller.opponent = opponent; controller.cameraPivot = pivot; controller.subjectiveVolume = volume; controller.status = text;
            var audio = logic.AddComponent<DirectionAudio>(); sequence.audioSystem = audio;
            controller.crowd = Source(root.transform, "Crowd_RealChatter_TemporaryAmbience", AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Gamemaster Audio - Pro Sound Collection/Backgrounds/background_crowd_people_chatter_loop_01.wav"), true, .38f, 0);
            controller.crowd.playOnAwake = true; controller.crowdFilter = controller.crowd.gameObject.AddComponent<AudioLowPassFilter>(); controller.crowdFilter.cutoffFrequency = 22000;
            controller.outsideStarter = Source(root.transform, "OutsideAthletics_TEMP", TestClip("TEMP_Starter", .28f, 0), false, .4f, 1); controller.outsideStarter.transform.position = new Vector3(-12, 4, 8);
            controller.approach = Source(root.transform, "SubjectiveEmitter_NO_PROJECTILE", TestClip("TEMP_Approach", 2.5f, 1), false, .05f, 1); controller.subjectiveEmitter = controller.approach.transform;
            controller.ringing = Source(root.transform, "TEMP_Ringing", TestClip("TEMP_Ringing", .8f, 2), false, .14f, 0);
            sequence.director = director; sequence.cinematicCamera = cine; sequence.timelineTimeout = 60; sequence.screen = null;
            UnityEventTools.AddPersistentListener(sequence.onStarted, controller.OnStarted); UnityEventTools.AddPersistentListener(sequence.onCompleted, controller.OnCompleted); UnityEventTools.AddPersistentListener(sequence.onCancelled, controller.OnCancelled);
            var timeline = ScriptableObject.CreateInstance<TimelineAsset>(); timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = 12;
            AssetDatabase.CreateAsset(timeline, Root + "/Timelines/S06_FirstTrauma.playable");
            var receiver = logic.AddComponent<SignalReceiver>(); var track = timeline.CreateTrack<SignalTrack>(null, "Bout psychological cue events");
            Cue(track, receiver, "StarterShot", 3, controller.StarterShot); Cue(track, receiver, "IncomingPerception", 3.7, controller.BeginApproach); Cue(track, receiver, "BriefTrauma", 6, controller.BriefHallucination); Cue(track, receiver, "LostRhythm", 6.75, controller.DisruptBout); Cue(track, receiver, "SecondPlace", 10, controller.ShowSecondPlace);
            director.playableAsset = timeline; director.playOnAwake = false; director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime; director.extrapolationMode = DirectorWrapMode.None; director.SetGenericBinding(track, receiver);
            AssetDatabase.SaveAssetIfDirty(timeline);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, Root + "/Prefabs/S06_TestRig.prefab", InteractionMode.AutomatedAction);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("S06_BUILD_PASS " + ScenePath);
        }
        static void Cue(SignalTrack track, SignalReceiver receiver, string name, double time, UnityAction action)
        {
            var signal = ScriptableObject.CreateInstance<SignalAsset>(); AssetDatabase.CreateAsset(signal, Root + "/Timelines/" + name + ".signal");
            var marker = track.CreateMarker<SignalEmitter>(time); marker.asset = signal; marker.emitOnce = true; marker.retroactive = true;
            var reaction = new UnityEvent(); UnityEventTools.AddPersistentListener(reaction, action); receiver.AddReaction(signal, reaction);
        }
        static AudioClip TestClip(string name, float seconds, int kind)
        {
            int rate = 22050; var samples = new float[Mathf.CeilToInt(seconds * rate)]; var random = new System.Random(601);
            for (int i = 0; i < samples.Length; i++) { float t = (float)i / rate, p = t / seconds; float noise = (float)(random.NextDouble() * 2 - 1); samples[i] = kind == 0 ? noise * Mathf.Exp(-t * 35) * .5f : kind == 1 ? (noise * .16f + Mathf.Sin(t * (1600 + 900 * p)) * .09f) * Mathf.Sin(Mathf.PI * p) : Mathf.Sin(t * Mathf.PI * 2 * 2600) * .12f * Mathf.Sin(Mathf.PI * p); }
            string path = Root + "/Audio/" + name + ".wav";
            using (var stream = new BinaryWriter(File.Create(path)))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); stream.Write(36 + samples.Length * 2); stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                stream.Write(16); stream.Write((short)1); stream.Write((short)1); stream.Write(rate); stream.Write(rate * 2); stream.Write((short)2); stream.Write((short)16);
                stream.Write(System.Text.Encoding.ASCII.GetBytes("data")); stream.Write(samples.Length * 2); foreach (var sample in samples) stream.Write((short)(Mathf.Clamp(sample, -1, 1) * 32767));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        public static void RepairGeneratedAudio()
        {
            var prefab = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/S06_TestRig.prefab");
            try
            {
                var c = prefab.GetComponentInChildren<S06BoutSequence>();
                c.sequence.timelineTimeout = 60;
                c.outsideStarter.clip = TestClip("TEMP_Starter", .28f, 0); c.approach.clip = TestClip("TEMP_Approach", 2.5f, 1); c.ringing.clip = TestClip("TEMP_Ringing", .8f, 2);
                PrefabUtility.SaveAsPrefabAsset(prefab, Root + "/Prefabs/S06_TestRig.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            foreach (var name in new[] { "TEMP_Starter", "TEMP_Approach", "TEMP_Ringing" }) AssetDatabase.DeleteAsset(Root + "/Audio/" + name + ".asset");
            Debug.Log("S06_AUDIO_REPAIR_PASS");
        }
        public static void ApplyTimeoutConfiguration()
        {
            var prefab = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/S06_TestRig.prefab");
            try { prefab.GetComponentInChildren<S06BoutSequence>().sequence.timelineTimeout = 60; PrefabUtility.SaveAsPrefabAsset(prefab, Root + "/Prefabs/S06_TestRig.prefab"); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
        }
        static AudioSource Source(Transform root, string name, AudioClip clip, bool loop, float volume, float spatial)
        {
            var source = Child(root, name).gameObject.AddComponent<AudioSource>(); source.clip = clip; source.loop = loop; source.volume = volume; source.spatialBlend = spatial; source.playOnAwake = false; source.minDistance = 4; source.maxDistance = 45; source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0; return source;
        }
        static Transform Child(Transform parent, string name) { var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform; }
        static Material Mat(string name, Color color) { var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = color; AssetDatabase.CreateAsset(mat, Root + "/Materials/" + name + ".mat"); return mat; }
        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material mat) { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat; return go; }
        static void Body(Transform actor, Material mat)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.transform.SetParent(actor, false); body.transform.localPosition = Vector3.up * .9f; body.transform.localScale = new Vector3(.6f, .9f, .6f); body.layer = 2; UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>()); body.GetComponent<Renderer>().sharedMaterial = mat;
            foreach (int sign in new[] { -1, 1 }) { var glove = Box(actor, "TestGuardGlove", new Vector3(sign * .36f, 1.25f, .32f), Vector3.one * .24f, mat); UnityEngine.Object.DestroyImmediate(glove.GetComponent<Collider>()); }
        }
        static Bounds BoundsOf(GameObject go) { var renderers = go.GetComponentsInChildren<Renderer>(); var b = renderers[0].bounds; foreach (var r in renderers) b.Encapsulate(r.bounds); return b; }
    }
}
