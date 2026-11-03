using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;

namespace Capstone.Direction.Workspace.S03.Editor
{
    public static partial class S03WorkbenchBuilder
    {
        public static void InspectArenaResources()
        {
            foreach (string path in new[] { "Assets/3D Boxing Arena Pack/Prefabs/Crowd Area.prefab", "Assets/3D Boxing Arena Pack/Prefabs/Crowd area with props.prefab" })
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                        Debug.Log("S03_RESOURCE_DETAIL " + path + " / " + r.name + " bounds=" + r.bounds + " local=" + r.transform.localPosition);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }
        [MenuItem("Capstone/Direction/S03 Workspace/Upgrade existing cinematic")]
        public static void UpgradeExisting()
        {
            if (!CanBuild()) throw new InvalidOperationException("Stop Play and wait for compilation.");
            var path = AssetDatabase.FindAssets("t:Scene", new[] { Root + "/Scenes" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderByDescending(p => p).FirstOrDefault();
            if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Generate the workbench first.");
            var previous = SceneManager.GetActiveScene();
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; resolve them before upgrade. Nothing was saved.");
            bool alreadyLoaded = SceneManager.GetSceneByPath(path).isLoaded;
            var mode = Application.isBatchMode && string.IsNullOrEmpty(previous.path)
                ? OpenSceneMode.Single : OpenSceneMode.Additive;
            var scene = alreadyLoaded ? SceneManager.GetSceneByPath(path) : EditorSceneManager.OpenScene(path, mode);
            try
            {
                SceneManager.SetActiveScene(scene);
                var bridge = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<S03SceneBridge>(true)).Single();
                var instance = bridge.transform.root.gameObject;
                string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance);
                if (string.IsNullOrEmpty(prefabPath) || !prefabPath.StartsWith(Root + "/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Only a prefab within the S03 workspace may be upgraded.");
                // Preserve existing prefab/scene GUIDs, Timeline, signals and all external asset references.
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    ApplyCinematic(contents);
                    PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                // Refresh only the known configuration references on the instance; keep scene-local authored additions.
                ConfigureBridge(instance);
                ConfigureSceneLighting(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Could not save upgraded S03 scene.");
                Debug.Log("S03_CINEMATIC_UPGRADE_COMPLETE " + path + " (same scene/prefab GUIDs; no duplicate scene)");
            }
            finally
            {
                if (!alreadyLoaded && scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                    EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }

        static void ApplyCinematic(GameObject root)
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!pipeline) throw new InvalidOperationException("S03 requires an active URP pipeline. No shared pipeline settings will be changed.");
            var environment = root.transform.Find("Environment");
            var arena = environment.Find("Arena");
            var home = environment.Find("Home_Blockout");
            var old = arena.Find("S03_CinematicInterior");
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject); // Only this tool's generated S03 geometry.
            var interior = Child(arena, "S03_CinematicInterior");
            var structure = CinematicMaterial("Structure", new Color(0.075f, 0.095f, 0.13f), 0.3f);
            var panels = CinematicMaterial("WallPanels", new Color(0.16f, 0.20f, 0.25f), 0.15f);
            var accent = CinematicMaterial("AccentRed", new Color(0.34f, 0.055f, 0.06f), 0.2f);
            var concrete = CinematicMaterial("Walkway", new Color(0.18f, 0.19f, 0.21f), 0.08f);
            var strip = CinematicMaterial("LightStrips", new Color(0.75f, 0.85f, 1), 0.5f, new Color(0.65f, 0.8f, 1) * 2);
            var portal = CinematicMaterial("DoorGlow", Color.white, 0.1f, Color.white * 4);
            arena.Find("ArenaGround").localScale = new Vector3(48, 0.3f, 44);

            // Original stadium meshes first: a whole audience structure and the roof, with new S03 material copies.
            var materialCopies = new Dictionary<Material, Material>();
            // The vendor audience mesh is a large bowl, not a modular wall. Keep it behind the ring;
            // centering the entire bowl around the test rig puts its underside through the camera aisle.
            ReuseArenaPrefab("Assets/3D Boxing Arena Pack/Prefabs/Crowd Area.prefab", interior,
                "Audience_Backdrop_Asset", new Vector3(80, 0, 23), 40, materialCopies);
            ReuseArenaPrefab("Assets/3D Boxing Arena Pack/Prefabs/Boxing RIng Props 04/Crowd_Chair01.prefab", interior,
                "Audience_WestSeats", new Vector3(63, 0, 0), 14, materialCopies);
            ReuseArenaPrefab("Assets/3D Boxing Arena Pack/Prefabs/Boxing RIng Props 04/Crowd_Chair002.prefab", interior,
                "Audience_EastSeats", new Vector3(97, 0, 0), 14, materialCopies);
            ReuseArenaPrefab("Assets/3D Boxing Arena Pack/Prefabs/Complete Roof.prefab", interior,
                "Roof_Asset", new Vector3(80, 10.4f, 0), 43, materialCopies);

            // Opaque boundaries remain even if the vendor audience is open-backed.
            Box(interior, "WestBoundary", new Vector3(56.5f, 5.5f, 0), new Vector3(0.5f, 11, 44), panels);
            Box(interior, "EastBoundary", new Vector3(103.5f, 5.5f, 0), new Vector3(0.5f, 11, 44), panels);
            Box(interior, "NorthBoundary", new Vector3(80, 5.5f, 21.5f), new Vector3(47, 11, 0.5f), panels);
            Box(interior, "SouthBoundary", new Vector3(80, 5.5f, -21.5f), new Vector3(47, 11, 0.5f), panels);
            Box(interior, "CeilingBoundary", new Vector3(80, 11.5f, 0), new Vector3(48, 0.3f, 44), structure);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(interior, "UpperAccent_" + side, new Vector3(80, 7.2f, side * 21.15f), new Vector3(46, 0.45f, 0.16f), accent);
                Box(interior, "UpperLight_" + side, new Vector3(80, 8.2f, side * 21.1f), new Vector3(44, 0.09f, 0.12f), strip);
                for (int bay = -3; bay <= 3; bay++)
                {
                    Box(interior, "WallColumn_" + side + "_" + bay, new Vector3(80 + bay * 6.7f, 5.4f, side * 21.05f), new Vector3(0.45f, 10.8f, 0.55f), structure);
                    Box(interior, "CeilingBeam_" + side + "_" + bay, new Vector3(80 + bay * 6.7f, 10.55f, 0), new Vector3(0.25f, 0.35f, 42), structure);
                }
                // Tiered side galleries and aisle detail; central circulation and arrival remain clear.
                for (int row = 0; row < 4; row++)
                    Box(interior, "GalleryTier_" + side + "_" + row,
                        new Vector3(80 + side * (15 + row * 1.2f), 0.3f + row * 0.45f, 0), new Vector3(1.25f, 0.6f + row * 0.9f, 30), concrete);
                Box(interior, "SideRail_" + side, new Vector3(80 + side * 13.8f, 1.1f, 0), new Vector3(0.08f, 0.08f, 29), accent);
            }
            Box(interior, "ArrivalAisle", new Vector3(80, 0.015f, -14), new Vector3(5, 0.03f, 12), concrete);
            for (int i = -1; i <= 1; i += 2)
                Box(interior, "ArrivalAisleLight_" + i, new Vector3(80 + i * 2.35f, 0.04f, -14), new Vector3(0.06f, 0.04f, 11), strip);

            var arenaLights = root.transform.Find("ArenaLights");
            // The original outdoor directional casts the new ceiling's shadow across the entire room.
            // Use it as an indoor soft fill; local ring spots provide the focal lighting.
            var indoorFill = arenaLights.Find("ArenaCoolLight").GetComponent<Light>();
            indoorFill.shadows = LightShadows.None; indoorFill.intensity = 1.35f;
            var oldLights = arenaLights.Find("S03_InteriorLighting");
            if (oldLights) UnityEngine.Object.DestroyImmediate(oldLights.gameObject);
            var lighting = Child(arenaLights, "S03_InteriorLighting");
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var fixture = Box(interior, "RingLightFixture_" + x + "_" + z, new Vector3(80 + x * 5, 9, z * 5), new Vector3(2.2f, 0.15f, 0.65f), strip);
                    fixture.transform.rotation = Quaternion.Euler(0, x * z * 25, 0);
                    var light = Child(lighting, "RingSpot_" + x + "_" + z).gameObject.AddComponent<Light>();
                    light.type = LightType.Spot; light.range = 23; light.spotAngle = 75; light.intensity = 28;
                    light.color = new Color(0.86f, 0.93f, 1); light.shadows = LightShadows.None;
                    light.transform.position = new Vector3(80 + x * 5, 8.6f, z * 5);
                    light.transform.LookAt(new Vector3(80, 0, 0));
                }
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    var fill = Child(lighting, "AudienceFill_" + x + "_" + z).gameObject.AddComponent<Light>();
                    fill.type = LightType.Point; fill.range = 25; fill.intensity = 9;
                    fill.color = z < 0 ? new Color(0.8f, 0.88f, 1) : new Color(1, 0.86f, 0.76f);
                    fill.shadows = LightShadows.None; fill.transform.position = new Vector3(80 + x * 12, 5, z * 11);
                }
            Box(interior, "ArenaSignBacking", new Vector3(80, 7.7f, 10.1f), new Vector3(22, 2.7f, 0.15f), structure);
            for (int side = -1; side <= 1; side += 2)
                Box(interior, "ArenaSignSupport_" + side, new Vector3(80 + side * 10.2f, 9.8f, 10.1f), new Vector3(0.15f, 2.2f, 0.15f), structure);
            var signObject = new GameObject("ArenaIdentity", typeof(RectTransform), typeof(TMPro.TextMeshPro));
            signObject.transform.SetParent(interior, false); signObject.transform.position = new Vector3(80, 7.7f, 9.95f);
            var sign = signObject.GetComponent<TMPro.TextMeshPro>();
            sign.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/S03Prototype/Fonts/S03PromptFont.asset");
            sign.text = "UTOPIA ARENA"; sign.fontSize = 15; sign.alignment = TMPro.TextAlignmentOptions.Center;
            sign.color = new Color(0.8f, 0.89f, 1); sign.rectTransform.sizeDelta = new Vector2(21, 2.6f);
            Box(interior, "ArenaSignUnderline", new Vector3(80, 6.45f, 9.95f), new Vector3(20, 0.08f, 0.1f), accent);

            var existingHome = home.Find("S03_DoorwayLight");
            if (existingHome) UnityEngine.Object.DestroyImmediate(existingHome.gameObject);
            var doorway = Child(home, "S03_DoorwayLight");
            Box(doorway, "HomeCeiling", new Vector3(0, 3.25f, -1), new Vector3(8.3f, 0.2f, 10.2f), panels);
            var focus = Child(doorway, "DoorFocus"); focus.position = new Vector3(0, 1.45f, 4.1f);
            var end = Child(doorway, "DoorDollyEnd"); end.position = new Vector3(0, 1.65f, 3.05f);
            var glow = GameObject.CreatePrimitive(PrimitiveType.Quad); glow.name = "DoorGlowPortal";
            glow.transform.SetParent(doorway, false); glow.transform.position = new Vector3(0, 1.3f, 4.2f); glow.transform.localScale = new Vector3(2.1f, 2.65f, 1);
            UnityEngine.Object.DestroyImmediate(glow.GetComponent<Collider>());
            glow.GetComponent<Renderer>().sharedMaterial = portal; glow.GetComponent<Renderer>().enabled = false;
            var doorLight = Child(doorway, "DoorLight").gameObject.AddComponent<Light>();
            doorLight.type = LightType.Point; doorLight.range = 7; doorLight.intensity = 0; doorLight.color = new Color(1, 0.97f, 0.88f);
            doorLight.transform.position = new Vector3(0, 1.8f, 3.7f);

            var fx = root.GetComponentInChildren<S03SceneBridge>(true);
            var volumeObject = fx.transform.Find("S03_CinematicVolume");
            if (!volumeObject) volumeObject = Child(fx.transform, "S03_CinematicVolume");
            var volume = volumeObject.GetComponent<Volume>() ?? volumeObject.gameObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 80; volume.weight = 1; volume.sharedProfile = CinematicProfile();
            var effects = fx.GetComponent<S03CinematicLight>() ?? fx.gameObject.AddComponent<S03CinematicLight>();
            effects.volume = volume; effects.doorwayLight = doorLight; effects.portal = glow.GetComponent<Renderer>();
            ConfigureBridge(root);
            Debug.Log("S03 URP preflight: supportsHDR=" + pipeline.supportsHDR + "; dedicated profile, layer 0, camera post-processing. Existing pipeline assets untouched.");
        }

        static void ConfigureBridge(GameObject root)
        {
            var bridge = root.GetComponentInChildren<S03SceneBridge>(true);
            var doorway = root.transform.Find("Environment/Home_Blockout/S03_DoorwayLight");
            bridge.cinematicTransition = true; bridge.whiteBuildDuration = 3; bridge.revealDuration = 1.8f; bridge.overallTimeout = 14;
            bridge.doorFocus = doorway.Find("DoorFocus"); bridge.doorDollyEnd = doorway.Find("DoorDollyEnd");
            bridge.outputCamera = root.GetComponentInChildren<Camera>(true);
            bridge.lightEffects = bridge.GetComponent<S03CinematicLight>();
            bridge.outputCamera.allowHDR = true;
            var data = bridge.outputCamera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.volumeLayerMask = 1;
            var brain = bridge.outputCamera.GetComponent<CinemachineBrain>();
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.85f);
            bridge.cinematicCamera.transform.position = bridge.outputCamera.transform.position;
            bridge.cinematicCamera.transform.rotation = bridge.outputCamera.transform.rotation;
            var lens = bridge.cinematicCamera.Lens; lens.FieldOfView = 60; bridge.cinematicCamera.Lens = lens;
            bridge.arenaShotStart.position = new Vector3(89, 5.3f, -15);
            bridge.arenaShotEnd.position = new Vector3(86, 4.2f, -12.5f);
            bridge.destination.position = new Vector3(80, 0.05f, -8.5f);
            EditorUtility.SetDirty(bridge); EditorUtility.SetDirty(data); EditorUtility.SetDirty(brain);
        }

        static void ConfigureSceneLighting(Scene scene)
        {
            if (!scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<S03SceneBridge>(true)))
                throw new InvalidOperationException("Ambient changes are restricted to the S03 test scene.");
            SceneManager.SetActiveScene(scene);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.19f, 0.22f, 0.27f);
            RenderSettings.ambientIntensity = 1;
        }

        static Material CinematicMaterial(string name, Color color, float smoothness, Color emission = default)
        {
            string path = Root + "/Materials/S03_Cinematic_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Cull", 0);
            if (emission.maxColorComponent > 0) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", emission); }
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        static VolumeProfile CinematicProfile()
        {
            const string path = Root + "/Materials/S03_CinematicProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, path); }
            T Get<T>() where T : VolumeComponent
            {
                if (profile.TryGet<T>(out var component)) return component;
                component = profile.Add<T>(); AssetDatabase.AddObjectToAsset(component, profile); return component;
            }
            var bloom = Get<Bloom>(); bloom.threshold.Override(0.9f); bloom.intensity.Override(0.22f); bloom.scatter.Override(0.65f);
            var color = Get<ColorAdjustments>(); color.postExposure.Override(0); color.contrast.Override(7); color.saturation.Override(-7);
            var tone = Get<Tonemapping>(); tone.mode.Override(TonemappingMode.ACES);
            foreach (var component in profile.components) { EditorUtility.SetDirty(component); AssetDatabase.SaveAssetIfDirty(component); }
            EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile); return profile;
        }

        static void ReuseArenaPrefab(string path, Transform parent, string name, Vector3 bottomCenter, float width, Dictionary<Material, Material> copies, float depth = 0)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!source) throw new InvalidOperationException("Missing restored arena prefab: " + path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(source, parent); go.name = name;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Arena resource has no renderer: " + path);
            Bounds bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Debug.Log("S03_ARENA_ASSET_INSPECTION " + path + " renderers=" + renderers.Length + " sourceBounds=" + bounds.size);
            float longest = Mathf.Max(bounds.size.x, bounds.size.z);
            if (longest <= 0.01f) throw new InvalidOperationException("Arena resource bounds invalid: " + path);
            go.transform.localScale = Vector3.Scale(go.transform.localScale, depth > 0
                ? new Vector3(width / bounds.size.x, width / longest, depth / bounds.size.z)
                : Vector3.one * (width / longest));
            bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            go.transform.position += bottomCenter - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var original = mats[i]; if (!original) continue;
                    if (!copies.TryGetValue(original, out var copy))
                    {
                        string id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
                        string localPath = Root + "/Materials/S03_Reused_" + id + ".mat";
                        copy = AssetDatabase.LoadAssetAtPath<Material>(localPath);
                        if (!copy)
                        {
                            copy = new Material(original); copy.shader = Shader.Find("Universal Render Pipeline/Lit");
                            if (copy.GetTexture("_BumpMap")) copy.EnableKeyword("_NORMALMAP");
                            if (copy.GetTexture("_MetallicGlossMap")) copy.EnableKeyword("_METALLICSPECGLOSSMAP");
                            copy.SetFloat("_Cull", 0); AssetDatabase.CreateAsset(copy, localPath);
                        }
                        copies.Add(original, copy);
                    }
                    mats[i] = copy;
                }
                r.sharedMaterials = mats;
            }
        }
    }
}
