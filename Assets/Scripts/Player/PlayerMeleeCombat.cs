using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMeleeCombat : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerStatus status;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerAnimatorController animator;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0f)] private float punchDamage = 10f;
    [SerializeField, Min(0f)] private float daggerDamage = 25f;
    [SerializeField, Min(0.01f)] private float range = 1.5f;
    [SerializeField, Range(0f, 180f)] private float attackAngle = 80f;
    [SerializeField, Min(0f)] private float hitDelay = 0.2f;
    [SerializeField, Min(0f)] private float attackInterval = 0.6f;
    [SerializeField] private ActionTimingMode attackTiming;
    [SerializeField, Min(0.1f)] private float animationEventTimeout = 3f;
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
    [SerializeField, Min(0f)] private float strongAttackInterval = 1f;
    private float activeAttackDamage;
    private DemoAction activeAttackAction;
    private Coroutine attack;
    private float nextAttack;
    private float dodgeUntil;
    private bool blocking;
    private bool hitConsumed;
    private bool resolvingHit;
    private EnemyHealth grabbedTarget;
    private bool assassinating;
    private int requestId;
    public int RequestId => requestId;
    public bool IsDodging => Time.time < dodgeUntil;
    public bool IsBlocking => blocking;
    public event Action<DemoAction, UnityEngine.Object> Succeeded;
    private bool CanAct => isActiveAndEnabled && playerController != null && playerController.CanUseMelee &&
        status != null && !status.IsDead && equipment != null && equipment.IsMelee;
    private void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (status == null) status = GetComponent<PlayerStatus>();
        if (movement == null) movement = GetComponent<PlayerMovement>();
        if (animator == null) animator = GetComponent<PlayerAnimatorController>();
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
    }
    public bool TryAttack() => TryAttack(false);
    public bool TryStrongAttack() => TryAttack(true);
    private bool TryAttack(bool strong)
    {
        if (!CanAct || attack != null || resolvingHit || blocking || IsDodging || attackOrigin == null || Time.time < nextAttack) return false;
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
        return true;
    }
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
    // Can also be connected to a clip event. Clearing the routine makes the timer fallback harmless.
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
    public void OnAttackHitFrame(int id) { if (id == requestId) OnAttackHitFrame(); }
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
                float finalDamage = opponent != null && opponent.IsGuarding ? amount * opponent.GuardDamageMultiplier : amount;
                if (enemy.TryTakeDamage(finalDamage, hitbox != null ? hitbox.Part : EnemyHitPart.Body)) Succeeded?.Invoke(action, enemy);
            }
            else if (target != null && !target.IsDead && damaged.Add(target))
            {
                if (target.TryTakeDamage(amount)) Succeeded?.Invoke(action, target);
            }
        }
        }
        finally { resolvingHit = false; }
    }
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
    public void SetBlocking(bool value)
    {
        blocking = value && CanAct && equipment.CurrentKind == PlayerEquipmentKind.Gloves && attack == null && !IsDodging;
        if (animator != null) animator.OnDefense(blocking);
    }
    public bool TryDodge(Vector3 direction)
    {
        if (!CanAct || IsDodging || attack != null || movement == null ||
            !movement.CanDodge || !status.TryUseStamina(dodgeStamina)) return false;
        if (!movement.TryDodge(direction, dodgeSpeed, dodgeDuration)) return false;
        SetBlocking(false);
        dodgeUntil = Time.time + dodgeDuration;
        if (animator != null) animator.OnDodge();
        Succeeded?.Invoke(DemoAction.Dodge, this);
        return true;
    }
    public bool ReceiveAttack(float amount, Vector3 sourcePosition)
    {
        if (status == null || status.IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount) ||
            !isActiveAndEnabled || playerController == null || !playerController.CanControlPlayer) return false;
        if (IsDodging)
        {
            Succeeded?.Invoke(DemoAction.EvadeHit, this);
            return false;
        }
        bool defended = blocking && Vector3.Angle(transform.forward, sourcePosition - transform.position) <= blockAngle * 0.5f;
        status.TakeDamage(defended ? amount * (1f - blockReduction) : amount);
        if (defended) Succeeded?.Invoke(DemoAction.BlockHit, this);
        if (defended && animator != null) animator.OnDefenseHit();
        return true;
    }
    public bool TryGrab(bool assassinate)
    {
        if (!CanAct || attack != null || IsDodging || blocking || attackOrigin == null || Time.time < nextAttack ||
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
    private void Update()
    {
        if (!CanAct) { CancelActions(); return; }
        if (blocking && equipment.CurrentKind != PlayerEquipmentKind.Gloves) SetBlocking(false);
        if (blocking && !status.TryUseStamina(blockStaminaPerSecond * Time.deltaTime)) SetBlocking(false);
    }
    public void CancelActions()
    {
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
    private void OnDisable() => CancelActions();
}
