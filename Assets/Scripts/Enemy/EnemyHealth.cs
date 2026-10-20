using UnityEngine;
using UnityEngine.Events;

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

    private bool notifyingDamage;
    private bool resetRequested;
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    private void Awake() => ResetHealth();

    public void TakeDamage(float baseDamage, EnemyHitPart part) => TryTakeDamage(baseDamage, part);

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

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        isDead = currentHealth <= 0f;
        notifyingDamage = true;
        try
        {
            onDamaged.Invoke();
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

    [ContextMenu("Combat/Reset Health")]
    public void ResetHealth()
    {
        if (notifyingDamage) { resetRequested = true; return; }
        resetRequested = false;
        if (maxHealth <= 0f || float.IsNaN(maxHealth) || float.IsInfinity(maxHealth)) maxHealth = 100f;
        currentHealth = maxHealth;
        isDead = false;
    }
}
