using Unity.VisualScripting;
using UnityEngine;

public class PlayerAnimatorController : MonoBehaviour
{
    [Header("Animators")]
    [SerializeField] private Animator firstPersonAnimator;
    [SerializeField] private Animator thirdPersonAnimator;

    [Header("Weapon Animation Layers")]
    [SerializeField, Min(0)] private int firstPersonWeaponLayer = 0;
    [SerializeField, Min(0)] private int thirdPersonWeaponLayer = 1;

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
    [System.Serializable]
    private class ThirdPersonMotion
    {
        public string state;
        public AnimationClip motion;
    }

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
    [Header("Optional Story First Person Motions")]
    [SerializeField] private ThirdPersonMotion[] firstPersonStoryMotions = new ThirdPersonMotion[0];
    private UnityEngine.Object storyAnimationOwner;
    private CutsceneController storyCutscene;
    private int storyRequestId;
    private UnityEngine.Object storyRequestOwner;
    private readonly System.Collections.Generic.Dictionary<string, bool> storyBoolValues = new();
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
    private int upperStartedFrame;
    private bool upperResetPending;
    private bool AnimationPoseLocked => playerController != null && playerController.HasAnimationControlLock;
    private bool IsDead => status != null && status.IsDead;
    private PlayerController playerController;
    private bool reloadCancellationPending;
    private bool firstPersonReloadCancellationPending;
    private bool thirdPersonReloadCancellationPending;
    private WeaponName currentWeapon;
    private WeaponAnimationNames Names => currentWeapon == WeaponName.Pistol ? pistolAnimations : rifleAnimations;

    private static bool HasParameter(Animator target, string name, AnimatorControllerParameterType type)
    {
        if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null || string.IsNullOrEmpty(name)) return false;
        foreach (AnimatorControllerParameter parameter in target.parameters)
            if (parameter.name == name && parameter.type == type) return true;
        return false;
    }

    public void OnCrouching(bool value) => SetBool(crouchParameter, value);
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
    }

    private void Trigger(string name)
    {
        if (AnimationPoseLocked || IsDead) return;
        TriggerParameter(thirdPersonAnimator, name);
    }

    private static void TriggerParameter(Animator target, string name)
    {
        if (!HasParameter(target, name, AnimatorControllerParameterType.Trigger)) return;
        target.ResetTrigger(name);
        target.SetTrigger(name);
    }

    private string PunchPath(bool strong) => equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Gloves
        ? (strong ? mmaHeavyPunchState : mmaPunchState) : (strong ? defaultHeavyPunchState : defaultPunchState);
    private string GrabPath(bool assassination) => assassination ? knifeAssassinateState :
        equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Dagger ? knifeTakedownState : mmaGrabState;

    private void RequestMeleeAnimation(string trigger, string firstState, string thirdState)
    {
        if (AnimationPoseLocked || IsDead) return;
        ResetUpperLayer();
        // 3인칭은 Trigger 전이 한 경로만 사용한다. Empty State에는 요청하지 않는다.
        if (HasThirdPersonMotion(thirdState)) TriggerParameter(thirdPersonAnimator, trigger);
    }
    public void OnPunch() => RequestMeleeAnimation(punchTrigger, punchState, PunchPath(false));
    public void OnStrongAttack(bool dagger) => RequestMeleeAnimation(dagger ? strongDaggerTrigger : strongPunchTrigger,
        dagger ? strongDaggerState : strongPunchState, dagger ? knifeHeavyAttackState : PunchPath(true));
    public void OnDefense(bool value)
    {
        isDefending = value && !IsDead;
        SetBool(defenseParameter, isDefending);
        if (!isDefending && (activeUpperState == guardState || activeUpperState == guardHitState)) ResetUpperLayer();
    }
    public void OnHit()
    {
        if (AnimationPoseLocked || IsDead) return;
        if (HasThirdPersonMotion(hitState))
        {
            ResetUpperLayer();
            TriggerParameter(thirdPersonAnimator, hitTrigger);
        }
    }
    public void OnDefenseHit()
    {
        if (AnimationPoseLocked || IsDead) return;
        ResetTrigger(thirdPersonAnimator, hitTrigger);
        StartUpperLayer(guardHitState);
    }
    public void OnDeath()
    {
        isReloading = false;
        isDefending = false;
        ResetUpperLayer();
        if (!AnimationPoseLocked) SetBool("IsDead", true);
    }
    private void MovementSucceeded(DemoAction action, UnityEngine.Object source)
    {
        if (action != DemoAction.Jump || AnimationPoseLocked || IsDead) return;
        if (HasThirdPersonMotion(jumpState))
        {
            ResetUpperLayer();
            TriggerParameter(thirdPersonAnimator, "onJump");
        }
    }
    public void OnKnockdown()
    {
        if (AnimationPoseLocked || IsDead || equipment == null || equipment.CurrentKind != PlayerEquipmentKind.Gloves) return;
        if (HasThirdPersonMotion("Base Layer.MMA.Knockdown"))
        {
            ResetUpperLayer();
            TriggerParameter(thirdPersonAnimator, "onKnockdown");
        }
    }
    public void OnDodge() => Trigger(dodgeTrigger);
    public void OnDaggerAttack() => RequestMeleeAnimation(daggerTrigger, daggerState, knifeAttackState);
    public void OnGrab() => RequestMeleeAnimation(grabTrigger, grabState, GrabPath(false));
    public void OnAssassinate() => RequestMeleeAnimation(assassinationTrigger, assassinationState, GrabPath(true));
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
    public bool CanPlayMelee(bool dagger) => HasThirdPersonMotion(dagger ? knifeAttackState : PunchPath(false));
    public bool CanPlayStrongMelee(bool dagger) => HasThirdPersonMotion(dagger ? knifeHeavyAttackState : PunchPath(true));
    public bool CanPlayGrab(bool assassination) => HasThirdPersonMotion(GrabPath(assassination));

    [Header("Medication Animation")]
    [SerializeField] private string medicationState;
    [SerializeField] private int medicationLayer;
    public bool CanPlayMedication => HasThirdPersonMotion(medicationState);
    private static bool CanPlay(Animator target, string state, int layer)
    {
        if (target == null || !target.isActiveAndEnabled || target.runtimeAnimatorController == null || string.IsNullOrWhiteSpace(state) || layer < 0 || layer >= target.layerCount) return false;
        string path = state.Contains(".") ? state : target.GetLayerName(layer) + "." + state;
        return target.HasState(layer, Animator.StringToHash(path));
    }
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
    public void OnMedication() => Play(medicationState, medicationLayer, 0f);

    private float currentSpeed;
    private float moveHorizontal;
    private float moveVertical;

    void Awake()
    {
        playerController = GetComponent<PlayerController>();
        status = GetComponent<PlayerStatus>();
        movement = GetComponent<PlayerMovement>();
        equipment = GetComponent<PlayerEquipment>();
        if (firstPersonAnimator != null) firstPersonAnimator.enabled = false;
        PrepareAnimator(thirdPersonAnimator);
    }

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

    private void SetFloat(string parameter, float value)
    {
        if (AnimationPoseLocked) return;
        if (HasParameter(thirdPersonAnimator, parameter, AnimatorControllerParameterType.Float))
            thirdPersonAnimator.SetFloat(parameter, value);
    }

    private void SetBool(string parameter, bool value)
    {
        if (AnimationPoseLocked) return;
        if (HasParameter(thirdPersonAnimator, parameter, AnimatorControllerParameterType.Bool))
            thirdPersonAnimator.SetBool(parameter, value);
    }

    public void OnAiming(bool isAiming)
    {
        SetBool("IsAim", isAiming);
    }

    public void OnFire()
    {
        if (!AnimationPoseLocked && !IsDead) StartUpperLayer(Names.thirdPersonFire);
    }

    public void OnReload()
    {
        reloadCancellationPending = false;
        isReloading = true;
        SetBool("IsReload", true);
        // 3인칭 재장전은 직접 재생만 사용해 Trigger와 이중 진입하지 않는다.
        if (!AnimationPoseLocked && !IsDead) StartUpperLayer(Names.thirdPersonReloadState);
    }

    private void TriggerReload(Animator target)
    {
        TriggerParameter(target, "onReload");
    }

    public void OnReloadCancelled()
    {
        OnReloadFinished();
        reloadCancellationPending = true;
        firstPersonReloadCancellationPending = false;
        thirdPersonReloadCancellationPending = true;
        RestoreReloadPose();
    }

    private void OnEnable()
    {
        if (status != null) { status.Damaged += OnHit; status.Died += OnDeath; }
        if (movement != null) movement.Succeeded += MovementSucceeded;
        upperResetPending = true;
    }
    private void OnDisable()
    {
        if (storyAnimationOwner != null) EndStoryFirstPerson(storyAnimationOwner, storyRequestId);
        if (status != null) { status.Damaged -= OnHit; status.Died -= OnDeath; }
        if (movement != null) movement.Succeeded -= MovementSucceeded;
        isReloading = false;
        isDefending = false;
        ResetUpperLayer();
    }
    private void Update()
    {
        if (AnimationPoseLocked) return;
        RestoreReloadPose();
        SetBool("IsDead", IsDead);
        if (equipment != null && HasParameter(thirdPersonAnimator, weaponParameter, AnimatorControllerParameterType.Int))
            thirdPersonAnimator.SetInteger(weaponParameter, (int)equipment.CurrentKind);
        if (movement != null && movement.IsSimulationEnabled) SetBool("IsGrounded", movement.IsGrounded);
        UpdateUpperLayer();
    }
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
    private void StartUpperLayer(string state)
    {
        if (!HasThirdPersonMotion(state)) return;
        activeUpperState = state;
        upperStartedFrame = Time.frameCount;
        upperResetPending = false;
        thirdPersonAnimator.SetLayerWeight(thirdPersonWeaponLayer, 1f);
        PlayState(thirdPersonAnimator, state, thirdPersonWeaponLayer, 0f);
    }
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
        if (isDefending && equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Gloves &&
            activeUpperState != guardHitState && activeUpperState != guardState) StartUpperLayer(guardState);
        if (activeUpperState == null) { thirdPersonAnimator.SetLayerWeight(thirdPersonWeaponLayer, 0f); return; }
        if (Time.frameCount <= upperStartedFrame) return;
        AnimatorStateInfo upper = thirdPersonAnimator.GetCurrentAnimatorStateInfo(thirdPersonWeaponLayer);
        bool reload = activeUpperState == rifleAnimations.thirdPersonReloadState || activeUpperState == pistolAnimations.thirdPersonReloadState;
        bool guard = activeUpperState == guardState;
        if (upper.IsName(upperNeutralState) || (!reload && !guard && upper.normalizedTime >= 1f)) ResetUpperLayer();
        else if ((reload && !isReloading) || (guard && !isDefending)) ResetUpperLayer();
    }
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
        PlayState(thirdPersonAnimator, path, 0, 0f);
    }

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

    public void OnReloadFinished()
    {
        isReloading = false;
        ResetUpperLayer();
        SetBool("IsReload", false);
        ResetReloadTriggers(thirdPersonAnimator);
    }

    private void ResetReloadTriggers(Animator target)
    {
        ResetTrigger(target, rifleAnimations.firstPersonReloadTrigger);
        ResetTrigger(target, rifleAnimations.thirdPersonReloadTrigger);
        ResetTrigger(target, pistolAnimations.firstPersonReloadTrigger);
        ResetTrigger(target, pistolAnimations.thirdPersonReloadTrigger);
    }

    private static void ResetTrigger(Animator target, string name)
    {
        if (HasParameter(target, name, AnimatorControllerParameterType.Trigger)) target.ResetTrigger(name);
    }

    public void Play(string stateName, int layer, float normalizedTime)
    {
        PlayState(thirdPersonAnimator, stateName,
            layer < 0 ? thirdPersonWeaponLayer : layer, normalizedTime);
    }

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

    public bool CurrentAnimationIs(string name)
    {
        // 기존 호출과 호환: 전신 Animator의 Base Layer를 우선 확인한다.
        Animator target = thirdPersonAnimator;
        return target != null && target.runtimeAnimatorController != null && target.layerCount > 0 &&
            target.GetCurrentAnimatorStateInfo(0).IsName(name);
    }
}
