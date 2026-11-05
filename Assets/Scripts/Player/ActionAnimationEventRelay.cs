using UnityEngine;

// Attach only to the object holding the Animator, then connect its parent action controller.
/// <summary>
/// Animator의 Animation Event를 현재 요청 토큰과 상태로 검증하여 공격, 아이템 사용 및 문 동작에 전달한다.
/// </summary>
public class ActionAnimationEventRelay : MonoBehaviour
{
    private PlayerCombatActions combatOwner;
    private CombatAnimationProfile.Binding combatBinding;
    // 전투 Relay가 수신하도록 예약된 요청 번호.
    private int combatId;
    private int combatStartedFrame;
    private bool combatArmed;
    private bool combatHitConsumed;
    private bool combatCompleteConsumed;
    private int combatArmedFrame;

    /// <summary>
    /// 새 전투 요청의 소유자·번호·State·Clip을 기록하고 이벤트 소비 상태를 초기화한다.
    /// </summary>
    public void BeginCombatRequest(PlayerCombatActions owner, int id, CombatAnimationProfile.Binding binding, Animator source)
    {
        combatOwner = owner;
        combatId = id;
        combatBinding = binding;
        animator = source;
        combatStartedFrame = Time.frameCount;
        combatArmed = false;
        combatHitConsumed = false;
        combatCompleteConsumed = false;
    }

    /// <summary>
    /// 같은 소유자와 요청 번호의 전투 이벤트 수신 상태만 정리한다.
    /// </summary>
    public void EndCombatRequest(PlayerCombatActions owner, int id)
    {
        if (combatOwner != owner || combatId != id) return;
        combatOwner = null;
        combatBinding = null;
        combatArmed = false;
    }

    /// <summary>
    /// 이벤트가 현재 전투 요청의 State·Layer·Clip 및 재생 시점과 일치하는지 검사한다.
    /// </summary>
    private bool MatchesCombatEvent(AnimationEvent evt)
    {
        if (combatOwner == null || !combatOwner.IsRunning || combatOwner.RequestId != combatId || combatBinding == null ||
            animator == null || !animator.isActiveAndEnabled || evt == null || Time.frameCount < combatStartedFrame) return false;
        int index = animator.GetLayerIndex(combatBinding.layerName);
        if (index < 0) return false;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(index);
        // 콤보 블렌드 초반의 Arm 이벤트는 다음 State에서 발생한다. 이전 State의 이벤트는 계속 거부한다.
        if (!current.IsName(combatBinding.statePath) && animator.IsInTransition(index))
        {
            current = animator.GetNextAnimatorStateInfo(index);
        }
        if (!current.IsName(combatBinding.statePath) || current.fullPathHash != evt.animatorStateInfo.fullPathHash ||
            Mathf.Abs(current.normalizedTime - evt.animatorStateInfo.normalizedTime) > 0.1f) return false;
        foreach (AnimationClip clip in combatBinding.requiredClips)
        {
            if (clip != null && evt.animatorClipInfo.clip == clip) return true;
        }
        return false;
    }
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerMeleeCombat melee;
    [SerializeField] private ItemUseController itemUse;
    [SerializeField] private DoorController door;
    [SerializeField] private string actionState;
    [SerializeField] private int layer;
    // 모션 초반 Arm 이벤트에서 확정한 요청 번호. 이전 행동의 이벤트를 거부할 때 사용한다.
    private int armedId;
    private int observedId = -1;
    private bool armed;
    private int armedFrame;
    private int CurrentId => melee != null ? melee.RequestId : itemUse != null ? itemUse.RequestId : door != null ? door.RequestId : -1;
    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake() { if (animator == null) animator = GetComponent<Animator>(); }
    /// <summary>
    /// 현재 요청 번호가 바뀌면 이전 Arm 상태를 폐기하여 지난 행동 이벤트를 거부한다.
    /// </summary>
    private void Update()
    {
        if (CurrentId != observedId) { observedId = CurrentId; armed = false; }
    }
    // Clip: place ArmAction near normalized time 0, then ActionHit/ActionCompleted at its real timing.
    /// <summary>
    /// 모션 초반의 유효한 이벤트로 현재 요청의 후속 타격·완료 수신을 준비한다.
    /// </summary>
    public void ArmAction(AnimationEvent evt)
    {
        if (combatOwner != null)
        {
            // 빠른 재생·긴 프레임에서는 평가 시간이 이미 진행됐을 수 있으므로, 실제 Arm 이벤트의 Clip 시점을 검사한다.
            if (MatchesCombatEvent(evt) && evt.time <= evt.animatorClipInfo.clip.length * 0.15f)
            {
                combatArmed = true;
                combatArmedFrame = Time.frameCount;
            }
            return;
        }
        if (!MatchesCurrentState(evt) || evt.animatorStateInfo.normalizedTime > 0.15f) return;
        armedId = CurrentId;
        observedId = armedId;
        armedFrame = Time.frameCount;
        armed = true;
    }
    /// <summary>
    /// 기존 행동 이벤트가 현재 Animator 상태와 시점에서 발생했는지 검사한다.
    /// </summary>
    private bool MatchesCurrentState(AnimationEvent evt)
    {
        if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null ||
            string.IsNullOrWhiteSpace(actionState) || layer < 0 || layer >= animator.layerCount) return false;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(layer);
        return current.IsName(actionState) && current.fullPathHash == evt.animatorStateInfo.fullPathHash &&
            Mathf.Abs(current.normalizedTime - evt.animatorStateInfo.normalizedTime) < 0.15f;
    }
    /// <summary>
    /// 요청과 이벤트를 검증하여 중복되지 않은 타격 알림을 전투 담당자에게 전달한다.
    /// </summary>
    public void ActionHit(AnimationEvent evt)
    {
        if (combatOwner != null)
        {
            // 네 타격은 같은 요청을 공유하지만 이벤트의 1~4번 타격 번호로 각각 검증한다.
            if (combatOwner.IsMmaComboPunch)
            {
                if (combatArmed && Time.frameCount >= combatArmedFrame && !combatCompleteConsumed && MatchesCombatEvent(evt))
                    combatOwner.ConsumeComboHit(combatId, evt.intParameter);
                return;
            }
            if (combatArmed && Time.frameCount >= combatArmedFrame && !combatHitConsumed && !combatCompleteConsumed && MatchesCombatEvent(evt))
            {
                combatHitConsumed = true;
                combatOwner.ConsumeHit(combatId);
            }
            return;
        }
        if (!armed || armedId != CurrentId || Time.frameCount <= armedFrame || !MatchesCurrentState(evt)) return;
        armed = false;
        if (melee != null) melee.OnAttackHitFrame(armedId);
    }
    /// <summary>
    /// 요청과 이벤트를 검증하여 전투·아이템·문 동작의 완료를 전달한다.
    /// </summary>
    public void ActionCompleted(AnimationEvent evt)
    {
        if (combatOwner != null)
        {
            if (combatArmed && Time.frameCount >= combatArmedFrame && !combatCompleteConsumed && MatchesCombatEvent(evt))
            {
                combatCompleteConsumed = true;
                combatOwner.ConsumeComplete(combatId);
            }
            return;
        }
        if (!armed || armedId != CurrentId || Time.frameCount <= armedFrame || !MatchesCurrentState(evt)) return;
        armed = false;
        if (itemUse != null) itemUse.CompleteUseFromAnimation(armedId);
        else if (door != null) door.CompleteOpenFromAnimation(armedId);
    }
    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        armed = false;
        observedId = -1;
        if (combatOwner != null) combatOwner.CancelAction();
        combatOwner = null;
        combatBinding = null;
    }
}
