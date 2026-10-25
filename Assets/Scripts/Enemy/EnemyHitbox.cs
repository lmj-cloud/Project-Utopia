using UnityEngine;

/// <summary>
/// 적의 머리, 몸통, 다리 피격 부위를 구분한다.
/// </summary>
public enum EnemyHitPart { Head = 0, Body = 1, Leg = 2 }

/// <summary>
/// 맞은 신체 부위를 EnemyHealth에 전달하여 부위별 피해 배율이 적용되게 한다.
/// </summary>
public class EnemyHitbox : MonoBehaviour
{
    [Header("Hit Region")]
    [SerializeField] private EnemyHitPart part = EnemyHitPart.Body;
    [SerializeField] private EnemyHealth enemyHealth;
    private bool warnedMissingHealth;

    public EnemyHealth Health => enemyHealth;
    public EnemyHitPart Part => part;

    /// <summary>
    /// 기존 외부 호출 경로를 유지하면서 피해 적용을 요청한다.
    /// </summary>
    public void TakeDamage(float baseDamage) => TryTakeDamage(baseDamage);

    /// <summary>
    /// 유효한 피해와 생존 상태를 검사한 뒤 체력 감소와 피격 알림을 처리한다.
    /// </summary>
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
