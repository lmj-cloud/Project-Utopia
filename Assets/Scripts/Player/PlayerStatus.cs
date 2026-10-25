using UnityEngine;

/// <summary>
/// 플레이어의 체력과 스태미나를 관리하고 피격, 사망 및 상태 복원 기능을 제공한다.
/// </summary>
public class PlayerStatus : MonoBehaviour
{
    /// <summary>
    /// 플레이어의 확정된 체력과 스태미나를 저장하는 스냅샷이다.
    /// </summary>
    [System.Serializable]
    public class StatusSnapshot
    {
        public float health;
        public float stamina;
    }

    /// <summary>
    /// 현재 체력과 스태미나를 저장용 스냅샷으로 반환한다.
    /// </summary>
    public StatusSnapshot CaptureStatus()
    {
        return new StatusSnapshot { health = currentHealth, stamina = currentStamina };
    }

    /// <summary>
    /// 유효한 저장 수치를 범위에 맞추어 복원하며 진행 중 동작을 재개하지 않는다.
    /// </summary>
    public bool RestoreStatus(StatusSnapshot snapshot)
    {
        // 씬 전환은 살아 있는 플레이어만 허용하며 사망 상태를 새 플레이어에 복사하지 않는다.
        if (snapshot == null || float.IsNaN(snapshot.health) || float.IsInfinity(snapshot.health) ||
            float.IsNaN(snapshot.stamina) || float.IsInfinity(snapshot.stamina) ||
            snapshot.health <= 0f || snapshot.health > maxHealth || snapshot.stamina < 0f || snapshot.stamina > maxStamina)
        {
            return false;
        }
        currentHealth = snapshot.health;
        currentStamina = snapshot.stamina;
        IsDead = false;
        lastStaminaUseTime = Time.time;
        return true;
    }

    [Header("Walk, Run Speed")]
    [SerializeField]
    private float walkSpeed;
    [SerializeField]
    private float runSpeed;

    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [Header("Stamina")]
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [SerializeField, Min(0f)] private float staminaRecoveryPerSecond = 18f;
    [SerializeField, Min(0f)] private float staminaRecoveryDelay = 0.75f;
    [SerializeField] private float runStaminaCostPerSecond = 20f;

    private float currentHealth;
    private float currentStamina;
    private float lastStaminaUseTime;

    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthNormalized => currentHealth / maxHealth;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float StaminaNormalized => currentStamina / maxStamina;
    public float RunStaminaCostPerSecond => runStaminaCostPerSecond;

    public event System.Action Damaged;
    public event System.Action Died;
    public bool IsDead { get; private set; }

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        RestoreFullStatus();
    }

    /// <summary>
    /// 살아 있고 스태미나 사용 후 회복 지연 시간이 지났을 때 초당 회복량을 적용한다.
    /// </summary>
    private void Update()
    {
        if (IsDead || Time.time < lastStaminaUseTime + staminaRecoveryDelay)
        {
            return;
        }

        RestoreStamina(staminaRecoveryPerSecond * Time.deltaTime);
    }

    /// <summary>
    /// 생존·음수 비용·잔량을 검사한 뒤 요청한 스태미나를 차감한다.
    /// </summary>
    public bool TryUseStamina(float amount)
    {
        if (IsDead || amount < 0f || currentStamina < amount)
        {
            return false;
        }

        currentStamina = Mathf.Max(0f, currentStamina - amount);
        lastStaminaUseTime = Time.time;
        return true;
    }

    /// <summary>
    /// 유효한 양만큼 스태미나를 회복하되 최대값을 넘지 않게 한다.
    /// </summary>
    public void RestoreStamina(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
    }

    /// <summary>
    /// 기존 외부 호출 경로를 유지하면서 피해 적용을 요청한다.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);

        if (currentHealth <= 0f)
        {
            IsDead = true;
            Debug.Log("Player died.", this);
            Died?.Invoke();
        }
        Damaged?.Invoke();
    }

    /// <summary>
    /// 살아 있는 플레이어의 체력을 유효한 회복량만큼 최대값 이내에서 회복한다.
    /// </summary>
    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    /// <summary>
    /// 체력과 스태미나를 최대값으로 초기화한다.
    /// </summary>
    public void RestoreFullStatus()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        IsDead = false;
        lastStaminaUseTime = Time.time;
    }
}