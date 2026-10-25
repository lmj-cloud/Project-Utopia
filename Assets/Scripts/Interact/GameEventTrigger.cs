using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어가 지정 영역에 들어왔을 때 조건에 맞는 이벤트를 요청하고 완료·재시도 상태를 관리한다.
/// </summary>
public class GameEventTrigger : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private GameFlowManager flow;
    [SerializeField] private StoryProgress storyProgress;
    [SerializeField] private GameFlowStage requiredStage;
    // 아이템 사용이나 진행의 선행 조건으로 확인할 스토리 플래그.
    [SerializeField] private string requiredFlag;
    [SerializeField] private bool onlyOnce = true;
    [SerializeField] private UnityEvent onRequested = new();
    private readonly HashSet<Collider> inside = new();
    private bool pending;
    private bool completed;
    // 진행 단계의 버전. 이전 단계에서 뒤늦게 온 완료 요청을 거부하는 데 사용한다.
    private int revision;
    /// <summary>
    /// 외부 동작이 실제 완료됐음을 확인한 뒤 트리거 완료 상태를 확정한다.
    /// </summary>
    public void ConfirmCompleted()
    {
        if (!pending || flow == null || flow.Revision != revision || flow.CurrentStage != requiredStage) return;
        pending = false;
        completed = true;
    }
    /// <summary>
    /// 완료되지 않은 요청을 정리하여 기존 재시도 정책을 적용할 수 있게 한다.
    /// </summary>
    public void RequestFailedOrCancelled() => pending = false;
    /// <summary>
    /// 플레이어가 영역에 진입하면 조건에 맞는 트리거 요청을 처리한다.
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        if (player == null || other.GetComponentInParent<PlayerController>() != player || !inside.Add(other)) return;
        if (inside.Count == 1) Request();
    }
    /// <summary>
    /// 영역에서 빠져나온 플레이어를 추적 상태에서 제거한다.
    /// </summary>
    private void OnTriggerExit(Collider other) => inside.Remove(other);
    /// <summary>
    /// 플레이어가 영역 안에 남아 있을 때 요청을 다시 시도한다.
    /// </summary>
    public void RetryWhileInside() { if (inside.Count > 0) Request(); }
    /// <summary>
    /// 중복 실행과 선행 조건을 확인한 뒤 트리거에 연결된 동작을 요청한다.
    /// </summary>
    private void Request()
    {
        if (!isActiveAndEnabled || onRequested.GetPersistentEventCount() == 0 || pending || (onlyOnce && completed) || flow == null || !flow.HasBegun || flow.CurrentStage != requiredStage ||
            (!string.IsNullOrWhiteSpace(requiredFlag) && (storyProgress == null || !storyProgress.HasFlag(requiredFlag)))) return;
        pending = true;
        revision = flow.Revision;
        onRequested.Invoke();
    }
    /// <summary>
    /// 영역 이벤트의 완료·진행 상태를 초기화한다.
    /// </summary>
    public void ResetTrigger() { pending = false; completed = false; inside.Clear(); }
    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable() { pending = false; inside.Clear(); }
}
