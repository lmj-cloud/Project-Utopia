using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class SceneTransitionController : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerCameraController cameraController;
    [SerializeField] private ScreenEffectController screenEffects;
    [SerializeField] private Transform destination;
    [SerializeField] private bool whiteFade;
    [SerializeField] private UnityEvent onCompleted = new();
    [SerializeField] private UnityEvent onCancelled = new();
    [SerializeField] private UnityEvent onFailed = new();
    private Coroutine routine;
    private bool running;
    private bool previousLookEnabled;
    private bool hasLookSnapshot;
    [SerializeField] private GameFlowManager flow;
    private GameFlowStage requestedStage;
    private int flowRevision;
    public void SetDestination(Transform value) { if (!running) destination = value; }
    public void SetWhiteFade(bool value) { if (!running) whiteFade = value; }
    public void BeginTransition()
    {
        if (!isActiveAndEnabled || running || playerController == null || movement == null || cameraController == null ||
            destination == null || screenEffects == null || !screenEffects.TryAcquire(this))
        { if (!running) onFailed.Invoke(); return; }
        running = true;
        if (flow != null) { requestedStage = flow.CurrentStage; flowRevision = flow.Revision; }
        playerController.AcquireControlLock(this);
        playerController.AcquireMovementSimulationLock(this);
        previousLookEnabled = cameraController.LookEnabled;
        hasLookSnapshot = true;
        cameraController.SetLookEnabled(false);
        routine = StartCoroutine(Transition());
    }
    private IEnumerator Transition()
    {
        yield return screenEffects.Fade(whiteFade ? Color.white : Color.black, 1f);
        if (!running || destination == null || screenEffects == null || !screenEffects.IsReady || !screenEffects.IsRunning) { CancelTransition(); yield break; }
        movement.Teleport(destination.position, destination.rotation);
        cameraController.SynchronizeDirection(destination.rotation);
        yield return screenEffects.Fade(whiteFade ? Color.white : Color.black, 0f);
        if (screenEffects == null || !screenEffects.IsReady || !screenEffects.IsRunning) { CancelTransition(); yield break; }
        routine = null;
        running = false;
        screenEffects.Release(this);
        RestoreControl();
        GameFlowStage stage = requestedStage;
        int revision = flowRevision;
        onCompleted.Invoke();
        if (flow != null) flow.TryCompleteStage(stage, revision, this);
    }
    private void RestoreControl()
    {
        if (playerController != null)
        {
            playerController.ReleaseMovementSimulationLock(this);
            playerController.ReleaseControlLock(this);
        }
        if (cameraController != null && hasLookSnapshot) cameraController.SetLookEnabled(previousLookEnabled);
        hasLookSnapshot = false;
    }
    public void CancelTransition()
    {
        bool cancelled = running;
        running = false;
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (cancelled && screenEffects != null) screenEffects.CancelEffect();
        if (screenEffects != null) screenEffects.Release(this);
        RestoreControl();
        if (cancelled) onCancelled.Invoke();
    }
    private void OnDisable() => CancelTransition();
}
