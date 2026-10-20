using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class DoorController : MonoBehaviour
{
    [SerializeField] private StoryProgress storyProgress;
    [SerializeField] private TutorialManager tutorial;
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
    private int flowRevision;
    private bool opened;
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
    private void Awake()
    {
        if (doorLeaf != null)
        {
            closedRotation = doorLeaf.localRotation;
            closedPosition = doorLeaf.localPosition;
            closedScale = doorLeaf.localScale;
        }
    }
    public void OpenDoor() => TryOpen();
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
    private void OnTimelineFinishing(CutsceneController source, int id)
    {
        if (!opening || source != openingTimeline || id != timelineId || doorLeaf == null) return;
        openedPosition = doorLeaf.localPosition;
        openedRotation = doorLeaf.localRotation;
        openedScale = doorLeaf.localScale;
        finalPoseCaptured = true;
    }
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
    private void OnTimelineCancelled(CutsceneController source, int id)
    {
        if (opening && source == openingTimeline && id == timelineId) CancelOpen();
    }
    private void DetachTimeline()
    {
        if (openingTimeline == null) return;
        openingTimeline.Finishing -= OnTimelineFinishing;
        openingTimeline.Completed -= OnTimelineCompleted;
        openingTimeline.Cancelled -= OnTimelineCancelled;
    }
    private void RestoreClosedPose()
    {
        if (doorLeaf == null) return;
        doorLeaf.localPosition = closedPosition;
        doorLeaf.localRotation = closedRotation;
        doorLeaf.localScale = closedScale;
    }
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
    public void CompleteOpenFromAnimation(int id) { if (timing == ActionTimingMode.AnimationEvent) CompleteOpen(id); }
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
    public void ResetDoor() { CancelOpen(); opened = false; finalPoseCaptured = false; RestoreClosedPose(); }
    private void OnDisable() => CancelOpen();
}
