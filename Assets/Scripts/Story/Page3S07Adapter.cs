using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Capstone.Direction.Workspace.S06;
using Capstone.Direction.Workspace.S07;

/// <summary>원본 S07 표시 리그와 실제 플레이어를 분리하고, 정상 완료 후 같은 집의 침대로 인계한다.</summary>
public class Page3S07Adapter : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerCameraController playerCamera;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private S07AwardSequence sequence;
    [SerializeField] private Transform wakeSpawn;
    [SerializeField] private UnityEngine.Playables.PlayableDirector wakePose;
    [SerializeField] private ScreenEffectController effects;
    [SerializeField] private UnityEvent onCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onCancelled = new UnityEvent();
    public bool IsPlaying { get; private set; }
    public bool Completed { get; private set; }
    public int CompletionCount { get; private set; }
    public S07AwardSequence Sequence { get { return sequence; } }
    [SerializeField] private PageTimelineSequence revealTimeline;
    [SerializeField] private PageTimelineSequence homeArrivalTimeline;
    private Renderer[] renderers;
    private bool[] rendering;
    private bool ownsEffects;
    private bool owned;
    private void OnEnable()
    {
        if (sequence == null) return;
        sequence.onCompleted.AddListener(Finished);
        sequence.onCancelled.AddListener(Cancelled);
    }
    public bool TryPlay()
    {
        if (owned || Completed || sequence == null || wakeSpawn == null || !effects.IsReady) return false;
        owned = true;
        player.AcquireControlLock(this);
        player.AcquireMovementSimulationLock(this);
        playerCamera.AcquireTargetControl(this);
        gameplayUI.AcquireHidden(this);
        renderers = player.GetComponentsInChildren<Renderer>(true);
        rendering = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            rendering[i] = renderers[i].forceRenderingOff;
            renderers[i].forceRenderingOff = true;
        }
        sequence.enabled = true;
        IsPlaying = sequence.AcceptAward(new S06AwardState { placing = 2, intentionalFirstPerson = true,
            firstTraumaExperienced = true, medalTransformationPending = true });
        if (!IsPlaying) { Restore(); return false; }
        StartCoroutine(Reveal());
        return true;
    }
    private IEnumerator Reveal()
    {
        while (sequence.Active && sequence.Playhead <= 0) yield return null;
        if (!sequence.Active) yield break;
        if (!effects.TryAcquire(this)) { sequence.Cancel(); yield break; }
        ownsEffects = true;
        yield return revealTimeline.Play();
        effects.Release(this);
        ownsEffects = false;
    }
    private void Finished()
    {
        if (!owned || !IsPlaying || !sequence.Completed || sequence.CompletionCount != 1) return;
        // DirectionSequence의 카메라 복구 직후에도 마지막 시점을 검정까지 유지한다.
        sequence.sequence.cinematicCamera.Priority = 100;
        StartCoroutine(ArriveHome());
    }
    private IEnumerator ArriveHome()
    {
        if (!effects.TryAcquire(this)) { Cancelled(); yield break; }
        ownsEffects = true;
        yield return homeArrivalTimeline.Play();
        if (!homeArrivalTimeline.Completed) { Cancelled(); yield break; }
        effects.Release(this);
        ownsEffects = false;
        IsPlaying = false;
        Completed = true;
        CompletionCount++;
        Restore();
        onCompleted.Invoke();
    }
    public void TimelineHomeArrived()
    {
        if (!Application.isPlaying || !owned || !IsPlaying) return;
        movement.Teleport(wakeSpawn.position, wakeSpawn.rotation);
        if (wakePose != null) { wakePose.Play(); wakePose.Evaluate(); }
        player.GetComponent<PlayerStatus>().RestoreFullStatus();
        playerCamera.SynchronizeDirection(wakeSpawn.rotation);
        sequence.hands.SetActive(false);
        sequence.thirdPersonBody.SetActive(false);
        sequence.sequence.cinematicCamera.Priority = 0;
        sequence.sequence.cinematicCamera.enabled = false;
        sequence.enabled = false;
        RestoreRenderers();
    }

    private void Cancelled()
    {
        if (!owned || Completed) return;
        StopAllCoroutines();
        if (revealTimeline != null) revealTimeline.Cancel();
        if (homeArrivalTimeline != null) homeArrivalTimeline.Cancel();
        IsPlaying = false;
        Restore();
        onCancelled.Invoke();
    }
    private void RestoreRenderers()
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].forceRenderingOff = rendering[i];
        renderers = null;
    }
    private void Restore()
    {
        if (!owned) return;
        owned = false;
        if (ownsEffects) effects.Release(this);
        ownsEffects = false;
        RestoreRenderers();
        if (player != null) { player.ReleaseMovementSimulationLock(this); player.ReleaseControlLock(this); }
        if (playerCamera != null) playerCamera.ReleaseTargetControl(this, false);
        if (gameplayUI != null) gameplayUI.ReleaseHidden(this);
        if (sequence != null && sequence.sequence.cinematicCamera != null) sequence.sequence.cinematicCamera.Priority = 0;
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        if (sequence != null)
        {
            sequence.onCompleted.RemoveListener(Finished);
            sequence.onCancelled.RemoveListener(Cancelled);
            if (sequence.Active) sequence.Cancel();
        }
        Restore();
    }
}
