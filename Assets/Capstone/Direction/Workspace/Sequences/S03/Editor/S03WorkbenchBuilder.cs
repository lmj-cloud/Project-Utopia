using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.Rendering.Universal;
using Unity.Cinemachine;
using S03Prototype;

namespace Capstone.Direction.Workspace.S03.Editor
{
    public static partial class S03WorkbenchBuilder
    {
        const string Root = "Assets/Capstone/Direction/Workspace/Sequences/S03";
        const string Arena = "Assets/3D Boxing Arena Pack/Prefabs/Complete Props/Boxing Ring .prefab";
        const string DoorAudio = "Assets/Gamemaster Audio - Pro Sound Collection/Doors/door_creak_short_01.wav";
        const string CrowdAudio = "Assets/Gamemaster Audio - Pro Sound Collection/Backgrounds/background_crowd_people_chatter_loop_01.wav";
        const string Menu = "Capstone/Direction/S03 Workspace/Generate isolated workbench %#F9";

        [MenuItem(Menu, true)]
        static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

        [MenuItem("Capstone/Direction/S03 Workspace/Open latest workbench")]
        public static void OpenLatest()
        {
            if (!CanBuild()) throw new InvalidOperationException("Stop Play and wait for compilation.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes. Resolve them yourself before opening the test scene; this tool will not save or discard them.");
            var path = AssetDatabase.FindAssets("t:Scene", new[] { Root + "/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.Contains("S03_Workbench_"))
                .OrderByDescending(p => p).FirstOrDefault();
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Generate a workbench first.");
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }

        [MenuItem(Menu)]
        public static void Build()
        {
            if (!CanBuild()) throw new InvalidOperationException("Stop Play and wait for compilation before generation.");
            // Repeated generation upgrades the existing workbench; do not accumulate duplicate scenes.
            if (AssetDatabase.FindAssets("t:Scene", new[] { Root + "/Scenes" }).Length > 0)
            {
                UpgradeExisting();
                return;
            }
            // Fail before scene/asset generation if required external resources are not imported.
            var arenaAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Arena);
            var doorClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DoorAudio);
            var crowdClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CrowdAudio);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/S03Prototype/Fonts/S03PromptFont.asset");
            if (!arenaAsset || !doorClip || !crowdClip || !shader || !font)
                throw new InvalidOperationException("S03 requires imported arena prefab, both restored WAVs, URP Lit and the existing S03 prompt font. Check Console/import first.");
            foreach (string folder in new[] { "Scenes", "Prefabs", "Timelines", "Materials" })
                if (!AssetDatabase.IsValidFolder(Root + "/" + folder))
                    throw new InvalidOperationException("Missing workspace folder: " + folder);

            string id = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            string scenePath = Unique("Scenes", "S03_Workbench_" + id, ".unity");
            var previous = SceneManager.GetActiveScene();
            if (!Application.isBatchMode && previous.IsValid() && string.IsNullOrEmpty(previous.path))
                throw new InvalidOperationException("Open a saved scene before generation. The tool will not save or replace your untitled scene.");
            Scene scene = default;
            try
            {
                // Batch mode starts with an untitled empty scene that cannot accept additive creation.
                // Replace only that batch startup scene; interactive user scenes always remain loaded.
                var mode = Application.isBatchMode && string.IsNullOrEmpty(previous.path)
                    ? NewSceneMode.Single : NewSceneMode.Additive;
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("S03_TEST_ONLY");
                var environment = Child(root.transform, "Environment");
                var homeMat = MakeMaterial(shader, "HomeWalls_" + id, new Color(0.55f, 0.47f, 0.38f));
                var floorMat = MakeMaterial(shader, "HomeFloor_" + id, new Color(0.23f, 0.16f, 0.1f));
                var doorMat = MakeMaterial(shader, "Door_" + id, new Color(0.27f, 0.11f, 0.045f));
                var arenaMat = MakeMaterial(shader, "ArenaFloor_" + id, new Color(0.065f, 0.085f, 0.11f));
                var actorMat = MakeMaterial(shader, "TestPlayer_" + id, new Color(0.07f, 0.45f, 0.68f));

                var home = Child(environment, "Home_Blockout");
                Box(home, "Floor", new Vector3(0, -0.15f, -1), new Vector3(8, 0.3f, 10), floorMat);
                Box(home, "LeftWall", new Vector3(-4, 1.5f, -1), new Vector3(0.2f, 3, 10), homeMat);
                Box(home, "RightWall", new Vector3(4, 1.5f, -1), new Vector3(0.2f, 3, 10), homeMat);
                Box(home, "BackWall", new Vector3(0, 1.5f, -6), new Vector3(8, 3, 0.2f), homeMat);
                Box(home, "FrontLeft", new Vector3(-2.6f, 1.5f, 4), new Vector3(2.8f, 3, 0.2f), homeMat);
                Box(home, "FrontRight", new Vector3(2.6f, 1.5f, 4), new Vector3(2.8f, 3, 0.2f), homeMat);
                Box(home, "DoorHeader", new Vector3(0, 2.85f, 4), new Vector3(2.4f, 0.3f, 0.2f), homeMat);
                var hinge = Child(home, "FrontDoorHinge");
                hinge.position = new Vector3(-1, 0, 4);
                Box(hinge, "DoorPanel", new Vector3(1, 1.25f, 0), new Vector3(2, 2.5f, 0.15f), doorMat);
                Box(hinge, "DoorHandle", new Vector3(1.75f, 1.15f, -0.13f), new Vector3(0.18f, 0.08f, 0.15f), actorMat);
                var door = hinge.gameObject.AddComponent<S03DoorInteraction>();

                var arena = Child(environment, "Arena");
                Box(arena, "ArenaGround", new Vector3(80, -0.15f, 0), new Vector3(30, 0.3f, 30), arenaMat);
                var ring = (GameObject)PrefabUtility.InstantiatePrefab(arenaAsset, arena);
                ring.name = "BoxingRing_ExternalMesh_LocalMaterials";
                if (ring.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                    throw new InvalidOperationException("Arena prefab contains unexpected scripts. Review before using.");
                FitRing(ring, new Vector3(80, 0, 0));
                CloneMaterials(ring, id);

                var homeLights = Child(root.transform, "HomeLights");
                var arenaLights = Child(root.transform, "ArenaLights");
                Directional(homeLights, "HomeWarmLight", new Color(1, 0.87f, 0.72f), 1.1f, new Vector3(45, -25, 0));
                Directional(arenaLights, "ArenaCoolLight", new Color(0.82f, 0.9f, 1), 1.8f, new Vector3(55, 20, 0));
                arenaLights.gameObject.SetActive(false);

                var actor = Child(root.transform, "TestPlayer_PrototypeLocomotion");
                actor.position = new Vector3(0, 0.05f, -2);
                actor.gameObject.layer = 2; // Ignore Raycast: avoid the camera/door probing the test actor itself.
                var controller = actor.gameObject.AddComponent<CharacterController>();
                controller.height = 1.8f; controller.radius = 0.3f; controller.center = Vector3.up * 0.9f;
                var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                visual.name = "TestBody"; visual.transform.SetParent(actor, false);
                visual.transform.localPosition = Vector3.up * 0.9f;
                visual.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
                visual.layer = 2;
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<Renderer>().sharedMaterial = actorMat;
                var pivot = Child(actor, "ViewPivot"); pivot.localPosition = Vector3.up * 1.55f;
                var player = actor.gameObject.AddComponent<S03FirstPersonPlayer>();
                Set(player, "view", pivot);
                Set(door, "player", player); Set(door, "playerView", pivot);
                Set(door, "localInteractionPoint", new Vector3(1, 1.2f, 0));
                Set(door, "interactionDistance", 2.4f);
                Set(door, "openDuration", 1.1f);

                var cameraRoot = Child(root.transform, "Cameras");
                var main = Child(cameraRoot, "Main Camera"); main.gameObject.tag = "MainCamera";
                var camera = main.gameObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.05f; camera.farClipPlane = 250;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.04f, 0.055f, 0.08f);
                main.gameObject.AddComponent<AudioListener>();
                main.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
                var brain = main.gameObject.AddComponent<CinemachineBrain>();
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.6f);
                var follow = Child(cameraRoot, "CM_ThirdPerson").gameObject.AddComponent<CinemachineCamera>();
                follow.Priority = 20;
                var followRig = follow.gameObject.AddComponent<S03ThirdPersonRig>(); followRig.pivot = pivot; followRig.Snap();
                main.SetPositionAndRotation(follow.transform.position, follow.transform.rotation);
                var cine = Child(cameraRoot, "CM_DoorAndArena").gameObject.AddComponent<CinemachineCamera>();
                cine.Priority = 5;
                cine.transform.SetPositionAndRotation(main.position, main.rotation);
                var destination = Child(root.transform, "ArenaArrival"); destination.position = new Vector3(80, 0.05f, -10);
                var shotStart = Child(root.transform, "ArenaShotStart"); shotStart.position = new Vector3(87, 4, -12);
                var shotEnd = Child(root.transform, "ArenaShotEnd"); shotEnd.position = new Vector3(84, 3, -8);
                var lookTarget = Child(root.transform, "ArenaLookTarget"); lookTarget.position = new Vector3(80, 1.2f, 0);

                var effects = Child(root.transform, "S03_DirectionRoot");
                var sequence = effects.gameObject.AddComponent<DirectionSequence>();
                var screen = effects.gameObject.AddComponent<DirectionScreen>();
                var audio = effects.gameObject.AddComponent<DirectionAudio>();
                var director = effects.gameObject.AddComponent<PlayableDirector>();
                var receiver = effects.gameObject.AddComponent<SignalReceiver>();
                var controlLock = effects.gameObject.AddComponent<S03ControlLockAdapter>();
                var bridge = effects.gameObject.AddComponent<S03SceneBridge>();
                var canvas = Child(root.transform, "S03_UI").gameObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32000;
                var scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                var status = Label(canvas.transform, "Status", font, new Vector2(0.5f, 1), new Vector2(0, -100), new Vector2(1500, 160), 28);
                var promptText = Label(canvas.transform, "DoorPrompt", font, new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(900, 80), 38);
                promptText.text = "[E] Open door";
                var prompt = promptText.gameObject.AddComponent<CanvasGroup>(); prompt.alpha = 0;
                prompt.blocksRaycasts = false; prompt.interactable = false; Set(door, "prompt", prompt);
                var overlay = new GameObject("WhiteOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                overlay.transform.SetParent(canvas.transform, false);
                var rect = (RectTransform)overlay.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                screen.overlay = overlay.GetComponent<UnityEngine.UI.Image>();
                screen.overlay.color = Color.clear; screen.overlay.raycastTarget = false;

                controlLock.prototypePlayer = player;
                sequence.screen = screen; sequence.audioSystem = audio; sequence.director = director;
                sequence.cinematicCamera = cine; sequence.crowdLoop = crowdClip;
                sequence.transitionSound = null; // Door sound is played once at opening, not again at the white fade.
                sequence.enableF8Test = false; sequence.timelineTimeout = 8;
                sequence.inputComponents = Array.Empty<Behaviour>();
                bridge.sequence = sequence; bridge.door = door; bridge.player = player;
                bridge.controlLock = controlLock; bridge.gameplayRig = followRig; bridge.cinematicCamera = cine;
                bridge.destination = destination; bridge.arenaShotStart = shotStart; bridge.arenaShotEnd = shotEnd;
                bridge.arenaLookTarget = lookTarget; bridge.homeLights = homeLights.gameObject;
                bridge.arenaLights = arenaLights.gameObject; bridge.doorClip = doorClip; bridge.status = status; bridge.prompt = prompt;
                UnityEventTools.AddPersistentListener(door.OpeningStarted, bridge.BeginDoorShot);
                UnityEventTools.AddPersistentListener(door.Opened, bridge.StartTransition);
                UnityEventTools.AddPersistentListener(sequence.onTransitionCovered, bridge.TransferWhileCovered);
                UnityEventTools.AddPersistentListener(sequence.onCompleted, bridge.Complete);
                UnityEventTools.AddPersistentListener(sequence.onCancelled, bridge.OnSequenceCancelled);

                var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                timeline.name = "S03_ArenaArrival"; timeline.durationMode = TimelineAsset.DurationMode.FixedLength; timeline.fixedDuration = 3;
                AssetDatabase.CreateAsset(timeline, Unique("Timelines", "S03_ArenaArrival_" + id, ".playable"));
                var signals = timeline.CreateTrack<SignalTrack>(null, "Arena camera pan (screen/audio owned by DirectionSequence)");
                AddSignal(signals, receiver, "StartArenaShot_" + id, 0.05, bridge.StartArenaShot);
                AddSignal(signals, receiver, "EndArenaShot_" + id, 2.9, bridge.EndArenaShot);
                director.playableAsset = timeline; director.playOnAwake = false;
                director.extrapolationMode = DirectorWrapMode.None; director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                director.SetGenericBinding(signals, receiver);
                AssetDatabase.SaveAssetIfDirty(timeline);

                ApplyCinematic(root);
                ConfigureSceneLighting(scene);

                string prefabPath = Unique("Prefabs", "S03_TestRig_" + id, ".prefab");
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
                if (!EditorSceneManager.SaveScene(scene, scenePath)) throw new InvalidOperationException("Could not save generated S03 scene.");
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                EditorGUIUtility.PingObject(Selection.activeObject);
                Debug.Log("S03 generated: " + scenePath + "\nOpen this scene alone, press Play, click Game, WASD to the door, E. This is a blockout test, not a finished production scene. Home ambience is unassigned; crowd chatter is temporary. Original scenes and materials were not saved.");
            }
            finally
            {
                // Only close the newly generated scene. Never save or close the user's original scenes.
                if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        static string Unique(string folder, string name, string extension) => AssetDatabase.GenerateUniqueAssetPath(Root + "/" + folder + "/" + name + extension);
        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); return go.transform;
        }
        static GameObject Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static Material MakeMaterial(Shader shader, string name, Color color)
        {
            var material = new Material(shader) { name = name }; material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, Unique("Materials", name, ".mat")); return material;
        }
        static void Directional(Transform parent, string name, Color color, float intensity, Vector3 angles)
        {
            var light = Child(parent, name).gameObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.color = color; light.intensity = intensity; light.shadows = LightShadows.Soft; light.transform.eulerAngles = angles;
        }
        static void FitRing(GameObject ring, Vector3 center)
        {
            var renderers = ring.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new InvalidOperationException("Arena prefab has no imported renderers.");
            Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            float size = Mathf.Max(bounds.size.x, bounds.size.z);
            if (size < 0.01f) throw new InvalidOperationException("Arena mesh bounds are invalid. Check FBX import.");
            ring.transform.localScale *= 12f / size;
            bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            ring.transform.position += center - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        }
        static void CloneMaterials(GameObject ring, string id)
        {
            var copies = new Dictionary<Material, Material>();
            foreach (var renderer in ring.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i]; if (!source) continue;
                    if (!copies.TryGetValue(source, out var copy))
                    {
                        copy = new Material(source) { name = "S03_" + source.name };
                        copy.shader = Shader.Find("Universal Render Pipeline/Lit");
                        if (!copy.GetTexture("_BaseMap") && source.HasProperty("_MainTex")) copy.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
                        if (copy.GetTexture("_BumpMap")) copy.EnableKeyword("_NORMALMAP");
                        if (copy.GetTexture("_MetallicGlossMap")) copy.EnableKeyword("_METALLICSPECGLOSSMAP");
                        AssetDatabase.CreateAsset(copy, Unique("Materials", "ArenaMaterial_" + id + "_" + copies.Count, ".mat"));
                        copies.Add(source, copy);
                    }
                    materials[i] = copy;
                }
                renderer.sharedMaterials = materials;
            }
        }
        static TMPro.TMP_Text Label(Transform parent, string name, TMPro.TMP_FontAsset font, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = anchor; rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<TMPro.TextMeshProUGUI>(); text.font = font; text.fontSize = fontSize;
            text.color = Color.white; text.alignment = TMPro.TextAlignmentOptions.Center; text.raycastTarget = false;
            text.outlineWidth = 0.12f; // Instance material only; do not edit the shared font material asset.
            return text;
        }
        static void AddSignal(SignalTrack track, SignalReceiver receiver, string name, double time, UnityAction callback)
        {
            var signal = ScriptableObject.CreateInstance<SignalAsset>(); signal.name = name;
            AssetDatabase.CreateAsset(signal, Unique("Timelines", name, ".signal"));
            var emitter = track.CreateMarker<SignalEmitter>(time); emitter.asset = signal; emitter.emitOnce = true; emitter.retroactive = true;
            var reaction = new UnityEvent(); UnityEventTools.AddPersistentListener(reaction, callback); receiver.AddReaction(signal, reaction);
        }
        static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(property).objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Set(UnityEngine.Object target, string property, Vector3 value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(property).vector3Value = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        static void Set(UnityEngine.Object target, string property, float value)
        {
            var serialized = new SerializedObject(target); serialized.FindProperty(property).floatValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
