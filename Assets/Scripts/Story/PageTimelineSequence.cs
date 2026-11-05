using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;

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
    }

    [SerializeField] private PlayableDirector director;
    [SerializeField] private OfficialDialoguePlayer dialogue;
    [SerializeField] private DialogueCue[] dialogueCues = new DialogueCue[0];
    [SerializeField] private CanvasGroup[] retainAlphas = new CanvasGroup[0];
    [SerializeField] private Transform[] retainTransforms = new Transform[0];
    [SerializeField] private Transform origin;
    [SerializeField] private Transform referencePose;
    [SerializeField] private Transform follower;
    [SerializeField] private Transform walkingActor;
    [SerializeField, Min(1f)] private float followDistance = 6f;
    [SerializeField] private bool holdFinalPose;
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
        if (!Completed && waiting) CancelWait();
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

    public IEnumerator Play()
    {
        if (!Begin()) yield break;
        while (active && (director.time < director.duration || waiting)) yield return null;
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
        IEnumerator routine = dialogue.Play(cue.section, cue.blocksGameplay, cue.first, cue.count, this);
        if (cue.pauseTimeline) WaitFor(routine);
        else StartCoroutine(routine);
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
