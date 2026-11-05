using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Page1 기상 연출의 순서만 연결한다. 잠금과 1인칭 복구는 기존 CutsceneController가 담당한다.</summary>
public class Page1WakeSequence : MonoBehaviour
{
    [SerializeField] private CutsceneController cutscene;
    [SerializeField] private StoryFirstPersonController firstPerson;
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerCameraController playerCamera;
    [SerializeField] private ScreenEffectController screenEffects;
    [SerializeField] private Transform lyingEyePoint;
    [SerializeField] private bool beginOnStart = true;
    [SerializeField] private OfficialDialoguePlayer officialDialogue;
    [Header("Timeline Signal 연결 지점")]
    [Tooltip("1인칭 PlayerFirstPersonModel용 손 가림 연출. US-Soldier의 State를 연결하지 않는다.")]
    [SerializeField] private UnityEvent onHandCoverRequested = new UnityEvent();
    [Tooltip("3인칭 기상 Clip은 Timeline에서 연결한다. 이 이벤트는 추가 게임 상태 연결용이다.")]
    [SerializeField] private UnityEvent onStandUpRequested = new UnityEvent();
    [SerializeField] private UnityEvent onWakeCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onWakeCancelled = new UnityEvent();
    private Coroutine presentation;
    private int requestId;
    private bool running;
    private bool hasCameraSnapshot;
    private bool ownsEffects;
    private Vector3 eyeLocalPosition;
    private Quaternion eyeLocalRotation;
    private Quaternion thirdPersonRotation;
    public bool IsRunning { get { return running; } }
    public bool HasCompleted { get; private set; }
    public int CompletionCount { get; private set; }
    public string Phase { get; private set; } = "Ready";

    private void OnEnable()
    {
        if (cutscene == null) return;
        cutscene.Completed += Completed;
        cutscene.Cancelled += Cancelled;
    }

    private IEnumerator Start()
    {
        // 다른 플레이어 컴포넌트의 Start 초기화가 끝난 후 소유권을 요청한다.
        yield return null;
        if (beginOnStart) BeginWake();
    }

    public void BeginWake()
    {
        if (running || HasCompleted || cutscene == null || firstPerson == null || player == null ||
            playerCamera == null || lyingEyePoint == null || screenEffects == null || !screenEffects.IsReady ||
            !player.CanControlPlayer || playerCamera.HasTargetControl || !cutscene.CanPlay()) return;
        if (!screenEffects.TryAcquire(this)) return;
        ownsEffects = true;
        Transform eye = playerCamera.FirstPersonTarget;
        eyeLocalPosition = eye.localPosition;
        eyeLocalRotation = eye.localRotation;
        thirdPersonRotation = playerCamera.ThirdPersonTarget.rotation;
        hasCameraSnapshot = true;
        running = true;
        if (!cutscene.TryPlay(this, out requestId))
        {
            Cleanup();
            running = false;
            Phase = "Failed";
            return;
        }
        // 카메라 입력의 소유권을 얻은 뒤에만 천장을 보는 임시 시점을 적용한다.
        eye.SetPositionAndRotation(lyingEyePoint.position, lyingEyePoint.rotation);
        Phase = "Timeline";
    }

    // 시각 연출의 시간은 Page1Wake의 Clip과 Signal이 소유한다.
    public void TimelineHandCover()
    {
        if (!IsCurrentRequest()) return;
        Phase = "HandCoverHook";
        onHandCoverRequested.Invoke();
    }

    public void TimelineStandUp()
    {
        if (!IsCurrentRequest()) return;
        Phase = "StandUpHook";
        onStandUpRequested.Invoke();
    }

    public void TimelineThirdPerson()
    {
        if (!IsCurrentRequest()) return;
        cutscene.ExitStoryFirstPerson();
        Phase = "ThirdPerson";
    }

    public void TimelineMonologue() { BeginTimelineDialogue(0, 2); }
    public void TimelineWife() { BeginTimelineDialogue(2, 1); }
    public void TimelineReply() { BeginTimelineDialogue(3, 1); }

    private void BeginTimelineDialogue(int first, int count)
    {
        if (!IsCurrentRequest() || presentation != null || officialDialogue == null) return;
        cutscene.Director.Pause();
        presentation = StartCoroutine(WaitForDialogue(first, count));
    }

    private IEnumerator WaitForDialogue(int first, int count)
    {
        // 대사 입력 대기만 C#에서 처리한다. 다음 연출 시점은 Signal 위치로 편집한다.
        yield return officialDialogue.Play(1, true, first, count, this);
        presentation = null;
        if (!IsCurrentRequest()) yield break;
        if (!officialDialogue.LastCompleted) { CancelWake(); yield break; }
        cutscene.Director.Resume();
    }

    private bool IsCurrentRequest()
    {
        return running && cutscene != null && cutscene.OwnsRequest(this, requestId);
    }

    private void Completed(CutsceneController source, int id)
    {
        if (!running || source != cutscene || id != requestId) return;
        Cleanup();
        running = false;
        HasCompleted = true;
        CompletionCount++;
        Phase = "Completed";
        onWakeCompleted.Invoke();
    }
    private void Cancelled(CutsceneController source, int id)
    {
        if (!running || source != cutscene || id != requestId) return;
        Cleanup();
        running = false;
        Phase = "Cancelled";
        onWakeCancelled.Invoke();
    }

    private void RestoreEye()
    {
        if (!hasCameraSnapshot || playerCamera == null) return;
        Transform eye = playerCamera.FirstPersonTarget;
        eye.localPosition = eyeLocalPosition;
        eye.localRotation = eyeLocalRotation;
        playerCamera.ThirdPersonTarget.rotation = thirdPersonRotation;
        hasCameraSnapshot = false;
    }

    private void Cleanup()
    {
        if (presentation != null && officialDialogue != null) officialDialogue.CancelOwned(this);
        if (presentation != null) StopCoroutine(presentation);
        presentation = null;
        RestoreEye();
        if (ownsEffects && screenEffects != null)
        {
            // 이 연출이 예약한 전용 화면 효과만 정리한다.
            screenEffects.CancelEffect();
            ownsEffects = false;
        }
    }

    public void CancelWake()
    {
        if (running && cutscene != null) cutscene.StopOwned(this, requestId);
    }

    private void OnDisable()
    {
        CancelWake();
        Cleanup();
        if (cutscene == null) return;
        cutscene.Completed -= Completed;
        cutscene.Cancelled -= Cancelled;
    }
}
