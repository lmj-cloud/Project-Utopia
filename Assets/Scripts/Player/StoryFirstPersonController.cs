using UnityEngine;
using UnityEngine.Events;

// One first-person presentation session inside an existing owned cutscene request.
/// <summary>
/// 소유권이 확인된 컷신 요청에만 1인칭 표시를 허용하고 종료 시 장비·카메라·효과를 정리한다.
/// </summary>
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
    // 비동기 타격·완료 콜백이 현재 행동에 속하는지 구분하는 요청 번호.
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
    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (playerController == null) playerController = GetComponent<PlayerController>();
        if (cameraController == null) cameraController = GetComponent<PlayerCameraController>();
        if (playerAnimator == null) playerAnimator = GetComponent<PlayerAnimatorController>();
        if (equipment == null) equipment = GetComponent<PlayerEquipment>();
        ClearEffects();
    }
    /// <summary>
    /// 현재 컷신을 통해 1인칭 연출 구간 진입을 요청한다.
    /// </summary>
    public void EnterFirstPerson()
    {
        if (cutscene == null || !TryEnter(cutscene, cutscene.RequestOwner, cutscene.RequestId)) onFailed.Invoke();
    }
    /// <summary>
    /// 컷신 요청 소유권과 1인칭 참조·장비 표시 준비 여부를 검사하여 구간을 시작한다.
    /// </summary>
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
    /// <summary>
    /// 현재 컷신 소유 요청에 연결된 1인칭 상태 재생을 요청한다.
    /// </summary>
    public bool PlayAnimation(int id, string state, int layer = -1)
    {
        return IsActive && id == requestId && activeCutscene.OwnsRequest(requestOwner, id) &&
            playerAnimator.PlayStoryFirstPerson(this, id, state, layer);
    }
    // For a synchronous Timeline Signal. Delayed callers must retain and pass the captured request id.
    /// <summary>
    /// 현재 컷신 소유 요청에 연결된 1인칭 상태 재생을 요청한다.
    /// </summary>
    public void PlayAnimation(string state) => PlayAnimation(requestId, state);
    // Animation-only state: this never starts a gameplay reload or changes ammunition.
    /// <summary>
    /// 현재 1인칭 컷신 소유권을 통해 재장전 파라미터 변경을 요청한다.
    /// </summary>
    public void SetReloading(bool value)
    {
        if (IsActive) playerAnimator.SetStoryFirstPersonBool(this, requestId, "IsReload", value);
    }
    /// <summary>
    /// 정상적인 1인칭 연출 구간 종료를 요청한다.
    /// </summary>
    public void ExitFirstPerson() => EndSegment(activeCutscene, requestOwner, requestId);
    /// <summary>
    /// 실행 중인 1인칭 연출 구간을 취소 정리한다.
    /// </summary>
    public void CancelFirstPerson()
    {
        if (!IsActive) return;
        Cleanup(true);
        onCancelled.Invoke();
    }
    /// <summary>
    /// 현재 구간의 요청 정보에 맞추어 1인칭 표시와 소유권을 정리한다.
    /// </summary>
    public bool EndSegment(CutsceneController source, UnityEngine.Object owner, int id)
    {
        if (!Matches(source, owner, id)) return false;
        Cleanup(true);
        onExited.Invoke();
        return true;
    }
    /// <summary>
    /// 지정된 소유자와 번호에 해당하는 구간만 종료한다.
    /// </summary>
    public void EndForRequest(CutsceneController source, int id, bool completed, bool notify = true)
    {
        if (!IsActive || activeCutscene != source || requestId != id) return;
        Cleanup(true);
        if (notify) NotifyRequestEnded(source, id, completed);
    }
    /// <summary>
    /// 컷신 요청이 끝났음을 확인하고 현재 표시 구간을 정리한다.
    /// </summary>
    internal void NotifyRequestEnded(CutsceneController source, int id, bool completed)
    {
        if (resetCutscene != source || resetRequestId != id) return; // A new session/restart supersedes this presentation notification.
        if (completed) onExited.Invoke(); else onCancelled.Invoke();
    }
    /// <summary>
    /// 현재 저장된 컷신 소유자와 요청 번호가 전달된 값과 일치하는지 확인한다.
    /// </summary>
    private bool Matches(CutsceneController source, UnityEngine.Object owner, int id) =>
        IsActive && activeCutscene == source && requestOwner == owner && requestId == id;
    /// <summary>
    /// 현재 컷신의 완료 알림에 맞추어 1인칭 구간을 정리한다.
    /// </summary>
    private void OnCompleted(CutsceneController source, int id) => EndForRequest(source, id, true);
    /// <summary>
    /// 현재 컷신의 취소 알림에 맞추어 1인칭 구간을 정리한다.
    /// </summary>
    private void OnCancelled(CutsceneController source, int id) => EndForRequest(source, id, false);
    /// <summary>
    /// 이 컴포넌트가 소유한 연출 연결과 표시·입력 상태를 정리한다.
    /// </summary>
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
    /// <summary>
    /// 연출 Volume의 효과 가중치를 초기화한다.
    /// </summary>
    private void ClearEffects()
    {
        foreach (GameObject effect in firstPersonEffects ?? System.Array.Empty<GameObject>())
            if (effect != null) effect.SetActive(false);
    }
    /// <summary>
    /// 매 프레임 현재 상태와 입력을 확인하여 이 컴포넌트의 동작을 갱신한다.
    /// </summary>
    private void Update()
    {
        if (!IsActive) return;
        if (playerController == null || !playerController.isActiveAndEnabled || !playerController.IsAlive || playerAnimator == null || !playerAnimator.isActiveAndEnabled ||
            cameraController == null || !cameraController.isActiveAndEnabled || (equipment != null && !equipment.isActiveAndEnabled) || !activeCutscene.OwnsRequest(requestOwner, requestId)) CancelFirstPerson();
    }
    /// <summary>
    /// 남아 있는 표시 동작을 정리하고 보관한 초기 표시 상태로 복구한다.
    /// </summary>
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
    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable() => CancelFirstPerson();
}
