using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

public enum Page3Stage { Loading, S07Playing, WakeUp, MedicineTutorial, MedicineUsing, Blackout, WifeDialogue, WalkToCar, VehicleReady, Driving, Failed, SeatedAfterBlackout, WifeLeavingRoom, PlayerStandingUp, WalkToCarTutorial, VehicleBoarding, S10Accident, FadeToBlack, Completed }

/// <summary>Page3는 같은 집의 기상·기존 약 사용·침대 대화·퇴장·운전을 기존 시스템에 연결한다.</summary>
public class Page3StoryController : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerCameraController playerCamera;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private Page3S07Adapter memory;
    [SerializeField] private Page3TutorialBridge tutorialBridge;
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private ItemUseController itemUse;
    [SerializeField] private NPCDialogue wife;
    [SerializeField] private VehicleController vehicle;
    [SerializeField] private ScreenEffectController effects;
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField] private Unity.Cinemachine.CinemachineCamera medicineCamera;
    [SerializeField] private TMPro.TMP_Text objective;
    [SerializeField] private UnityEvent onMedicineUseStarted = new UnityEvent();
    [SerializeField] private UnityEvent onMedicineAnimationRequested = new UnityEvent();
    [SerializeField] private UnityEvent onMedicineUseCompleted = new UnityEvent();
    [SerializeField] private Page3Stage stage;
    [SerializeField] private OfficialDialoguePlayer officialDialogue;
    [SerializeField] private GameObject familyChildren;
    [SerializeField] private GameObject vehicleFamily;
    [SerializeField] private bool hideFamilyAfterExit = true;
    [SerializeField] private Transform son;
    [SerializeField] private Transform daughter;
    [SerializeField] private Transform sonExitEnd;
    [SerializeField] private Transform daughterExitEnd;
    public bool WifeExitCompleted { get; private set; }
    public bool SonExitCompleted { get; private set; }
    public bool DaughterExitCompleted { get; private set; }
    private bool drivingDialogueStarted;
    private bool drivingDialogueCompleted;
    private bool completingAccident;
    public Page3Stage Stage { get { return stage; } }
    public bool MemoryStarted { get { return memory != null && memory.IsPlaying; } }
    public int MedicineCompletionCount { get; private set; }
    public int DialogueCompletionCount { get; private set; }
    [Header("Page3 사고·다음 페이지 인계")]
    [SerializeField] private UnityEvent onAccidentStarted = new UnityEvent();
    [SerializeField] private UnityEvent onS10Completed = new UnityEvent();
    [SerializeField] private UnityEvent onPage3Completed = new UnityEvent();
    [SerializeField] private UnityEvent onPage4TransitionRequested = new UnityEvent();
    public UnityEvent OnPage4TransitionRequested { get { return onPage4TransitionRequested; } }
    public int AccidentStartCount { get; private set; }
    public int Page3CompletionCount { get; private set; }
    public bool CanStartAccident { get { return stage == Page3Stage.Driving && vehicle.IsOccupied && vehicle.Driver == player &&
        (officialDialogue == null || (drivingDialogueCompleted && !officialDialogue.IsBusy)); } }
    public float BlackAlpha { get { return blackOverlay.alpha; } }
    [Header("Page3 침대 대화")]
    [SerializeField] private PlayableDirector seatedDirector;
    [SerializeField, Min(0f)] private float medicinePoseStart = 1f;
    [SerializeField] private CutsceneController standUp;
    [SerializeField] private Transform seatedSpawn;
    [SerializeField] private Transform standingSpawn;
    [SerializeField] private Unity.Cinemachine.CinemachineCamera conversationCamera;
    [SerializeField] private Animator wifeAnimator;
    [SerializeField] private Transform[] wifeExitWaypoints;
    [SerializeField, Min(.1f)] private float wifeWalkSpeed = 1.15f;
    [SerializeField, Min(1f)] private float wifeTurnSpeed = 240f;
    [SerializeField] private LayerMask wifeObstacleMask = ~0;
    public int WifeExitCompletionCount { get; private set; }
    public int StandCompletionCount { get; private set; }
    public bool VehicleTutorialCompleted { get { return tutorial.IsCompleted && vehicle.IsOccupied && vehicle.Driver == player; } }
    [Header("Page3 편집용 Timeline")]
    [SerializeField] private PageTimelineSequence sofaWakeTimeline;
    [SerializeField] private PageTimelineSequence afterMedicineTimeline;
    [SerializeField] private PageTimelineSequence blackoutTimeline;
    [SerializeField] private PageTimelineSequence wifeArrivalTimeline;
    [SerializeField] private PageTimelineSequence familyDialogueTimeline;
    [SerializeField] private PageTimelineSequence wifeExitTimeline;
    [SerializeField] private PageTimelineSequence vehicleDialogueTimeline;
    private bool ownsEffects;
    private int standRequest;
    private bool standFinished;
    private bool standCancelled;
    private void OnEnable()
    {
        if (standUp != null) { standUp.Completed += StandFinished; standUp.Cancelled += StandCancelled; }
    }
    private void StandFinished(CutsceneController source, int id)
    {
        if (stage == Page3Stage.PlayerStandingUp && source == standUp && id == standRequest) standFinished = true;
    }
    private void StandCancelled(CutsceneController source, int id)
    {
        if (stage == Page3Stage.PlayerStandingUp && source == standUp && id == standRequest) standCancelled = true;
    }
    private void Awake()
    {
        stage = Page3Stage.Loading;
        // HUD Awake의 원래 Alpha 캡처를 방해하지 않는다.
        player.AcquireControlLock(this);
        player.AcquireMovementSimulationLock(this);
        playerCamera.AcquireTargetControl(this);
        blackOverlay.alpha = 1;
        if (officialDialogue != null)
        {
            // 아내는 Timeline 경로의 화면 밖 시작점에서 대기한다. 등장 때 활성화하지 않는다.
            wife.gameObject.SetActive(true);
            if (familyChildren != null) familyChildren.SetActive(true);
            if (vehicleFamily != null) vehicleFamily.SetActive(false);
        }
    }
    private IEnumerator Start()
    {
        yield return null;
        player.GetComponent<PlayerStatus>().RestoreFullStatus();
        player.GetComponent<PlayerEquipment>().RestoreEquipment(PlayerEquipmentKind.BareHands);
        if (Page2Page3Transition.Active != null)
        {
            if (!Page2Page3Transition.Active.NotifyReady(this)) Fail("Page2 인계 요청 불일치");
        }
        else AcceptArrival();
    }
    public void AcceptArrival()
    {
        if (stage != Page3Stage.Loading) return;
        stage = Page3Stage.S07Playing;
        if (!memory.TryPlay()) { Fail("S07 시작 조건을 확인하세요."); return; }
        Release();
    }
    public void MemoryCompleted()
    {
        if (stage != Page3Stage.S07Playing || !memory.Completed) return;
        stage = Page3Stage.WakeUp;
        if (officialDialogue != null) StartCoroutine(SofaAwakening());
        else { stage = Page3Stage.MedicineTutorial; tutorialBridge.Begin(); }
    }
    private IEnumerator SofaAwakening()
    {
        // 이동만 잠가 앉은 상태의 F/인벤토리/퀵슬롯 사용은 기존 입력 경로로 유지한다.
        player.AcquireMovementSimulationLock(this);
        playerCamera.AcquireTargetControl(this);
        ResumeSeatedPose();
        conversationCamera.Priority = 110;
        yield return sofaWakeTimeline.Play();
        if (!officialDialogue.LastCompleted) { Fail("소파 기상 대화 취소"); yield break; }
        stage = Page3Stage.MedicineTutorial;
        tutorialBridge.Begin();
    }
    public void MemoryCancelled() { Fail("S07 취소. 다음 단계로 진행하지 않습니다."); }
    public void MedicineUseStarted()
    {
        if (stage != Page3Stage.MedicineTutorial || !itemUse.IsUsing) return;
        stage = Page3Stage.MedicineUsing;
        if (officialDialogue != null)
        {
            // 같은 Sitting 그래프의 상체 Eat 구간만 진행한다. 복용 Director는 카메라와 소비 완료를 담당한다.
            seatedDirector.time = medicinePoseStart;
            SetSeatedSpeed(1d);
            seatedDirector.Evaluate();
            conversationCamera.Priority = 0;
        }
        // 약 복용 카메라는 기존 ItemUse Timeline의 Shot/Transform Track이 담당한다.
        // 잠금은 ItemUseController가 요청한 기존 Cutscene에 맡긴다.
        onMedicineUseStarted.Invoke();
        onMedicineAnimationRequested.Invoke();
    }
    public void MedicineCancelled()
    {
        if (stage != Page3Stage.MedicineUsing) return;
        medicineCamera.Priority = 0;
        stage = Page3Stage.MedicineTutorial;
        if (officialDialogue != null) { ResumeSeatedPose(); conversationCamera.Priority = 110; }
    }
    public void MedicineCompleted()
    {
        if (stage != Page3Stage.MedicineUsing || itemUse.IsUsing || MedicineCompletionCount != 0) return;
        MedicineCompletionCount++;
        medicineCamera.Priority = 0;
        if (officialDialogue != null) { ResumeSeatedPose(); conversationCamera.Priority = 110; }
        onMedicineUseCompleted.Invoke();
        if (officialDialogue != null) StartCoroutine(AfterMedicine());
        else StartCoroutine(Blackout());
    }
    private IEnumerator AfterMedicine()
    {
        yield return afterMedicineTimeline.Play();
        if (!officialDialogue.LastCompleted) { Fail("약 복용 후 대화 취소"); yield break; }
        // 성공한 복용 이력을 기존 튜토리얼 UI가 처리한 뒤에 암전한다. 숨긴 UI 완료를 기다리는 교착을 막는다.
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!tutorialBridge.MedicineTutorialCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (!tutorialBridge.MedicineTutorialCompleted) { Fail("약 튜토리얼 완료를 확인하지 못했습니다."); yield break; }
        yield return Blackout();
    }
    private IEnumerator Blackout()
    {
        stage = Page3Stage.Blackout;
        Block();
        if (!effects.TryAcquire(this)) { Fail("암전 소유권 실패"); yield break; }
        ownsEffects = true;
        yield return blackoutTimeline.Play();
        if (!blackoutTimeline.Completed) { Fail("가족 등장 Timeline 취소"); yield break; }
        effects.Release(this);
        ownsEffects = false;
        // 입력 잠금은 아내 퇴장과 일어서기 완료까지 유지한다. HUD만 대화에 넘긴다.
        gameplayUI.ReleaseHidden(this);
        float deadline = Time.realtimeSinceStartup + 15;
        while (!tutorialBridge.MedicineTutorialCompleted && Time.realtimeSinceStartup < deadline) yield return null;
        if (!tutorialBridge.MedicineTutorialCompleted) { Fail("약 튜토리얼 완료를 확인하지 못했습니다."); yield break; }
        // 경로와 Walk/Idle은 Timeline이 소유한다. 도착 완료 후에만 기존 대화 Signal을 실행한다.
        if (wifeArrivalTimeline == null) { Fail("아내 등장 Timeline 참조 누락"); yield break; }
        yield return wifeArrivalTimeline.Play();
        if (!wifeArrivalTimeline.Completed) { Fail("아내 등장 Timeline 취소"); yield break; }
        stage = Page3Stage.WifeDialogue;
        yield return familyDialogueTimeline.Play();
        if (!familyDialogueTimeline.Completed || DialogueCompletionCount != 1) { Fail("가족 대화 Timeline 취소"); yield break; }
        yield return LeaveRoomAndStand();
    }

    public void TimelineFamilyArrived()
    {
        if (!Application.isPlaying || stage != Page3Stage.Blackout) return;
        // 안전 이동과 현재 상태 인계만 처리한다. Fade와 등장 시점은 Track이 소유한다.
        player.GetComponent<PlayerMovement>().Teleport(seatedSpawn.position, seatedSpawn.rotation);
        playerCamera.SynchronizeDirection(seatedSpawn.rotation);
        ResumeSeatedPose();
        stage = Page3Stage.SeatedAfterBlackout;
    }

    public void TimelineFamilyDialogue()
    {
        if (!Application.isPlaying || stage != Page3Stage.WifeDialogue) return;
        familyDialogueTimeline.WaitFor(WaitForWifeDialogue());
    }

    private IEnumerator WaitForWifeDialogue()
    {
        wife.enabled = true;
        while (!wife.CanStartDialogue) yield return null;
        wife.StartDialogue();
        if (objective != null) objective.text = "Enter로 아내와의 대화를 진행하세요.";
        while (DialogueCompletionCount == 0) yield return null;
    }

    public void WifeDialogueCompleted()
    {
        if (stage != Page3Stage.WifeDialogue || DialogueCompletionCount != 0) return;
        DialogueCompletionCount++;
        // 실제 대화 완료를 Timeline 대기 루틴이 확인한 뒤 퇴장을 시작한다.
    }
    private IEnumerator LeaveRoomAndStand()
    {
        stage = Page3Stage.WifeLeavingRoom;
        if (objective != null) objective.text = "아내가 방을 나갈 때까지 기다리세요.";
        wife.enabled = false;
        if (wifeWalkSpeed <= 0 || wifeTurnSpeed <= 0) { Fail("퇴장 초기 설정이 유효하지 않습니다."); yield break; }
        yield return wifeExitTimeline.Play();
        if (!wifeExitTimeline.Completed)
        { Fail("가족 퇴장 Timeline이 완료되지 않았습니다."); yield break; }
        // 각자의 월드 종점 도착을 확인한 뒤 해당 NPC만 숨긴다. 부모와 Binding은 유지한다.
        Transform wifeExitEnd = wifeExitWaypoints.Length > 0 ? wifeExitWaypoints[wifeExitWaypoints.Length - 1] : null;
        WifeExitCompleted = CompleteFamilyExit(wife.transform, wifeExitEnd);
        SonExitCompleted = CompleteFamilyExit(son, sonExitEnd);
        DaughterExitCompleted = CompleteFamilyExit(daughter, daughterExitEnd);
        if (!WifeExitCompleted || !SonExitCompleted || !DaughterExitCompleted)
        { Fail("가족 3명의 실제 퇴장 종점 도착을 확인하지 못했습니다."); yield break; }
        WifeExitCompletionCount++;
        stage = Page3Stage.PlayerStandingUp;
        if (objective != null) objective.text = officialDialogue != null ? "소파에서 일어나는 중입니다." : "침대에서 일어나는 중입니다.";
        seatedDirector.Stop();
        if (!standUp.TryPlay(this, out standRequest)) { Fail("일어서기 Timeline 시작 실패"); yield break; }
        while (!standFinished && !standCancelled) yield return null;
        if (standCancelled) { Fail("일어서기 연출 취소"); yield break; }
        StandCompletionCount++;
        player.GetComponent<PlayerMovement>().Teleport(standingSpawn.position, standingSpawn.rotation);
        playerCamera.SynchronizeDirection(standingSpawn.rotation);
        conversationCamera.Priority = 0;
        Release();
        stage = Page3Stage.WalkToCarTutorial;
        vehicle.enabled = true;
        tutorialBridge.BeginVehicleTutorial();
    }
    private void ResumeSeatedPose()
    {
        // 약 상체 연출의 완료와 기상을 분리한다. 기존 Sitting 그래프가 가족 퇴장까지 자세를 소유한다.
        seatedDirector.extrapolationMode = DirectorWrapMode.Hold;
        if (!seatedDirector.playableGraph.IsValid()) seatedDirector.Play();
        seatedDirector.time = 0;
        SetSeatedSpeed(0d);
        seatedDirector.Evaluate();
    }
    private void SetSeatedSpeed(double speed)
    {
        // Pause는 Animator의 기본 Controller가 포즈를 다시 쓸 수 있다. Playing 그래프를 0배속으로 평가한다.
        var graph = seatedDirector.playableGraph;
        for (int i = 0; i < graph.GetRootPlayableCount(); i++) graph.GetRootPlayable(i).SetSpeed(speed);
    }
    private bool CompleteFamilyExit(Transform actor, Transform end)
    {
        if (actor == null || end == null || Vector3.Distance(actor.position, end.position) > .1f) return false;
        if (hideFamilyAfterExit) actor.gameObject.SetActive(false);
        return true;
    }
    private void Update()
    {
        if (stage != Page3Stage.WalkToCarTutorial && stage != Page3Stage.VehicleBoarding && stage != Page3Stage.Driving) return;
        if (vehicle.IsOccupied && vehicle.Driver == player)
        {
            stage = Page3Stage.Driving;
            if (officialDialogue != null && !drivingDialogueStarted)
            {
                drivingDialogueStarted = true;
                StartCoroutine(DrivingDialogue());
            }
            if (objective != null) objective.text = "앞쪽 길을 따라 직진하세요.";
        }
        else
        {
            stage = tutorialBridge.VehicleInReach ? Page3Stage.VehicleBoarding : Page3Stage.WalkToCarTutorial;
            if (objective != null) objective.text = stage == Page3Stage.VehicleBoarding ? "F키를 눌러 차량에 탑승하세요." : "차량으로 이동하세요.";
        }
    }
    private IEnumerator DrivingDialogue()
    {
        // 차량 카메라 전환은 기존 VehicleController가 끝냈다. 기존 Activation Track으로 좌석 가족만 표시한다.
        if (!WifeExitCompleted || !SonExitCompleted || !DaughterExitCompleted)
        { Fail("집 가족 퇴장이 완료되지 않아 차량 가족을 표시하지 않습니다."); yield break; }
        yield return vehicleDialogueTimeline.Play();
        drivingDialogueCompleted = officialDialogue.LastCompleted;
        if (!drivingDialogueCompleted) Fail("차량 가족 대화 취소");
    }
    private void Block()
    {
        player.AcquireControlLock(this);
        player.AcquireMovementSimulationLock(this);
        playerCamera.AcquireTargetControl(this);
        gameplayUI.AcquireHidden(this);
    }
    public void AccidentStarted()
    {
        if (!CanStartAccident || AccidentStartCount != 0) return;
        AccidentStartCount++;
        stage = Page3Stage.S10Accident;
        Block();
        if (objective != null) objective.enabled = false;
        onAccidentStarted.Invoke();
    }
    public void AccidentFadeStarted()
    {
        if (stage == Page3Stage.S10Accident) stage = Page3Stage.FadeToBlack;
    }
    public void S10Completed()
    {
        if ((stage != Page3Stage.S10Accident && stage != Page3Stage.FadeToBlack) || Page3CompletionCount != 0 || completingAccident) return;
        completingAccident = true;
        // 원본 S10의 정상 병원 인계가 확인된 경우에만 완료한다. 조작은 암전 상태로 잠근다.
        blackOverlay.alpha = 1;
        onS10Completed.Invoke();
        StartCoroutine(CompletePage3());
    }
    private IEnumerator CompletePage3()
    {
        // 실제 Timeline 완료와 대본 완료를 분리해 마지막 발화를 씬 로딩으로 자르지 않는다.
        if (officialDialogue != null) while (officialDialogue.IsBusy) yield return null;
        stage = Page3Stage.Completed;
        Page3CompletionCount++;
        onPage3Completed.Invoke();
        onPage4TransitionRequested.Invoke();
        Debug.Log("[Page3] S10 완료. 검은 화면에서 Page4 인계를 요청했습니다.", this);
    }
    public void AccidentCancelled()
    {
        if (stage == Page3Stage.S10Accident || stage == Page3Stage.FadeToBlack) Fail("S10이 취소되어 Page3 완료를 요청하지 않습니다.");
    }
    private void Release()
    {
        if (player != null) { player.ReleaseMovementSimulationLock(this); player.ReleaseControlLock(this); }
        if (playerCamera != null) playerCamera.ReleaseTargetControl(this, false);
        if (gameplayUI != null) gameplayUI.ReleaseHidden(this);
    }
    private void Fail(string reason)
    {
        stage = Page3Stage.Failed;
        if (seatedDirector != null) seatedDirector.Stop();
        if (conversationCamera != null) conversationCamera.Priority = 0;
        Release();
        Debug.LogError("[Page3] " + reason, this);
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        foreach (PageTimelineSequence timeline in new[] { sofaWakeTimeline, afterMedicineTimeline, blackoutTimeline, wifeArrivalTimeline, familyDialogueTimeline, wifeExitTimeline, vehicleDialogueTimeline })
            if (timeline != null) timeline.Cancel();
        if (standUp != null) { standUp.Completed -= StandFinished; standUp.Cancelled -= StandCancelled; if (standUp.IsPlaying && standUp.RequestOwner == this) standUp.StopCutscene(); }
        if (seatedDirector != null) seatedDirector.Stop();
        if (conversationCamera != null) conversationCamera.Priority = 0;
        if (stage == Page3Stage.MedicineUsing && itemUse != null && itemUse.IsUsing) itemUse.CancelUse();
        if (medicineCamera != null) medicineCamera.Priority = 0;
        if (ownsEffects && effects != null) effects.Release(this);
        Release();
    }
}
