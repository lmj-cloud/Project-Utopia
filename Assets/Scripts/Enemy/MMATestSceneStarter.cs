using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>명시적으로 연결된 MMA 대련의 준비, 시작 및 안전지대 복귀를 담당한다.</summary>
public class MMATestSceneStarter : MonoBehaviour
{
    [SerializeField] private PlayerMeleeCombat playerCombat;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private SparringOpponent opponent;
    [SerializeField] private NavMeshAgent opponentAgent;
    [SerializeField, Min(0.1f)] private float readinessTimeout = 5f;
    [SerializeField] private bool beginOnStart = true;
    private bool isCombatActive;
    public bool IsCombatActive { get { return isCombatActive; } }

    /// <summary>초기화 완료를 기다린다. 자동 시작을 끄면 GameFlow 이벤트에서 BeginCombat을 호출한다.</summary>
    private IEnumerator Start()
    {
        EnterSafeZone();
        if (!beginOnStart) yield break;
        yield return null;
        float deadline = Time.realtimeSinceStartup + readinessTimeout;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (AreReferencesReady() && playerController.CanControlPlayer)
            {
                BeginCombat();
                yield break;
            }
            yield return null;
        }
        Debug.LogError("MMA 테스트: 플레이어·상대 참조 또는 NavMesh 준비 시간이 초과되었습니다.", this);
    }

    /// <summary>외부 UnityEvent에서도 호출할 수 있는 자동 맨손 전환 및 대련 시작 경로다.</summary>
    public void BeginCombat()
    {
        if (isCombatActive || !isActiveAndEnabled || !AreReferencesReady()) return;
        if (!playerController.TryBeginMmaCombat(this)) return;
        opponent.BindPlayer(playerCombat, playerController);
        opponent.BeginTraining();
        isCombatActive = opponent.IsTraining;
        if (!isCombatActive)
        {
            playerController.EndMmaCombat(this);
            Debug.LogError("MMA 테스트: 상대의 애니메이션과 전투 참조를 확인하세요.", this);
        }
    }

    /// <summary>대련과 자기 소유 장비 제한만 끝낸다. 다른 컷신 잠금과 장비 권한은 보존한다.</summary>
    public void EndCombat()
    {
        isCombatActive = false;
        if (opponent != null) opponent.EndTraining();
        if (playerController != null) playerController.EndMmaCombat(this);
    }

    /// <summary>기본 이동과 다른 무기 기능을 유지하면서 MMA 공격 및 AI 대련을 중단한다.</summary>
    public void EnterSafeZone()
    {
        EndCombat();
        if (playerController != null) playerController.SetMmaActionsEnabled(false);
    }

    private bool AreReferencesReady()
    {
        return playerCombat != null && playerController != null && opponent != null &&
            opponentAgent != null && playerController.isActiveAndEnabled &&
            opponentAgent.isActiveAndEnabled && opponentAgent.isOnNavMesh;
    }

    /// <summary>플레이어 사망 또는 외부 대련 종료 후에도 장비 제한이 남지 않도록 정리한다.</summary>
    private void Update()
    {
        if (isCombatActive && (playerController == null || !playerController.isActiveAndEnabled || !playerController.IsAlive ||
            !playerController.IsMmaCombatActive || opponent == null || !opponent.IsTraining))
        {
            EndCombat();
        }
    }

    /// <summary>씬 종료와 컴포넌트 비활성화에서도 자기 전투의 예약을 정리한다.</summary>
    private void OnDisable()
    {
        EndCombat();
    }
}
