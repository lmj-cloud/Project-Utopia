using System;
using UnityEngine;
using UnityEngine.Playables;
using Unity.Cinemachine;
using Capstone.Direction;

/// <summary>S06 원본 Signal/Timeline/오디오를 실플레이어용으로 받는다. 원본의 더미 이동과 2등 확정 처리는 실행하지 않는다.</summary>
public class Page2S06Adapter : MonoBehaviour
{
    [SerializeField] private DirectionSequence sequence;
    [SerializeField] private OfficialDialoguePlayer officialDialogue;
    [SerializeField] private PlayableDirector director;
    [SerializeField] private CinemachineCamera cinematicCamera;
    [SerializeField] private Transform player;
    [SerializeField] private Transform opponent;
    [SerializeField] private AudioSource crowd;
    [SerializeField] private AudioSource starterSound;
    [SerializeField] private AudioSource approachSound;
    [SerializeField] private AudioSource ringing;
    [SerializeField] private UnityEngine.UI.Image traumaOverlay;
    [SerializeField] private PageTimelineSequence presentation;
    private float crowdVolume;
    private bool running;
    private bool sawPlaying;
    private double maximumTime;
    public int SignalStep { get; private set; }
    public int StartCount { get; private set; }
    public int CompletionCount { get; private set; }
    public event Action Completed;
    public event Action Cancelled;
    public bool IsReady
    {
        get { return isActiveAndEnabled && sequence != null && director != null && director.playableAsset != null &&
            cinematicCamera != null && player != null && opponent != null && starterSound != null &&
            starterSound.clip != null && approachSound != null && approachSound.clip != null && ringing != null && ringing.clip != null; }
    }

    private void OnEnable()
    {
        if (director != null) director.played += OriginalTimelineStarted;
    }

    private void OriginalTimelineStarted(PlayableDirector source)
    {
        // 원본의 선행 White Fade가 끝나 실제 Director가 시작한 프레임에 편집용 Track을 동기화한다.
        if (!running || presentation == null || !presentation.IsRunning) return;
        presentation.Director.time = 0;
        presentation.Director.Evaluate();
    }

    public bool Begin()
    {
        if (!IsReady || running || CompletionCount != 0 || sequence.IsRunning) return false;
        running = true;
        SignalStep = 0;
        maximumTime = 0;
        sawPlaying = false;
        sequence.Play();
        if (!sequence.IsRunning) { running = false; return false; }
        return true;
    }

    public void SequenceStarted()
    {
        if (!running) return;
        StartCount++;
        if (crowd != null) crowdVolume = crowd.volume;
        // 원본 S06은 그대로 실행하고 Page2 전용 Track이 시청각 연출을 담당한다.
        if (presentation == null || !presentation.Begin()) { SequenceCancelled(); return; }
    }

    private void Update()
    {
        if (!running) return;
        if (director.state == PlayState.Playing) sawPlaying = true;
        maximumTime = Math.Max(maximumTime, director.time);
    }

    public void StarterShot() { if (running && SignalStep == 0) { SignalStep = 1; /* 오디오와 발화는 Page2 S06 Presentation Track */ } }
    public void BeginApproach() { if (running && SignalStep == 1) { SignalStep = 2;  } }
    public void BriefHallucination() { if (running && SignalStep == 2) { SignalStep = 3;  } }
    public void DisruptBout() { if (running && SignalStep == 3) { SignalStep = 4; } }
    public void ShowSecondPlace() { if (running && SignalStep == 4) SignalStep = 5; }

    public void SequenceCompleted()
    {
        if (!running) return;
        bool valid = sawPlaying && SignalStep == 5 && maximumTime >= director.playableAsset.duration - .15;
        Cleanup();
        if (!valid)
        {
            Debug.LogError("Page2 S06: Timeline 종료/Signal 순서를 확인하지 못해 진행하지 않습니다.", this);
            Cancelled?.Invoke();
            return;
        }
        CompletionCount++;
        Completed?.Invoke();
    }

    public void SequenceCancelled()
    {
        if (!running) return;
        Cleanup();
        Cancelled?.Invoke();
    }

    public void Cancel()
    {
        if (sequence != null && sequence.IsRunning) sequence.Cancel();
    }

    private void Cleanup()
    {
        running = false;
        if (presentation != null) presentation.StopVisuals();
        if (starterSound != null) starterSound.Stop();
        if (approachSound != null) approachSound.Stop();
        if (ringing != null) ringing.Stop();
        if (crowd != null) crowd.volume = crowdVolume;
        if (traumaOverlay != null) traumaOverlay.color = Color.clear;
    }

    private void OnDisable()
    {
        if (director != null) director.played -= OriginalTimelineStarted;
        Cancel();
        Cleanup();
    }
}

