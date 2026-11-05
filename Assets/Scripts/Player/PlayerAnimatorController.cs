using Unity.VisualScripting;
using UnityEngine.Playables;
using UnityEngine;

/// <summary>
/// 장비와 플레이어 상태를 Animator 파라미터와 상태로 전달하고 상체 행동과 컷신 자세를 조율한다.
/// </summary>
public class PlayerAnimatorController : MonoBehaviour
{
    public Animator CombatAnimator { get { return thirdPersonAnimator; } }

    /// <summary>
    /// 새 근접 행동 후 복귀할 장비별 이동 Motion이 실제로 연결되어 있는지 확인한다.
    /// </summary>
    public bool HasCombatLocomotion()
    {
        if (equipment == null) return false;
        string group;
        if (equipment.CurrentKind == PlayerEquipmentKind.Gloves) group = "MMA";
        else if (equipment.CurrentKind == PlayerEquipmentKind.Dagger) group = "Knife";
        else return false;
        return HasThirdPersonMotion("Base Layer." + group + ".Locomotion");
    }

    /// <summary>
    /// 전투 프로필과 잠금 소유권을 확인하고 상체 Layer를 정리한 뒤 지정 모션을 요청한다.
    /// </summary>
    public bool TryPlayCombatAction(CombatAnimationProfile profile, CombatActionKind action, Object owner)
    {
        return TryPlayCombatAction(profile, action, owner, 0f);
    }

    /// <summary>콤보 연결에만 짧은 블렌드를 적용하고 기존 단발 행동의 즉시 진입은 보존한다.</summary>
    public bool TryPlayCombatAction(CombatAnimationProfile profile, CombatActionKind action, Object owner, float transitionSeconds)
    {
        if (profile == null || IsDead || IsHitReacting) return false;
        if (AnimationPoseLocked && (owner == null || playerController == null || !playerController.CanContinueOwnedCombat(owner))) return false;
        string reason;
        if (!profile.CanPlay(thirdPersonAnimator, action, out reason)) return false;
        CombatAnimationProfile.Binding binding = profile.GetBinding(action);
        combatRestorePending = false;
        // 전신 행동은 상체 발사/재장전과 동시에 자세를 덮어쓰지 않는다.
        if (thirdPersonWeaponLayer < thirdPersonAnimator.layerCount) thirdPersonAnimator.SetLayerWeight(thirdPersonWeaponLayer, 0f);
        activeUpperState = null;
        int layer = thirdPersonAnimator.GetLayerIndex(binding.layerName);
        if (transitionSeconds > 0f)
        {
            thirdPersonAnimator.CrossFadeInFixedTime(binding.statePath, transitionSeconds, layer, 0f);
        }
        else
        {
            thirdPersonAnimator.Play(Animator.StringToHash(binding.statePath), layer, 0f);
        }
        return true;
    }

    /// <summary>
    /// 다른 잠금이 없으면 이동 자세를 복구하고, 잠겨 있으면 복구를 뒤로 미룬다.
    /// </summary>
    public void EndCombatAction()
    {
        // 다른 컷신 소유자가 잠근 상태에서는 게임플레이 자세를 강제로 복구하지 않는다.
        combatRestorePending = AnimationPoseLocked;
        if (!combatRestorePending && !IsDead) RestoreLocomotion();
    }
    private bool combatRestorePending;
    private int lastHitFrame = -1;
    /// <summary>실제 피격 State가 끝나기 전에는 새로운 근접 공격으로 자세를 덮어쓰지 않는다.</summary>
    public bool IsHitReacting
    {
        get
        {
            if (!HasThirdPersonMotion(hitState)) return false;
            if (lastHitFrame == Time.frameCount) return true;
            if (thirdPersonAnimator.IsInTransition(0) && thirdPersonAnimator.GetNextAnimatorStateInfo(0).IsName(hitState)) return true;
            AnimatorStateInfo state = thirdPersonAnimator.GetCurrentAnimatorStateInfo(0);
            return state.IsName(hitState) && state.normalizedTime < 1f;
        }
    }
    [Header("Animators")]
    [SerializeField] private Animator firstPersonAnimator;
    [SerializeField] private Animator thirdPersonAnimator;

    [Header("Weapon Animation Layers")]
    [SerializeField, Min(0)] private int firstPersonWeaponLayer = 0;
    [SerializeField, Min(0)] private int thirdPersonWeaponLayer = 1;

    /// <summary>
    /// 총기별 발사·재장전·장착에 사용할 1·3인칭 Animator 상태와 Trigger 이름을 묶는다.
    /// </summary>
    [System.Serializable]
    private class WeaponAnimationNames
    {
        public string firstPersonFire = "Fire";
        public string thirdPersonFire = "Fire";
        public string firstPersonReloadState = "";
        public string thirdPersonReloadState = "";
        public string firstPersonReloadTrigger = "onReload";
        public string thirdPersonReloadTrigger = "onReload";
        public string firstPersonReloadCancelState = "Movement";
        public string thirdPersonReloadCancelState = "Default Rifle";
        public string firstPersonEquipState = "";
        public string thirdPersonEquipState = "";
    }

    [Header("Weapon Animation Names")]
    [SerializeField] private WeaponAnimationNames rifleAnimations = new WeaponAnimationNames();
    [SerializeField] private WeaponAnimationNames pistolAnimations = new WeaponAnimationNames
    {
        firstPersonFire = "PistolFire", thirdPersonFire = "PistolFire",
        thirdPersonReloadCancelState = "",
        firstPersonReloadTrigger = "onPistolReload", thirdPersonReloadTrigger = "onPistolReload"
    };
    [Header("Optional Parameters")]
    [SerializeField] private string crouchParameter = "IsCrouching";
    [SerializeField] private string weaponParameter = "WeaponType";
    [SerializeField] private string punchTrigger = "onPunch";
    [SerializeField] private string strongPunchTrigger = "onStrongPunch";
    [SerializeField] private string strongDaggerTrigger = "onStrongDaggerAttack";
    [SerializeField] private string defenseParameter = "IsDefending";
    [SerializeField] private string hitTrigger = "onHit";
    [SerializeField] private string dodgeTrigger = "onDodge";
    [SerializeField] private string daggerTrigger = "onDaggerAttack";
    [SerializeField] private string grabTrigger = "onGrab";
    [SerializeField] private string assassinationTrigger = "onAssassinate";
    [SerializeField] private string equipTrigger = "onEquip";
    /// <summary>
    /// Animator 상태 경로와 실제 재생 가능한 Animation Clip 참조를 짝지어 보관한다.
    /// </summary>
    [System.Serializable]
    private class ThirdPersonMotion
    {
        public string state;
        public AnimationClip motion;
    }

    // 상태가 있다는 이유만으로 빈 모션을 재생하지 않도록 실제 Clip 연결을 확인하는 목록.
    [Header("Third Person Motion Availability (Empty states stay inactive)")]
    [SerializeField] private ThirdPersonMotion[] thirdPersonMotions = new ThirdPersonMotion[0];
    [SerializeField] private string upperNeutralState = "UpperLayer.Neutral";
    [SerializeField] private string guardState = "UpperLayer.MMAGuard";
    [SerializeField] private string guardHitState = "UpperLayer.MMAGuardHit";
    [SerializeField] private string hitState = "Base Layer.HitReaction";
    [SerializeField] private string jumpState = "Base Layer.Jump";
    [SerializeField] private string mmaPunchState = "Base Layer.MMA.Punch";
    [SerializeField] private string mmaHeavyPunchState = "Base Layer.MMA.HeavyPunch";
    [SerializeField] private string defaultPunchState = "Base Layer.Default.Punch";
    [SerializeField] private string defaultHeavyPunchState = "Base Layer.Default.HeavyPunch";
    [SerializeField] private string knifeAttackState = "Base Layer.Knife.Attack";
    [SerializeField] private string knifeHeavyAttackState = "Base Layer.Knife.HeavyAttack";
    [SerializeField] private string mmaGrabState = "Base Layer.MMA.Grab";
    [SerializeField] private string knifeAssassinateState = "Base Layer.Knife.Assassinate";
    [SerializeField] private string knifeTakedownState = "Base Layer.Knife.Takedown";
    // 컷신 소유자가 요청할 수 있는 실제 1인칭 모션 연결 목록.
    [Header("Optional Story First Person Motions")]
    [SerializeField] private ThirdPersonMotion[] firstPersonStoryMotions = new ThirdPersonMotion[0];
    private UnityEngine.Object storyAnimationOwner;
    private CutsceneController storyCutscene;
    private int storyRequestId;
    private UnityEngine.Object storyRequestOwner;
    private readonly System.Collections.Generic.Dictionary<string, bool> storyBoolValues = new();
    /// <summary>
    /// 컷신 요청과 Animator 소유권을 검사하고 원래 값을 보관한 뒤 1인칭 bool을 변경한다.
    /// </summary>
    public bool SetStoryFirstPersonBool(UnityEngine.Object owner, int id, string parameter, bool value)
    {
        if (owner == null || owner != storyAnimationOwner || storyCutscene == null || id != storyRequestId ||
            !storyCutscene.OwnsRequest(storyRequestOwner, id) || storyCutscene.ControlsAnimator(firstPersonAnimator) ||
            !HasParameter(firstPersonAnimator, parameter, AnimatorControllerParameterType.Bool)) return false;
        if (!storyBoolValues.ContainsKey(parameter)) storyBoolValues[parameter] = firstPersonAnimator.GetBool(parameter);
        firstPersonAnimator.SetBool(parameter, value);
        return true;
    }
    public int StoryFirstPersonLayer => firstPersonWeaponLayer;
    public Animator StoryFirstPersonAnimator => firstPersonAnimator;
    public bool StoryFirstPersonReady => firstPersonAnimator != null && firstPersonAnimator.runtimeAnimatorController != null;
    /// <summary>
    /// 유효한 컷신 소유자와 요청 번호로 1인칭 Animator 사용을 예약한다.
    /// </summary>
    public bool BeginStoryFirstPerson(UnityEngine.Object owner, CutsceneController source, UnityEngine.Object requestOwner, int id)
    {
        if (owner == null || storyAnimationOwner != null || !StoryFirstPersonReady || source == null || !source.OwnsRequest(requestOwner, id)) return false;
        storyAnimationOwner = owner;
        storyCutscene = source;
        storyRequestId = id;
        storyRequestOwner = requestOwner;
        firstPersonAnimator.enabled = true;
        return true;
    }
    /// <summary>
    /// 해당 컷신이 소유한 요청과 실제 Clip 연결을 확인하고 1인칭 상태를 재생한다.
    /// </summary>
    public bool PlayStoryFirstPerson(UnityEngine.Object owner, int id, string state, int layer = -1)
    {
        if (layer < 0) layer = firstPersonWeaponLayer;
        if (owner == null || owner != storyAnimationOwner || storyCutscene == null || id != storyRequestId ||
            !storyCutscene.OwnsRequest(storyRequestOwner, id) || storyCutscene.ControlsAnimator(firstPersonAnimator) ||
            !CanPlay(firstPersonAnimator, state, layer)) return false;
        string path = state.Contains(".") ? state : firstPersonAnimator.GetLayerName(layer) + "." + state;
        foreach (ThirdPersonMotion binding in firstPersonStoryMotions ?? System.Array.Empty<ThirdPersonMotion>())
            if (binding != null && binding.state == path && binding.motion != null)
                foreach (AnimationClip clip in firstPersonAnimator.runtimeAnimatorController.animationClips)
                    if (clip == binding.motion) { firstPersonAnimator.Play(Animator.StringToHash(path), layer, 0f); return true; }
        return false;
    }
    /// <summary>
    /// 요청이 일치하면 보관한 파라미터를 복구하고 1인칭 Animator 사용을 종료한다.
    /// </summary>
    public void EndStoryFirstPerson(UnityEngine.Object owner, int id)
    {
        if (owner != storyAnimationOwner || id != storyRequestId) return;
        if (firstPersonAnimator != null && (storyCutscene == null || !storyCutscene.ControlsAnimator(firstPersonAnimator)))
            foreach (var parameter in storyBoolValues)
                if (HasParameter(firstPersonAnimator, parameter.Key, AnimatorControllerParameterType.Bool))
                    firstPersonAnimator.SetBool(parameter.Key, parameter.Value);
        storyBoolValues.Clear();
        storyAnimationOwner = null;
        storyCutscene = null;
        storyRequestOwner = null;
        if (firstPersonAnimator != null) firstPersonAnimator.enabled = false;
    }
    private PlayerStatus status;
    private PlayerMovement movement;
    private PlayerEquipment equipment;
    private bool isReloading;
    private bool isDefending;
    private string activeUpperState;
    [SerializeField] private string pistolAimState = "UpperLayer.PistolAim";
    private int upperStartedFrame;
    private bool upperResetPending;
    private PlayerMeleeCombat scriptedVictimCombat;
    private UnityEngine.Playables.PlayableGraph scriptedDefeatGraph;
    private UnityEngine.Animations.AnimationClipPlayable scriptedDefeatPlayable;
    private AnimationClip scriptedDefeatClip;
    public bool IsScriptedDefeatPlaying { get { return scriptedDefeatGraph.IsValid(); } }
    public double ScriptedDefeatTime { get { return scriptedDefeatGraph.IsValid() ? scriptedDefeatPlayable.GetTime() : 0; } }
    private bool AnimationPoseLocked => playerController != null && playerController.HasAnimationControlLock;
    private bool IsDead => status != null && status.IsDead;
    private PlayerController playerController;
    private bool reloadCancellationPending;
    private bool firstPersonReloadCancellationPending;
    private bool thirdPersonReloadCancellationPending;
    private WeaponName currentWeapon;
    private WeaponAnimationNames Names => currentWeapon == WeaponName.Pistol ? pistolAnimations : rifleAnimations;

    /// <summary>
    /// 활성 Animator에 지정 이름과 자료형의 파라미터가 있는지 확인한다.
    /// </summary>
    private static bool HasParameter(Animator target, string name, AnimatorControllerParameterType type)
    {
        if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return false;
        foreach (AnimatorControllerParameter parameter in target.parameters)
            if (parameter.name == name && parameter.type == type) return true;
        return false;
    }

    /// <summary>
    /// 현재 앉기 상태를 선택된 Animator 파라미터에 전달한다.
    /// </summary>
    public void OnCrouching(bool value) => SetBool(crouchParameter, value);
    /// <summary>
    /// 무기 종류와 재장전 상태를 정리하고 해당 장비의 이동·장착 애니메이션을 요청한다.
    /// </summary>
    public void OnWeaponChanged(WeaponName value)
    {
        OnReloadFinished();
        currentWeapon = value;
        if (!AnimationPoseLocked && HasParameter(thirdPersonAnimator, weaponParameter, AnimatorControllerParameterType.Int))
            thirdPersonAnimator.SetInteger(weaponParameter, (int)value);
        // 장비 Parameter만으로 SSM이 바뀌지 않으므로 장착 시 이동 상태도 한 번 전환한다.
        if (!AnimationPoseLocked && !IsDead) RestoreLocomotion();
        OnEquip();
    }

    /// <summary>
    /// 총기와 근접 장비를 구분하여 장비 파라미터 및 애니메이션 변경을 전달한다.
    /// </summary>
    public void OnEquipmentChanged(PlayerEquipmentKind value)
    {
        if (value == PlayerEquipmentKind.AssaultRifle || value == PlayerEquipmentKind.Pistol)
        {
            OnWeaponChanged(value == PlayerEquipmentKind.Pistol ? WeaponName.Pistol : WeaponName.AssaultRifle);
            return;
        }
        OnReloadFinished();
        if (!AnimationPoseLocked && HasParameter(thirdPersonAnimator, weaponParameter, AnimatorControllerParameterType.Int))
            thirdPersonAnimator.SetInteger(weaponParameter, (int)value);
        Trigger(equipTrigger);
        // 실제 Motion이 연결된 근접 장비만 장착 시 해당 이동 자세로 진입한다.
        // 비어 있는 기존 단검 구성에는 영향을 주지 않는다.
        if ((value == PlayerEquipmentKind.Gloves || value == PlayerEquipmentKind.Dagger) &&
            !AnimationPoseLocked && !IsDead && HasCombatLocomotion())
        {
            RestoreLocomotion();
        }
    }

    /// <summary>
    /// 잠금·사망 상태를 검사한 뒤 선택된 Animator Trigger를 요청한다.
    /// </summary>
    private void Trigger(string name)
    {
        if (AnimationPoseLocked || IsDead) return;
        TriggerParameter(thirdPersonAnimator, name);
    }

    /// <summary>
    /// 유효한 Trigger를 초기화한 뒤 설정하여 이번 요청을 전달한다.
    /// </summary>
    private static void TriggerParameter(Animator target, string name)
    {
        if (!HasParameter(target, name, AnimatorControllerParameterType.Trigger)) return;
        target.ResetTrigger(name);
        target.SetTrigger(name);
    }

    /// <summary>
    /// 현재 장비와 강공격 여부에 맞는 기존 펀치 상태 경로를 선택한다.
    /// </summary>
    private string PunchPath(bool strong) => equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Gloves
        ? (strong ? mmaHeavyPunchState : mmaPunchState) : (strong ? defaultHeavyPunchState : defaultPunchState);
    /// <summary>
    /// 암살 여부에 맞는 기존 잡기 또는 단검 암살 상태 경로를 선택한다.
    /// </summary>
    private string GrabPath(bool assassination) => assassination ? knifeAssassinateState :
        equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Dagger ? knifeTakedownState : mmaGrabState;

    /// <summary>
    /// 실제 근접 Motion이 준비된 상태에만 애니메이션 요청을 전달한다.
    /// </summary>
    private void RequestMeleeAnimation(string trigger, string firstState, string thirdState)
    {
        if (AnimationPoseLocked || IsDead) return;
        ResetUpperLayer();
        // 3인칭은 Trigger 전이 한 경로만 사용한다. Empty State에는 요청하지 않는다.
        if (HasThirdPersonMotion(thirdState)) TriggerParameter(thirdPersonAnimator, trigger);
    }
    /// <summary>
    /// 현재 장비의 기존 일반 펀치 애니메이션을 요청한다.
    /// </summary>
    public void OnPunch() => RequestMeleeAnimation(punchTrigger, punchState, PunchPath(false));
    /// <summary>
    /// 현재 근접 장비의 기존 강공격 애니메이션을 요청한다.
    /// </summary>
    public void OnStrongAttack(bool dagger) => RequestMeleeAnimation(dagger ? strongDaggerTrigger : strongPunchTrigger,
        dagger ? strongDaggerState : strongPunchState, dagger ? knifeHeavyAttackState : PunchPath(true));
    /// <summary>
    /// 방어 상태를 전달하고 방어 종료 시 상체 방어 동작을 정리한다.
    /// </summary>
    public void OnDefense(bool value)
    {
        isDefending = value && !IsDead;
        SetBool(defenseParameter, isDefending);
        if (!isDefending && (activeUpperState == guardState || activeUpperState == guardHitState)) ResetUpperLayer();
    }
    /// <summary>
    /// 실제 피격 Motion이 준비되었을 때 상체 동작을 정리하고 피격 Trigger를 전달한다.
    /// </summary>
    public void OnHit()
    {
        bool scriptedHit = scriptedVictimCombat != null && scriptedVictimCombat.CanReceiveScriptedMmaAttacks;
        if ((AnimationPoseLocked && !scriptedHit) || IsDead || lastHitFrame == Time.frameCount) return;
        if (HasThirdPersonMotion(hitState))
        {
            // Damaged 구독과 공격 취소 경로가 같은 피해를 알려도 한 번만 요청한다.
            lastHitFrame = Time.frameCount;
            combatRestorePending = false;
            ResetUpperLayer();
            if (scriptedHit && thirdPersonWeaponLayer < thirdPersonAnimator.layerCount)
                thirdPersonAnimator.SetLayerWeight(thirdPersonWeaponLayer, 0f);
            TriggerParameter(thirdPersonAnimator, hitTrigger);
        }
    }
    /// <summary>
    /// 방어 피격에 해당하는 상체 행동을 요청한다.
    /// </summary>
    public void OnDefenseHit()
    {
        if (AnimationPoseLocked || IsDead) return;
        ResetTrigger(thirdPersonAnimator, hitTrigger);
        StartUpperLayer(guardHitState);
    }
    /// <summary>
    /// 재장전과 방어 상태를 정리하고 사망 상태를 Animator에 전달한다.
    /// </summary>
    public void OnDeath()
    {
        isReloading = false;
        isDefending = false;
        ResetUpperLayer();
        if (!AnimationPoseLocked) SetBool("IsDead", true);
    }
    /// <summary>사망한 스토리 피격 대상에게만 기존 Humanoid 클립을 재생한다. 공용 Animator Controller는 수정하지 않는다.</summary>
    public bool TryPlayScriptedDefeat(UnityEngine.Object owner, AnimationClip clip, float playbackSpeed)
    {
        if (!IsDead || scriptedVictimCombat == null || !scriptedVictimCombat.OwnsScriptedMmaVictim(owner) ||
            playerController == null || playerController.HasOtherControlLock(owner) || clip == null || !clip.humanMotion ||
            thirdPersonAnimator == null || !thirdPersonAnimator.isActiveAndEnabled || !thirdPersonAnimator.isHuman ||
            scriptedDefeatGraph.IsValid()) return false;
        activeUpperState = null;
        combatRestorePending = false;
        if (thirdPersonWeaponLayer < thirdPersonAnimator.layerCount) thirdPersonAnimator.SetLayerWeight(thirdPersonWeaponLayer, 0f);
        scriptedDefeatClip = clip;
        scriptedDefeatGraph = UnityEngine.Playables.PlayableGraph.Create("Owned Page2 Defeat");
        scriptedDefeatGraph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.UnscaledGameTime);
        scriptedDefeatPlayable = UnityEngine.Animations.AnimationClipPlayable.Create(scriptedDefeatGraph, clip);
        scriptedDefeatPlayable.SetApplyFootIK(false);
        scriptedDefeatPlayable.SetSpeed(Mathf.Max(0.01f, playbackSpeed));
        var output = UnityEngine.Animations.AnimationPlayableOutput.Create(scriptedDefeatGraph, "Defeat", thirdPersonAnimator);
        output.SetSourcePlayable(scriptedDefeatPlayable);
        scriptedDefeatGraph.Play();
        return true;
    }

    /// <summary>
    /// 성공한 이동 행동 중 점프에 대응하는 실제 Motion이 있으면 점프 Trigger를 전달한다.
    /// </summary>
    private void MovementSucceeded(DemoAction action, UnityEngine.Object source)
    {
        if (action != DemoAction.Jump || AnimationPoseLocked || IsDead) return;
        if (HasThirdPersonMotion(jumpState))
        {
            ResetUpperLayer();
            TriggerParameter(thirdPersonAnimator, "onJump");
        }
    }
    /// <summary>
    /// 글러브 장비의 실제 쓰러짐 Motion이 있을 때 전신 행동을 요청한다.
    /// </summary>
    public void OnKnockdown()
    {
        if (AnimationPoseLocked || IsDead || equipment == null || equipment.CurrentKind != PlayerEquipmentKind.Gloves) return;
        if (HasThirdPersonMotion("Base Layer.MMA.Knockdown"))
        {
            ResetUpperLayer();
            TriggerParameter(thirdPersonAnimator, "onKnockdown");
        }
    }
    /// <summary>
    /// 기존 회피 Trigger를 요청한다.
    /// </summary>
    public void OnDodge() => Trigger(dodgeTrigger);
    /// <summary>
    /// 기존 단검 일반 공격의 Motion을 검사하고 애니메이션을 요청한다.
    /// </summary>
    public void OnDaggerAttack() => RequestMeleeAnimation(daggerTrigger, daggerState, knifeAttackState);
    /// <summary>
    /// 기존 잡기 Motion을 검사하고 애니메이션을 요청한다.
    /// </summary>
    public void OnGrab() => RequestMeleeAnimation(grabTrigger, grabState, GrabPath(false));
    /// <summary>
    /// 기존 암살 Motion을 검사하고 애니메이션을 요청한다.
    /// </summary>
    public void OnAssassinate() => RequestMeleeAnimation(assassinationTrigger, assassinationState, GrabPath(true));
    /// <summary>
    /// 장착 Trigger와 설정된 장비 Layer 상태 재생을 요청한다.
    /// </summary>
    public void OnEquip()
    {
        Trigger(equipTrigger);
        PlayState(thirdPersonAnimator, Names.thirdPersonEquipState, thirdPersonWeaponLayer, 0f);
    }

    [Header("Melee Animation States (optional in Timer mode)")]
    [SerializeField] private string punchState;
    [SerializeField] private string strongPunchState;
    [SerializeField] private string strongDaggerState;
    [SerializeField] private string daggerState;
    [SerializeField] private string grabState;
    [SerializeField] private string assassinationState;
    [SerializeField] private int meleeLayer;
    /// <summary>
    /// 해당 장비의 일반 근접 공격에 실제 Motion이 있는지 확인한다.
    /// </summary>
    public bool CanPlayMelee(bool dagger) => HasThirdPersonMotion(dagger ? knifeAttackState : PunchPath(false));
    /// <summary>
    /// 해당 장비의 강공격에 실제 Motion이 있는지 확인한다.
    /// </summary>
    public bool CanPlayStrongMelee(bool dagger) => HasThirdPersonMotion(dagger ? knifeHeavyAttackState : PunchPath(true));
    /// <summary>
    /// 잡기 또는 암살 상태에 실제 Motion이 연결되어 있는지 확인한다.
    /// </summary>
    public bool CanPlayGrab(bool assassination) => HasThirdPersonMotion(GrabPath(assassination));

    [Header("Medication Animation")]
    [SerializeField] private string medicationState;
    [SerializeField] private int medicationLayer;
    public bool CanPlayMedication => HasThirdPersonMotion(medicationState);
    /// <summary>
    /// 지정된 애니메이션을 재생할 수 있는 참조와 상태 조건인지 검사한다.
    /// </summary>
    private static bool CanPlay(Animator target, string state, int layer)
    {
        if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(state) || layer < 0 || layer >= target.layerCount) return false;
        string path = state.Contains(".") ? state : target.GetLayerName(layer) + "." + state;
        return target.HasState(layer, Animator.StringToHash(path));
    }
    /// <summary>
    /// 근접 Trigger와 방어·상체 상태를 정리하고 허용된 경우 이동 자세로 복구한다.
    /// </summary>
    public void OnMeleeCancelled()
    {
        ResetTrigger(thirdPersonAnimator, strongPunchTrigger);
        ResetTrigger(thirdPersonAnimator, strongDaggerTrigger);
        ResetTrigger(thirdPersonAnimator, punchTrigger);
        ResetTrigger(thirdPersonAnimator, daggerTrigger);
        ResetTrigger(thirdPersonAnimator, grabTrigger);
        ResetTrigger(thirdPersonAnimator, assassinationTrigger);
        OnDefense(false);
        if (!AnimationPoseLocked && !IsDead && thirdPersonAnimator != null && thirdPersonAnimator.isActiveAndEnabled &&
            thirdPersonAnimator.GetCurrentAnimatorStateInfo(0).IsTag("FullBodyAction")) RestoreLocomotion();
    }
    /// <summary>
    /// 설정된 복약 상태와 Layer의 재생을 요청한다.
    /// </summary>
    public void OnMedication() => Play(medicationState, medicationLayer, 0f);

    [Header("MMA 전용 이동 (해당 테스트 씬에서만 활성화)")]
    [SerializeField] private bool useMmaCombatLocomotion;
    /// <summary>명시적으로 활성화한 씬의 MMA 전투에서만 복싱 이동을 사용한다.</summary>
    public bool UsesMmaCombatLocomotion
    {
        get
        {
            return useMmaCombatLocomotion && playerController != null && playerController.IsMmaCombatStance &&
                equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Gloves;
        }
    }

    private float currentSpeed;
    private float moveHorizontal;
    private float moveVertical;

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        scriptedVictimCombat = GetComponent<PlayerMeleeCombat>();
        status = GetComponent<PlayerStatus>();
        movement = GetComponent<PlayerMovement>();
        equipment = GetComponent<PlayerEquipment>();
        if (firstPersonAnimator != null) firstPersonAnimator.enabled = false;
        PrepareAnimator(thirdPersonAnimator);
    }

    /// <summary>
    /// 캐릭터 이동은 이동 담당자가 처리하도록 Root Motion을 끄고 Animator 갱신 방식을 준비한다.
    /// </summary>
    private void PrepareAnimator(Animator target)
    {
        if (target == null)
            return;

        // 실제 이동은 PlayerMovement에서만 처리한다.
        target.applyRootMotion = false;
        // Renderer를 숨겨도 재장전 등 애니메이션 시간은 계속 진행한다.
        target.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    public float CurrentSpeed
    {
        get => currentSpeed;
        set
        {
            currentSpeed = value;
            SetFloat("Speed", value);
        }
    }

    public float MoveHorizontal
    {
        get => moveHorizontal;
        set
        {
            moveHorizontal = value;
            SetFloat("MoveX", value);
        }
    }

    public float MoveVertical
    {
        get => moveVertical;
        set
        {
            moveVertical = value;
            SetFloat("MoveY", value);
        }
    }

    /// <summary>
    /// 유효한 float 파라미터가 있을 때만 Animator 값을 전달한다.
    /// </summary>
    private void SetFloat(string parameter, float value)
    {
        if (AnimationPoseLocked) return;
        if (HasParameter(thirdPersonAnimator, parameter, AnimatorControllerParameterType.Float))
            thirdPersonAnimator.SetFloat(parameter, value);
    }

    /// <summary>
    /// 유효한 bool 파라미터가 있을 때만 Animator 값을 전달한다.
    /// </summary>
    private void SetBool(string parameter, bool value)
    {
        if (AnimationPoseLocked) return;
        if (HasParameter(thirdPersonAnimator, parameter, AnimatorControllerParameterType.Bool))
            thirdPersonAnimator.SetBool(parameter, value);
    }

    /// <summary>
    /// 현재 조준 상태를 Animator에 전달한다.
    /// </summary>
    public void OnAiming(bool isAiming)
    {
        SetBool("IsAim", isAiming);
    }

    /// <summary>
    /// 현재 총기의 상체 발사 상태를 요청한다.
    /// </summary>
    public void OnFire()
    {
        if (!AnimationPoseLocked && !IsDead) StartUpperLayer(Names.thirdPersonFire);
    }

    /// <summary>
    /// 재장전 bool과 현재 총기 상체 상태를 설정한다.
    /// </summary>
    public void OnReload()
    {
        reloadCancellationPending = false;
        isReloading = true;
        SetBool("IsReload", true);
        // 3인칭 재장전은 직접 재생만 사용해 Trigger와 이중 진입하지 않는다.
        if (!AnimationPoseLocked && !IsDead) StartUpperLayer(Names.thirdPersonReloadState);
    }

    /// <summary>
    /// 해당 Animator의 유효한 재장전 Trigger를 전달한다.
    /// </summary>
    private void TriggerReload(Animator target)
    {
        TriggerParameter(target, "onReload");
    }

    /// <summary>
    /// 재장전 취소 상태를 기록하고 복구 가능한 자세와 파라미터를 정리한다.
    /// </summary>
    public void OnReloadCancelled()
    {
        OnReloadFinished();
        reloadCancellationPending = true;
        firstPersonReloadCancellationPending = false;
        thirdPersonReloadCancellationPending = true;
        RestoreReloadPose();
    }

    /// <summary>
    /// 플레이어 피격·사망 및 이동 성공 알림을 Animator 갱신에 연결한다.
    /// </summary>
    private void OnEnable()
    {
        if (status != null) { status.Damaged += OnHit; status.Died += OnDeath; }
        if (movement != null) movement.Succeeded += MovementSucceeded;
        upperResetPending = true;
    }
    /// <summary>
    /// 피격·사망·이동 알림을 해제하고 남은 애니메이션 상태를 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (scriptedDefeatGraph.IsValid()) scriptedDefeatGraph.Destroy();
        scriptedDefeatClip = null;
        if (storyAnimationOwner != null) EndStoryFirstPerson(storyAnimationOwner, storyRequestId);
        if (status != null) { status.Damaged -= OnHit; status.Died -= OnDeath; }
        if (movement != null) movement.Succeeded -= MovementSucceeded;
        isReloading = false;
        isDefending = false;
        ResetUpperLayer();
    }
    /// <summary>
    /// 잠금이 없을 때 보류 자세 복구와 장비·접지 파라미터, 상체 Layer를 갱신한다.
    /// </summary>
    private void Update()
    {
        if (scriptedDefeatGraph.IsValid())
        {
            // 마지막 자세를 유지하여 검은 화면 뒤에서 Idle로 돌아오지 않는다.
            if (scriptedDefeatClip != null && scriptedDefeatPlayable.GetTime() >= scriptedDefeatClip.length - 0.001f)
            {
                scriptedDefeatPlayable.SetTime(scriptedDefeatClip.length - 0.001f);
                scriptedDefeatPlayable.SetSpeed(0);
            }
            return;
        }
        if (AnimationPoseLocked) return;
        if (combatRestorePending)
        {
            combatRestorePending = false;
            if (!IsDead) RestoreLocomotion();
        }
        RestoreReloadPose();
        // 기존 일반 이동 Tree는 0, MMA 전투의 복싱 Tree는 1로 선택한다.
        SetFloat("MmaCombat", UsesMmaCombatLocomotion ? 1f : 0f);
        SetBool("IsDead", IsDead);
        if (equipment != null && HasParameter(thirdPersonAnimator, weaponParameter, AnimatorControllerParameterType.Int))
            thirdPersonAnimator.SetInteger(weaponParameter, (int)equipment.CurrentKind);
        if (movement != null && movement.IsSimulationEnabled) SetBool("IsGrounded", movement.IsGrounded);
        UpdateUpperLayer();
    }
    /// <summary>
    /// 상태 경로와 바인딩 및 Controller Clip 참조가 실제로 일치하는지 확인한다.
    /// </summary>
    private bool HasThirdPersonMotion(string state)
    {
        if (!CanPlay(thirdPersonAnimator, state, state != null && state.StartsWith("UpperLayer.") ? thirdPersonWeaponLayer : 0)) return false;
        if (thirdPersonMotions == null) return false;
        foreach (ThirdPersonMotion binding in thirdPersonMotions)
        {
            if (binding == null || binding.state != state || binding.motion == null) continue;
            foreach (AnimationClip clip in thirdPersonAnimator.runtimeAnimatorController.animationClips)
                if (clip == binding.motion) return true;
        }
        return false;
    }
    /// <summary>
    /// 실제 상체 Motion이 있을 때 Layer를 켜고 재생 상태와 시작 프레임을 기록한다.
    /// </summary>
    private void StartUpperLayer(string state)
    {
        if (!HasThirdPersonMotion(state)) return;
        activeUpperState = state;
        upperStartedFrame = Time.frameCount;
        upperResetPending = false;
        thirdPersonAnimator.SetLayerWeight(thirdPersonWeaponLayer, 1f);
        PlayState(thirdPersonAnimator, state, thirdPersonWeaponLayer, 0f);
    }
    /// <summary>
    /// 상체 행동을 정리하고, 잠금 중이면 자세 초기화를 뒤로 미룬다.
    /// </summary>
    private void ResetUpperLayer()
    {
        activeUpperState = null;
        upperResetPending = true;
        if (AnimationPoseLocked || thirdPersonAnimator == null || !thirdPersonAnimator.isActiveAndEnabled ||
            thirdPersonWeaponLayer < 0 || thirdPersonWeaponLayer >= thirdPersonAnimator.layerCount) return;
        thirdPersonAnimator.SetLayerWeight(thirdPersonWeaponLayer, 0f);
        PlayState(thirdPersonAnimator, upperNeutralState, thirdPersonWeaponLayer, 0f);
        upperResetPending = false;
    }
    /// <summary>
    /// 전신 행동·방어·재장전과 모션 종료 시점에 맞추어 상체 Layer를 유지하거나 복구한다.
    /// </summary>
    private void UpdateUpperLayer()
    {
        if (thirdPersonAnimator == null || !thirdPersonAnimator.isActiveAndEnabled || thirdPersonAnimator.runtimeAnimatorController == null ||
            thirdPersonWeaponLayer < 0 || thirdPersonWeaponLayer >= thirdPersonAnimator.layerCount) return;
        AnimatorStateInfo body = thirdPersonAnimator.GetCurrentAnimatorStateInfo(0);
        bool fullBody = body.IsTag("FullBodyAction") || body.IsTag("Death");
        if (thirdPersonAnimator.IsInTransition(0))
        {
            AnimatorStateInfo next = thirdPersonAnimator.GetNextAnimatorStateInfo(0);
            fullBody |= next.IsTag("FullBodyAction") || next.IsTag("Death");
        }
        if (IsDead || fullBody) { ResetUpperLayer(); return; }
        if (upperResetPending) ResetUpperLayer();
        // 권총 조준 이동은 기존 하체 이동 위에 실제 조준 Idle의 상체만 겹친다.
        bool usePistolAim = equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Pistol &&
            playerController != null && playerController.IsAiming;
        if (activeUpperState == pistolAimState && !usePistolAim) ResetUpperLayer();
        if (activeUpperState == null && usePistolAim) StartUpperLayer(pistolAimState);
        if (isDefending && equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Gloves &&
            activeUpperState != guardHitState && activeUpperState != guardState) StartUpperLayer(guardState);
        if (activeUpperState == null) { thirdPersonAnimator.SetLayerWeight(thirdPersonWeaponLayer, 0f); return; }
        if (Time.frameCount <= upperStartedFrame) return;
        AnimatorStateInfo upper = thirdPersonAnimator.GetCurrentAnimatorStateInfo(thirdPersonWeaponLayer);
        bool reload = activeUpperState == rifleAnimations.thirdPersonReloadState || activeUpperState == pistolAnimations.thirdPersonReloadState;
        bool guard = activeUpperState == guardState;
        bool aimPose = activeUpperState == pistolAimState;
        if (upper.IsName(upperNeutralState) || (!reload && !guard && !aimPose && upper.normalizedTime >= 1f)) ResetUpperLayer();
        else if ((reload && !isReloading) || (guard && !isDefending)) ResetUpperLayer();
    }
    /// <summary>
    /// 현재 장비·앉기·조준 상태에 맞는 준비된 이동 상태를 선택한다.
    /// </summary>
    private void RestoreLocomotion()
    {
        PlayerEquipmentKind kind = equipment != null ? equipment.CurrentKind : PlayerEquipmentKind.BareHands;
        string group = kind == PlayerEquipmentKind.AssaultRifle ? "Rifle" : kind == PlayerEquipmentKind.Pistol ? "Pistol" :
            kind == PlayerEquipmentKind.Dagger ? "Knife" : kind == PlayerEquipmentKind.Gloves ? "MMA" : "Default";
        bool crouch = movement != null && movement.IsCrouching;
        bool aim = playerController != null && playerController.IsAiming && (kind == PlayerEquipmentKind.AssaultRifle || kind == PlayerEquipmentKind.Pistol);
        string path = "Base Layer." + group + "." + (crouch ? (aim ? "CrouchAimLocomotion" : "CrouchLocomotion") : aim ? "AimLocomotion" : "Locomotion");
        if (!HasThirdPersonMotion(path)) path = kind == PlayerEquipmentKind.AssaultRifle && aim ? "Base Layer.Rifle.AimLocomotion" :
            kind == PlayerEquipmentKind.AssaultRifle ? "Base Layer.Rifle.Locomotion" : "Base Layer.Default.Locomotion";
        // MMA 공격의 마지막 자세에서 이동 자세로 짧게 연결한다. 공격 진입과 타격 이벤트 시점은 유지한다.
        if (kind == PlayerEquipmentKind.Gloves && thirdPersonAnimator != null &&
            thirdPersonAnimator.GetCurrentAnimatorStateInfo(0).IsTag("FullBodyAction") && HasThirdPersonMotion(path))
        {
            thirdPersonAnimator.CrossFadeInFixedTime(path, 0.08f, 0, 0f);
        }
        else
        {
            PlayState(thirdPersonAnimator, path, 0, 0f);
        }
    }

    /// <summary>
    /// 입력 잠금이 해제된 뒤 보류했던 재장전 파라미터와 자세를 복구한다.
    /// </summary>
    private void RestoreReloadPose()
    {
        if (!reloadCancellationPending || AnimationPoseLocked) return;
        if (thirdPersonReloadCancellationPending && (thirdPersonAnimator == null || thirdPersonAnimator.isActiveAndEnabled))
        {
            ResetReloadTriggers(thirdPersonAnimator);
            if (HasParameter(thirdPersonAnimator, "IsReload", AnimatorControllerParameterType.Bool))
                thirdPersonAnimator.SetBool("IsReload", false);
            PlayState(thirdPersonAnimator, Names.thirdPersonReloadCancelState, thirdPersonWeaponLayer, 0f);
            thirdPersonReloadCancellationPending = false;
        }
        reloadCancellationPending = firstPersonReloadCancellationPending || thirdPersonReloadCancellationPending;
    }

    /// <summary>
    /// 재장전 상태와 상체 동작을 정리하고 파라미터 복구를 요청한다.
    /// </summary>
    public void OnReloadFinished()
    {
        isReloading = false;
        ResetUpperLayer();
        SetBool("IsReload", false);
        ResetReloadTriggers(thirdPersonAnimator);
    }

    /// <summary>
    /// 해당 Animator의 재장전 Trigger들을 초기화한다.
    /// </summary>
    private void ResetReloadTriggers(Animator target)
    {
        ResetTrigger(target, rifleAnimations.firstPersonReloadTrigger);
        ResetTrigger(target, rifleAnimations.thirdPersonReloadTrigger);
        ResetTrigger(target, pistolAnimations.firstPersonReloadTrigger);
        ResetTrigger(target, pistolAnimations.thirdPersonReloadTrigger);
    }

    /// <summary>
    /// 유효한 Trigger 파라미터만 초기화한다.
    /// </summary>
    private static void ResetTrigger(Animator target, string name)
    {
        if (HasParameter(target, name, AnimatorControllerParameterType.Trigger)) target.ResetTrigger(name);
    }

    /// <summary>
    /// 설정된 애니메이션 재생 요청을 실제 Animator 상태 검사 경로로 전달한다.
    /// </summary>
    public void Play(string stateName, int layer, float normalizedTime)
    {
        PlayState(thirdPersonAnimator, stateName,
            layer < 0 ? thirdPersonWeaponLayer : layer, normalizedTime);
    }

    /// <summary>
    /// 잠금과 실제 Motion 준비 여부를 확인한 뒤 지정 Animator 상태를 재생한다.
    /// </summary>
    private void PlayState(Animator target, string stateName, int layer, float normalizedTime)
    {
        if (AnimationPoseLocked || target == firstPersonAnimator) return;
        if (target == thirdPersonAnimator && stateName != upperNeutralState && !HasThirdPersonMotion(stateName)) return;
        if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null || string.IsNullOrEmpty(stateName) || layer < 0)
            return;

        if (layer >= target.layerCount)
        {
            return;
        }

        // 예: Base Layer.Fire / UpperBody.Fire
        string statePath = stateName.Contains(".")
            ? stateName : target.GetLayerName(layer) + "." + stateName;
        int hash = Animator.StringToHash(statePath);
        if (target.HasState(layer, hash)) target.Play(hash, layer, normalizedTime);
    }

    /// <summary>
    /// 선택된 Layer에서 현재 재생 중인 상태가 지정 상태인지 확인한다.
    /// </summary>
    public bool CurrentAnimationIs(string name)
    {
        // 기존 호출과 호환: 전신 Animator의 Base Layer를 우선 확인한다.
        Animator target = thirdPersonAnimator;
        return target != null && target.runtimeAnimatorController != null && target.layerCount > 0 &&
            target.GetCurrentAnimatorStateInfo(0).IsName(name);
    }
}
