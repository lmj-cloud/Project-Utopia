using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Video;

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
    private int flowRevision;
    private bool started;
    private float requestedAt;
    private int requestId;
    private VideoPlayer.EventHandler prepareHandler;
    private VideoPlayer.EventHandler finishHandler;
    private VideoPlayer.ErrorEventHandler errorHandler;
    public bool IsPlaying => requested;
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
    private void Prepared(VideoPlayer source)
    {
        if (!requested || started || source != videoPlayer || !source.isPrepared) return;
        started = true;
        source.Play();
    }
    private void Finished(VideoPlayer source)
    {
        if (!requested || !started || source != videoPlayer) return;
        GameFlowStage stage = requestedStage;
        int revision = flowRevision;
        Cleanup();
        onFinished.Invoke();
        if (flow != null) flow.TryCompleteStage(stage, revision, this);
    }
    private void Failed(VideoPlayer source, string message)
    {
        if (!requested || source != videoPlayer) return;
        Debug.LogWarning("Video cutscene failed: " + message, this);
        Cleanup();
        onFailed.Invoke();
    }
    private void Update()
    {
        if (requested && !started && Time.unscaledTime - requestedAt >= preparationTimeout)
        {
            Cleanup();
            onFailed.Invoke();
        }
    }
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
    public void CancelVideo()
    {
        bool cancelled = requested;
        Cleanup();
        if (cancelled) onCancelled.Invoke();
    }
    private void OnDisable() => CancelVideo();
}
