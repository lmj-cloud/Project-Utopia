using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 기존 공유 에셋을 수정하지 않고 Walk 한 종류의 독립 Avatar 테스트를 구성한다.
public static class HumanoidWalkAudit
{
    public const string Folder = "Assets/UtopiaDemo/AutomationTests/HumanoidWalk";
    public const string ScenePath = Folder + "/SoldierWalkTest.unity";
    private const string SoldierPath = "Assets/US-soldier/Mesh/US-Soldier.fbx";
    private const string WalkPath = "Assets/Models/TestModel/Walk.fbx";
    private const string Output = @"C:\Users\minjo\Documents\Codex\2026-11-04\blender-5-2-2-lts-utopia\outputs";

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Play Mode에서 에셋을 만들지 않습니다.");
        string copyPath = Folder + "/Walk_Independent.fbx";
        string controllerPath = Folder + "/WalkOnly.controller";
        string prefabPath = Folder + "/SoldierWalkTest.prefab";
        if (AssetDatabase.LoadMainAssetAtPath(copyPath) != null || File.Exists(ScenePath) || File.Exists(controllerPath) || File.Exists(prefabPath))
            throw new InvalidOperationException("테스트 출력이 이미 있습니다. 기존 결과를 덮어쓰지 않습니다.");
        if (!AssetDatabase.CopyAsset(WalkPath, copyPath)) throw new InvalidOperationException("Walk 사본 생성 실패");
        ModelImporter importer = AssetImporter.GetAtPath(copyPath) as ModelImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.sourceAvatar = null;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        ModelImporterClipAnimation[] importClips = importer.defaultClipAnimations;
        for (int i = 0; i < importClips.Length; i++)
        {
            importClips[i].name = "Walk_InPlace_Test";
            importClips[i].loopTime = true;
            importClips[i].loopPose = true;
            importClips[i].lockRootRotation = true;
            importClips[i].lockRootHeightY = true;
            importClips[i].lockRootPositionXZ = true;
            importClips[i].keepOriginalOrientation = true;
            importClips[i].keepOriginalPositionY = true;
            importClips[i].keepOriginalPositionXZ = true;
        }
        importer.clipAnimations = importClips;
        importer.SaveAndReimport();
        Avatar walkAvatar = null;
        AnimationClip walkClip = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(copyPath))
        {
            if (asset is Avatar) walkAvatar = asset as Avatar;
            if (asset is AnimationClip && !asset.name.StartsWith("__preview__")) walkClip = asset as AnimationClip;
        }
        if (walkAvatar == null || !walkAvatar.isValid || !walkAvatar.isHuman || walkClip == null || !walkClip.humanMotion)
            throw new InvalidOperationException("독립 Walk Avatar 또는 Humanoid Clip 검증 실패");
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorState state = controller.layers[0].stateMachine.AddState("Walk");
        state.motion = walkClip;
        state.iKOnFeet = false;
        controller.layers[0].stateMachine.defaultState = state;
        AssetDatabase.SaveAssetIfDirty(controller);

        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(SoldierPath);
            GameObject soldier = PrefabUtility.InstantiatePrefab(model, testScene) as GameObject;
            soldier.name = "OriginalSoldier_WalkOnly";
            Animator animator = soldier.GetComponent<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("기존 Soldier Avatar가 유효하지 않습니다.");
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            List<SkinnedMeshRenderer> skins = new List<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer skin in soldier.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (skin.name == "BSurfaceMesh.004" || skin.name == "BSurfaceMesh.003" || skin.name == "glove.001" || skin.name == "boots") skins.Add(skin);
            }
            if (skins.Count < 2) throw new InvalidOperationException("실제 스킨 평가 대상이 부족합니다.");
            WalkRetargetProbe probe = soldier.AddComponent<WalkRetargetProbe>();
            probe.Configure(animator, skins.ToArray(), Path.Combine(Output, "unity_walk_playmode.json"));
            PrefabUtility.SaveAsPrefabAsset(soldier, prefabPath);

            GameObject cameraObject = new GameObject("Walk Test Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, testScene);
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            camera.transform.position = new Vector3(2.7f, 1.6f, 4.2f);
            camera.transform.LookAt(new Vector3(0f, 0.95f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.15f, 0.17f, 0.21f);
            camera.fieldOfView = 31f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            GameObject lightObject = new GameObject("Walk Test Light");
            SceneManager.MoveGameObjectToScene(lightObject, testScene);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(40f, -25f, 0f);
            EditorSceneManager.SaveScene(testScene, ScenePath);
            File.WriteAllText(Path.Combine(Output, "unity_walk_build.json"), JsonUtility.ToJson(new WalkBuildReport { sourceModel = SoldierPath, sourceWalk = WalkPath, testWalk = copyPath, testScene = ScenePath, controller = controllerPath, prefab = prefabPath, targetAvatar = AssetDatabase.GetAssetPath(animator.avatar), sourceAvatarValid = walkAvatar.isValid, targetAvatarValid = animator.avatar.isValid, clipHuman = walkClip.humanMotion, sampledSkins = skins.Count }, true));
        }
        finally
        {
            EditorSceneManager.CloseScene(testScene, true);
        }
        return ScenePath;
    }
}

[Serializable]
public class WalkBuildReport
{
    public string sourceModel;
    public string sourceWalk;
    public string testWalk;
    public string testScene;
    public string controller;
    public string prefab;
    public string targetAvatar;
    public bool sourceAvatarValid;
    public bool targetAvatarValid;
    public bool clipHuman;
    public int sampledSkins;
}
