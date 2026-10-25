using UnityEngine;

/// <summary>
/// 실제 모션이 준비된 좌우 공격과 암살·마무리를 요청하고 대상 예약, 이벤트 소비, 취소와 잠금 정리를 담당한다.
/// </summary>
[RequireComponent(typeof(PlayerMeleeCombat))]
public class PlayerCombatActions : MonoBehaviour
{
    // 이 컴포넌트가 있을 때 좌우 손 구분 입력을 사용할지 선택한다.
    [SerializeField] private bool useDirectionalInput = true;
    [SerializeField] private CombatAnimationProfile profile;
    [SerializeField] private PlayerController controller;
    [SerializeField] private PlayerMeleeCombat melee;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerAnimatorController animator;
    [SerializeField] private ActionAnimationEventRelay relay;
    // 암살·마무리의 거리와 장애물 검사를 시작하는 기준점.
    [SerializeField] private Transform executionOrigin;
    [SerializeField, Min(0.1f)] private float executionRange = 1.2f;
    [SerializeField, Range(0f, 180f)] private float executionFacingAngle = 80f;
    [SerializeField, Range(0f, 180f)] private float behindAngle = 60f;
    [SerializeField] private LayerMask executionLayers = ~0;
    // 공격과 상호작용 사이의 장애물 판정에 포함할 Layer.
    [SerializeField] private LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
    private EnemyExecutionTarget target;
    private CombatActionKind action;
    private PlayerEquipmentKind startedEquipment;
    // 비동기 타격·완료 콜백이 현재 행동에 속하는지 구분하는 요청 번호.
    private int requestId = -1;
    private bool isRunning;
    // 현재 전투 요청의 타격 이벤트가 이미 소비되었는지 기록한다.
    private bool hasConsumedHit;
    // 타격과 별도로 현재 요청의 완료 이벤트 소비 여부를 기록한다.
    private bool hasConsumedComplete;
    // 이벤트 누락으로 행동이 영구 잠금되지 않도록 정한 실제 시간 기준 종료 제한.
    private float deadline;
    private PlayerStatus status;
    public bool UsesDirectionalInput { get { return isActiveAndEnabled && useDirectionalInput; } }
    public bool IsRunning { get { return isRunning; } }
    public int RequestId { get { return requestId; } }
    public bool OwnsControlLock { get; private set; }
    public string LastFailure { get; private set; }
    public bool CanContinueAction
    {
        get
        {
            return isRunning && isActiveAndEnabled && controller != null && controller.CanContinueOwnedCombat(this) &&
                melee != null && melee.RequestId == requestId && equipment != null && equipment.CurrentKind == startedEquipment &&
                (target == null ? !OwnsControlLock : target.OwnsReservation(this, requestId) && target.Health != null &&
                    (hasConsumedHit || !target.Health.IsDead));
        }
    }

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (controller == null) controller = GetComponent<PlayerController>();
        if (melee == null) melee = GetComponent<PlayerMeleeCombat>();
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        if (animator == null) animator = GetComponent<PlayerAnimatorController>();
    }

    /// <summary>
    /// 컴포넌트 활성화 시 필요한 알림과 초기 표시를 연결한다.
    /// </summary>
    private void OnEnable()
    {
        status = GetComponent<PlayerStatus>();
        if (status == null) return;
        status.Damaged += CancelFromDamage;
        status.Died += CancelAction;
    }

    /// <summary>
    /// 현재 장비에 맞는 좌우 펀치 또는 찌르기 행동을 선택한다.
    /// </summary>
    public bool TryDirectionalAttack(bool rightHand)
    {
        if (equipment == null) return Fail("Equipment 연결이 없습니다.");
        CombatActionKind requested;
        if (equipment.CurrentKind == PlayerEquipmentKind.Gloves)
            requested = rightHand ? CombatActionKind.MmaRightPunch : CombatActionKind.MmaLeftPunch;
        else if (equipment.CurrentKind == PlayerEquipmentKind.Dagger)
            requested = rightHand ? CombatActionKind.KnifeRightThrust : CombatActionKind.KnifeLeftThrust;
        else return Fail("좌우 공격은 Gloves 또는 Dagger 전용입니다.");
        return BeginAction(requested, null, false);
    }

    /// <summary>
    /// 거리·각도·장애물과 대상 상태를 검사하여 암살 또는 마무리를 요청한다.
    /// </summary>
    public bool TryExecution(bool assassination)
    {
        if (equipment == null || executionOrigin == null) return Fail("처형 기준점 또는 장비가 없습니다.");
        bool dagger = equipment.CurrentKind == PlayerEquipmentKind.Dagger;
        if (!dagger && equipment.CurrentKind != PlayerEquipmentKind.Gloves) return Fail("근접 장비가 필요합니다.");
        CombatActionKind requested;
        if (assassination) requested = dagger ? CombatActionKind.KnifeAssassinate : CombatActionKind.MmaAssassinate;
        else requested = dagger ? CombatActionKind.KnifeFinish : CombatActionKind.MmaFinish;
        EnemyExecutionTarget nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (Collider collider in Physics.OverlapSphere(executionOrigin.position, executionRange, executionLayers, QueryTriggerInteraction.Collide))
        {
            EnemyExecutionTarget candidate = collider.GetComponentInParent<EnemyExecutionTarget>();
            if (candidate == null || !candidate.IsEligible(assassination) || !HasGeometry(candidate, assassination)) continue;
            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance < nearestDistance) { nearestDistance = distance; nearest = candidate; }
        }
        if (nearest == null) return Fail("거리·방향·상태 조건을 만족하는 대상이 없습니다.");
        return BeginAction(requested, nearest, assassination);
    }

    /// <summary>
    /// 모션과 행동 조건을 검사한 뒤 토큰·대상을 예약하고 필요한 잠금과 재생 요청을 시작한다.
    /// </summary>
    private bool BeginAction(CombatActionKind requested, EnemyExecutionTarget candidate, bool assassination)
    {
        if (isRunning || !UsesDirectionalInput || controller == null || !controller.CanUseMelee || melee == null ||
            !melee.CanBeginExternalAction || animator == null || relay == null || profile == null) return Fail("행동 준비 조건을 만족하지 않습니다.");
        string reason;
        if (!profile.CanPlay(animator.CombatAnimator, requested, out reason)) return Fail(reason);
        if (!animator.HasCombatLocomotion()) return Fail("행동 후 복귀할 장비 이동 Motion이 없습니다.");
        if (candidate != null && !candidate.IsAnimationReady(requested)) return Fail("대상 실제 Motion이 준비되지 않았습니다.");
        requestId = melee.ReserveExternalAction(this);
        if (requestId < 0) return Fail("다른 근접 행동이 진행 중입니다.");
        if (candidate != null && !candidate.TryReserve(this, requestId, assassination))
        {
            melee.ReleaseExternalAction(this, requestId);
            return Fail("다른 실행자가 대상을 예약했습니다.");
        }
        if (!melee.TrySpendExternalStamina(this, requestId))
        {
            if (candidate != null) candidate.Release(this, requestId);
            melee.ReleaseExternalAction(this, requestId);
            return Fail("행동에 필요한 스태미나가 부족합니다.");
        }
        target = candidate;
        action = requested;
        startedEquipment = equipment.CurrentKind;
        hasConsumedHit = false;
        hasConsumedComplete = false;
        isRunning = true;
        OwnsControlLock = target != null;
        CombatAnimationProfile.Binding binding = profile.GetBinding(action);
        deadline = Time.unscaledTime + Mathf.Max(0.1f, binding.timeoutSeconds);
        if (OwnsControlLock)
        {
            controller.AcquireControlLock(this);
            if (!isRunning || !CanContinueAction)
            {
                CancelAction();
                return Fail("잠금 획득 중 다른 행동이 시작되어 취소했습니다.");
            }
            controller.AcquireMovementSimulationLock(this);
        }
        relay.BeginCombatRequest(this, requestId, binding, animator.CombatAnimator);
        if (!animator.TryPlayCombatAction(profile, action, OwnsControlLock ? this : null) ||
            (target != null && !target.PlayVictim(this, requestId, action)))
        {
            CancelAction();
            return Fail("행동 재생 준비에 실패했습니다.");
        }
        LastFailure = "";
        if (target == null && startedEquipment == PlayerEquipmentKind.Gloves)
            melee.NotifyMmaAttackStarted(this, requestId);
        return true;
    }

    /// <summary>
    /// 대상의 거리와 정면·후방 각도, 사이 장애물을 확인한다.
    /// </summary>
    private bool HasGeometry(EnemyExecutionTarget candidate, bool assassination)
    {
        if (candidate == null || executionOrigin == null) return false;
        Collider collider = candidate.GetComponentInChildren<Collider>();
        if (collider == null || Vector3.Distance(executionOrigin.position, collider.ClosestPoint(executionOrigin.position)) > executionRange) return false;
        Vector3 direction = candidate.transform.position - transform.position;
        if (Vector3.Angle(transform.forward, direction) > executionFacingAngle * 0.5f) return false;
        if (assassination && Vector3.Angle(-candidate.transform.forward, -direction) > behindAngle * 0.5f) return false;
        Vector3 delta = collider.bounds.center - executionOrigin.position;
        foreach (RaycastHit hit in Physics.RaycastAll(executionOrigin.position, delta.normalized, delta.magnitude, obstructionLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(candidate.transform)) return false;
        }
        return true;
    }

    /// <summary>
    /// 현재 요청의 타격 알림을 한 번만 소비하여 일반 공격 또는 예약 대상 피해를 처리한다.
    /// </summary>
    public bool ConsumeHit(int id)
    {
        if (!CanContinueAction || id != requestId || hasConsumedHit || hasConsumedComplete) return false;
        hasConsumedHit = true;
        if (OwnsControlLock)
        {
            bool assassination = action == CombatActionKind.MmaAssassinate || action == CombatActionKind.KnifeAssassinate;
            if (target == null || !HasGeometry(target, assassination) || !target.ApplyExecutionHit(this, id))
            {
                CancelAction();
                return false;
            }
        }
        else melee.ApplyExternalHit(this, id, startedEquipment == PlayerEquipmentKind.Dagger);
        return true;
    }

    /// <summary>
    /// 현재 요청의 완료 알림을 한 번만 소비하여 행동을 종료한다.
    /// </summary>
    public bool ConsumeComplete(int id)
    {
        if (!CanContinueAction || id != requestId || hasConsumedComplete) return false;
        hasConsumedComplete = true;
        EndAction();
        return true;
    }

    /// <summary>
    /// 진행 중 행동을 취소하고 자신의 예약과 잠금을 정리한다.
    /// </summary>
    public void CancelAction()
    {
        if (!isRunning) return;
        EndAction();
    }

    /// <summary>
    /// 피격 시 행동을 정리하되 이동 자세 복구가 피격 반응을 덮어쓰지 않게 한다.
    /// </summary>
    private void CancelFromDamage()
    {
        if (!isRunning) return;
        EndAction(false);
        if (animator != null) animator.OnHit();
    }

    /// <summary>
    /// 현재 요청의 Relay, 대상 예약과 자기 소유 잠금을 해제하고 필요한 자세 복구를 요청한다.
    /// </summary>
    private void EndAction(bool restoreAnimation = true)
    {
        isRunning = false;
        if (relay != null) relay.EndCombatRequest(this, requestId);
        if (target != null) target.Release(this, requestId);
        target = null;
        if (OwnsControlLock && controller != null)
        {
            controller.ReleaseMovementSimulationLock(this);
            controller.ReleaseControlLock(this);
        }
        OwnsControlLock = false;
        if (melee != null) melee.ReleaseExternalAction(this, requestId);
        if (restoreAnimation && animator != null) animator.EndCombatAction();
    }

    /// <summary>
    /// 현재 요청의 소유·대상·장비 조건 또는 실제 시간 제한이 깨지면 행동을 취소한다.
    /// </summary>
    private void Update()
    {
        if (isRunning && (!CanContinueAction || Time.unscaledTime >= deadline)) CancelAction();
    }

    /// <summary>
    /// 실패 이유를 기록하고 행동을 시작하지 않았음을 반환한다.
    /// </summary>
    private bool Fail(string reason)
    {
        LastFailure = reason;
        return false;
    }

    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (status != null)
        {
            status.Damaged -= CancelFromDamage;
            status.Died -= CancelAction;
        }
        CancelAction();
    }
}
