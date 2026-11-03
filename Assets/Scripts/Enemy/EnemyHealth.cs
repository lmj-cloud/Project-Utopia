using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 적의 부위별 피해와 체력, 사망 이벤트 및 체력 초기화에 따른 생명주기를 관리한다.
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(0.01f)] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private bool isDead;

    [Header("Hit Multipliers")]
    [SerializeField, Min(0f)] private float headDamageMultiplier = 2f;
    [SerializeField, Min(0f)] private float bodyDamageMultiplier = 1f;
    [SerializeField, Min(0f)] private float legDamageMultiplier = 0.5f;

    [Header("Damage Events")]
    [SerializeField] private UnityEvent onDamaged = new();
    [SerializeField] private UnityEvent onHeadHit = new();
    [SerializeField] private UnityEvent onBodyHit = new();
    [SerializeField] private UnityEvent onLegHit = new();
    [SerializeField] private UnityEvent onDied = new();

    private Object survivalOwner;
    private float survivalHealth;

    /// <summary>스토리 대련 소유자에게만 양수 체력 하한을 예약한다. 일반 적은 기본 사망 처리를 유지한다.</summary>
    public bool TryProtectFromDefeat(Object owner, float minimumHealth)
    {
        if (owner == null || isDead || (survivalOwner != null && survivalOwner != owner) ||
            float.IsNaN(minimumHealth) || float.IsInfinity(minimumHealth) || minimumHealth <= 0f) return false;
        survivalOwner = owner;
        survivalHealth = Mathf.Min(currentHealth, Mathf.Clamp(minimumHealth, 0.001f, maxHealth));
        return true;
    }

    /// <summary>자신이 예약한 패배 방지만 해제한다. 체력을 임의로 회복하지 않는다.</summary>
    public void ReleaseDefeatProtection(Object owner)
    {
        if (owner == null || survivalOwner != owner) return;
        survivalOwner = null;
        survivalHealth = 0f;
    }

    private bool notifyingDamage;
    private bool resetRequested;
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;
    public int LifeVersion { get; private set; }
    /// <summary>실제 양수 피해가 적용된 순서. 기존 UnityEvent와 코드 구독의 중복 피격 반응을 구분한다.</summary>
    public int DamageVersion { get; private set; }
    /// <summary>실제 체력 감소가 확정된 경우에만 알리는 코드 연결용 이벤트.</summary>
    public event System.Action Damaged;

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake() => ResetHealth();

    /// <summary>
    /// 기존 외부 호출 경로를 유지하면서 피해 적용을 요청한다.
    /// </summary>
    public void TakeDamage(float baseDamage, EnemyHitPart part) => TryTakeDamage(baseDamage, part);

    /// <summary>
    /// 유효한 피해와 생존 상태를 검사한 뒤 체력 감소와 피격 알림을 처리한다.
    /// </summary>
    public bool TryTakeDamage(float baseDamage, EnemyHitPart part)
    {
        if (!isActiveAndEnabled || isDead || notifyingDamage || baseDamage <= 0f ||
            float.IsNaN(baseDamage) || float.IsInfinity(baseDamage)) return false;

        float multiplier;
        UnityEvent partEvent;
        switch (part)
        {
            case EnemyHitPart.Head: multiplier = headDamageMultiplier; partEvent = onHeadHit; break;
            case EnemyHitPart.Body: multiplier = bodyDamageMultiplier; partEvent = onBodyHit; break;
            case EnemyHitPart.Leg: multiplier = legDamageMultiplier; partEvent = onLegHit; break;
            default: return false;
        }
        float amount = baseDamage * multiplier;
        if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return false;

        float minimum = survivalOwner != null ? survivalHealth : 0f;
        float nextHealth = Mathf.Max(minimum, currentHealth - amount);
        if (nextHealth >= currentHealth) return false;
        currentHealth = nextHealth;
        isDead = currentHealth <= 0f;
        DamageVersion++;
        notifyingDamage = true;
        try
        {
            onDamaged.Invoke();
            Damaged?.Invoke();
            partEvent.Invoke();
            if (isDead) onDied.Invoke();
        }
        finally
        {
            notifyingDamage = false;
            if (resetRequested) ResetHealth();
        }
        return true;
    }

    /// <summary>
    /// 체력과 사망 상태를 초기화한다. 진행 중 피해 알림이 있으면 초기화를 뒤로 미룬다.
    /// </summary>
    [ContextMenu("Combat/Reset Health")]
    public void ResetHealth()
    {
        if (notifyingDamage) { resetRequested = true; return; }
        resetRequested = false;
        LifeVersion++;
        if (maxHealth <= 0f || float.IsNaN(maxHealth) || float.IsInfinity(maxHealth)) maxHealth = 100f;
        currentHealth = maxHealth;
        isDead = false;
    }
}
