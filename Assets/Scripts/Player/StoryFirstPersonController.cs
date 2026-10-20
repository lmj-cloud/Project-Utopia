using UnityEngine;
using UnityEngine.Events;

// One first-person presentation session inside an existing owned cutscene request.
public class StoryFirstPersonController : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerCameraController cameraController;
    [SerializeField] private PlayerAnimatorController playerAnimator;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private CutsceneController cutscene;
    [SerializeField] private GameObject firstPersonModel;
    [SerializeField] private GameObject[] firstPersonEffects = new GameObject[0];
    [SerializeField] private PlayerEquipmentKind storyEquipment = PlayerEquipmentKind.AssaultRifle;
    [SerializeField] private bool timelineControlsCamera;
    [SerializeField] private UnityEvent onEntered = new();
    [SerializeField] private UnityEvent onExited = new();
    [SerializeField] private UnityEvent onCancelled = new();
    [SerializeField] private UnityEvent onFailed = new();
    private CutsceneController activeCutscene;
    private CutsceneController resetCutscene;
    private UnityEngine.Object resetOwner;
    private int resetRequestId;
    private UnityEngine.Object requestOwner;
    private int requestId;
    private bool modelWasActive;
    public bool IsActive => activeCutscene != null;
    public bool ReferencesReady => isActiveAndEnabled && playerController != null && playerController.isActiveAndEnabled && playerController.IsAlive &&
        cameraController != null && cameraController.isActiveAndEnabled && !cameraController.IsStoryFirstPerson && cameraController.StoryReferencesReady &&
        playerAnimator != null && playerAnimator.isActiveAndEnabled && playerAnimator.StoryFirstPersonReady &&
        firstPersonModel != null && firstPersonModel != playerController.gameObject &&
        playerAnimator.StoryFirstPersonAnimator.transform.IsChildOf(firstPersonModel.transform) &&
        firstPersonModel.GetComponentsInChildren<Renderer>(true).Length > 0 &&
        (equipment == null || equipment.CanShowStoryEquipment(storyEquipment));
    private void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (cameraController == null) cameraController = GetComponent<PlayerCameraController>();
        if (playerAnimator == null) playerAnimator = GetComponent<PlayerAnimatorController>();
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        ClearEffects();
    }
    public void EnterFirstPerson()
    {
        if (cutscene == null || !TryEnter(cutscene, cutscene.RequestOwner, cutscene.RequestId)) onFailed.Invoke();
    }
    public bool TryEnter(CutsceneController source, UnityEngine.Object owner, int id)
    {
        if (IsActive || !ReferencesReady || source == null || source.Player != playerController || !source.OwnsRequest(owner, id)) return false;
        if (!source.RegisterStoryPresentation(this, owner, id, cameraController)) return false;
        activeCutscene = source;
        requestOwner = owner;
        requestId = id;
        modelWasActive = firstPersonModel.activeSelf;
        ClearEffects();
        firstPersonModel.SetActive(true);
        bool cameraReady = cameraController.TryBeginStoryView(this, timelineControlsCamera);
        bool equipmentReady = cameraReady && (equipment == null || equipment.BeginStoryDisplay(this, storyEquipment));
        bool animationReady = equipmentReady && playerAnimator.BeginStoryFirstPerson(this, source, owner, id);
        if (!animationReady)
        {
            Cleanup(false);
            return false;
        }
        resetCutscene = source;
        resetOwner = owner;
        resetRequestId = id;
        source.Completed += OnCompleted;
        source.Cancelled += OnCancelled;
        onEntered.Invoke();
        return true;
    }
    public bool PlayAnimation(int id, string state, int layer = -1)
    {
        return IsActive && id == requestId && activeCutscene.OwnsRequest(requestOwner, id) &&
            playerAnimator.PlayStoryFirstPerson(this, id, state, layer);
    }
    // For a synchronous Timeline Signal. Delayed callers must retain and pass the captured request id.
    public void PlayAnimation(string state) => PlayAnimation(requestId, state);
    // Animation-only state: this never starts a gameplay reload or changes ammunition.
    public void SetReloading(bool value)
    {
        if (IsActive) playerAnimator.SetStoryFirstPersonBool(this, requestId, "IsReload", value);
    }
    public void ExitFirstPerson() => EndSegment(activeCutscene, requestOwner, requestId);
    public void CancelFirstPerson()
    {
        if (!IsActive) return;
        Cleanup(true);
        onCancelled.Invoke();
    }
    public bool EndSegment(CutsceneController source, UnityEngine.Object owner, int id)
    {
        if (!Matches(source, owner, id)) return false;
        Cleanup(true);
        onExited.Invoke();
        return true;
    }
    public void EndForRequest(CutsceneController source, int id, bool completed, bool notify = true)
    {
        if (!IsActive || activeCutscene != source || requestId != id) return;
        Cleanup(true);
        if (notify) NotifyRequestEnded(source, id, completed);
    }
    internal void NotifyRequestEnded(CutsceneController source, int id, bool completed)
    {
        if (resetCutscene != source || resetRequestId != id) return; // A new session/restart supersedes this presentation notification.
        if (completed) onExited.Invoke(); else onCancelled.Invoke();
    }
    private bool Matches(CutsceneController source, UnityEngine.Object owner, int id) =>
        IsActive && activeCutscene == source && requestOwner == owner && requestId == id;
    private void OnCompleted(CutsceneController source, int id) => EndForRequest(source, id, true);
    private void OnCancelled(CutsceneController source, int id) => EndForRequest(source, id, false);
    private void Cleanup(bool restoreModel)
    {
        CutsceneController source = activeCutscene;
        int id = requestId;
        activeCutscene = null;
        requestOwner = null;
        requestId = 0;
        if (source != null)
        {
            source.Completed -= OnCompleted;
            source.Cancelled -= OnCancelled;
            source.UnregisterStoryPresentation(this, id, restoreModel);
        }
        if (playerAnimator != null) playerAnimator.EndStoryFirstPerson(this, id);
        if (cameraController != null) cameraController.EndStoryView(this);
        if (equipment != null) equipment.EndStoryDisplay(this);
        ClearEffects();
        if (firstPersonModel != null) firstPersonModel.SetActive(modelWasActive);
        // Only presentation is released; the cutscene's control, movement and HUD locks remain owned by its request.
    }
    private void ClearEffects()
    {
        foreach (GameObject effect in firstPersonEffects ?? System.Array.Empty<GameObject>())
            if (effect != null) effect.SetActive(false);
    }
    private void Update()
    {
        if (!IsActive) return;
        if (playerController == null || !playerController.isActiveAndEnabled || !playerController.IsAlive || playerAnimator == null || !playerAnimator.isActiveAndEnabled ||
            cameraController == null || !cameraController.isActiveAndEnabled || (equipment != null && !equipment.isActiveAndEnabled) || !activeCutscene.OwnsRequest(requestOwner, requestId)) CancelFirstPerson();
    }
    public void ResetPresentation()
    {
        CutsceneController source = resetCutscene;
        UnityEngine.Object owner = resetOwner;
        int id = resetRequestId;
        resetCutscene = null;
        resetOwner = null;
        resetRequestId = 0;
        // An explicit restart also cancels this captured request after a first-person segment has already ended.
        if (source != null && source.OwnsRequest(owner, id)) source.StopCutscene();
        CancelFirstPerson();
        if (cameraController != null) cameraController.SetView(true);
        if (equipment != null) equipment.RefreshDisplays();
    }
    private void OnDisable() => CancelFirstPerson();
}
