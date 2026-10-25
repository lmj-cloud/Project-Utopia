using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

/// <summary>
/// VideoPlayer 준비와 재생, 종료·오류·시간 초과를 감시하고 컷신 입력 잠금을 정리한다.
/// </summary>
public class VideoCutsceneController : MonoBehaviour
{
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField, Min(0.1f)] private float preparationTimeout = 10f;
    [SerializeField] private UnityEvent onFinished = new();
    [SerializeField] private UnityEvent onCancelled = new();
    [SerializeField] private UnityEvent onFailed = new();
    private bool requested;
    [SerializeField] private GameFlowManager flow;
    private GameFlowStage requestedStage;
    // 행동 시작 시 보관한 진행 버전. 완료 시 현재 단계와 함께 검증한다.
    private int flowRevision;
    private bool started;
    private float requestedAt;
    // 비동기 타격·완료 콜백이 현재 행동에 속하는지 구분하는 요청 번호.
    private int requestId;
    private VideoPlayer.EventHandler prepareHandler;
    private VideoPlayer.EventHandler finishHandler;
    private VideoPlayer.ErrorEventHandler errorHandler;
    public bool IsPlaying => requested;
    /// <summary>
    /// 동영상 참조와 현재 실행 상태를 검사하고 준비 이벤트 및 입력 잠금을 연결한다.
    /// </summary>
    public void PlayVideo()
    {
        if (!isActiveAndEnabled || requested || videoPlayer == null || playerController == null ||
            (videoPlayer.source == VideoSource.VideoClip ? videoPlayer.clip == null : string.IsNullOrWhiteSpace(videoPlayer.url)))
        { if (!requested) onFailed.Invoke(); return; }
        requested = true;
        if (flow != null) { requestedStage = flow.CurrentStage; flowRevision = flow.Revision; }
        started = false;
        requestedAt = Time.unscaledTime;
        videoPlayer.isLooping = false;
        int id = ++requestId;
        prepareHandler = source => { if (id == requestId) Prepared(source); };
        finishHandler = source => { if (id == requestId) Finished(source); };
        errorHandler = (source, message) => { if (id == requestId) Failed(source, message); };
        videoPlayer.prepareCompleted += prepareHandler;
        videoPlayer.loopPointReached += finishHandler;
        videoPlayer.errorReceived += errorHandler;
        playerController.AcquireControlLock(this);
        playerController.AcquireMovementSimulationLock(this);
        if (gameplayUI != null) gameplayUI.AcquireHidden(this);
        videoPlayer.Prepare();
    }
    /// <summary>
    /// 동영상 준비 완료를 받아 현재 요청의 재생을 시작한다.
    /// </summary>
    private void Prepared(VideoPlayer source)
    {
        if (!requested || started || source != videoPlayer || !source.isPrepared) return;
        started = true;
        source.Play();
    }
    /// <summary>
    /// 동영상의 정상 종료를 정리하고 완료 이벤트를 전달한다.
    /// </summary>
    private void Finished(VideoPlayer source)
    {
        if (!requested || !started || source != videoPlayer) return;
        GameFlowStage stage = requestedStage;
        int revision = flowRevision;
        Cleanup();
        onFinished.Invoke();
        if (flow != null) flow.TryCompleteStage(stage, revision, this);
    }
    /// <summary>
    /// 동영상 오류를 정리하고 실패 이벤트를 전달한다.
    /// </summary>
    private void Failed(VideoPlayer source, string message)
    {
        if (!requested || source != videoPlayer) return;
        Debug.LogWarning("Video cutscene failed: " + message, this);
        Cleanup();
        onFailed.Invoke();
    }
    /// <summary>
    /// 매 프레임 현재 상태와 입력을 확인하여 이 컴포넌트의 동작을 갱신한다.
    /// </summary>
    private void Update()
    {
        if (requested && !started && Time.unscaledTime - requestedAt >= preparationTimeout)
        {
            Cleanup();
            onFailed.Invoke();
        }
    }
    /// <summary>
    /// 이 컴포넌트가 소유한 연출 연결과 표시·입력 상태를 정리한다.
    /// </summary>
    private void Cleanup()
    {
        requested = false;
        started = false;
        requestId++;
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= prepareHandler;
            videoPlayer.loopPointReached -= finishHandler;
            videoPlayer.errorReceived -= errorHandler;
            videoPlayer.Stop();
        }
        if (playerController != null)
        {
            playerController.ReleaseMovementSimulationLock(this);
            playerController.ReleaseControlLock(this);
        }
        if (gameplayUI != null) gameplayUI.ReleaseHidden(this);
    }
    /// <summary>
    /// 동영상 재생과 이벤트 연결을 취소하고 보관한 입력·표시 상태를 복구한다.
    /// </summary>
    public void CancelVideo()
    {
        bool cancelled = requested;
        Cleanup();
        if (cancelled) onCancelled.Invoke();
    }
    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable() => CancelVideo();
}
