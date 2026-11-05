using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 애니메이션 행동 종류를 구분한다. 기존 숫자 값은 외부 호출과의 호환성을 위해 유지한다.
/// </summary>
public enum CombatActionKind
{
    None = 0,
    MmaLeftPunch = 1,
    MmaRightPunch = 2,
    KnifeLeftThrust = 3,
    KnifeRightThrust = 4,
    MmaAssassinate = 5,
    MmaFinish = 6,
    KnifeAssassinate = 7,
    KnifeFinish = 8,
    MmaComboPunch = 9
}

/// <summary>
/// 행동별 Animator 상태와 실제 모션의 연결 정보를 보관하고 재생 준비 여부를 검사한다.
/// </summary>
[CreateAssetMenu(menuName = "Utopia/Combat Animation Profile")]
public class CombatAnimationProfile : ScriptableObject
{
    /// <summary>
    /// 전투 행동의 상태 경로, Layer, Motion과 필요한 Clip 및 종료 제한 시간을 보관한다.
    /// </summary>
    [Serializable]
    public class Binding
    {
        public CombatActionKind action;
        public string statePath;
        public string layerName = "Base Layer";
        public Motion motion;
        [HideInInspector] public Motion appliedMotion;
        public AnimationClip[] requiredClips = new AnimationClip[0];
        [Min(0.1f)] public float timeoutSeconds = 4f;
    }

    [SerializeField] private RuntimeAnimatorController controller;
    [SerializeField] private List<Binding> bindings = new List<Binding>();
    // 이전 연결 도구가 차단했던 전환 식별 목록. 임의로 기존 전환을 해제하지 않기 위한 정보.
    [SerializeField, HideInInspector] private List<string> guardedTransitions = new List<string>();
    public RuntimeAnimatorController Controller { get { return controller; } }
    public IReadOnlyList<Binding> Bindings { get { return bindings; } }
    public List<string> GuardedTransitions { get { return guardedTransitions; } }

    /// <summary>
    /// 행동 종류와 일치하는 Animator 연결 정보를 찾는다.
    /// </summary>
    public Binding GetBinding(CombatActionKind action)
    {
        foreach (Binding binding in bindings)
        {
            if (binding != null && binding.action == action && action != CombatActionKind.None) return binding;
        }
        return null;
    }

    /// <summary>
    /// 지정된 애니메이션을 재생할 수 있는 참조와 상태 조건인지 검사한다.
    /// </summary>
    public bool CanPlay(Animator animator, CombatActionKind action, out string reason)
    {
        Binding binding = GetBinding(action);
        if (animator == null || !animator.isActiveAndEnabled || !animator.isInitialized || animator.runtimeAnimatorController != controller)
        {
            reason = "Animator 또는 Profile Controller 연결이 일치하지 않습니다.";
            return false;
        }
        if (binding == null || binding.motion == null || binding.requiredClips == null || binding.requiredClips.Length == 0)
        {
            reason = "실제 Motion이 연결되지 않았습니다.";
            return false;
        }
        if (binding.motion != binding.appliedMotion || binding.timeoutSeconds <= 0f || float.IsNaN(binding.timeoutSeconds) || float.IsInfinity(binding.timeoutSeconds))
        {
            reason = "Binder 적용 또는 유효한 제한 시간이 필요합니다.";
            return false;
        }
        int layer = animator.GetLayerIndex(binding.layerName);
        if (layer < 0 || !animator.HasState(layer, Animator.StringToHash(binding.statePath)))
        {
            reason = "State 또는 Layer가 없습니다.";
            return false;
        }
        foreach (AnimationClip clip in binding.requiredClips)
        {
            if (clip == null || clip.length <= 0f || clip.isLooping)
            {
                reason = "단발 행동에 필요한 Clip 또는 Loop 설정을 확인하세요.";
                return false;
            }
            bool containsClip = false;
            foreach (AnimationClip connected in controller.animationClips)
            {
                if (connected == clip) containsClip = true;
            }
            if (!containsClip)
            {
                reason = "필수 Clip이 Controller에 연결되지 않았습니다.";
                return false;
            }
        }
        reason = "";
        return true;
    }
}
