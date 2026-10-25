using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System;
using System.Collections.Generic;

/// <summary>
/// PlayableDirector 재생 요청의 소유자와 번호를 관리하며 카메라, 입력 잠금과 종료·취소 복구를 조율한다.
/// </summary>
[RequireComponent(typeof(PlayableDirector))]
public class CutsceneController : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private UnityEvent onFailed = new();
    [SerializeField] private UnityEvent onFinished = new();
    [SerializeField] private UnityEvent onCancelled = new();

    [SerializeField] private bool controlsPlayerPosition = true;

    [SerializeField] private PlayableDirector director;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerCameraController cameraController;
    [SerializeField] private bool controlsCameraTargets;
    [SerializeField] private UnityEngine.Object[] optionalBindings = new UnityEngine.Object[0];
    [SerializeField] private string[] requiredExposedReferences = new string[0];
    [Header("Optional Story First Person")]
    [SerializeField] private StoryFirstPersonController storyFirstPerson;
    [SerializeField] private bool beginInFirstPerson;
    private StoryFirstPersonController activeStoryPresentation;
    private bool storyCameraControl;
    private bool requestEnding;
    private bool presentationCameraLeaseAdded;
    public PlayerController Player => playerController;
    public UnityEngine.Object RequestOwner => lockOwner;
    /// <summary>
    /// 현재 실행 중 요청의 소유자와 번호가 일치하는지 확인한다.
    /// </summary>
    public bool OwnsRequest(UnityEngine.Object owner, int id) => isPlaying && !requestEnding && !completionPending && !normalCompletionRequested &&
        owner != null && lockOwner == owner && requestId == id;
    /// <summary>
    /// 재생 중 Timeline이 해당 Animator를 제어하고 있는지 확인한다.
    /// </summary>
    public bool ControlsAnimator(Animator target)
    {
        if (target == null || director == null || director.playableAsset == null) return false;
        foreach (PlayableBinding output in director.playableAsset.outputs)
        {
            if (output.sourceObject == null || output.outputTargetType == null || !typeof(Animator).IsAssignableFrom(output.outputTargetType)) continue;
            UnityEngine.Object binding = director.GetGenericBinding(output.sourceObject);
            if (binding == target || binding == target.gameObject) return true;
        }
        return false;
    }
    /// <summary>
    /// 요청과 일치하는 1인칭 표시 담당자를 현재 컷신에 등록한다.
    /// </summary>
    internal bool RegisterStoryPresentation(StoryFirstPersonController presentation, UnityEngine.Object owner, int id, PlayerCameraController camera)
    {
        if (presentation == null || !OwnsRequest(owner, id) || camera == null || camera != cameraController || !camera.CanStartStoryView(this) ||
            (activeStoryPresentation != null && activeStoryPresentation != presentation)) return false;
        activeStoryPresentation = presentation;
        presentationCameraLeaseAdded = !storyCameraControl && !controlsCameraTargets;
        storyCameraControl = true;
        cameraController.AcquireTargetControl(this);
        return true;
    }
    /// <summary>
    /// 등록한 표시 담당자의 연결을 해제한다.
    /// </summary>
    internal void UnregisterStoryPresentation(StoryFirstPersonController presentation, int id, bool keepCameraControl = true)
    {
        if (requestId != id || activeStoryPresentation != presentation) return;
        activeStoryPresentation = null;
        if (!keepCameraControl && presentationCameraLeaseAdded)
        {
            storyCameraControl = false;
            if (cameraController != null) cameraController.ReleaseTargetControl(this, true);
        }
        presentationCameraLeaseAdded = false;
        // The camera lease lasts for this cutscene, including a later third-person segment.
    }
    /// <summary>
    /// 현재 컷신 요청으로 1인칭 표시 구간 진입을 전달한다.
    /// </summary>
    public void EnterStoryFirstPerson()
    {
        if (!isPlaying || storyFirstPerson == null || !storyFirstPerson.TryEnter(this, lockOwner, requestId)) onFailed.Invoke();
    }
    /// <summary>
    /// 현재 요청의 1인칭 표시 구간 종료를 전달한다.
    /// </summary>
    public void ExitStoryFirstPerson()
    {
        if (activeStoryPresentation != null) activeStoryPresentation.EndSegment(this, lockOwner, requestId);
    }
    private UnityEngine.Object completionOwner;
    private UnityEngine.Object lockOwner;
    // 비동기 타격·완료 콜백이 현재 행동에 속하는지 구분하는 요청 번호.
    private int requestId;
    private Action<PlayableDirector> stoppedHandler;
    private bool completionPending;
    private Vector3 finalPlayerPosition;
    private Quaternion finalPlayerRotation;
    private readonly List<TargetPose> finalCameraPoses = new();
    /// <summary>
    /// 컷신 전의 Transform 위치·회전을 보관하여 종료 후 복원할 수 있게 한다.
    /// </summary>
    private struct TargetPose { public Transform target; public Vector3 position; public Quaternion rotation; public Vector3 scale; }
    public int RequestId => requestId;
    public bool IsPlaying => isPlaying;
    public PlayableDirector Director => director;
    public event Action<CutsceneController, int> Finishing;
    public event Action<CutsceneController, int> Completed;
    public event Action<CutsceneController, int> Cancelled;
    private bool isPlaying;
    [SerializeField] private GameFlowManager flow;
    private GameFlowStage requestedStage;
    // 행동 시작 시 보관한 진행 버전. 완료 시 현재 단계와 함께 검증한다.
    private int flowRevision;
    private bool normalCompletionRequested;

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (director == null) director = GetComponent<PlayableDirector>();
        if (playerController != null)
        {
            if (movement == null) movement = playerController.GetComponent<PlayerMovement>();
            if (cameraController == null) cameraController = playerController.GetComponent<PlayerCameraController>();
        }
        if (director == null) return;
        director.playOnAwake = false;
        director.extrapolationMode = DirectorWrapMode.Hold;
    }

    /// <summary>
    /// 컴포넌트 활성화 시 필요한 알림과 초기 표시를 연결한다.
    /// </summary>
    private void OnEnable()
    {
        if (director == null) return;
        director.played += OnPlayed;

    }

    /// <summary>
    /// 기존 호출을 현재 컷신 요청 검사와 실행 경로로 전달한다.
    /// </summary>
    [ContextMenu("Cutscene/Play")]
    public void PlayCutscene()
    {
        TryPlay(null, out _);
    }

    /// <summary>
    /// 현재 실행 컷신을 정지하고 취소·복구 경로를 처리한다.
    /// </summary>
    [ContextMenu("Cutscene/Cancel")]
    public void StopCutscene()
    {
        if (!isPlaying || requestEnding) return;
        normalCompletionRequested = false;
        completionPending = false;
        if (director != null && director.playableGraph.IsValid()) director.Stop();
        if (isPlaying) FinishCancelled();
    }

    /// <summary>
    /// 매 프레임 현재 상태와 입력을 확인하여 이 컴포넌트의 동작을 갱신한다.
    /// </summary>
    private void Update()
    {
        if (isPlaying && (playerController == null || !playerController.isActiveAndEnabled || !playerController.IsAlive)) { StopCutscene(); return; }
        if (!isPlaying || completionPending || director == null || !director.playableGraph.IsValid()) return;
        double duration = director.duration;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0 || director.time < duration) return;
        normalCompletionRequested = true;
        if (controlsPlayerPosition)
        {
            finalPlayerPosition = playerController.transform.position;
            finalPlayerRotation = playerController.transform.rotation;
        }
        finalCameraPoses.Clear();
        if (controlsCameraTargets || storyCameraControl)
        {
            CaptureCamera(cameraController.FirstPersonTarget);
            CaptureCamera(cameraController.ThirdPersonTarget);
        }
        Finishing?.Invoke(this, requestId);
        director.Stop();
    }

    /// <summary>
    /// Director 재생 알림에서 현재 요청과 제어 상태를 연결한다.
    /// </summary>
    private void OnPlayed(PlayableDirector source)
    {
        if (source != director) return;
        if (requestEnding) { source.Stop(); return; }
        if (isPlaying) return;
        // Direct Director.Play calls must meet the same asset/binding checks.
        if (!CanPlay()) { source.Stop(); onFailed.Invoke(); return; }
        BeginRequest(null);
        if (beginInFirstPerson && !storyFirstPerson.TryEnter(this, lockOwner, requestId)) { StopCutscene(); onFailed.Invoke(); return; }
        source.extrapolationMode = DirectorWrapMode.Hold;
    }

    /// <summary>
    /// Director 정지 원인을 확인하여 정상 완료 또는 취소 경로로 분기한다.
    /// </summary>
    private void OnStopped(PlayableDirector source)
    {
        if (!isPlaying || source != director) return;
        if (normalCompletionRequested) completionPending = true;
        else FinishCancelled();
    }

    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (director != null)
        {
            director.played -= OnPlayed;
            if (stoppedHandler != null) director.stopped -= stoppedHandler;
        }
        StopCutscene();
    }
    /// <summary>
    /// Director와 PlayableAsset, 실행 상태 및 필수 제어 참조가 컷신 재생을 허용하는지 검사한다.
    /// </summary>
    public bool CanPlay()
    {
        if (!isActiveAndEnabled || director == null || !director.isActiveAndEnabled || playerController == null || !playerController.isActiveAndEnabled || !playerController.IsAlive ||
            !(director.playableAsset is TimelineAsset) || (controlsPlayerPosition && movement == null) ||
            (controlsCameraTargets && (cameraController == null || !cameraController.isActiveAndEnabled))) return false;
        if (beginInFirstPerson && (storyFirstPerson == null || !storyFirstPerson.ReferencesReady)) return false;
        double duration = director.playableAsset.duration;
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration <= 0) return false;
        foreach (PlayableBinding output in director.playableAsset.outputs)
        {
            if (output.sourceObject == null || output.outputTargetType == null) continue;
            bool optional = false;
            foreach (UnityEngine.Object binding in optionalBindings ?? Array.Empty<UnityEngine.Object>())
                if (binding == output.sourceObject) { optional = true; break; }
            if (!optional && director.GetGenericBinding(output.sourceObject) == null) return false;
        }
        foreach (string name in requiredExposedReferences ?? Array.Empty<string>())
            if (!string.IsNullOrWhiteSpace(name) &&
                (director.GetReferenceValue(new PropertyName(name), out bool valid) == null || !valid)) return false;
        return true;
    }
    /// <summary>
    /// 기존 실행과 참조·소유권을 검사하여 요청 번호가 있는 컷신 재생을 시작한다.
    /// </summary>
    public bool TryPlay(UnityEngine.Object owner, out int id)
    {
        id = 0;
        if (isPlaying || requestEnding) return false;
        if (!CanPlay()) { if (owner == null) onFailed.Invoke(); return false; }
        BeginRequest(owner);
        id = requestId;
        director.extrapolationMode = DirectorWrapMode.Hold;
        director.time = 0;
        // Enable the optional story Animator before Timeline builds its animation outputs.
        if (beginInFirstPerson && !storyFirstPerson.TryEnter(this, lockOwner, requestId)) { StopCutscene(); onFailed.Invoke(); return false; }
        if (!isPlaying || requestId != id) return false;
        director.Play();
        return isPlaying && requestId == id;
    }
    /// <summary>
    /// 요청 번호와 소유자를 기록하고 입력·이동·카메라 상태를 보관하여 컷신 제어를 시작한다.
    /// </summary>
    private void BeginRequest(UnityEngine.Object owner)
    {
        requestId++;
        storyCameraControl = false;
        requestEnding = false;
        activeStoryPresentation = null;
        isPlaying = true;
        int id = requestId;
        stoppedHandler = source => { if (id == requestId) OnStopped(source); };
        director.stopped += stoppedHandler;
        completionOwner = owner;
        lockOwner = owner != null ? owner : this;
        normalCompletionRequested = false;
        completionPending = false;
        if (flow != null) { requestedStage = flow.CurrentStage; flowRevision = flow.Revision; }
        playerController.AcquireControlLock(lockOwner);
        if (controlsPlayerPosition) playerController.AcquireMovementSimulationLock(lockOwner);
        if (controlsCameraTargets) cameraController.AcquireTargetControl(this);
        if (gameplayUI != null) gameplayUI.AcquireHidden(this);
    }
    /// <summary>
    /// 같은 소유자의 현재 요청일 때만 Director 정지를 허용한다.
    /// </summary>
    public void StopOwned(UnityEngine.Object owner, int id)
    {
        if (isPlaying && completionOwner == owner && requestId == id) StopCutscene();
    }
    /// <summary>
    /// 컷신 이전 카메라 우선순위와 활성 상태를 보관한다.
    /// </summary>
    private void CaptureCamera(Transform target)
    {
        if (target != null) finalCameraPoses.Add(new TargetPose { target = target, position = target.localPosition,
            rotation = target.localRotation, scale = target.localScale });
    }
    /// <summary>
    /// 일반 Update 이후 최종 상태에 맞추어 표시나 자세를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (!completionPending || !isPlaying) return;
        requestEnding = true;
        // Apply final poses after Stop has torn down the Timeline graph, before releasing control.
        if (controlsPlayerPosition) movement.Teleport(finalPlayerPosition, finalPlayerRotation);
        foreach (TargetPose pose in finalCameraPoses)
            if (pose.target != null) { pose.target.localPosition = pose.position; pose.target.localRotation = pose.rotation; pose.target.localScale = pose.scale; }
        int id = requestId;
        GameFlowStage stage = requestedStage;
        int revision = flowRevision;
        bool standalone = completionOwner == null;
        StoryFirstPersonController presentation = activeStoryPresentation;
        if (presentation != null) presentation.EndForRequest(this, id, normalCompletionRequested, false);
        isPlaying = false;
        completionPending = false;
        normalCompletionRequested = false;
        RestoreControl(true);
        Completed?.Invoke(this, id);
        if (presentation != null) presentation.NotifyRequestEnded(this, id, true);
        if (standalone)
        {
            onFinished.Invoke();
            if (flow != null) flow.TryCompleteStage(stage, revision, this);
        }
    }
    /// <summary>
    /// 취소할 요청 정보를 보관하고 소유 제어를 복구한 뒤 취소 알림을 전달한다.
    /// </summary>
    private void FinishCancelled()
    {
        if (!isPlaying || requestEnding) return;
        requestEnding = true;
        int id = requestId;
        bool standalone = completionOwner == null;
        StoryFirstPersonController presentation = activeStoryPresentation;
        if (presentation != null) presentation.EndForRequest(this, id, normalCompletionRequested, false);
        isPlaying = false;
        completionPending = false;
        normalCompletionRequested = false;
        RestoreControl(false);
        Cancelled?.Invoke(this, id);
        if (presentation != null) presentation.NotifyRequestEnded(this, id, false);
        if (standalone) onCancelled.Invoke();
    }
    /// <summary>
    /// 컷신이 보관한 제어·시점 상태와 자기 소유 입력·이동 잠금을 복구한다.
    /// </summary>
    private void RestoreControl(bool completed)
    {
        if (director != null && stoppedHandler != null) director.stopped -= stoppedHandler;
        stoppedHandler = null;
        if (playerController != null)
        {
            playerController.ReleaseMovementSimulationLock(lockOwner);
            playerController.ReleaseControlLock(lockOwner);
        }
        if ((controlsCameraTargets || storyCameraControl) && cameraController != null)
            cameraController.ReleaseTargetControl(this, completed || storyCameraControl);
        storyCameraControl = false;
        activeStoryPresentation = null;
        if (gameplayUI != null) gameplayUI.ReleaseHidden(this);
        completionOwner = null;
        lockOwner = null;
        finalCameraPoses.Clear();
        requestEnding = false;
    }

}
