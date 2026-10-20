using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using System;
using System.Collections.Generic;

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
    public bool OwnsRequest(UnityEngine.Object owner, int id) => isPlaying && !requestEnding && !completionPending && !normalCompletionRequested &&
        owner != null && lockOwner == owner && requestId == id;
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
    public void EnterStoryFirstPerson()
    {
        if (!isPlaying || storyFirstPerson == null || !storyFirstPerson.TryEnter(this, lockOwner, requestId)) onFailed.Invoke();
    }
    public void ExitStoryFirstPerson()
    {
        if (activeStoryPresentation != null) activeStoryPresentation.EndSegment(this, lockOwner, requestId);
    }
    private UnityEngine.Object completionOwner;
    private UnityEngine.Object lockOwner;
    private int requestId;
    private Action<PlayableDirector> stoppedHandler;
    private bool completionPending;
    private Vector3 finalPlayerPosition;
    private Quaternion finalPlayerRotation;
    private readonly List<TargetPose> finalCameraPoses = new();
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
    private int flowRevision;
    private bool normalCompletionRequested;

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

    private void OnEnable()
    {
        if (director == null) return;
        director.played += OnPlayed;

    }

    [ContextMenu("Cutscene/Play")]
    public void PlayCutscene()
    {
        TryPlay(null, out _);
    }

    [ContextMenu("Cutscene/Cancel")]
    public void StopCutscene()
    {
        if (!isPlaying || requestEnding) return;
        normalCompletionRequested = false;
        completionPending = false;
        if (director != null && director.playableGraph.IsValid()) director.Stop();
        if (isPlaying) FinishCancelled();
    }

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

    private void OnStopped(PlayableDirector source)
    {
        if (!isPlaying || source != director) return;
        if (normalCompletionRequested) completionPending = true;
        else FinishCancelled();
    }

    private void OnDisable()
    {
        if (director != null)
        {
            director.played -= OnPlayed;
            if (stoppedHandler != null) director.stopped -= stoppedHandler;
        }
        StopCutscene();
    }
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
    public void StopOwned(UnityEngine.Object owner, int id)
    {
        if (isPlaying && completionOwner == owner && requestId == id) StopCutscene();
    }
    private void CaptureCamera(Transform target)
    {
        if (target != null) finalCameraPoses.Add(new TargetPose { target = target, position = target.localPosition,
            rotation = target.localRotation, scale = target.localScale });
    }
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
