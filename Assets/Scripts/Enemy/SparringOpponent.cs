using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 기존 NavMesh 대련 상대의 추적, 좌우 펀치와 확률 방어를 관리한다.
/// 체력 계산은 EnemyHealth, 시각 표현은 EnemyAnimatorController가 담당한다.
/// </summary>
public class SparringOpponent : MonoBehaviour
{
    [Header("기존 대련 참조")]
    [SerializeField] private PlayerMeleeCombat player;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
    [SerializeField] private EnemyAnimatorController enemyAnimator;

    [Header("MMA 효과음: 기존 플레이어 효과 설정을 재사용")]
    [SerializeField] private AudioSource mmaAudioSource;

    [Header("기존 공격 설정")]
    [SerializeField, Min(0.1f)] private float attackDistance = 1.6f;
    [SerializeField, Min(0.1f)] private float attackInterval = 1.5f;
    [SerializeField, Min(0f)] private float telegraphDuration = 0.5f;
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Range(0f, 180f)] private float attackAngle = 120f;
    [SerializeField, Min(0f)] private float recoveryDuration = 0.25f;

    [Header("방어 설정: 기본 수치는 임시 시험값")]
    [SerializeField, Range(0f, 180f)] private float guardAngle = 120f;
    [SerializeField, Range(0f, 1f)] private float normalGuardChance = 0.25f;
    [SerializeField, Range(0f, 1f)] private float reactiveGuardChance = 0.4f;
    [SerializeField, Min(0f)] private float guardDuration = 1f;
    [SerializeField, Min(0f)] private float guardCooldown = 1f;

    [Header("Timer는 개발용 판정이며 실제 모션 재생을 의미하지 않음")]
    [SerializeField] private ActionTimingMode attackTiming = ActionTimingMode.Timer;
    [SerializeField, Min(0.1f)] private float animationEventTimeout = 3f;

    [Header("스토리 MMA 전용 (일반 적의 사망에는 영향 없음)")]
    [SerializeField] private bool storyMode;
    [SerializeField, Range(0.01f, 1f)] private float storyThresholdRatio = 0.25f;
    [SerializeField, Range(0.001f, 0.25f)] private float storyMinimumHealthRatio = 0.01f;
    [SerializeField] private UnityEngine.Events.UnityEvent onStoryThresholdReached = new UnityEngine.Events.UnityEvent();
    private int thresholdLifeVersion = -1;
    public bool IsStoryMode { get { return storyMode; } }
    public bool HasReachedStoryThreshold { get { return health != null && thresholdLifeVersion == health.LifeVersion; } }

    private float grabbedUntil;
    private float nextPathRequest;
    private Coroutine attack;
    private bool training;
    private bool ownsAgentRotation;
    private bool previousAgentRotation;
    private float nextAttack;
    private bool isAttacking;
    private float guardEndTime;
    private float nextGuardTime;
    private float nextGuardDecisionTime;
    private Object executionOwner;
    private int executionId;
    // 중단된 공격의 이벤트는 다음 공격에 피해를 줄 수 없다.
    private int attackRequestId;
    private bool isRightPunch;
    private bool hasConsumedHit;
    private bool hasFinishedAnimation;
    private EnemyHealth subscribedHealth;
    private int lastDamageVersion = -1;
    private bool hasWarnedStart;

    public bool IsTraining { get { return training; } }
    public bool IsAttacking { get { return isAttacking; } }
    public bool IsGuarding { get; private set; }
    public int AttackRequestId { get { return attackRequestId; } }
    public bool IsRightPunch { get { return isRightPunch; } }
    public ActionTimingMode AttackTiming { get { return attackTiming; } }
    // 이전 외부 호출의 호환용이다. 실제 방어는 방향을 검사하는 CanBlockAttack을 사용한다.
    public float GuardDamageMultiplier { get { return 0f; } }
    public event System.Action<CombatHitResult> AttackResolved;

    private bool CanAct
    {
        get
        {
            return training && isActiveAndEnabled && executionOwner == null && Time.time >= grabbedUntil &&
                health != null && !health.IsDead && player != null && playerController != null &&
                playerController.CanUseMma && agent != null && agent.enabled && agent.isOnNavMesh;
        }
    }

    /// <summary>시작 전에 기존 체력과 이동 컴포넌트 참조를 확인한다.</summary>
    private void Awake()
    {
        if (health == null) health = GetComponent<EnemyHealth>();
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (enemyAnimator == null) enemyAnimator = GetComponent<EnemyAnimatorController>();
    }

    /// <summary>실제 체력 감소 알림을 구독한다. Inspector의 기존 피격 연결도 중복 적용하지 않는다.</summary>
    private void OnEnable()
    {
        subscribedHealth = health;
        if (subscribedHealth == null) return;
        lastDamageVersion = subscribedHealth.DamageVersion;
        subscribedHealth.Damaged += OnDamaged;
    }

    /// <summary>기존 훈련을 중단하고 새 플레이어 참조를 명시적으로 연결한다.</summary>
    public void BindPlayer(PlayerMeleeCombat target, PlayerController controller)
    {
        EndTraining();
        player = target;
        playerController = controller;
    }

    /// <summary>필수 참조와 NavMesh 배치를 검사하고 대련을 시작한다.</summary>
    public void BeginTraining()
    {
        if (training) return;
        if (!isActiveAndEnabled || health == null || health.IsDead || player == null || playerController == null ||
            !playerController.CanUseMma || attackOrigin == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            if (!hasWarnedStart)
            {
                Debug.LogWarning("SparringOpponent: 플레이어·체력·공격 기준점과 NavMeshAgent 배치를 확인하세요.", this);
                hasWarnedStart = true;
            }
            return;
        }
        if (attackTiming == ActionTimingMode.AnimationEvent && (enemyAnimator == null || !enemyAnimator.IsReadyForPlayback()))
        {
            if (!hasWarnedStart)
            {
                Debug.LogWarning("SparringOpponent: AnimationEvent 모드는 실제 적 모션 연결 후 시작할 수 있습니다.", this);
                hasWarnedStart = true;
            }
            return;
        }
        if (storyMode && !health.TryProtectFromDefeat(this, health.MaxHealth * Mathf.Clamp(storyMinimumHealthRatio, 0.001f, Mathf.Clamp01(storyThresholdRatio)))) return;
        hasWarnedStart = false;
        grabbedUntil = 0f;
        training = true;
        if (enemyAnimator != null && enemyAnimator.UsesDirectionalLocomotion && !ownsAgentRotation)
        {
            previousAgentRotation = agent.updateRotation;
            agent.updateRotation = false;
            ownsAgentRotation = true;
        }
        nextGuardDecisionTime = Time.time;
        if (enemyAnimator != null) enemyAnimator.SetTraining(true);
    }

    /// <summary>훈련을 끝내고 이동·공격·방어와 남은 Animator 요청을 정리한다.</summary>
    public void EndTraining()
    {
        if (player != null) player.ClearMmaFeedback();
        if (mmaAudioSource != null) mmaAudioSource.Stop();
        training = false;
        if (ownsAgentRotation && agent != null) agent.updateRotation = previousAgentRotation;
        ownsAgentRotation = false;
        if (health != null) health.ReleaseDefeatProtection(this);
        CancelAttack();
        EndGuard();
        StopMoving();
        if (enemyAnimator != null)
        {
            enemyAnimator.SetTraining(false);
            enemyAnimator.ResetCombatAnimation();
        }
    }

    /// <summary>기존 처형 소유권을 유지하면서 일반 대련 공격·방어·이동을 중단한다.</summary>
    public bool PauseForExecution(Object owner, int id)
    {
        if (owner == null || executionOwner != null) return false;
        executionOwner = owner;
        executionId = id;
        CancelAttack();
        EndGuard();
        StopMoving();
        return true;
    }

    /// <summary>같은 소유자와 요청 번호의 처형 정지 상태만 해제한다.</summary>
    public void ReleaseExecution(Object owner, int id)
    {
        if (executionOwner != owner || executionId != id) return;
        executionOwner = null;
    }

    /// <summary>정지 조건, 방어 시간, 주기적 행동 결정 후 기존 추적·공격 순서로 갱신한다.</summary>
    private void Update()
    {
        if (training && !isAttacking && attackTiming == ActionTimingMode.AnimationEvent &&
            (enemyAnimator == null || !enemyAnimator.IsReadyForPlayback()))
        {
            // 연결이 끊긴 뒤 새 행동은 시작하지 않는다. 진행 중 요청은 자체 취소·시간 제한으로 정리한다.
            EndGuard();
            StopMoving();
            return;
        }
        if (health != null && health.IsDead)
        {
            if (training) EndTraining();
            return;
        }
        if (!CanAct)
        {
            CancelAttack();
            EndGuard();
            StopMoving();
            return;
        }
        UpdateCombatFacing();
        if (IsGuarding && Time.time >= guardEndTime) EndGuard();
        if (isAttacking || IsGuarding)
        {
            StopMoving();
            return;
        }
        // 확률 판정 횟수가 프레임 수에 비례하지 않도록 다음 결정 시점을 별도로 기록한다.
        if (Time.time >= nextGuardDecisionTime)
        {
            nextGuardDecisionTime = Time.time + Mathf.Max(0.1f, guardCooldown);
            if (CanStartGuard() && Roll(normalGuardChance)) BeginGuard();
        }
        if (IsGuarding) { StopMoving(); return; }
        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (distance > attackDistance)
        {
            agent.isStopped = false;
            if (!agent.pathPending && Time.time >= nextPathRequest)
            {
                nextPathRequest = Time.time + 0.5f;
                if (!NavMesh.SamplePosition(player.transform.position, out NavMeshHit destination, 1f, agent.areaMask) ||
                    !agent.SetDestination(destination.position)) StopMoving();
            }
            if (!agent.pathPending && agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete) StopMoving();
            if (enemyAnimator != null) enemyAnimator.SetMovementSpeed(agent.velocity.magnitude);
        }
        else
        {
            StopMoving();
            TryBeginAttack();
        }
    }

    /// <summary>방향별 복싱 이동에서만 진행 방향과 분리해 플레이어를 바라본다. 위치는 NavMesh가 처리한다.</summary>
    private void UpdateCombatFacing()
    {
        if (!ownsAgentRotation || player == null || agent == null) return;
        Vector3 direction = player.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
            agent.angularSpeed * Time.deltaTime);
    }

    /// <summary>대기·거리·방어 조건을 검사한 뒤 좌우 손을 선택하고 공격 전체 구간을 예약한다.</summary>
    public bool TryBeginAttack()
    {
        if (!CanAct || isAttacking || attack != null || IsGuarding || attackOrigin == null || Time.time < nextAttack ||
            Vector3.Distance(transform.position, player.transform.position) > attackDistance) return false;
        bool right = Random.value >= 0.5f;
        if (attackTiming == ActionTimingMode.AnimationEvent &&
            (enemyAnimator == null || !enemyAnimator.CanPlayPunch(right))) return false;
        attackRequestId++;
        int id = attackRequestId;
        isRightPunch = right;
        isAttacking = true;
        hasConsumedHit = false;
        hasFinishedAnimation = false;
        nextAttack = Time.time + attackInterval;
        StopMoving();
        if (enemyAnimator != null)
        {
            enemyAnimator.SetGuard(false);
            enemyAnimator.SetAttacking(true);
            if (attackTiming == ActionTimingMode.Timer)
            {
                enemyAnimator.BindAttackRequest(this, id, right);
                if (right) enemyAnimator.PlayPunchRight();
                else enemyAnimator.PlayPunchLeft();
            }
        }
        attack = StartCoroutine(Attack(id, right));
        return true;
    }

    /// <summary>개발용 Timer 판정과 실제 Animation Event 대기를 서로 배타적으로 실행한다.</summary>
    private IEnumerator Attack(int id, bool right)
    {
        yield return null;
        if (!OwnsAttack(id, right)) yield break;
        if (attackTiming == ActionTimingMode.Timer)
        {
            yield return new WaitForSeconds(telegraphDuration);
            if (!OwnsAttack(id, right)) yield break;
            ResolveHit(id, right);
        }
        else
        {
            // 이벤트 판정도 기존 예고 시간을 지킨 뒤 실제 펀치를 시작한다.
            yield return new WaitForSeconds(telegraphDuration);
            if (!OwnsAttack(id, right)) yield break;
            if (enemyAnimator == null || !enemyAnimator.CanPlayPunch(right))
            {
                CancelAttack();
                yield break;
            }
            enemyAnimator.BindAttackRequest(this, id, right);
            if (right) enemyAnimator.PlayPunchRight();
            else enemyAnimator.PlayPunchLeft();
            if (player != null) player.PlayMmaSwing(right, mmaAudioSource);
            float deadline = Time.unscaledTime + Mathf.Max(0.1f, animationEventTimeout);
            while (OwnsAttack(id, right) && !hasFinishedAnimation && Time.unscaledTime < deadline) yield return null;
            if (!OwnsAttack(id, right)) yield break;
            if (!hasFinishedAnimation) { CancelAttack(); yield break; }
        }
        // 회복 구간이 끝날 때까지 isAttacking을 유지하여 중간 방어 진입을 막는다.
        yield return new WaitForSeconds(recoveryDuration);
        if (!OwnsAttack(id, right)) yield break;
        attack = null;
        isAttacking = false;
        if (enemyAnimator != null)
        {
            enemyAnimator.ClearAttackRequest(id);
            enemyAnimator.SetAttacking(false);
        }
    }

    /// <summary>현재 공격 토큰·손 구분과 행동 가능 상태를 함께 확인한다.</summary>
    public bool OwnsAttack(int id, bool right)
    {
        return CanAct && isAttacking && id == attackRequestId && right == isRightPunch;
    }

    /// <summary>이벤트 모드에서만 타격 알림을 받는다. Timer 모드의 Clip 이벤트는 무시한다.</summary>
    public bool OnAttackHitFrame(int id, bool right)
    {
        if (attackTiming != ActionTimingMode.AnimationEvent) return false;
        return ResolveHit(id, right);
    }

    /// <summary>타격과 별도로 실제 모션 완료를 소비한다. 피해가 누락돼도 임의로 만들어 적용하지 않는다.</summary>
    public bool OnAttackAnimationFinished(int id, bool right)
    {
        if (attackTiming != ActionTimingMode.AnimationEvent || !OwnsAttack(id, right) || hasFinishedAnimation) return false;
        hasFinishedAnimation = true;
        return true;
    }

    /// <summary>실제 타격 시점의 거리·각도·장애물을 재검사하여 최대 한 번만 플레이어에게 공격을 전달한다.</summary>
    private bool ResolveHit(int id, bool right)
    {
        if (!OwnsAttack(id, right) || hasConsumedHit || hasFinishedAnimation || attackOrigin == null) return false;
        hasConsumedHit = true;
        if (Vector3.Distance(transform.position, player.transform.position) > attackDistance) return false;
        Vector3 delta = player.transform.position + Vector3.up - attackOrigin.position;
        if (Vector3.Angle(attackOrigin.forward, delta) > attackAngle * 0.5f) return false;
        foreach (RaycastHit hit in Physics.RaycastAll(attackOrigin.position, delta.normalized, delta.magnitude, obstructionLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(player.transform)) return false;
        }
        CombatHitResult result = player.ReceiveAttackResult(damage, transform.position);
        if (result == CombatHitResult.UnblockedHit || result == CombatHitResult.Blocked)
        {
            Collider targetCollider = player.GetComponent<Collider>();
            Vector3 point = player.transform.position + Vector3.up;
            if (targetCollider != null) point = targetCollider.ClosestPoint(attackOrigin.position);
            player.PlayMmaContact(result == CombatHitResult.Blocked, point, attackOrigin.position - point, mmaAudioSource);
        }
        AttackResolved?.Invoke(result);
        return result != CombatHitResult.Rejected;
    }

    /// <summary>실제로 시작한 플레이어 공격 중 가까운 전방 공격에만 반응 방어 확률을 적용한다.</summary>
    public void OnPlayerAttackStarted(Vector3 attackSourcePosition)
    {
        if (!CanStartGuard() || !IsSourceInFront(attackSourcePosition)) return;
        Vector3 delta = attackSourcePosition - transform.position;
        delta.y = 0f;
        if (delta.sqrMagnitude > attackDistance * attackDistance) return;
        if (Roll(reactiveGuardChance)) BeginGuard();
    }

    /// <summary>수평 기준 전방 방어 각도를 실제 공격 발생 위치와 비교한다.</summary>
    public bool CanBlockAttack(Vector3 sourcePosition)
    {
        return CanAct && IsGuarding && !isAttacking && Time.time < guardEndTime && IsSourceInFront(sourcePosition);
    }

    /// <summary>공격 높이는 제외하고 적 정면을 기준으로 전체 방어 각도의 절반씩 비교한다.</summary>
    private bool IsSourceInFront(Vector3 sourcePosition)
    {
        Vector3 direction = sourcePosition - transform.position;
        direction.y = 0f;
        Vector3 forward = transform.forward;
        forward.y = 0f;
        return direction.sqrMagnitude > 0.0001f && Vector3.Angle(forward, direction) <= guardAngle * 0.5f;
    }

    /// <summary>공격 준비·타격·회복, 잡기·처형과 쿨다운 중의 방어 진입을 거부한다.</summary>
    private bool CanStartGuard()
    {
        return CanAct && !isAttacking && attack == null && !IsGuarding && Time.time >= nextGuardTime && guardDuration > 0f;
    }

    /// <summary>0과 1을 정확한 불가능·확정 값으로 처리하고 그 사이 값만 확률 추첨한다.</summary>
    private static bool Roll(float chance)
    {
        if (float.IsNaN(chance) || chance <= 0f) return false;
        if (chance >= 1f) return true;
        return Random.value < chance;
    }

    /// <summary>자격을 다시 확인한 뒤 방어 유지 시간을 기록하고 이동을 멈춘다.</summary>
    private void BeginGuard()
    {
        if (!CanStartGuard()) return;
        IsGuarding = true;
        guardEndTime = Time.time + guardDuration;
        StopMoving();
        if (enemyAnimator != null) enemyAnimator.SetGuard(true);
    }

    /// <summary>실제 방어를 종료한 시점부터 다음 방어 쿨다운을 시작한다.</summary>
    private void EndGuard()
    {
        if (!IsGuarding) return;
        IsGuarding = false;
        nextGuardTime = Time.time + Mathf.Max(0f, guardCooldown);
        if (enemyAnimator != null) enemyAnimator.SetGuard(false);
    }

    /// <summary>실제 양수 피해당 한 번만 반응하여 공격·방어를 취소하고 회복 시간과 피격 표시를 적용한다.</summary>
    public void OnDamaged()
    {
        if (health == null || health.DamageVersion == lastDamageVersion) return;
        lastDamageVersion = health.DamageVersion;
        CancelAttack();
        EndGuard();
        nextAttack = Time.time + recoveryDuration;
        if (health.IsDead) EndTraining();
        else if (enemyAnimator != null) enemyAnimator.PlayHit();
        // 생명주기당 한 번만 알린다. 실제 컷신이나 플레이어 사망은 여기서 실행하지 않는다.
        if (storyMode && training && !health.IsDead && !HasReachedStoryThreshold &&
            health.CurrentHealth <= health.MaxHealth * Mathf.Clamp01(storyThresholdRatio))
        {
            thresholdLifeVersion = health.LifeVersion;
            onStoryThresholdReached.Invoke();
        }
    }

    /// <summary>기존 잡기 API를 유지하며 지정 시간 동안 공격·방어·이동을 중단한다.</summary>
    public bool TryGrab(float duration)
    {
        if (!isActiveAndEnabled || health == null || health.IsDead || Time.time < grabbedUntil || float.IsNaN(duration) || float.IsInfinity(duration)) return false;
        grabbedUntil = Time.time + Mathf.Max(0f, duration);
        CancelAttack();
        EndGuard();
        StopMoving();
        return true;
    }

    /// <summary>현재 공격 Coroutine과 토큰을 폐기하여 중단된 모션의 이벤트를 무효화한다.</summary>
    private void CancelAttack()
    {
        int id = attackRequestId;
        if (attack != null || isAttacking) attackRequestId++;
        if (attack != null) StopCoroutine(attack);
        attack = null;
        isAttacking = false;
        if (enemyAnimator != null)
        {
            enemyAnimator.ClearAttackRequest(id);
            enemyAnimator.SetAttacking(false);
        }
    }

    /// <summary>NavMesh가 유효할 때만 이동을 멈추며 Animator 속도도 0으로 갱신한다.</summary>
    private void StopMoving()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = true;
            // 경로를 지워도 남아 있는 이동 속도가 다음 프레임까지 적용되지 않도록 정리한다.
            agent.velocity = Vector3.zero;
        }
        if (enemyAnimator != null) enemyAnimator.SetMovementSpeed(0f);
    }

    /// <summary>자신이 등록한 체력 이벤트를 해제하고 남은 대련 동작을 종료한다.</summary>
    private void OnDisable()
    {
        if (mmaAudioSource != null) mmaAudioSource.Stop();
        if (subscribedHealth != null) subscribedHealth.Damaged -= OnDamaged;
        subscribedHealth = null;
        EndTraining();
    }
}
