using UnityEngine;
using UnityEngine.Events;

public class CombatTarget : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(0.01f)] private float maxHealth = 100f;
    [SerializeField] private float currentHealth = 100f;
    [SerializeField] private bool isDead;

    [Header("Damage Events")]
    [SerializeField] private UnityEvent onDamaged = new();
    [SerializeField] private UnityEvent onDied = new();

    private bool notifyingDamage;
    private bool resetRequested;
    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    private void Awake() => ResetTarget();

    public void TakeDamage(float amount) => TryTakeDamage(amount);

    public bool TryTakeDamage(float amount)
    {
        if (!isActiveAndEnabled || isDead || notifyingDamage || amount <= 0f ||
            float.IsNaN(amount) || float.IsInfinity(amount)) return false;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        isDead = currentHealth <= 0f;
        notifyingDamage = true;
        try
        {
            onDamaged.Invoke();
            if (isDead) onDied.Invoke();
        }
        finally
        {
            notifyingDamage = false;
            if (resetRequested) ResetTarget();
        }
        return true;
    }

    [ContextMenu("Combat/Reset Target")]
    public void ResetTarget()
    {
        // Finish the current hit's events before a listener resets this target for reuse.
        if (notifyingDamage) { resetRequested = true; return; }
        resetRequested = false;
        if (maxHealth <= 0f || float.IsNaN(maxHealth) || float.IsInfinity(maxHealth)) maxHealth = 100f;
        currentHealth = maxHealth;
        isDead = false;
    }
}
