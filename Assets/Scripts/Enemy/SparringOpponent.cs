using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

public class SparringOpponent : MonoBehaviour
{
    [SerializeField] private PlayerMeleeCombat player;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private EnemyHealth health;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform attackOrigin;
    [SerializeField] private LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.1f)] private float attackDistance = 1.6f;
    [SerializeField, Min(0.1f)] private float attackInterval = 1.5f;
    [SerializeField, Min(0f)] private float telegraphDuration = 0.5f;
    [SerializeField, Min(0f)] private float damage = 10f;
    [SerializeField, Range(0f, 180f)] private float attackAngle = 120f;
    [SerializeField, Min(0f)] private float recoveryDuration = 0.25f;
    [SerializeField, Range(0f, 1f)] private float guardDamageMultiplier = 0.5f;
    public float GuardDamageMultiplier => guardDamageMultiplier;
    private float grabbedUntil;
    private float nextPathRequest;
    [SerializeField] private UnityEvent onAttackTelegraph = new();
    [SerializeField] private UnityEvent onAttack = new();
    [SerializeField] private UnityEvent onHitReaction = new();
    [SerializeField] private UnityEvent onGuardChanged = new();
    [SerializeField] private UnityEvent onStartFailed = new();
    private Coroutine attack;
    private bool training;
    private float nextAttack;
    public bool IsGuarding { get; private set; }
    private void Awake() { if (health == null) health = GetComponent<EnemyHealth>(); if (agent == null) agent = GetComponent<NavMeshAgent>(); }
    public void BeginTraining()
    {
        if (training) return;
        if (!isActiveAndEnabled || health == null || health.IsDead || player == null || playerController == null || attackOrigin == null ||
            agent == null || !agent.enabled || !agent.isOnNavMesh) { onStartFailed.Invoke(); return; }
        grabbedUntil = 0f;
        training = true;
    }
    public void EndTraining()
    {
        training = false;
        CancelAttack();
        StopMoving();
    }
    private void Update()
    {
        if (!training || Time.time < grabbedUntil || health == null || health.IsDead || player == null || playerController == null || !playerController.CanControlPlayer)
        { CancelAttack(); StopMoving(); return; }
        float distance = Vector3.Distance(transform.position, player.transform.position);
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (distance > attackDistance && attack == null)
            {
                agent.isStopped = false;
                if (!agent.pathPending && Time.time >= nextPathRequest)
                {
                    nextPathRequest = Time.time + 0.5f;
                    if (NavMesh.SamplePosition(player.transform.position, out NavMeshHit destination, 1f, agent.areaMask))
                    {
                        if (!agent.SetDestination(destination.position)) StopMoving();
                    }
                    else StopMoving();
                }
                if (!agent.pathPending && agent.hasPath && agent.pathStatus != NavMeshPathStatus.PathComplete)
                    StopMoving();
            }
            else StopMoving();
        }
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) { CancelAttack(); return; }
        if (distance <= attackDistance && Time.time >= nextAttack && attack == null)
        {
            nextAttack = Time.time + attackInterval;
            attack = StartCoroutine(Attack());
        }
    }
    private IEnumerator Attack()
    {
        yield return null;
        if (!training || health == null || health.IsDead || playerController == null || !playerController.CanControlPlayer)
        { attack = null; yield break; }
        IsGuarding = false;
        onGuardChanged.Invoke();
        onAttackTelegraph.Invoke();
        yield return new WaitForSeconds(telegraphDuration);
        if (training && health != null && !health.IsDead && player != null && playerController.CanControlPlayer &&
            attackOrigin != null && Vector3.Distance(transform.position, player.transform.position) <= attackDistance)
        {
            Vector3 destination = player.transform.position + Vector3.up;
            Vector3 delta = destination - attackOrigin.position;
            bool blocked = false;
            foreach (RaycastHit hit in Physics.RaycastAll(attackOrigin.position, delta.normalized, delta.magnitude, obstructionLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(player.transform)) { blocked = true; break; }
            if (!blocked && Vector3.Angle(attackOrigin.forward, delta) <= attackAngle * 0.5f)
            { onAttack.Invoke(); player.ReceiveAttack(damage, transform.position); }
        }
        yield return new WaitForSeconds(recoveryDuration);
        attack = null;
        if (training && health != null && !health.IsDead)
        {
            IsGuarding = true;
            onGuardChanged.Invoke();
        }
    }
    public void OnDamaged()
    {
        CancelAttack();
        nextAttack = Time.time + recoveryDuration;
        onHitReaction.Invoke();
        if (health != null && health.IsDead) EndTraining();
    }
    public bool TryGrab(float duration)
    {
        if (!isActiveAndEnabled || health == null || health.IsDead || Time.time < grabbedUntil) return false;
        grabbedUntil = Time.time + Mathf.Max(0f, duration);
        CancelAttack();
        StopMoving();
        return true;
    }
    private void CancelAttack()
    {
        if (attack != null) StopCoroutine(attack);
        attack = null;
        IsGuarding = false;
    }
    private void StopMoving()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
    }
    private void OnDisable() => EndTraining();
}
