using UnityEngine;

public class PlayerStatus : MonoBehaviour
{
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

    private void Awake()
    {
        RestoreFullStatus();
    }

    private void Update()
    {
        if (IsDead || Time.time < lastStaminaUseTime + staminaRecoveryDelay)
        {
            return;
        }

        RestoreStamina(staminaRecoveryPerSecond * Time.deltaTime);
    }

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

    public void RestoreStamina(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
    }

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

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    public void RestoreFullStatus()
    {
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        IsDead = false;
        lastStaminaUseTime = Time.time;
    }
}