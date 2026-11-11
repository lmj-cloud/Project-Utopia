using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
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
    [Header("Page3 소파·회상 Timeline 표시")]
    [SerializeField] private bool useSofaPresentation;
    [SerializeField] private CanvasGroup blackOverlay;
    [Header("암전 중 현실 / 회상 복장 전환")]
    [SerializeField] private Renderer[] memoryUniformGear = new Renderer[0];
    [SerializeField] private Renderer casualShirt;
    [SerializeField] private Material casualShirtMaterial;
    [SerializeField] private Material militaryShirtMaterial;
    private bool warCovered;
    private bool realityCovered;
    private bool finishCueReceived;
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
            if (!useSofaPresentation) renderers[i].forceRenderingOff = true;
        }
        if (useSofaPresentation)
        {
            // 같은 S07 Director/DirectionSequence를 재사용한다. 공간·포즈·Fade는 편집된 Track이 소유한다.
            if (blackOverlay == null || !effects.TryAcquire(this)) { Restore(); return false; }
            ownsEffects = true;
            sequence.enabled = false;
            sequence.sequence.director.timeUpdateMode = UnityEngine.Playables.DirectorUpdateMode.UnscaledGameTime;
            sequence.sequence.Play();
            IsPlaying = sequence.sequence.IsRunning;
            if (!IsPlaying) Restore();
            return IsPlaying;
        }
        sequence.enabled = true;
        IsPlaying = sequence.AcceptAward(new S06AwardState { placing = 2, intentionalFirstPerson = true,
            firstTraumaExperienced = true, medalTransformationPending = true });
        if (!IsPlaying) { Restore(); return false; }
        StartCoroutine(Reveal());
        return true;
    }
    public void SofaPresentationStarted()
    {
        if (!Application.isPlaying || !useSofaPresentation || !owned) return;
        warCovered = false;
        realityCovered = false;
        finishCueReceived = false;
        sequence.hands.SetActive(false);
        sequence.thirdPersonBody.SetActive(false);
    }

    public void TimelineWarCovered()
    {
        if (!Application.isPlaying || !useSofaPresentation || !owned) return;
        // Signal은 완전 암전 키 구간에 배치한다. 알림 시점의 Alpha는 이전 프레임 값일 수 있다.
        // 느린 프레임에서도 공간 전환 전에 덮개를 확보하고, 이후 Fade는 같은 Timeline이 계속 소유한다.
        blackOverlay.alpha = 1f;
        SetMemoryUniform(true);
        warCovered = true;
    }

    public void TimelineRealityCovered()
    {
        if (!Application.isPlaying || !useSofaPresentation || !owned) return;
        if (!warCovered)
        {
            CancelSofaPresentation();
            return;
        }
        blackOverlay.alpha = 1f;
        SetMemoryUniform(false);
        realityCovered = true;
        // 본체와 장비는 삭제하지 않고 1인칭 구간 동안만 원래 Renderer 상태를 보관해 가린다.
        if (renderers != null)
            foreach (Renderer item in renderers) if (item != null) item.forceRenderingOff = true;
    }

    public void TimelineSofaFinished()
    {
        if (!Application.isPlaying || !useSofaPresentation || !owned || !warCovered || !realityCovered) return;
        finishCueReceived = true;
        sequence.sequence.director.Stop();
    }

    public void SofaPresentationCompleted()
    {
        if (!Application.isPlaying || !useSofaPresentation || !owned) return;
        if (!finishCueReceived) { CancelSofaPresentation(); return; }
        // 원래 Page3 복귀 Fade/안전 이동/착석 Director와 완료 이벤트로 인계한다.
        sequence.sequence.cinematicCamera.Priority = 100;
        blackOverlay.alpha = 0;
        effects.Release(this);
        ownsEffects = false;
        StartCoroutine(ArriveHome());
    }

    public void CancelSofaPresentation()
    {
        if (!Application.isPlaying) return;
        if (sequence != null && sequence.Active) sequence.sequence.Cancel();
        else Cancelled();
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
        if (wakePose != null)
        {
            wakePose.Play();
            wakePose.time = 0;
            // 소파 자세를 계속 평가하되 상체 복용 구간으로 자동 진행하지 않는다.
            var graph = wakePose.playableGraph;
            for (int i = 0; i < graph.GetRootPlayableCount(); i++) graph.GetRootPlayable(i).SetSpeed(0d);
            wakePose.Evaluate();
        }
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
    // 기존 완전 암전 Signal에서만 메시 표시와 상의 재질을 바꾼다. 뼈대와 장비 슬롯은 유지한다.
    private void SetMemoryUniform(bool isMemory)
    {
        foreach (Renderer gear in memoryUniformGear)
        {
            if (gear != null)
            {
                gear.gameObject.SetActive(isMemory);
                gear.enabled = isMemory;
            }
        }
        Material shirtMaterial = isMemory ? militaryShirtMaterial : casualShirtMaterial;
        if (casualShirt != null && shirtMaterial != null) casualShirt.sharedMaterial = shirtMaterial;
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
        SetMemoryUniform(false);
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

