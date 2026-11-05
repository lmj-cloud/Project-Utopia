using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 격리된 Walk 테스트에서 프레임 진행과 실제 관절/스킨 변형을 기록한다.
public class WalkRetargetProbe : MonoBehaviour
{
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private SkinnedMeshRenderer[] skinRenderers;
    [SerializeField] private string outputPath;
    [SerializeField] private int sampleCount = 90;
    private readonly List<WalkSample> samples = new List<WalkSample>();
    private readonly List<Mesh> bakedMeshes = new List<Mesh>();
    private Vector3 initialPosition;
    private float nextSampleTime;
    private HumanBodyBones[] bones = { HumanBodyBones.Hips, HumanBodyBones.LeftShoulder, HumanBodyBones.RightShoulder, HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };

    public void Configure(Animator animator, SkinnedMeshRenderer[] skins, string path)
    {
        targetAnimator = animator;
        skinRenderers = skins;
        outputPath = path;
    }

    private void Start()
    {
        if (targetAnimator == null || !targetAnimator.isHuman || targetAnimator.avatar == null || !targetAnimator.avatar.isValid)
        {
            Debug.LogError("WalkRetargetProbe: 유효한 Soldier Humanoid 참조가 필요합니다.");
            enabled = false;
            return;
        }
        initialPosition = targetAnimator.transform.position;
        for (int i = 0; i < skinRenderers.Length; i++) bakedMeshes.Add(new Mesh());
    }

    private void LateUpdate()
    {
        if (samples.Count >= sampleCount || Time.unscaledTime < nextSampleTime) return;
        nextSampleTime = Time.unscaledTime + 1f / 30f;
        WalkSample sample = new WalkSample();
        sample.frame = Time.frameCount;
        sample.time = Time.unscaledTime;
        sample.normalizedTime = targetAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        sample.rootDrift = Vector3.Distance(initialPosition, targetAnimator.transform.position);
        sample.joints = new WalkJoint[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = targetAnimator.GetBoneTransform(bones[i]);
            sample.joints[i] = new WalkJoint { name = bones[i].ToString(), position = targetAnimator.transform.InverseTransformPoint(bone.position), rotation = bone.localRotation };
        }
        sample.skins = new WalkSkin[skinRenderers.Length];
        for (int i = 0; i < skinRenderers.Length; i++)
        {
            // 이 원본 모델의 Renderer scale은 100이다. true로 스케일을 보상해
            // TransformPoint가 같은 스케일을 두 번 적용하지 않도록 한다.
            skinRenderers[i].BakeMesh(bakedMeshes[i], true);
            Vector3[] vertices = bakedMeshes[i].vertices;
            // 크기가 큰 스킨도 재현 가능한 간격으로 실제 평가 정점을 기록한다.
            List<Vector3> selected = new List<Vector3>();
            int step = Mathf.Max(1, vertices.Length / 96);
            for (int j = 0; j < vertices.Length; j += step) selected.Add(skinRenderers[i].transform.TransformPoint(vertices[j]));
            sample.skins[i] = new WalkSkin { name = skinRenderers[i].name, vertices = selected.ToArray() };
        }
        samples.Add(sample);
        if (samples.Count == sampleCount)
        {
            WalkPlaybackReport report = new WalkPlaybackReport { completed = true, avatarValid = targetAnimator.avatar.isValid, isHuman = targetAnimator.isHuman, applyRootMotion = targetAnimator.applyRootMotion, samples = samples.ToArray() };
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
            Debug.Log("WalkRetargetProbe: 90 samples completed, frame " + Time.frameCount);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < bakedMeshes.Count; i++) Destroy(bakedMeshes[i]);
    }
}

[Serializable] public class WalkJoint { public string name; public Vector3 position; public Quaternion rotation; }
[Serializable] public class WalkSkin { public string name; public Vector3[] vertices; }
[Serializable] public class WalkSample { public int frame; public float time; public float normalizedTime; public float rootDrift; public WalkJoint[] joints; public WalkSkin[] skins; }
[Serializable] public class WalkPlaybackReport { public bool completed; public bool avatarValid; public bool isHuman; public bool applyRootMotion; public WalkSample[] samples; }
