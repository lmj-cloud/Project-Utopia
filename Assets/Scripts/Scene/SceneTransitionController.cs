using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 화면을 가린 동안 지정 위치로 플레이어를 이동시키고 시선과 입력을 복구한다. 실제 Scene 로드는 수행하지 않는다.
/// </summary>
public class SceneTransitionController : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerCameraController cameraController;
    [SerializeField] private ScreenEffectController screenEffects;
    [SerializeField] private Transform destination;
    [SerializeField] private bool whiteFade;
    [SerializeField] private UnityEvent onCompleted = new();
    [SerializeField] private UnityEvent onCancelled = new();
    [SerializeField] private UnityEvent onFailed = new();
    private Coroutine routine;
    private bool running;
    private bool previousLookEnabled;
    private bool hasLookSnapshot;
    [SerializeField] private GameFlowManager flow;
    private GameFlowStage requestedStage;
    // 행동 시작 시 보관한 진행 버전. 완료 시 현재 단계와 함께 검증한다.
    private int flowRevision;
    /// <summary>
    /// 전환 중이 아닐 때만 다음 이동 목적지를 설정한다.
    /// </summary>
    public void SetDestination(Transform value) { if (!running) destination = value; }
    /// <summary>
    /// 전환 중이 아닐 때만 화면 가림 색 선택을 변경한다.
    /// </summary>
    public void SetWhiteFade(bool value) { if (!running) whiteFade = value; }
    /// <summary>
    /// 필수 참조와 화면 소유권을 확보하고 이동·시선을 잠근 뒤 전환 Coroutine을 시작한다.
    /// </summary>
    public void BeginTransition()
    {
        if (!isActiveAndEnabled || running || playerController == null || movement == null || cameraController == null ||
            destination == null || screenEffects == null || !screenEffects.TryAcquire(this))
        { if (!running) onFailed.Invoke(); return; }
        running = true;
        if (flow != null) { requestedStage = flow.CurrentStage; flowRevision = flow.Revision; }
        playerController.AcquireControlLock(this);
        playerController.AcquireMovementSimulationLock(this);
        previousLookEnabled = cameraController.LookEnabled;
        hasLookSnapshot = true;
        cameraController.SetLookEnabled(false);
        routine = StartCoroutine(Transition());
    }
    /// <summary>
    /// 페이드로 화면을 가리고 지정 위치로 이동한 뒤 화면과 입력을 복구한다.
    /// </summary>
    private IEnumerator Transition()
    {
        yield return screenEffects.Fade(whiteFade ? Color.white : Color.black, 1f);
        if (!running || destination == null || screenEffects == null || !screenEffects.IsReady || !screenEffects.IsRunning) { CancelTransition(); yield break; }
        movement.Teleport(destination.position, destination.rotation);
        cameraController.SynchronizeDirection(destination.rotation);
        yield return screenEffects.Fade(whiteFade ? Color.white : Color.black, 0f);
        if (screenEffects == null || !screenEffects.IsReady || !screenEffects.IsRunning) { CancelTransition(); yield break; }
        routine = null;
        running = false;
        screenEffects.Release(this);
        RestoreControl();
        GameFlowStage stage = requestedStage;
        int revision = flowRevision;
        onCompleted.Invoke();
        if (flow != null) flow.TryCompleteStage(stage, revision, this);
    }
    /// <summary>
    /// 자기 이동·입력 잠금과 보관했던 시선 허용 상태를 복원한다.
    /// </summary>
    private void RestoreControl()
    {
        if (playerController != null)
        {
            playerController.ReleaseMovementSimulationLock(this);
            playerController.ReleaseControlLock(this);
        }
        if (cameraController != null && hasLookSnapshot) cameraController.SetLookEnabled(previousLookEnabled);
        hasLookSnapshot = false;
    }
    /// <summary>
    /// Coroutine과 화면 연출을 취소하고 자기 잠금 및 시선 상태를 정리한다.
    /// </summary>
    public void CancelTransition()
    {
        bool cancelled = running;
        running = false;
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (cancelled && screenEffects != null) screenEffects.CancelEffect();
        if (screenEffects != null) screenEffects.Release(this);
        RestoreControl();
        if (cancelled) onCancelled.Invoke();
    }
    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable() => CancelTransition();
}
