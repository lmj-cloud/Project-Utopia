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
    [Header("MMA 단일 입력 버퍼")]
    [SerializeField, Range(0f, 1f)] private float comboBufferStart = 0.45f;
    [SerializeField, Range(0f, 1f)] private float comboBufferEnd = 0.95f;
    [SerializeField, Min(0f)] private float comboResetSeconds = 0.8f;
    [SerializeField, Range(0f, 0.1f)] private float comboTransitionSeconds = 0.08f;
    [SerializeField] private bool useRecoveryCombo;
    [SerializeField, Range(0.05f, 1f)] private float comboInputBufferSeconds = 0.25f;
    [SerializeField, Range(0.4f, 0.95f)] private float comboLinkNormalizedTime = 0.62f;
    [SerializeField, Range(0f, 0.6f)] private float comboRecoverySeconds = 0.12f;
    private float bufferedMmaUntil;
    private bool hasBufferedMmaAttack;
    [Header("MMA 3단계 콤보")]
    [SerializeField] private bool useThreeStageCombo;
    [SerializeField, Min(0f)] private float comboFinisherRecoverySeconds = 0.6f;
    [SerializeField, Range(0f, 1f)] private float comboPunchDamageMultiplier = 0.5f;
    private int nextMmaStage;
    private int comboStrikeMask;
    private int lastComboStrikeFrame = -1;
    private float mmaRecoveryUntil;
    public bool IsMmaComboPunch { get { return action == CombatActionKind.MmaComboPunch; } }
    public bool IsMmaRecovering { get { return Time.time < mmaRecoveryUntil; } }
    private float lastMmaCompletedAt = float.NegativeInfinity;
    private bool mmaAttackHeld;
    private bool requireMmaRelease;
    private bool hasPendingMmaStart;
    private bool pendingMmaBlend;
    private float pendingMmaUntil;
    private Vector3 mmaAttackFacing;
    public bool HasMmaAttackFacing
    {
        get { return mmaAttackFacing.sqrMagnitude > 0.001f && (hasPendingMmaStart || (isRunning && IsMmaPunch())); }
    }
    public Vector3 MmaAttackFacingDirection { get { return mmaAttackFacing; } }
    public bool HasBufferedMmaAttack { get { return hasBufferedMmaAttack; } }

    /// <summary>입력 잠금으로 Update가 조기 종료되어도 버튼 해제를 확인한다.</summary>
    public void SetMmaAttackHeld(bool held)
    {
        mmaAttackHeld = held;
        if (!held) requireMmaRelease = false;
    }

    /// <summary>누르기 반복도 기존 한 칸 버퍼를 사용한다. 회복 연결 시점 전에 다음 공격을 미리 쌓지 않는다.</summary>
    public void TryMmaHeldAttack()
    {
        if (!mmaAttackHeld || requireMmaRelease || hasPendingMmaStart) return;
        if (isRunning)
        {
            float progress;
            if (!IsMmaPunch() || !hasConsumedHit || !TryGetMmaProgress(out progress) ||
                progress < comboLinkNormalizedTime) return;
        }
        // 짧은 회복 쿨다운은 누른 채 기다릴 수 있다. 준비 전에는 새 예약 번호도 발급하지 않는다.
        if (!isRunning && (melee == null || !melee.CanBeginExternalAction)) return;
        bool accepted = TryMmaComboAttack();
        // 스태미나 부족 등으로 시작하지 못하면 매 프레임 새 요청을 만들지 않고 재입력을 기다린다.
        if (!accepted && !isRunning) requireMmaRelease = true;
    }

    /// <summary>새 좌클릭을 현재 공격 후반에 하나만 예약하거나, 준비된 다음 손 공격을 시작한다.</summary>
    public bool TryMmaComboAttack()
    {
        if (equipment == null || equipment.CurrentKind != PlayerEquipmentKind.Gloves ||
            controller == null || !controller.CanUseMma || !controller.CanUseMelee || IsMmaRecovering) return false;
        if (hasPendingMmaStart) return false;
        if (isRunning)
        {
            float progress;
            if (!IsMmaPunch() || !CanContinueAction || hasBufferedMmaAttack ||
                !TryGetMmaProgress(out progress) || progress < comboBufferStart || progress > comboBufferEnd) return false;
            hasBufferedMmaAttack = true;
            bufferedMmaUntil = Time.unscaledTime + comboInputBufferSeconds;
            return true;
        }
        if (Time.unscaledTime - lastMmaCompletedAt >= comboResetSeconds) nextMmaStage = 0;
        return TryStartMmaPunch(false);
    }

    /// <summary>취소·장비 변경·새 전투에서는 예약 입력과 손 순서를 함께 초기화한다.</summary>
    public void ResetMmaCombo()
    {
        // 피격·컷씬·전투 종료 후 계속 눌린 버튼으로 자동 재개하지 않는다.
        if (mmaAttackHeld) requireMmaRelease = true;
        hasBufferedMmaAttack = false;
        hasPendingMmaStart = false;
        mmaAttackFacing = Vector3.zero;
        bufferedMmaUntil = 0f;
        nextMmaStage = 0;
        lastMmaCompletedAt = float.NegativeInfinity;
    }

    private bool IsMmaPunch()
    {
        return target == null && startedEquipment == PlayerEquipmentKind.Gloves &&
            (action == CombatActionKind.MmaLeftPunch || action == CombatActionKind.MmaRightPunch || IsMmaComboPunch);
    }

    /// <summary>현재 요청에 연결된 State의 정규화 시간만 사용한다.</summary>
    private bool TryGetMmaProgress(out float progress)
    {
        progress = 0f;
        if (profile == null || animator == null || animator.CombatAnimator == null) return false;
        CombatAnimationProfile.Binding binding = profile.GetBinding(action);
        if (binding == null) return false;
        Animator source = animator.CombatAnimator;
        int layer = source.GetLayerIndex(binding.layerName);
        if (layer < 0) return false;
        AnimatorStateInfo state = source.GetCurrentAnimatorStateInfo(layer);
        if ((IsKnifeAction || !state.IsName(binding.statePath)) && source.IsInTransition(layer)) state = source.GetNextAnimatorStateInfo(layer);
        if (!state.IsName(binding.statePath)) return false;
        progress = state.normalizedTime;
        return true;
    }

    private bool TryStartMmaPunch(bool blend)
    {
        // 각 공격 시작의 카메라 수평 방향을 한 번 확보한다. 공격 도중 카메라 이동은 이 방향을 바꾸지 않는다.
        mmaAttackFacing = controller.MmaCameraForward;
        if (!controller.PrepareMmaAttackFacing(mmaAttackFacing))
        {
            hasPendingMmaStart = true;
            pendingMmaBlend = blend;
            pendingMmaUntil = Time.unscaledTime + comboInputBufferSeconds;
            return true;
        }
        return StartPreparedMmaPunch(blend);
    }

    private bool StartPreparedMmaPunch(bool blend)
    {
        float transition = 0f;
        if (blend) transition = comboTransitionSeconds;
        if (useThreeStageCombo && nextMmaStage == 2)
        {
            bool started = BeginAction(CombatActionKind.MmaComboPunch, null, false, transition);
            if (started) nextMmaStage = 0;
            return started;
        }
        return TryDirectionalAttack(nextMmaStage == 1, transition);
    }

    [Header("단검 클릭 3타 콤보")]
    [SerializeField, Range(0f, 1f)] private float knifeBufferStart = .25f;
    [SerializeField, Range(0f, 1f)] private float knifeBufferEnd = .9f;
    [SerializeField, Min(0f)] private float knifeResetSeconds = .8f;
    [SerializeField, Range(0f, .2f)] private float knifeTransitionSeconds = .05f;
    private int knifeStartedFrame;
    private int nextKnifeStage;
    private bool hasBufferedKnifeAttack;
    private float lastKnifeCompletedAt = float.NegativeInfinity;
    public bool IsKnifeAction
    {
        get { return target == null && startedEquipment == PlayerEquipmentKind.Dagger &&
            (action == CombatActionKind.KnifeSlashRight || action == CombatActionKind.KnifeSlashLeft ||
             action == CombatActionKind.KnifeDoubleStep); }
    }
    public int KnifeStage { get { return IsKnifeAction && isRunning ? (action == CombatActionKind.KnifeSlashRight ? 1 : action == CombatActionKind.KnifeSlashLeft ? 2 : 3) : 0; } }

    /// <summary>클릭 한 번에 다음 단계 하나만 예약한다. MMA의 유지 입력 버퍼와 공유하지 않는다.</summary>
    public bool TryKnifeComboAttack()
    {
        if (controller == null || !controller.IsKnifeCombatEnabled || !controller.CanUseMelee ||
            equipment == null || equipment.CurrentKind != PlayerEquipmentKind.Dagger) return false;
        if (isRunning)
        {
            float progress;
            if (!IsKnifeAction || action == CombatActionKind.KnifeDoubleStep || hasBufferedKnifeAttack ||
                !CanContinueAction || !TryGetMmaProgress(out progress) || progress < knifeBufferStart || progress > knifeBufferEnd) return false;
            hasBufferedKnifeAttack = true;
            return true;
        }
        if (Time.unscaledTime - lastKnifeCompletedAt > knifeResetSeconds) nextKnifeStage = 0;
        return StartKnifeAttack();
    }

    private bool StartKnifeAttack()
    {
        CombatActionKind requested = CombatActionKind.KnifeSlashRight;
        if (nextKnifeStage == 1) requested = CombatActionKind.KnifeSlashLeft;
        else if (nextKnifeStage == 2) requested = CombatActionKind.KnifeDoubleStep;
        if (!BeginAction(requested, null, false, knifeTransitionSeconds)) return false;
        knifeStartedFrame = Time.frameCount;
        nextKnifeStage = (nextKnifeStage + 1) % 3;
        return true;
    }

    private void ResetKnifeCombo()
    {
        nextKnifeStage = 0;
        hasBufferedKnifeAttack = false;
        lastKnifeCompletedAt = float.NegativeInfinity;
    }

    /// <summary>실제 UMotion 손 궤적에서 확인한 프레임을 현재 요청의 Animator 시간으로 검사한다.</summary>
    private void UpdateKnifeActionTiming()
    {
        if (!isRunning || !IsKnifeAction || !CanContinueAction || Time.frameCount <= knifeStartedFrame) return;
        float progress;
        if (!TryGetMmaProgress(out progress)) return;
        CombatAnimationProfile.Binding binding = profile.GetBinding(action);
        if (binding == null || binding.normalizedHitTimes == null || binding.normalizedHitTimes.Length == 0) return;
        for (int i = 0; i < binding.normalizedHitTimes.Length; i++)
        {
            if (progress >= binding.normalizedHitTimes[i]) ConsumeKnifeHit(requestId, i + 1);
        }
        int allHits = (1 << binding.normalizedHitTimes.Length) - 1;
        if (progress >= 1f && comboStrikeMask == allHits) ConsumeComplete(requestId);
    }

    public bool ConsumeKnifeHit(int id, int strikeIndex)
    {
        int count = action == CombatActionKind.KnifeDoubleStep ? 2 : 1;
        if (!IsKnifeAction || !CanContinueAction || id != requestId || hasConsumedComplete ||
            strikeIndex < 1 || strikeIndex > count || lastComboStrikeFrame == Time.frameCount) return false;
        int bit = 1 << (strikeIndex - 1);
        if ((comboStrikeMask & bit) != 0) return false;
        comboStrikeMask |= bit;
        lastComboStrikeFrame = Time.frameCount;
        hasConsumedHit = true;
        // 매 타격의 대상 중복 제거·거리·각도·장애물·피해 처리는 기존 근접 경로가 담당한다.
        melee.ApplyExternalHit(this, id, true);
        return true;
    }

    private PlayerStatus status;
    public bool UsesDirectionalInput { get { return isActiveAndEnabled && useDirectionalInput; } }
    public bool IsRunning { get { return isRunning; } }
    public bool IsRightMmaPunch { get { return action == CombatActionKind.MmaRightPunch; } }
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
        if (!isRunning && controller != null && equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Gloves)
        {
            mmaAttackFacing = controller.MmaCameraForward;
            controller.PrepareMmaAttackFacing(mmaAttackFacing);
        }
        return TryDirectionalAttack(rightHand, 0f);
    }

    private bool TryDirectionalAttack(bool rightHand, float transitionSeconds)
    {
        if (equipment == null) return Fail("Equipment 연결이 없습니다.");
        CombatActionKind requested;
        if (equipment.CurrentKind == PlayerEquipmentKind.Gloves)
            requested = rightHand ? CombatActionKind.MmaRightPunch : CombatActionKind.MmaLeftPunch;
        else if (equipment.CurrentKind == PlayerEquipmentKind.Dagger)
            requested = rightHand ? CombatActionKind.KnifeRightThrust : CombatActionKind.KnifeLeftThrust;
        else return Fail("좌우 공격은 Gloves 또는 Dagger 전용입니다.");
        bool started = BeginAction(requested, null, false, transitionSeconds);
        // 명시적 좌우 공격 API에서도 실제 시작된 손을 기준으로 다음 순서를 맞춘다.
        if (started && equipment.CurrentKind == PlayerEquipmentKind.Gloves)
        {
            nextMmaStage = rightHand ? 0 : 1;
            if (rightHand && useThreeStageCombo) nextMmaStage = 2;
        }
        return started;
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
    private bool BeginAction(CombatActionKind requested, EnemyExecutionTarget candidate, bool assassination, float transitionSeconds = 0f)
    {
        if (equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Gloves && IsMmaRecovering)
            return Fail("Combo Punch 회복 중입니다.");
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
        comboStrikeMask = 0;
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
        if (!animator.TryPlayCombatAction(profile, action, OwnsControlLock ? this : null, transitionSeconds) ||
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
        if (IsMmaComboPunch || IsKnifeAction) return false;
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

    /// <summary>Combo Punch에만 요청 번호·개별 타격 번호·프레임 중복 검사를 적용한다.</summary>
    public bool ConsumeComboHit(int id, int strikeIndex)
    {
        if (!IsMmaComboPunch || !CanContinueAction || id != requestId || hasConsumedComplete ||
            strikeIndex < 1 || strikeIndex > 4 || lastComboStrikeFrame == Time.frameCount) return false;
        int bit = 1 << (strikeIndex - 1);
        if ((comboStrikeMask & bit) != 0) return false;
        comboStrikeMask |= bit;
        lastComboStrikeFrame = Time.frameCount;
        hasConsumedHit = true;
        if (strikeIndex > 1) melee.PlayMmaSwing(strikeIndex % 2 == 0);
        melee.ApplyExternalHit(this, id, false, comboPunchDamageMultiplier);
        return true;
    }

    /// <summary>
    /// 현재 요청의 완료 알림을 한 번만 소비하여 행동을 종료한다.
    /// </summary>
    public bool ConsumeComplete(int id)
    {
        if (!CanContinueAction || id != requestId || hasConsumedComplete) return false;
        hasConsumedComplete = true;
        if (IsKnifeAction)
        {
            bool chainKnife = action != CombatActionKind.KnifeDoubleStep && hasBufferedKnifeAttack;
            hasBufferedKnifeAttack = false;
            if (action == CombatActionKind.KnifeDoubleStep) nextKnifeStage = 0;
            lastKnifeCompletedAt = Time.unscaledTime;
            EndAction(!chainKnife, chainKnife);
            if (chainKnife && !StartKnifeAttack() && animator != null) animator.EndCombatAction();
            return true;
        }
        if (IsMmaComboPunch)
        {
            // 마지막 동작은 회복 구간을 생략하지 않는다. 예약은 버리고 다음 순환은 Jab부터 시작한다.
            nextMmaStage = 0;
            hasBufferedMmaAttack = false;
            lastMmaCompletedAt = Time.unscaledTime;
            mmaRecoveryUntil = Time.time + comboFinisherRecoverySeconds;
            EndAction(true, false, true, comboFinisherRecoverySeconds);
            return true;
        }
        bool mma = IsMmaPunch();
        bool chain = mma && hasBufferedMmaAttack && hasConsumedHit && controller != null && controller.CanUseMma && controller.CanUseMelee &&
            (!useRecoveryCombo || Time.unscaledTime <= bufferedMmaUntil);
        hasBufferedMmaAttack = false;
        if (mma) lastMmaCompletedAt = Time.unscaledTime;
        EndAction(!chain, chain, mma && useRecoveryCombo);
        // 이전 Relay와 예약을 먼저 종료한다. 다음 공격은 반드시 새 요청 번호와 스태미나 검사를 받는다.
        if (chain && !TryStartMmaPunch(true) && animator != null) animator.EndCombatAction();
        return true;
    }

    /// <summary>
    /// 진행 중 행동을 취소하고 자신의 예약과 잠금을 정리한다.
    /// </summary>
    public void CancelAction()
    {
        ResetKnifeCombo();
        ResetMmaCombo();
        if (!isRunning) return;
        EndAction();
    }

    /// <summary>
    /// 피격 시 행동을 정리하되 이동 자세 복구가 피격 반응을 덮어쓰지 않게 한다.
    /// </summary>
    private void CancelFromDamage()
    {
        ResetKnifeCombo();
        ResetMmaCombo();
        if (!isRunning) return;
        EndAction(false);
        if (animator != null) animator.OnHit();
    }

    /// <summary>
    /// 현재 요청의 Relay, 대상 예약과 자기 소유 잠금을 해제하고 필요한 자세 복구를 요청한다.
    /// </summary>
    private void EndAction(bool restoreAnimation = true, bool chainMma = false, bool completedMma = false, float recoveryOverride = -1f)
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
        if (melee != null)
        {
            if (chainMma) melee.ReleaseExternalActionForCombo(this, requestId);
            else if (completedMma) melee.ReleaseExternalMmaAction(this, requestId,
                recoveryOverride >= 0f ? recoveryOverride : comboRecoverySeconds);
            else melee.ReleaseExternalAction(this, requestId);
        }
        if (restoreAnimation && animator != null) animator.EndCombatAction();
    }

    /// <summary>
    /// 현재 요청의 소유·대상·장비 조건 또는 실제 시간 제한이 깨지면 행동을 취소한다.
    /// </summary>
    private void Update()
    {
        if (hasPendingMmaStart)
        {
            if (!UsesDirectionalInput || controller == null || !controller.CanUseMma || !controller.CanUseMelee ||
                equipment == null || equipment.CurrentKind != PlayerEquipmentKind.Gloves || IsMmaRecovering ||
                Time.unscaledTime > pendingMmaUntil)
            {
                ResetMmaCombo();
                if (animator != null) animator.EndCombatAction();
            }
            else if (controller.PrepareMmaAttackFacing(mmaAttackFacing))
            {
                hasPendingMmaStart = false;
                if (!StartPreparedMmaPunch(pendingMmaBlend) && animator != null) animator.EndCombatAction();
            }
        }
        if (isRunning && (!CanContinueAction || Time.unscaledTime >= deadline)) CancelAction();
        UpdateKnifeActionTiming();
        if (!isRunning && (equipment == null || equipment.CurrentKind != PlayerEquipmentKind.Dagger ||
            controller == null || !controller.CanUseMelee)) ResetKnifeCombo();
        if (useRecoveryCombo && isRunning && IsMmaPunch() && !IsMmaComboPunch && hasBufferedMmaAttack)
        {
            if (Time.unscaledTime > bufferedMmaUntil) hasBufferedMmaAttack = false;
            else
            {
                float progress;
                // 실제 타격을 소비한 뒤 회복 구간만 줄인다. 기존 완료 경로가 Relay와 요청을 먼저 정리한다.
                if (hasConsumedHit && CanContinueAction && TryGetMmaProgress(out progress) && progress >= comboLinkNormalizedTime)
                    ConsumeComplete(requestId);
            }
        }
        if (!isRunning && (equipment == null || equipment.CurrentKind != PlayerEquipmentKind.Gloves ||
            controller == null || !controller.CanUseMma)) ResetMmaCombo();
        else if (!isRunning && !hasPendingMmaStart && Time.unscaledTime - lastMmaCompletedAt >= comboResetSeconds)
            nextMmaStage = 0;
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
