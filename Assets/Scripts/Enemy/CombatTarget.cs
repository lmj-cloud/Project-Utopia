using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 훈련 과녁의 체력과 피해·사망 이벤트를 처리하고 재사용을 위한 초기화 기능을 제공한다.
/// </summary>
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

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake() => ResetTarget();

    /// <summary>
    /// 기존 외부 호출 경로를 유지하면서 피해 적용을 요청한다.
    /// </summary>
    public void TakeDamage(float amount) => TryTakeDamage(amount);

    /// <summary>
    /// 유효한 피해와 생존 상태를 검사한 뒤 체력 감소와 피격 알림을 처리한다.
    /// </summary>
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

    /// <summary>
    /// 과녁 체력과 사망 상태를 초기화하여 다시 훈련할 수 있게 한다.
    /// </summary>
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
