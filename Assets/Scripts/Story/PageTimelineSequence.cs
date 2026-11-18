using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Unity.Cinemachine;

/// <summary>Page 전용 Timeline의 대화 대기와 완료 인계만 담당한다. 연출 시간은 Track/Clip에 둔다.</summary>
[RequireComponent(typeof(PlayableDirector))]
public class PageTimelineSequence : MonoBehaviour
{
    [Serializable]
    private class DialogueCue
    {
        public int section;
        public int first;
        public int count = int.MaxValue;
        public bool blocksGameplay = true;
        public bool pauseTimeline = true;
        public bool overrideProgressMode;
        public DialogueProgressMode progressMode = DialogueProgressMode.Manual;
        public bool synchronizeCamera;
    }

    [SerializeField] private PlayableDirector director;
    [SerializeField] private OfficialDialoguePlayer dialogue;
    [SerializeField] private DialogueCue[] dialogueCues = new DialogueCue[0];
    [SerializeField, Min(.1f)] private float autoReadDelayMultiplier = 1f;
    [SerializeField] private CanvasGroup[] retainAlphas = new CanvasGroup[0];
    [SerializeField] private Transform[] retainTransforms = new Transform[0];
    [SerializeField] private Transform origin;
    [SerializeField] private Transform referencePose;
    [SerializeField] private Transform follower;
    [SerializeField] private Transform walkingActor;
    [SerializeField, Min(1f)] private float followDistance = 6f;
    [SerializeField] private bool holdFinalPose;
    [Header("Dialogue camera synchronization (optional)")]
    [SerializeField] private CinemachineTrack dialogueCameraTrack;
    [SerializeField] private SignalAsset[] dialogueCameraSignals = new SignalAsset[0];
    [SerializeField, Min(0f)] private float dialogueCameraBlendDuration = .4f;
    [SerializeField] private bool timelineOwnsDialogueCamera;
    private int dialogueCameraOverride = -1;
    private ICinemachineCamera currentDialogueCamera;
    private bool dialoguePresentationBusy;
    public bool IsDialoguePresentationBusy { get { return dialoguePresentationBusy; } }
    private bool waiting;
    private bool active;
    private bool looping;
    private Coroutine waitRoutine;
    private Vector3 lastActorPosition;
    private double lastSafeTime;
    private bool blockedByFollower;
    public bool IsRunning { get { return active; } }
    public bool Completed { get; private set; }
    public PlayableDirector Director { get { return director; } }

    private void Awake()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
    }

    private void OnEnable()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
        director.played += Played;
        director.stopped += Stopped;
    }

    private void Played(PlayableDirector source)
    {
        if (origin != null && referencePose != null)
            origin.SetPositionAndRotation(referencePose.position, Quaternion.Euler(0, referencePose.eulerAngles.y, 0));
        active = true;
        Completed = false;
        blockedByFollower = false;
        lastSafeTime = 0;
        if (walkingActor != null) lastActorPosition = walkingActor.position;
    }

    private void Stopped(PlayableDirector source)
    {
        active = false;
        if (!Completed)
        {
            CancelWait();
            StopAllCoroutines();
            ReleaseDialogueCamera();
        }
    }

    public bool Begin(bool loop = false)
    {
        if (active || director == null || director.playableAsset == null) return false;
        if (origin != null && referencePose != null)
        {
            // 재생 시작 때의 기준 좌표만 바인딩한다. 카메라 궤적은 Animation Clip이 소유한다.
            origin.SetPositionAndRotation(referencePose.position, Quaternion.Euler(0, referencePose.eulerAngles.y, 0));
        }
        looping = loop;
        director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
        director.extrapolationMode = loop ? DirectorWrapMode.Loop : DirectorWrapMode.Hold;
        director.time = 0;
        director.Play();
        return active;
    }

    public IEnumerator Play() => PlaySequence(false);

    public IEnumerator WaitForCompletion() => PlaySequence(true);

    private IEnumerator PlaySequence(bool alreadyStarted)
    {
        if (alreadyStarted ? !active : !Begin()) yield break;
        while (active && (director.time < director.duration || waiting || dialoguePresentationBusy)) yield return null;
        if (!active) yield break;
        Completed = true;
        active = false;
        // Clip 끝과 동일한 시각에는 가중치가 0이 될 수 있다. 마지막 유효 샘플을 인계한다.
        director.time = Math.Max(0, director.duration - .00001d);
        director.Evaluate();
        if (holdFinalPose) director.Pause();
        else FinishGraph();
    }

    private void Update()
    {
        if (!active || waiting || looping || walkingActor == null) return;
        // 장교 동행 거리 조건은 게임 판정이다. 거리 밖에서는 편집된 궤적의 진행만 멈춘다.
        if (blockedByFollower && follower != null)
        {
            blockedByFollower = Vector3.Distance(follower.position, walkingActor.position) < .9f;
        }
        bool shouldWait = blockedByFollower || (follower != null && Vector3.Distance(follower.position, walkingActor.position) > followDistance);
        if (shouldWait && director.state == PlayState.Playing) director.Pause();
        else if (!shouldWait && director.state == PlayState.Paused) director.Resume();
    }

    private void LateUpdate()
    {
        if (!active || waiting || walkingActor == null || director.state != PlayState.Playing) return;
        Vector3 delta = walkingActor.position - lastActorPosition;
        if (delta.sqrMagnitude > .000001f)
        {
            // 궤적은 Timeline이 계산한다. 이 검사는 편집된 경로가 벽/플레이어를 통과하지 않게 막는다.
            foreach (RaycastHit hit in Physics.CapsuleCastAll(lastActorPosition + Vector3.up * .4f,
                lastActorPosition + Vector3.up * 1.4f, .21f, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.enabled || !hit.collider.gameObject.activeInHierarchy) continue;
                if (hit.collider.transform.IsChildOf(walkingActor)) continue;
                if (follower != null && hit.collider.transform.IsChildOf(follower))
                {
                    director.time = lastSafeTime;
                    director.Evaluate();
                    blockedByFollower = true;
                    director.Pause();
                    return;
                }
                Debug.LogError("Timeline NPC 이동 경로 장애물: " + hit.collider.name, this);
                Cancel();
                return;
            }
        }
        lastActorPosition = walkingActor.position;
        lastSafeTime = director.time;
    }

    public void DialogueSignal(int index)
    {
        if (!Application.isPlaying || !active || dialogue == null || index < 0 || index >= dialogueCues.Length) return;
        DialogueCue cue = dialogueCues[index];
        IEnumerator routine = PlayDialogueCue(cue);
        if (cue.pauseTimeline) WaitFor(routine);
        else StartCoroutine(routine);
    }

    private IEnumerator PlayDialogueCue(DialogueCue cue)
    {
        DialogueProgressMode? mode = null;
        if (cue.overrideProgressMode) mode = cue.progressMode;
        if (!cue.synchronizeCamera || dialogueCameraTrack == null)
        {
            yield return dialogue.Play(cue.section, cue.blocksGameplay, cue.first, cue.count, this, mode, autoReadDelayMultiplier: autoReadDelayMultiplier);
            yield break;
        }
        dialoguePresentationBusy = true;
        currentDialogueCamera = CameraAtTime(director.time);
        try
        {
            // 기존 Timeline Shot/Signal을 읽고 같은 Brain의 소유 Override만 사용한다.
            yield return BlendDialogueCamera(CameraForLine(cue.first));
            yield return dialogue.Play(cue.section, cue.blocksGameplay, cue.first, cue.count, this, mode,
                next => BlendDialogueCamera(CameraForLine(cue.first + next)), autoReadDelayMultiplier);
            if (dialogue.LastCompleted) yield return BlendDialogueCamera(CameraAtTime(director.time));
        }
        finally
        {
            ReleaseDialogueCamera();
        }
    }
    private ICinemachineCamera CameraForLine(int line)
    {
        if (line >= 0 && line < dialogueCameraSignals.Length && director.playableAsset is TimelineAsset timeline)
        {
            SignalAsset signal = dialogueCameraSignals[line];
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                foreach (IMarker marker in track.GetMarkers())
                {
                    if (marker is SignalEmitter emitter && emitter.asset == signal)
                        return CameraAtTime(emitter.time);
                }
            }
        }
        return CameraAtTime(director.time);
    }
    private ICinemachineCamera CameraAtTime(double time)
    {
        ICinemachineCamera result = null;
        double start = double.MinValue;
        foreach (TimelineClip clip in dialogueCameraTrack.GetClips())
        {
            if (clip.start <= time && clip.start >= start && clip.asset is CinemachineShot shot)
            {
                CinemachineVirtualCameraBase camera = shot.VirtualCamera.Resolve(director);
                if (camera != null) { result = camera; start = clip.start; }
            }
        }
        return result;
    }
    private IEnumerator BlendDialogueCamera(ICinemachineCamera target)
    {
        CinemachineBrain brain = director.GetGenericBinding(dialogueCameraTrack) as CinemachineBrain;
        if (brain == null || target == null) yield break;
        if (currentDialogueCamera == null) currentDialogueCamera = target;
        float duration = currentDialogueCamera == target ? 0f : dialogueCameraBlendDuration;
        float elapsed = 0f;
        while (active && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float weight = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (!timelineOwnsDialogueCamera)
                dialogueCameraOverride = brain.SetCameraOverride(dialogueCameraOverride, dialogueCameraTrack.TrackPriority + 1,
                    currentDialogueCamera, target, weight, Time.unscaledDeltaTime);
            yield return null;
        }
        if (!active) yield break;
        if (!timelineOwnsDialogueCamera)
            dialogueCameraOverride = brain.SetCameraOverride(dialogueCameraOverride, dialogueCameraTrack.TrackPriority + 1,
                target, target, 1f, Time.unscaledDeltaTime);
        currentDialogueCamera = target;
        // Timeline 카메라를 사용하는 경우에도 기존 문장 전환 대기 시간은 유지한다.
        // Brain LateUpdate에서 최종 Camera가 반영된 다음 문장을 표시한다.
        yield return null;
    }
    private void ReleaseDialogueCamera()
    {
        if (dialogueCameraOverride >= 0 && director != null && dialogueCameraTrack != null)
        {
            CinemachineBrain brain = director.GetGenericBinding(dialogueCameraTrack) as CinemachineBrain;
            if (brain != null) brain.ReleaseCameraOverride(dialogueCameraOverride);
        }
        dialogueCameraOverride = -1;
        currentDialogueCamera = null;
        dialoguePresentationBusy = false;
    }

    public void WaitFor(IEnumerator gameplayCondition)
    {
        if (!Application.isPlaying || !active || waiting) return;
        waiting = true;
        director.Pause();
        waitRoutine = StartCoroutine(WaitAndResume(gameplayCondition));
    }

    private IEnumerator WaitAndResume(IEnumerator condition)
    {
        yield return condition;
        waitRoutine = null;
        waiting = false;
        if (active) director.Resume();
    }

    private void FinishGraph()
    {
        var positions = new Vector3[retainTransforms.Length];
        var rotations = new Quaternion[retainTransforms.Length];
        var alphas = new float[retainAlphas.Length];
        for (int i = 0; i < retainTransforms.Length; i++)
        {
            if (retainTransforms[i] == null) continue;
            positions[i] = retainTransforms[i].position;
            rotations[i] = retainTransforms[i].rotation;
        }
        for (int i = 0; i < retainAlphas.Length; i++)
            if (retainAlphas[i] != null) alphas[i] = retainAlphas[i].alpha;
        director.Stop();
        // Graph가 원래 값으로 되돌린 뒤, 정상 완료 시 최종 상태만 게임에 인계한다.
        for (int i = 0; i < retainTransforms.Length; i++)
            if (retainTransforms[i] != null) retainTransforms[i].SetPositionAndRotation(positions[i], rotations[i]);
        for (int i = 0; i < retainAlphas.Length; i++)
            if (retainAlphas[i] != null) retainAlphas[i].alpha = alphas[i];
    }

    public void StopVisuals()
    {
        // 실제 HP/원본 Timeline 완료로 시각 구간만 끝낸다. 이미 열린 공식 발화는 유지한다.
        Completed = true;
        active = false;
        FinishGraph();
    }

    private void CancelWait()
    {
        if (dialogue != null) dialogue.CancelOwned(this);
        if (waitRoutine != null) StopCoroutine(waitRoutine);
        waitRoutine = null;
        waiting = false;
    }

    public void Cancel()
    {
        CancelWait();
        StopAllCoroutines();
        ReleaseDialogueCamera();
        if (director != null && director.playableGraph.IsValid()) director.Stop();
        active = false;
        Completed = false;
    }

    private void OnDisable()
    {
        Cancel();
        if (director == null) return;
        director.played -= Played;
        director.stopped -= Stopped;
    }
}
