using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 상호작용으로 문 열기를 요청하고 Timeline 또는 완료 이벤트와 입력·진행 상태를 조율한다.
/// </summary>
public class DoorController : MonoBehaviour
{
    [SerializeField] private StoryProgress storyProgress;
    [SerializeField] private TutorialManager tutorial;
    // 아이템 사용이나 진행의 선행 조건으로 확인할 스토리 플래그.
    [SerializeField] private string requiredFlag;
    [SerializeField] private Transform doorLeaf;
    [SerializeField, HideInInspector] private Vector3 openLocalEuler;
    [SerializeField, HideInInspector] private Animator animator;
    [SerializeField, HideInInspector] private string openState;
    [SerializeField, HideInInspector] private string closedState;
    [SerializeField, HideInInspector] private int animationLayer;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openSound;
    [SerializeField, HideInInspector] private ActionTimingMode timing;
    [SerializeField, HideInInspector] private float openDuration;
    [SerializeField, HideInInspector] private float eventTimeout;
    [SerializeField] private UnityEvent onOpenRequested = new();
    [SerializeField] private UnityEvent onOpened = new();
    [SerializeField] private UnityEvent onCancelled = new();
    [SerializeField] private UnityEvent onFailed = new();
    private bool opening;
    [SerializeField] private GameFlowManager flow;
    private GameFlowStage requestedStage;
    // 행동 시작 시 보관한 진행 버전. 완료 시 현재 단계와 함께 검증한다.
    private int flowRevision;
    private bool opened;
    // 비동기 타격·완료 콜백이 현재 행동에 속하는지 구분하는 요청 번호.
    private int requestId;
    [SerializeField] private CutsceneController openingTimeline;
    private int timelineId;
    private bool timelineConfirmed;
    private bool finalPoseCaptured;
    private Vector3 closedPosition;
    private Vector3 closedScale;
    private Vector3 openedPosition;
    private Quaternion openedRotation;
    private Vector3 openedScale;
    private Quaternion closedRotation;
    public int RequestId => requestId;
    public bool IsOpen => opened;
    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (doorLeaf != null)
        {
            closedRotation = doorLeaf.localRotation;
            closedPosition = doorLeaf.localPosition;
            closedScale = doorLeaf.localScale;
        }
    }
    /// <summary>
    /// 기존 문 열기 요청을 수행한다. 실제 회전 Coroutine 또는 연결된 완료 경로를 사용한다.
    /// </summary>
    public void OpenDoor() => TryOpen();
    /// <summary>
    /// 중복 실행과 연결 조건을 검사하여 문 열기 또는 컷신을 요청한다.
    /// </summary>
    public bool TryOpen()
    {
        if (!isActiveAndEnabled || opening || opened) return false;
        if ((!string.IsNullOrWhiteSpace(requiredFlag) && (storyProgress == null || !storyProgress.HasFlag(requiredFlag))) ||
            doorLeaf == null || openingTimeline == null || openingTimeline.IsPlaying || !openingTimeline.CanPlay())
        { onFailed.Invoke(); return false; }
        opening = true;
        finalPoseCaptured = false;
        requestId++;
        if (flow != null) { requestedStage = flow.CurrentStage; flowRevision = flow.Revision; }
        openingTimeline.Finishing += OnTimelineFinishing;
        openingTimeline.Completed += OnTimelineCompleted;
        openingTimeline.Cancelled += OnTimelineCancelled;
        if (!openingTimeline.TryPlay(this, out timelineId))
        {
            opening = false;
            DetachTimeline();
            onFailed.Invoke();
            return false;
        }
        if (audioSource != null && openSound != null) audioSource.PlayOneShot(openSound);
        onOpenRequested.Invoke();
        return opening;
    }
    /// <summary>
    /// 문 연출의 종료 확정 직전에 유효한 요청인지 확인한다.
    /// </summary>
    private void OnTimelineFinishing(CutsceneController source, int id)
    {
        if (!opening || source != openingTimeline || id != timelineId || doorLeaf == null) return;
        openedPosition = doorLeaf.localPosition;
        openedRotation = doorLeaf.localRotation;
        openedScale = doorLeaf.localScale;
        finalPoseCaptured = true;
    }
    /// <summary>
    /// 연결된 컷신 완료가 현재 요청과 일치할 때만 후속 완료를 처리한다.
    /// </summary>
    private void OnTimelineCompleted(CutsceneController source, int id)
    {
        if (!opening || source != openingTimeline || id != timelineId) return;
        if (!finalPoseCaptured || doorLeaf == null) { CancelOpen(); return; }
        // Director.Stop can restore bindings; reapply the pose captured at the real end.
        doorLeaf.localPosition = openedPosition;
        doorLeaf.localRotation = openedRotation;
        doorLeaf.localScale = openedScale;
        timelineConfirmed = true;
        try { CompleteOpen(requestId); }
        finally { timelineConfirmed = false; }
    }
    /// <summary>
    /// 현재 요청에 해당하는 컷신 취소를 받아 진행 중 행동을 정리한다.
    /// </summary>
    private void OnTimelineCancelled(CutsceneController source, int id)
    {
        if (opening && source == openingTimeline && id == timelineId) CancelOpen();
    }
    /// <summary>
    /// 기존 Timeline 완료·취소 알림 구독을 해제한다.
    /// </summary>
    private void DetachTimeline()
    {
        if (openingTimeline == null) return;
        openingTimeline.Finishing -= OnTimelineFinishing;
        openingTimeline.Completed -= OnTimelineCompleted;
        openingTimeline.Cancelled -= OnTimelineCancelled;
    }
    /// <summary>
    /// 시작 시 보관한 문 닫힘 자세로 복구한다.
    /// </summary>
    private void RestoreClosedPose()
    {
        if (doorLeaf == null) return;
        doorLeaf.localPosition = closedPosition;
        doorLeaf.localRotation = closedRotation;
        doorLeaf.localScale = closedScale;
    }
    /// <summary>
    /// 문 열림 상태를 확정하고 상호작용 및 진행 완료를 전달한다.
    /// </summary>
    public void CompleteOpen(int id)
    {
        if (!opening || id != requestId || !timelineConfirmed) return;
        opening = false;
        opened = true;
        requestId++;
        DetachTimeline();
        GameFlowStage stage = requestedStage;
        int revision = flowRevision;
        if (tutorial != null) tutorial.OnDoorOpened(this);
        onOpened.Invoke();
        if (flow != null) flow.TryCompleteStage(stage, revision, this);
    }
    /// <summary>
    /// 현재 요청 번호와 일치하는 문 열기 완료 이벤트만 처리한다.
    /// </summary>
    public void CompleteOpenFromAnimation(int id) { if (timing == ActionTimingMode.AnimationEvent) CompleteOpen(id); }
    /// <summary>
    /// 문 열기 요청과 이벤트 연결을 취소하고 원래 자세·상호작용 상태를 복구한다.
    /// </summary>
    public void CancelOpen()
    {
        if (!opening) return;
        int id = timelineId;
        opening = false;
        finalPoseCaptured = false;
        requestId++;
        DetachTimeline();
        if (openingTimeline != null) openingTimeline.StopOwned(this, id);
        RestoreClosedPose();
        if (audioSource != null) audioSource.Stop();
        onCancelled.Invoke();
    }
    /// <summary>
    /// 진행 중 문 동작을 취소하고 초기 닫힘 상태로 되돌린다.
    /// </summary>
    public void ResetDoor() { CancelOpen(); opened = false; finalPoseCaptured = false; RestoreClosedPose(); }
    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable() => CancelOpen();
}
