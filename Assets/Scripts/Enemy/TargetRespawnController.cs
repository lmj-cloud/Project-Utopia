using UnityEngine;

/// <summary>
/// 비활성화되는 Target의 부모에서 리스폰 시간을 관리한다. 전역 Singleton이나 Coroutine은 사용하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class TargetRespawnController : MonoBehaviour
{
    [Header("Target Reference")]
    [SerializeField] private ShootingRangeTarget target;

    [Header("Target Respawn")]
    [SerializeField] private bool enableRespawn = true;
    [Tooltip("게임 시간 기준 초. 일시정지 중에는 리스폰 시간도 멈춘다.")]
    [SerializeField, Min(0.1f)] private float respawnDelay = 5f;
    [SerializeField] private bool resetPositionOnRespawn = true;

    private bool hasPendingRespawn;
    private float respawnAt;

    public bool IsRespawnPending { get { return hasPendingRespawn; } }

    /// <summary>동일 사망에 대한 요청은 한 번만 예약하며 잘못된 부모 연결은 받아들이지 않는다.</summary>
    public void RequestRespawn(ShootingRangeTarget requester)
    {
        if (!isActiveAndEnabled || !enableRespawn || hasPendingRespawn || target == null || requester != target)
        {
            return;
        }
        if (!target.IsDead || target.gameObject == gameObject || !target.transform.IsChildOf(transform))
        {
            return;
        }
        hasPendingRespawn = true;
        respawnAt = Time.time + Mathf.Max(0.1f, respawnDelay);
    }

    /// <summary>Target이 꺼져 있어도 이 부모는 살아 있으므로 기다린 뒤 안전하게 복구할 수 있다.</summary>
    private void Update()
    {
        if (!hasPendingRespawn) return;
        if (!enableRespawn || target == null || !target.IsDead)
        {
            hasPendingRespawn = false;
            return;
        }
        if (Time.time < respawnAt) return;

        // 복구 콜백 전에 예약부터 지워 재진입이나 중복 활성화를 막는다.
        hasPendingRespawn = false;
        target.ResetForRespawn(resetPositionOnRespawn);
    }

    /// <summary>Scene 해제 또는 부모 비활성화 후 예전 예약이 다시 실행되지 않게 한다.</summary>
    private void OnDisable()
    {
        hasPendingRespawn = false;
    }
}
