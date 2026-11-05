using UnityEngine;
using System.Collections;
using Capstone.Direction.Workspace.S13;

/// <summary>장교 안내 완료 후 기존 F 상호작용으로 원본 S13만 시작하고 정상 종료를 인계한다.</summary>
public class Page4S13Adapter : MonoBehaviour
{
    [SerializeField] private Page4StoryController story;
    [SerializeField] private InteractableObject exitInteraction;
    [SerializeField] private S13EndingAdapter ending;
    [SerializeField] private OfficialDialoguePlayer officialDialogue;
    [SerializeField] private PageTimelineSequence departureTimeline;
    private bool started;
    private bool completed;
    public int StartCount { get; private set; }
    public int CompletionCount { get; private set; }
    public S13EndingSequence Sequence { get { return ending.target; } }
    private void OnEnable()
    {
        if (ending != null)
        {
            ending.onEndingRequested.AddListener(Finished);
            ending.target.onCancelled.AddListener(Cancelled);
        }
    }
    private void Update()
    {
        if (exitInteraction != null) exitInteraction.enabled = !started && story.CanUseExit;
    }
    public void UseExit()
    {
        if (started || completed || !story.CanUseExit || !exitInteraction.IsExternalInteractionPending ||
            !ending.target.InRange) { exitInteraction.CancelExternalInteraction(); return; }
        if (!story.TryBeginS13()) { exitInteraction.CancelExternalInteraction(); return; }
        started = true;
        if (officialDialogue != null) StartCoroutine(DepartureDialogue());
        else BeginOriginalEnding();
    }
    private IEnumerator DepartureDialogue()
    {
        yield return departureTimeline.Play();
        if (!departureTimeline.Completed || !officialDialogue.LastCompleted) { Cancelled(); yield break; }
        yield return null;
        BeginOriginalEnding();
    }
    private void BeginOriginalEnding()
    {
        if (!isActiveAndEnabled || story.Stage != Page4Stage.S13Starting) { Cancelled(); return; }
        ending.target.enabled = true;
        if (!ending.TryStartFromExit()) { Cancelled(); return; }
        StartCount++;
        story.S13Started();
    }
    private void Finished()
    {
        if (!started || completed || !ending.target.Completed || ending.target.CompletionCount != 1 ||
            !ending.target.WhiteHeld || !ending.endingState.whiteCovered) return;
        completed = true;
        CompletionCount++;
        exitInteraction.CompleteExternalInteraction();
        story.S13Ready();
    }
    public void ReleaseWhite()
    {
        if (completed) ending.ReleaseWhiteAfterHostReady();
    }
    private void Cancelled()
    {
        if (!started || completed) return;
        started = false;
        exitInteraction.CancelExternalInteraction();
        story.S13Cancelled();
    }
    private void OnDisable()
    {
        if (departureTimeline != null) departureTimeline.Cancel();
        if (ending == null) return;
        ending.onEndingRequested.RemoveListener(Finished);
        ending.target.onCancelled.RemoveListener(Cancelled);
        if (ending.target.Active) ending.Cancel();
    }
}
