using UnityEngine;

public enum EnemyHitPart { Head = 0, Body = 1, Leg = 2 }

public class EnemyHitbox : MonoBehaviour
{
    [Header("Hit Region")]
    [SerializeField] private EnemyHitPart part = EnemyHitPart.Body;
    [SerializeField] private EnemyHealth enemyHealth;
    private bool warnedMissingHealth;

    public EnemyHealth Health => enemyHealth;
    public EnemyHitPart Part => part;

    public void TakeDamage(float baseDamage) => TryTakeDamage(baseDamage);

    public bool TryTakeDamage(float baseDamage)
    {
        if (!isActiveAndEnabled) return false;
        if (enemyHealth == null)
        {
            if (!warnedMissingHealth)
            {
                Debug.LogWarning("EnemyHitbox: Connect the owning enemy's EnemyHealth in the Inspector.", this);
                warnedMissingHealth = true;
            }
            return false;
        }
        warnedMissingHealth = false;
        return enemyHealth.TryTakeDamage(baseDamage, part);
    }
}
