using UnityEngine;

/// <summary>
/// 암살·마무리 대상의 조건과 예약을 관리하고 피격자 모션, 피해 및 예약 해제를 처리한다.
/// </summary>
[RequireComponent(typeof(EnemyHealth))]
public class EnemyExecutionTarget : MonoBehaviour
{
    [SerializeField] private EnemyHealth health;
    [SerializeField] private SparringOpponent opponent;
    [SerializeField] private Animator victimAnimator;
    [SerializeField] private CombatAnimationProfile victimProfile;
    // 현재 대상이 일반 암살을 허용하는지 결정하는 기획 설정.
    [Header("기획 확정 및 실제 Clip 연결 후 활성화")]
    [SerializeField] private bool canBeAssassinated;
    // 체력 조건을 만족했을 때 일반 마무리를 허용하는지 결정한다.
    [SerializeField] private bool canBeFinished;
    // 마무리를 허용하는 최대 체력 대비 현재 체력 비율. 스토리 패배 조건과 별개다.
    [SerializeField, Range(0f, 1f)] private float finisherHealthRatio;
    // 예약이 유효한 타격 시점에 적용할 몸통 기준 피해. 임의 즉사를 보장하지 않는다.
    [SerializeField, Min(0f)] private float executionDamage;
    private PlayerCombatActions reservationOwner;
    private int reservationId;
    // 대상 예약 시의 생명주기 번호. 체력 초기화 후 이전 예약이 재사용되는 것을 막는다.
    private int reservedLife;
    private bool reservedAssassination;
    private int previousState;
    private int previousLayer;
    public EnemyHealth Health { get { return health; } }

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        if (opponent == null) opponent = GetComponent<SparringOpponent>();
    }

    /// <summary>
    /// 살아 있는 미예약 대상인지 확인하고 암살 허용 또는 마무리 체력 조건을 검사한다.
    /// </summary>
    public bool IsEligible(bool assassination)
    {
        if (!isActiveAndEnabled || health == null || health.IsDead || reservationOwner != null || executionDamage <= 0f ||
            float.IsNaN(executionDamage) || float.IsInfinity(executionDamage)) return false;
        if (assassination) return canBeAssassinated;
        return canBeFinished && finisherHealthRatio > 0f && health.CurrentHealth / health.MaxHealth <= finisherHealthRatio;
    }

    /// <summary>
    /// 피격자 Animator와 프로필에 해당 행동의 실제 모션이 준비되었는지 검사한다.
    /// </summary>
    public bool IsAnimationReady(CombatActionKind action)
    {
        string reason;
        return victimProfile != null && victimProfile.CanPlay(victimAnimator, action, out reason);
    }

    /// <summary>
    /// 다른 실행자가 없는지 확인한 뒤 소유자·요청 번호·대상 생명주기를 예약한다.
    /// </summary>
    public bool TryReserve(PlayerCombatActions owner, int id, bool assassination)
    {
        if (owner == null || !IsEligible(assassination)) return false;
        if (opponent != null && !opponent.PauseForExecution(owner, id)) return false;
        reservationOwner = owner;
        reservationId = id;
        reservedLife = health.LifeVersion;
        reservedAssassination = assassination;
        previousLayer = -1;
        return true;
    }

    /// <summary>
    /// 현재 대상의 예약 소유자, 요청 번호와 생명주기가 모두 같은지 확인한다.
    /// </summary>
    public bool OwnsReservation(PlayerCombatActions owner, int id)
    {
        return reservationOwner == owner && owner != null && reservationId == id && isActiveAndEnabled &&
            health != null && health.LifeVersion == reservedLife;
    }

    /// <summary>
    /// 유효한 예약일 때 기존 자세를 보관하고 피격자의 독립 행동 모션을 요청한다.
    /// </summary>
    public bool PlayVictim(PlayerCombatActions owner, int id, CombatActionKind action)
    {
        if (!OwnsReservation(owner, id) || !IsAnimationReady(action)) return false;
        CombatAnimationProfile.Binding binding = victimProfile.GetBinding(action);
        previousLayer = victimAnimator.GetLayerIndex(binding.layerName);
        previousState = victimAnimator.GetCurrentAnimatorStateInfo(previousLayer).fullPathHash;
        victimAnimator.Play(Animator.StringToHash(binding.statePath), previousLayer, 0f);
        return true;
    }

    /// <summary>
    /// 현재 예약과 허용 상태를 다시 검사한 뒤 설정된 몸통 피해를 적용한다.
    /// </summary>
    public bool ApplyExecutionHit(PlayerCombatActions owner, int id)
    {
        if (!OwnsReservation(owner, id) || health.IsDead) return false;
        if (reservedAssassination && !canBeAssassinated) return false;
        if (!reservedAssassination && (!canBeFinished || finisherHealthRatio <= 0f || health.CurrentHealth / health.MaxHealth > finisherHealthRatio)) return false;
        return health.TryTakeDamage(executionDamage, EnemyHitPart.Body);
    }

    /// <summary>
    /// 같은 예약 소유자와 요청 번호일 때 대상 자세와 AI 예약을 해제한다.
    /// </summary>
    public void Release(PlayerCombatActions owner, int id)
    {
        if (reservationOwner != owner || reservationId != id) return;
        // 사망했거나 체력을 초기화한 대상에게 이전 생존 자세를 다시 적용하지 않는다.
        if (health != null && !health.IsDead && health.LifeVersion == reservedLife && victimAnimator != null &&
            victimAnimator.isActiveAndEnabled && victimAnimator.runtimeAnimatorController != null && previousLayer >= 0 &&
            previousLayer < victimAnimator.layerCount && victimAnimator.HasState(previousLayer, previousState))
            victimAnimator.Play(previousState, previousLayer, 0f);
        if (opponent != null) opponent.ReleaseExecution(owner, id);
        reservationOwner = null;
        previousLayer = -1;
    }

    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (reservationOwner != null) reservationOwner.CancelAction();
    }
}
