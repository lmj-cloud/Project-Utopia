using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일반·강공격의 피해 판정, 방어, 회피, 잡기와 근접 행동의 중복 실행을 관리한다.
/// </summary>
public class PlayerMeleeCombat : MonoBehaviour
{
    // 현재 근접 시스템을 예약한 확장 행동. 기존 Timer 공격과 동시 실행되지 않게 한다.
    private PlayerCombatActions externalAction;
    public bool CanBeginExternalAction
    {
        get { return CanAct && externalAction == null && attack == null && !resolvingHit && !blocking && !IsDodging && Time.time >= nextAttack; }
    }

    /// <summary>
    /// 기존 근접 행동과 겹치지 않을 때 외부 전투 행동의 요청 번호를 발급한다.
    /// </summary>
    public int ReserveExternalAction(PlayerCombatActions owner)
    {
        if (owner == null || !CanBeginExternalAction) return -1;
        requestId++;
        externalAction = owner;
        return requestId;
    }

    /// <summary>
    /// 현재 외부 행동 예약만 해제하고 다음 공격의 대기 시간을 설정한다.
    /// </summary>
    public void ReleaseExternalAction(PlayerCombatActions owner, int id)
    {
        if (externalAction != owner || id != requestId) return;
        externalAction = null;
        nextAttack = Time.time + attackInterval;
    }

    /// <summary>완료된 MMA 펀치에만 짧은 회복 시간을 적용한다. 일반·단검 공격의 간격은 보존한다.</summary>
    internal void ReleaseExternalMmaAction(PlayerCombatActions owner, int id, float recoverySeconds)
    {
        if (externalAction != owner || id != requestId) return;
        externalAction = null;
        nextAttack = Time.time + Mathf.Max(0f, recoverySeconds);
    }

    /// <summary>완료된 MMA 요청을 다음 콤보에 넘긴다. 일반 공격의 대기 시간 정책은 변경하지 않는다.</summary>
    internal void ReleaseExternalActionForCombo(PlayerCombatActions owner, int id)
    {
        if (externalAction != owner || id != requestId) return;
        externalAction = null;
        nextAttack = Time.time;
    }

    /// <summary>
    /// 유효한 외부 전투 요청인지 확인한 뒤 기존 공격 스태미나 비용을 소비한다.
    /// </summary>
    public bool TrySpendExternalStamina(PlayerCombatActions owner, int id)
    {
        return externalAction == owner && id == requestId && status != null && status.TryUseStamina(attackStamina);
    }

    /// <summary>
    /// 현재 외부 행동 토큰이 유효하면 기존 근접 피해 판정 경로를 사용한다.
    /// </summary>
    public void ApplyExternalHit(PlayerCombatActions owner, int id, bool dagger)
    {
        if (externalAction != owner || id != requestId) return;
        ApplyHit(dagger ? daggerDamage : punchDamage, dagger ? DemoAction.DaggerHit : DemoAction.PunchHit);
    }
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerStatus status;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerAnimatorController animator;
    [SerializeField] private PlayerEquipment equipment;
    // 근접 범위·방향·장애물 검사의 기준 Transform.
    [SerializeField] private Transform attackOrigin;
    // 근접 타격 또는 대상 탐색에 포함할 물리 Layer.
    [SerializeField] private LayerMask hitLayers = ~0;
    // 공격과 상호작용 사이의 장애물 판정에 포함할 Layer.
    [SerializeField] private LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0f)] private float punchDamage = 10f;
    [SerializeField, Min(0f)] private float daggerDamage = 25f;
    [SerializeField, Min(0.01f)] private float range = 1.5f;
    [SerializeField, Range(0f, 180f)] private float attackAngle = 80f;
    [SerializeField, Min(0f)] private float hitDelay = 0.2f;
    // 연속 공격이 중복되지 않게 다음 공격까지 기다릴 시간.
    [SerializeField, Min(0f)] private float attackInterval = 0.6f;
    // Timer와 Animation Event 중 기존 근접 타격 시점의 결정 방식을 선택한다.
    [SerializeField] private ActionTimingMode attackTiming;
    // Animation Event가 오지 않아도 행동을 취소할 수 있도록 둔 최대 대기 시간.
    [SerializeField, Min(0.1f)] private float animationEventTimeout = 3f;
    // 일반 근접 행동을 시작할 때 소비하는 스태미나.
    [SerializeField, Min(0f)] private float attackStamina = 8f;
    [SerializeField, Range(0f, 1f)] private float blockReduction = 0.7f;
    [SerializeField, Range(0f, 180f)] private float blockAngle = 100f;
    [SerializeField, Min(0f)] private float blockStaminaPerSecond = 5f;
    [SerializeField, Min(0.01f)] private float dodgeDuration = 0.35f;
    [SerializeField, Min(0f)] private float dodgeSpeed = 4f;
    [SerializeField, Min(0f)] private float dodgeStamina = 15f;
    [SerializeField, Min(0f)] private float grabRange = 1.2f;
    [SerializeField, Range(0f, 180f)] private float assassinationBehindAngle = 60f;
    [SerializeField, Min(0f)] private float assassinationDamage = 100f;
    [Header("Strong Attack")]
    [SerializeField, Min(0f)] private float strongPunchDamage = 20f;
    [SerializeField, Min(0f)] private float strongDaggerDamage = 50f;
    [SerializeField, Min(0f)] private float strongAttackStamina = 16f;
    [SerializeField, Min(0f)] private float strongHitDelay = 0.4f;
    // 기존 강공격 후 다음 공격까지 기다릴 시간.
    [SerializeField, Min(0f)] private float strongAttackInterval = 1f;
    private float activeAttackDamage;
    private DemoAction activeAttackAction;
    private Coroutine attack;
    private float nextAttack;
    private float dodgeUntil;
    private bool blocking;
    // 같은 공격의 피해 판정을 중복 적용하지 않게 기록한다.
    private bool hitConsumed;
    private bool resolvingHit;
    private EnemyHealth grabbedTarget;
    private bool assassinating;
    // 비동기 타격·완료 콜백이 현재 행동에 속하는지 구분하는 요청 번호.
    private int requestId;
    public int RequestId => requestId;
    public bool IsDodging => Time.time < dodgeUntil;
    public bool IsBlocking => blocking;
    public event Action<DemoAction, UnityEngine.Object> Succeeded;
    [Header("MMA 타격 효과 (미연결 상태에서도 전투 판정은 유지)")]
    [SerializeField] private AudioSource mmaAudioSource;
    [SerializeField] private AudioClip mmaJabSwing;
    [SerializeField] private AudioClip mmaCrossSwing;
    [SerializeField] private AudioClip mmaHitSound;
    [SerializeField] private AudioClip mmaBlockSound;
    [SerializeField, Range(0f, 1f)] private float mmaSoundVolume = 0.65f;
    [SerializeField] private GameObject mmaHitEffectPrefab;
    [SerializeField, Min(0.01f)] private float mmaHitEffectScale = 0.6f;
    private MemoryPool mmaHitEffectPool;
    private int lastMmaSwingRequest = -1;

    /// <summary>실제 시작한 펀치의 휘두르기 소리를 재생한다. AI도 자신의 AudioSource를 전달해 재사용한다.</summary>
    public void PlayMmaSwing(bool rightHand, AudioSource source = null)
    {
        AudioClip clip = rightHand ? mmaCrossSwing : mmaJabSwing;
        PlayMmaSound(clip, source);
    }

    private void PlayMmaSound(AudioClip clip, AudioSource source)
    {
        if (source == null) source = mmaAudioSource;
        if (source == null || !source.isActiveAndEnabled || clip == null) return;
        source.PlayOneShot(clip, mmaSoundVolume);
    }

    /// <summary>판정 완료 지점에서만 명중/방어음을 재생한다. 방어에는 일반 명중 파티클을 만들지 않는다.</summary>
    public void PlayMmaContact(bool blocked, Vector3 point, Vector3 normal, AudioSource source = null)
    {
        PlayMmaSound(blocked ? mmaBlockSound : mmaHitSound, source);
        if (blocked || mmaHitEffectPool == null || !isActiveAndEnabled) return;
        GameObject effect = mmaHitEffectPool.ActivatePoolItem();
        if (effect == null) return;
        if (normal.sqrMagnitude < 0.001f) normal = Vector3.up;
        effect.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
        effect.transform.localScale = mmaHitEffectPrefab.transform.localScale * mmaHitEffectScale;
        effect.GetComponent<Impact>().Setup(mmaHitEffectPool);
    }

    /// <summary>대련 종료와 비활성화에서 MMA 효과만 정리한다. 총기 AudioSource와 풀은 유지한다.</summary>
    public void ClearMmaFeedback()
    {
        if (mmaAudioSource != null) mmaAudioSource.Stop();
        if (mmaHitEffectPool != null) mmaHitEffectPool.DeactivateAllPoolItems();
    }

    /// <summary>씬의 MMA 전용 풀만 제거한다. 총기 충돌 효과 풀에는 관여하지 않는다.</summary>
    private void OnDestroy()
    {
        if (mmaHitEffectPool != null) mmaHitEffectPool.DestroyObjects();
    }

    private bool CanAct => isActiveAndEnabled && playerController != null && playerController.CanUseMelee &&
        status != null && !status.IsDead && equipment != null && equipment.IsMelee;
    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (status == null) status = GetComponent<PlayerStatus>();
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (animator == null) animator = GetComponent<PlayerAnimatorController>();
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        if (mmaHitEffectPrefab != null && mmaHitEffectPrefab.GetComponent<Impact>() != null &&
            mmaHitEffectPrefab.GetComponent<ParticleSystem>() != null)
            mmaHitEffectPool = new MemoryPool(mmaHitEffectPrefab);
    }
    /// <summary>
    /// 장비·대기 시간·스태미나를 검사하고 일반 또는 지정된 강공격을 요청한다.
    /// </summary>
    public bool TryAttack() => TryAttack(false);
    /// <summary>
    /// 기존 강공격의 피해·비용·타이밍 설정으로 공격을 요청한다.
    /// </summary>
    public bool TryStrongAttack() => TryAttack(true);
    /// <summary>
    /// 장비·대기 시간·스태미나를 검사하고 일반 또는 지정된 강공격을 요청한다.
    /// </summary>
    private bool TryAttack(bool strong)
    {
        if (!CanAct || externalAction != null || attack != null || resolvingHit || blocking || IsDodging || attackOrigin == null || Time.time < nextAttack) return false;
        bool dagger = equipment.CurrentKind == PlayerEquipmentKind.Dagger;
        if (attackTiming == ActionTimingMode.AnimationEvent &&
            (animator == null || (strong ? !animator.CanPlayStrongMelee(dagger) : !animator.CanPlayMelee(dagger)))) return false;
        if (!status.TryUseStamina(strong ? strongAttackStamina : attackStamina)) return false;
        nextAttack = Time.time + (strong ? strongAttackInterval : attackInterval);
        activeAttackDamage = strong ? (dagger ? strongDaggerDamage : strongPunchDamage) : (dagger ? daggerDamage : punchDamage);
        activeAttackAction = dagger ? DemoAction.DaggerHit : DemoAction.PunchHit;
        grabbedTarget = null;
        hitConsumed = false;
        requestId++;
        if (animator != null)
        {
            if (strong) animator.OnStrongAttack(dagger);
            else if (dagger) animator.OnDaggerAttack();
            else animator.OnPunch();
        }
        attack = StartCoroutine(Attack(requestId, strong ? strongHitDelay : hitDelay));
        if (!dagger) NotifyMmaOpponents();
        return true;
    }
    /// <summary>
    /// 요청 번호가 유지된 동안 타이머 판정 또는 Animation Event 대기 정책을 적용한다.
    /// </summary>
    private IEnumerator Attack(int id, float delay)
    {
        yield return new WaitForSeconds(attackTiming == ActionTimingMode.Timer ? Mathf.Max(0f, delay) : animationEventTimeout);
        if (id != requestId) yield break;
        if (attackTiming == ActionTimingMode.Timer && CanAct && !hitConsumed)
        {
            hitConsumed = true;
            ApplyHit(activeAttackDamage, activeAttackAction);
        }
        if (id == requestId) attack = null;
    }
    // Animation Event 모드의 대기 Coroutine을 정리하여 같은 타격을 중복 적용하지 않는다.
    /// <summary>
    /// 현재 공격 토큰과 타이밍 모드가 맞을 때 타격 이벤트를 한 번만 적용한다.
    /// </summary>
    private void OnAttackHitFrame()
    {
        // Timer mode deliberately ignores clip events.
        if (attackTiming != ActionTimingMode.AnimationEvent || attack == null || !CanAct || hitConsumed) return;
        hitConsumed = true;
        StopCoroutine(attack);
        attack = null;
        if (grabbedTarget != null) ApplyGrab(grabbedTarget, assassinating);
        else
        {
            ApplyHit(activeAttackDamage, activeAttackAction);
        }
    }
    /// <summary>
    /// 현재 공격 토큰과 타이밍 모드가 맞을 때 타격 이벤트를 한 번만 적용한다.
    /// </summary>
    public void OnAttackHitFrame(int id) { if (id == requestId) OnAttackHitFrame(); }
    /// <summary>
    /// 범위·방향·장애물과 적의 방어를 검사하여 같은 대상에 중복되지 않게 피해를 준다.
    /// </summary>
    private void ApplyHit(float amount, DemoAction action)
    {
        if (!CanAct || attackOrigin == null) return;
        int id = requestId;
        resolvingHit = true;
        try
        {
        HashSet<UnityEngine.Object> damaged = new();
        foreach (Collider collider in Physics.OverlapSphere(attackOrigin.position, range, hitLayers, QueryTriggerInteraction.Collide))
        {
            if (id != requestId || !CanAct || attackOrigin == null) break;
            if (collider.transform.IsChildOf(transform)) continue;
            EnemyHitbox hitbox = collider.GetComponent<EnemyHitbox>();
            EnemyHealth enemy = hitbox != null ? hitbox.Health : collider.GetComponentInParent<EnemyHealth>();
            CombatTarget target = hitbox == null && enemy == null ? collider.GetComponentInParent<CombatTarget>() : null;
            if (hitbox != null && enemy == null) continue;
            // 환경과 미지원 Collider는 실제 형상의 최근접점 계산 전에 제외한다.
            if (enemy == null && target == null) continue;
            if (!(collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider ||
                (collider is MeshCollider meshCollider && meshCollider.convex))) continue;
            Vector3 direction = collider.ClosestPoint(attackOrigin.position) - attackOrigin.position;
            if (Vector3.Angle(attackOrigin.forward, direction) > attackAngle * 0.5f || !HasLineOfSight(collider)) continue;
            if (enemy != null && !enemy.IsDead && damaged.Add(enemy))
            {
                SparringOpponent opponent = enemy.GetComponent<SparringOpponent>();
                // MMA 상대가 실제 타격 위치를 전방에서 방어한 경우에는 피해·피격·성공 알림을 모두 생략한다.
                Vector3 point = collider.ClosestPoint(attackOrigin.position);
                Vector3 normal = attackOrigin.position - point;
                bool mma = equipment != null && equipment.CurrentKind == PlayerEquipmentKind.Gloves;
                if (opponent != null && opponent.CanBlockAttack(attackOrigin.position))
                {
                    if (mma) PlayMmaContact(true, point, normal);
                    continue;
                }
                if (enemy.TryTakeDamage(amount, hitbox != null ? hitbox.Part : EnemyHitPart.Body))
                {
                    if (mma) PlayMmaContact(false, point, normal);
                    Succeeded?.Invoke(action, enemy);
                }
            }
            else if (target != null && !target.IsDead && damaged.Add(target))
            {
                if (target.TryTakeDamage(amount)) Succeeded?.Invoke(action, target);
            }
        }
        }
        finally { resolvingHit = false; }
    }
    /// <summary>
    /// 공격 기준점과 대상 사이에 자기 몸이나 대상 이외의 장애물이 있는지 검사한다.
    /// </summary>
    private bool HasLineOfSight(Collider target)
    {
        EnemyHitbox hitbox = target.GetComponent<EnemyHitbox>();
        EnemyHealth enemy = hitbox != null ? hitbox.Health : target.GetComponentInParent<EnemyHealth>();
        CombatTarget practice = enemy == null ? target.GetComponentInParent<CombatTarget>() : null;
        Transform owner = enemy != null ? enemy.transform : practice != null ? practice.transform : target.transform;
        Vector3 delta = target.bounds.center - attackOrigin.position;
        RaycastHit[] hits = Physics.RaycastAll(attackOrigin.position, delta.normalized, delta.magnitude,
            obstructionLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
            if (!hit.collider.transform.IsChildOf(transform) && hit.collider != target &&
                !hit.collider.transform.IsChildOf(owner)) return false;
        return true;
    }

    /// <summary>실제 시작된 글러브 좌우 공격의 동일 요청에 대해서만 MMA 상대에게 준비 신호를 전달한다.</summary>
    public void NotifyMmaAttackStarted(PlayerCombatActions owner, int id)
    {
        if (externalAction != owner || owner == null || !owner.IsRunning || id != requestId ||
            equipment == null || equipment.CurrentKind != PlayerEquipmentKind.Gloves) return;
        if (lastMmaSwingRequest != id)
        {
            lastMmaSwingRequest = id;
            PlayMmaSwing(owner.IsRightMmaPunch);
        }
        NotifyMmaOpponents();
    }

    /// <summary>기존 타격 범위·정면·장애물 검사에 맞는 MMA 상대에게 공격 시작을 한 번씩 알린다.</summary>
    private void NotifyMmaOpponents()
    {
        if (!CanAct || attackOrigin == null) return;
        int id = requestId;
        HashSet<SparringOpponent> notified = new HashSet<SparringOpponent>();
        foreach (Collider collider in Physics.OverlapSphere(attackOrigin.position, range, hitLayers, QueryTriggerInteraction.Collide))
        {
            if (id != requestId || !CanAct) break;
            if (collider.transform.IsChildOf(transform)) continue;
            EnemyHitbox hitbox = collider.GetComponent<EnemyHitbox>();
            EnemyHealth enemy = hitbox != null ? hitbox.Health : collider.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead) continue;
            SparringOpponent opponent = enemy.GetComponent<SparringOpponent>();
            if (opponent == null || notified.Contains(opponent)) continue;
            if (!(collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider ||
                (collider is MeshCollider mesh && mesh.convex))) continue;
            Vector3 direction = collider.ClosestPoint(attackOrigin.position) - attackOrigin.position;
            if (Vector3.Angle(attackOrigin.forward, direction) > attackAngle * 0.5f || !HasLineOfSight(collider)) continue;
            notified.Add(opponent);
            opponent.OnPlayerAttackStarted(attackOrigin.position);
        }
    }
    /// <summary>
    /// 장비와 행동 상태에 맞는 방어 여부를 설정하고 Animator에 전달한다.
    /// </summary>
    public void SetBlocking(bool value)
    {
        blocking = value && CanAct && externalAction == null && equipment.CurrentKind == PlayerEquipmentKind.Gloves && attack == null && !IsDodging;
        if (animator != null) animator.OnDefense(blocking);
    }
    /// <summary>
    /// 회피 가능 상태와 스태미나를 확인하여 이동 담당자에게 회피를 요청한다.
    /// </summary>
    public bool TryDodge(Vector3 direction)
    {
        if (!CanAct || externalAction != null || IsDodging || attack != null || movement == null ||
            !movement.CanDodge || !status.TryUseStamina(dodgeStamina)) return false;
        if (!movement.TryDodge(direction, dodgeSpeed, dodgeDuration)) return false;
        SetBlocking(false);
        dodgeUntil = Time.time + dodgeDuration;
        if (animator != null) animator.OnDodge();
        Succeeded?.Invoke(DemoAction.Dodge, this);
        return true;
    }
    /// <summary>
    /// 구분된 피격 결과를 기존 bool 반환 규칙으로 변환하여 호환 호출을 유지한다.
    /// </summary>
    public bool ReceiveAttack(float amount, Vector3 sourcePosition)
    {
        CombatHitResult result = ReceiveAttackResult(amount, sourcePosition);
        return result == CombatHitResult.Blocked || result == CombatHitResult.UnblockedHit;
    }

    public event Action<CombatHitResult> AttackReceived;

    /// <summary>
    /// 회피와 방어 각도를 검사하고 피해·알림을 적용한 뒤 실제 피격 결과를 구분한다.
    /// </summary>
    public CombatHitResult ReceiveAttackResult(float amount, Vector3 sourcePosition)
    {
        if (status == null || status.IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount) ||
            !isActiveAndEnabled || playerController == null || !playerController.CanControlPlayer) return CombatHitResult.Rejected;
        if (IsDodging)
        {
            Succeeded?.Invoke(DemoAction.EvadeHit, this);
            AttackReceived?.Invoke(CombatHitResult.Evaded);
            return CombatHitResult.Evaded;
        }
        bool defended = blocking && Vector3.Angle(transform.forward, sourcePosition - transform.position) <= blockAngle * 0.5f;
        status.TakeDamage(defended ? amount * (1f - blockReduction) : amount);
        if (defended) Succeeded?.Invoke(DemoAction.BlockHit, this);
        if (defended && animator != null) animator.OnDefenseHit();
        CombatHitResult result = defended ? CombatHitResult.Blocked : CombatHitResult.UnblockedHit;
        AttackReceived?.Invoke(result);
        return result;
    }
    /// <summary>
    /// 장비·거리·방향과 대상 조건을 검사하여 기존 잡기 또는 암살을 요청한다.
    /// </summary>
    public bool TryGrab(bool assassinate)
    {
        if (!CanAct || externalAction != null || attack != null || IsDodging || blocking || attackOrigin == null || Time.time < nextAttack ||
            (assassinate && equipment.CurrentKind != PlayerEquipmentKind.Dagger) ||
            (!assassinate && equipment.CurrentKind != PlayerEquipmentKind.Gloves)) return false;
        EnemyHealth chosen = null;
        float nearest = float.PositiveInfinity;
        foreach (Collider c in Physics.OverlapSphere(attackOrigin.position, grabRange, hitLayers, QueryTriggerInteraction.Collide))
        {
            EnemyHitbox box = c.GetComponent<EnemyHitbox>();
            EnemyHealth enemy = box != null ? box.Health : c.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || enemy.transform.IsChildOf(transform) || !HasLineOfSight(c)) continue;
            Vector3 toEnemy = enemy.transform.position - transform.position;
            if (Vector3.Angle(transform.forward, toEnemy) > attackAngle * 0.5f) continue;
            if (assassinate && Vector3.Angle(-enemy.transform.forward, transform.position - enemy.transform.position) > assassinationBehindAngle * 0.5f) continue;
            if (toEnemy.sqrMagnitude < nearest) { nearest = toEnemy.sqrMagnitude; chosen = enemy; }
        }
        if (chosen == null || (attackTiming == ActionTimingMode.AnimationEvent && (animator == null || !animator.CanPlayGrab(assassinate))) || !status.TryUseStamina(attackStamina)) return false;
        requestId++;
        hitConsumed = false;
        grabbedTarget = chosen;
        assassinating = assassinate;
        nextAttack = Time.time + attackInterval;
        if (animator != null) { if (assassinate) animator.OnAssassinate(); else animator.OnGrab(); }
        attack = StartCoroutine(Grab(chosen, assassinate));
        return true;
    }
    /// <summary>
    /// 기존 잡기 타이밍에 맞추어 대상 판정을 요청하고 대기 상태를 해제한다.
    /// </summary>
    private IEnumerator Grab(EnemyHealth enemy, bool assassinate)
    {
        yield return new WaitForSeconds(attackTiming == ActionTimingMode.Timer ? hitDelay : animationEventTimeout);
        if (attackTiming == ActionTimingMode.Timer && !hitConsumed)
        {
            hitConsumed = true;
            ApplyGrab(enemy, assassinate);
        }
        attack = null;
        grabbedTarget = null;
    }
    /// <summary>
    /// 잡기 시점의 대상 조건을 재검사하고 적 정지 또는 암살 피해를 적용한다.
    /// </summary>
    private void ApplyGrab(EnemyHealth enemy, bool assassinate)
    {
        if (!CanAct || enemy == null || enemy.IsDead || attackOrigin == null || Vector3.Distance(attackOrigin.position, enemy.transform.position + Vector3.up) > grabRange) return;
        Collider collider = enemy.GetComponentInChildren<Collider>();
        if (collider == null || !HasLineOfSight(collider)) return;
        if (Vector3.Angle(transform.forward, enemy.transform.position - transform.position) > attackAngle * 0.5f) return;
        if (assassinate && Vector3.Angle(-enemy.transform.forward, transform.position - enemy.transform.position) > assassinationBehindAngle * 0.5f) return;
        if (assassinate)
        {
            if (enemy.TryTakeDamage(assassinationDamage, EnemyHitPart.Body)) Succeeded?.Invoke(DemoAction.Assassination, enemy);
        }
        else
        {
            SparringOpponent opponent = enemy.GetComponent<SparringOpponent>();
            if (opponent != null && opponent.TryGrab(1f)) Succeeded?.Invoke(DemoAction.Grab, enemy);
        }
    }
    /// <summary>
    /// 유효하지 않은 제어 상태의 행동을 취소하고 방어 유지 비용을 소비한다.
    /// </summary>
    private void Update()
    {
        if (externalAction != null && externalAction.OwnsControlLock && externalAction.CanContinueAction) return;
        if (!CanAct) { CancelActions(); return; }
        if (blocking && equipment.CurrentKind != PlayerEquipmentKind.Gloves) SetBlocking(false);
        if (blocking && !status.TryUseStamina(blockStaminaPerSecond * Time.deltaTime)) SetBlocking(false);
    }
    /// <summary>
    /// 현재 근접 동작과 타이머·회피·방어를 정리하되 유효한 자기 처형 잠금은 보존한다.
    /// </summary>
    public void CancelActions()
    {
        if (externalAction != null)
        {
            // 이 행동 자신의 잠금은 유지하고, 외부 잠금/사망/장비 변경은 취소한다.
            if (externalAction.OwnsControlLock && externalAction.CanContinueAction) return;
            externalAction.CancelAction();
        }
        if (attack != null || resolvingHit || blocking || IsDodging)
        {
            requestId++;
            if (animator != null) animator.OnMeleeCancelled();
        }
        grabbedTarget = null;
        hitConsumed = true;
        if (attack != null) StopCoroutine(attack);
        attack = null;
        dodgeUntil = 0f;
        if (movement != null) movement.CancelDodge();
        if (blocking) SetBlocking(false);
    }
    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        CancelActions();
        ClearMmaFeedback();
    }
}
