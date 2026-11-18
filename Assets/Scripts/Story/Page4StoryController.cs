using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using Unity.Cinemachine;
using TMPro;

public enum Page4Stage
{
    Loading, HospitalIntro, DoctorDialogue, Blackout, PlayerAwakening, OfficerGreeting,
    OfficerWalkingToArmory, ArmoryArrival, RiflePickupTutorial, OfficerWalkingToRange,
    RangeBriefing, FreePractice, S13Starting, S13Playing, DemoCompleted, Failed, CorridorWaiting, OfficerCutscene
}

/// <summary>기존 플레이어·화면 효과·대화·튜토리얼을 Page4의 정상 완료 순서로 연결한다.</summary>
public class Page4StoryController : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerCameraController playerCamera;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private ScreenEffectController effects;
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField] private CinemachineCamera hospitalCamera;
    [SerializeField] private Transform wakeSpawn;
    [SerializeField] private TMP_Text subtitle;
    [SerializeField] private TMP_Text objective;
    [SerializeField, TextArea] private string[] doctorLines = { "환자분, 제 목소리 들리십니까?", "큰 사고를 당하셨습니다.", "의식이 아직 불안정합니다. 조금만 더 버텨주세요.", "환자분...?", "환자분! 환자분!!" };
    [SerializeField, Min(.2f)] private float doctorLineDuration = 2.5f;
    [SerializeField] private UnityEvent onHospitalCompleted = new UnityEvent();
    [Header("장교와 무기고")]
    [SerializeField] private Transform officer;
    [SerializeField] private Animator officerAnimator;
    [SerializeField] private NPCDialogue greeting;
    [SerializeField] private NPCDialogue armoryDialogue;
    [SerializeField] private NPCDialogue rangeDialogue;
    [SerializeField] private Transform[] armoryWaypoints;
    [SerializeField] private Transform[] rangeWaypoints;
    [SerializeField, Min(.1f)] private float officerWalkSpeed = 1.05f;
    [SerializeField, Min(1f)] private float officerTurnSpeed = 180f;
    [SerializeField, Min(2f)] private float followDistance = 6f;
    [SerializeField] private InteractableObject riflePickup;
    [SerializeField] private TutorialManager rifleTutorial;
    [SerializeField] private GameObject rangeGate;
    [SerializeField, TextArea] private string[] armoryWalkingLines = { "자세한 건 차차 알게 될 거다.", "일단 나를 따라오도록." };
    [SerializeField, TextArea] private string[] rangeWalkingLines = { "이제 사격장으로 안내하지." };
    public int OfficerDialogueCount { get; private set; }
    public int ArmoryArrivalCount { get; private set; }
    public int RangeArrivalCount { get; private set; }
    public bool RangeBriefingCompleted { get; private set; }
    private NPCDialogue activeConversation;
    private bool conversationCompleted;

    [Header("원본 S13 종료")]
    [SerializeField] private Page4S13Adapter ending;
    [SerializeField] private UnityEvent onDemoCompleted = new UnityEvent();
    public int DemoCompletionCount { get; private set; }
    public bool CanUseExit { get { return stage == Page4Stage.FreePractice && RangeBriefingCompleted; } }
    public UnityEvent OnDemoCompleted { get { return onDemoCompleted; } }
    [SerializeField] private Page4Stage stage;
    [Header("공식 병원·훈련 대본")]
    [SerializeField] private OfficialDialoguePlayer officialDialogue;
    [SerializeField] private GameObject nurse;
    [SerializeField] private Animator nurseAnimator;
    [SerializeField] private NPCDialogue nurseDialogue;
    [SerializeField] private Transform[] nurseEntryWaypoints;
    [SerializeField] private Transform[] nurseExitWaypoints;
    [SerializeField] private Transform[] officerEntryWaypoints;
    [SerializeField] private Transform hospitalDoor;
    [SerializeField] private Transform armoryDoor;
    [SerializeField] private NPCDialogue equipmentDialogue;
    [SerializeField] private InteractableObject knifePickup;
    [SerializeField] private GameObject familyKeepsake;
    [SerializeField] private UnityEvent onNurseCalledOfficer = new UnityEvent();
    [SerializeField] private UnityEvent onWorldExplanationRequested = new UnityEvent();
    [SerializeField] private UnityEvent onRehabilitationTimePassed = new UnityEvent();
    public int NurseDialogueCount { get; private set; }
    public bool RangeTrainingEnabled { get; private set; }
    public bool CanStartRangeAim => gunRangeEntered && RangeTrainingEnabled;
    public Page4Stage Stage { get { return stage; } }
    public bool IntroStarted { get; private set; }
    public int HospitalCompletionCount { get; private set; }
    public float BlackAlpha { get { return blackOverlay.alpha; } }
    [Header("Page4 편집용 Timeline")]
    [SerializeField] private PageTimelineSequence hospitalIntroTimeline;
    [SerializeField] private PageTimelineSequence hospitalDoorTimeline;
    [SerializeField] private PageTimelineSequence nurseEntryTimeline;
    [SerializeField] private PageTimelineSequence nurseDialogueTimeline;
    [SerializeField] private PageTimelineSequence nurseExitTimeline;
    [SerializeField] private PageTimelineSequence officerEntryTimeline;
    [SerializeField] private PageTimelineSequence officerGreetingTimeline;
    [SerializeField] private PageTimelineSequence lossTimeline;
    [SerializeField] private PageTimelineSequence armoryWalkTimeline;
    [SerializeField] private PageTimelineSequence armoryDialogueTimeline;
    [SerializeField] private PageTimelineSequence equipmentTimeline;
    [SerializeField] private PageTimelineSequence rangeWalkTimeline;
    [SerializeField] private PageTimelineSequence gunRangeWalkTimeline;
    [SerializeField] private PageTimelineSequence rangeDialogueTimeline;
    [SerializeField] private PageTimelineSequence rehabilitationTimeline;
    [SerializeField] private PageTimelineSequence endingFadeTimeline;
    [Header("복도·장비 보관함 Timeline")]
    [SerializeField] private PageTimelineSequence officerLockerTimeline;
    [SerializeField] private Transform officerCutsceneStart;
    [SerializeField] private Transform officerCutsceneFinish;
    [SerializeField] private InteractableObject pistolPickup;
    private bool corridorEntered;
    private bool gunRangeEntered;
    private bool gunRangeEscortCompleted;
    private bool officerSignalCompleted;
    private bool officerSceneOwnsRestore;
    private Vector3 officerStartPosition;
    private Quaternion officerStartRotation;
    private Quaternion officerStartCamera;
    private bool hospitalHandoffStarted;
    private bool ownsEffects;
    private Renderer[] playerRenderers;
    private bool[] hiddenStates;

    private void Awake()
    {
        stage = Page4Stage.Loading;
        Block();
        blackOverlay.alpha = 1;
        hospitalCamera.Priority = 110;
        // 병원 1인칭 카메라 안쪽에 몸/장비 메시가 보이지 않도록 기존 표시 상태를 보관한다.
        HideRenderers();
        if (officialDialogue != null)
        {
            nurse.SetActive(true);
            officer.gameObject.SetActive(true);
            if (familyKeepsake != null) familyKeepsake.SetActive(false);
        }
    }
    private IEnumerator Start()
    {
        yield return null;
        player.GetComponent<PlayerStatus>().RestoreFullStatus();
        player.GetComponent<PlayerEquipment>().RestoreEquipment(PlayerEquipmentKind.BareHands);
        gameplayUI.AcquireHidden(this);
        if (Page3Page4Transition.Active != null)
        {
            if (!Page3Page4Transition.Active.NotifyReady(this)) Fail("Page3 인계 불일치");
        }
        else AcceptArrival();
    }
    public void AcceptArrival()
    {
        if (stage != Page4Stage.Loading) return;
        IntroStarted = true;
        StartCoroutine(HospitalIntro());
    }
    private IEnumerator HospitalIntro()
    {
        if (officialDialogue == null || doctorLineDuration <= 0 || officerWalkSpeed <= 0 || officerTurnSpeed <= 0 || followDistance < 2)
        { Fail("공식 대본/연출 설정 누락"); yield break; }
        yield return OfficialHospital();
    }

    private IEnumerator Speak(NPCDialogue conversation, Transform actor = null)
    {
        if (conversation == null) { Fail("장교 대화 참조 누락"); yield break; }
        if (actor == null) actor = officer;
        while (Vector3.Distance(player.transform.position, actor.position) > 4) yield return null;
        // 병실의 최종 시선은 Timeline 회전 키가 담당한다. 대화 시작 때 다시 꺾지 않는다.
        if (conversation != nurseDialogue && conversation != greeting)
        {
            Quaternion facing = Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.position - actor.position, Vector3.up));
            if (actor.parent != null && actor.parent.name.EndsWith("Motion"))
                actor.parent.rotation = facing * Quaternion.Inverse(actor.localRotation);
            else actor.rotation = facing;
        }
        activeConversation = conversation;
        conversationCompleted = false;
        conversation.enabled = true;
        while (!conversation.CanStartDialogue) yield return null;
        conversation.StartDialogue();
        // 간호사 완료 이벤트가 다음 대화를 시작해도 이전 대화의 정리가 새 소유자를 지우지 않는다.
        while (activeConversation == conversation && !conversationCompleted) yield return null;
        conversation.enabled = false;
        if (activeConversation == conversation) activeConversation = null;
    }
    public void OfficerDialogueCompleted()
    {
        if (activeConversation == null || conversationCompleted) return;
        conversationCompleted = true;
        if (activeConversation == nurseDialogue)
        {
            NurseDialogueCount++;
            BeginHospitalHandoff();
        }
        else OfficerDialogueCount++;
    }
    private void BeginHospitalHandoff()
    {
        if (hospitalHandoffStarted) return;
        hospitalHandoffStarted = true;
        // 마지막 간호사 대화의 실제 완료 이벤트에서 두 재생을 함께 시작한다.
        StartCoroutine(nurseExitTimeline.Play());
        StartCoroutine(FinishNurseExit());
        if (stage == Page4Stage.Failed) return;
        onNurseCalledOfficer.Invoke();
        StartCoroutine(officerGreetingTimeline.Play());
    }
    private IEnumerator FinishNurseExit()
    {
        // 퇴장 완료는 간호사 숨김만 담당하고 장교 대화의 시작 조건으로 사용하지 않는다.
        while (nurseExitTimeline.IsRunning) yield return null;
        if (!nurseExitTimeline.Completed)
        {
            if (officerGreetingTimeline.IsRunning) officerGreetingTimeline.Cancel();
            if (lossTimeline.IsRunning) lossTimeline.Cancel();
            Fail("간호사 퇴장 Timeline 취소");
            yield break;
        }
        if (nurse.activeSelf) nurse.SetActive(false);
    }
    private IEnumerator OfficialHospital()
    {
        stage = Page4Stage.HospitalIntro;
        if (!effects.TryAcquire(this)) { Fail("병원 화면 효과 소유권 실패"); yield break; }
        ownsEffects = true;
        subtitle.text = "";
        subtitle.enabled = false;
        yield return hospitalIntroTimeline.Play();
        yield return hospitalDoorTimeline.Play();
        yield return nurseEntryTimeline.Play();
        if (!hospitalIntroTimeline.Completed || !hospitalDoorTimeline.Completed || !nurseEntryTimeline.Completed)
        { Fail("병원 입장 Timeline 취소"); yield break; }
        if (stage == Page4Stage.Failed) yield break;
        stage = Page4Stage.DoctorDialogue;
        yield return nurseDialogueTimeline.Play();
        if (!nurseDialogueTimeline.Completed || !hospitalHandoffStarted) { Fail("간호사 대화 인계 취소"); yield break; }
        // 퇴장은 독립 진행한다. 병실 다음 장면은 장교 대화 완료에만 맞춘다.
        while (officerGreetingTimeline.IsRunning && stage != Page4Stage.Failed) yield return null;
        if (stage == Page4Stage.Failed) yield break;
        if (!officerGreetingTimeline.Completed) { Fail("장교 대화 Timeline 취소"); yield break; }
        stage = Page4Stage.Blackout;
        yield return lossTimeline.Play();
        if (!officerGreetingTimeline.Completed || !lossTimeline.Completed) { Fail("병원 인계 Timeline 취소"); yield break; }
        effects.Release(this);
        ownsEffects = false;
        Release();
        HospitalCompletionCount++;
        onHospitalCompleted.Invoke();
        yield return OfficialArmoryAndRange();
    }
    public void TimelineNurseDialogue()
    {
        if (Application.isPlaying) nurseDialogueTimeline.WaitFor(Speak(nurseDialogue, nurse.transform));
    }
    public void TimelineOfficerGreeting()
    {
        if (Application.isPlaying) officerGreetingTimeline.WaitFor(Speak(greeting));
    }
    public void TimelineArmoryDialogue()
    {
        if (Application.isPlaying) armoryDialogueTimeline.WaitFor(Speak(armoryDialogue));
    }
    public void TimelineEquipmentDialogue()
    {
        if (Application.isPlaying) equipmentTimeline.WaitFor(Speak(equipmentDialogue));
    }
    public void TimelineRangeDialogue()
    {
        if (Application.isPlaying) rangeDialogueTimeline.WaitFor(Speak(rangeDialogue));
    }
    public void TimelineWorldExplanation()
    {
        if (!Application.isPlaying || stage != Page4Stage.Blackout) return;
        onWorldExplanationRequested.Invoke();
        // 완전 암전이 퇴장 도착보다 빠르면 가려진 상태에서 퇴장을 마무리한다.
        if (nurse.activeSelf && blackOverlay.alpha >= .999f)
        {
            nurseExitTimeline.StopVisuals();
            if (nurse.activeSelf) nurse.SetActive(false);
        }
    }
    public void TimelineHospitalWake()
    {
        if (!Application.isPlaying || stage != Page4Stage.Blackout) return;
        // 병렬 퇴장은 계속 재생한다. 완전 암전에서만 기다려 복도 재등장을 막는다.
        if (nurse.activeSelf) lossTimeline.WaitFor(WaitForNurseThenWake());
        else ApplyHospitalWake();
    }
    private IEnumerator WaitForNurseHidden()
    {
        while (nurse.activeSelf && stage == Page4Stage.Blackout) yield return null;
    }
    private IEnumerator WaitForNurseThenWake()
    {
        yield return WaitForNurseHidden();
        if (stage == Page4Stage.Blackout) ApplyHospitalWake();
    }
    private void ApplyHospitalWake()
    {
        player.GetComponent<PlayerMovement>().Teleport(wakeSpawn.position, wakeSpawn.rotation);
        playerCamera.SynchronizeDirection(wakeSpawn.rotation);
        hospitalCamera.Priority = 0;
        RestoreRenderers();
        stage = Page4Stage.PlayerAwakening;
    }
    public void TimelineRehabilitation()
    {
        if (!Application.isPlaying) return;
        onRehabilitationTimePassed.Invoke();
        objective.text = "약 1개월 후";
    }

    private IEnumerator OfficialArmoryAndRange()
    {
        // 병실 11번 장면의 정상 완료 뒤에만 실제 복도 도착을 기다린다.
        stage = Page4Stage.CorridorWaiting;
        objective.text = "";
        rifleTutorial.StopTutorial();
        gameplayUI.AcquireHidden(this);
        CorridorArrived();
        while (!corridorEntered) yield return null;
        officerSignalCompleted = false;
        Block();
        gameplayUI.AcquireHidden(this);
        stage = Page4Stage.OfficerCutscene;
        objective.text = "";
        // 진입 위치를 즉시 이동시키지 않는다. 경로 Track의 Ease In이 현재 자세에서 시작점을 연결한다.
        yield return officerLockerTimeline.Play();
        if (!officerLockerTimeline.Completed || !officerSignalCompleted) { Fail("장교 보관함 Timeline 취소"); yield break; }
        player.GetComponent<PlayerMovement>().Teleport(officerCutsceneFinish.position, officerCutsceneFinish.rotation);
        playerCamera.SynchronizeDirection(officerCutsceneFinish.rotation);
        officerSceneOwnsRestore = false;
        OfficerDialogueCount += 2;
        ArmoryArrivalCount++;
        Release();
        stage = Page4Stage.RiflePickupTutorial;
        rifleTutorial.BeginTutorialFromStep(2);
        objective.text = "보관함에서 무기를 획득하세요.";
        RefreshPickups();
        while (rifleTutorial.StepNumber <= 3 ||
            !player.GetComponent<PlayerEquipment>().HasOwnership(PlayerEquipmentKind.AssaultRifle) ||
            !player.GetComponent<PlayerEquipment>().HasOwnership(PlayerEquipmentKind.Dagger))
        {
            if (rifleTutorial.DisplayState == TutorialManager.TutorialDisplayState.WaitingForShow)
                rifleTutorial.RequestShowCurrentTutorial();
            yield return null;
        }
        rangeGate.SetActive(false);
        stage = Page4Stage.OfficerWalkingToRange;
        objective.text = "장교를 따라 단검 훈련장으로 이동하세요.";
        yield return rangeWalkTimeline.Play();
        if (!rangeWalkTimeline.Completed) { Fail("사격장 이동 Timeline 취소"); yield break; }
        if (stage == Page4Stage.Failed) yield break;
        // 같은 기존 튜토리얼에서 실제 단검 명중을 먼저 기다린다.
        stage = Page4Stage.RangeBriefing;
        RangeTrainingEnabled = true;
        while (rifleTutorial.StepNumber <= 4)
        {
            if (rifleTutorial.DisplayState == TutorialManager.TutorialDisplayState.WaitingForShow)
                rifleTutorial.RequestShowCurrentTutorial();
            yield return null;
        }
        RangeTrainingEnabled = false;
        stage = Page4Stage.OfficerWalkingToRange;
        objective.text = "더 안쪽 사격장으로 이동하세요.";
        if (gunRangeWalkTimeline == null) { Fail("사격장 동행 Timeline 연결 누락"); yield break; }
        yield return gunRangeWalkTimeline.Play();
        if (!gunRangeWalkTimeline.Completed) { Fail("사격장 동행 Timeline 취소"); yield break; }
        gunRangeEscortCompleted = true;
        while (!gunRangeEntered) yield return null;
        RangeArrivalCount++;
        stage = Page4Stage.RangeBriefing;
        yield return rangeDialogueTimeline.Play();
        if (!rangeDialogueTimeline.Completed) { Fail("사격장 대화 Timeline 취소"); yield break; }
        RangeTrainingEnabled = true;
        objective.text = "안내에 따라 사격·재장전·단검 훈련을 진행하세요.";
        while (rifleTutorial.StepNumber <= 7) yield return null;
        // 단검 상체 공격은 Base 이동 위에서 재생된다. 자기 잠금을 걸기 전에 현재 요청의 완료를 기다린다.
        PlayerCombatActions trainingCombat = player.GetComponent<PlayerCombatActions>();
        while (trainingCombat != null && trainingCombat.IsRunning && trainingCombat.IsKnifeAction) yield return null;
        // 훈련 완료 후 시간 경과·추가 대사 없이 기존 출입문 안내로 진행한다.
        RangeBriefingCompleted = true;
        stage = Page4Stage.FreePractice;
        objective.text = "벙커 출입문으로 이동하세요.";
    }
    public void GunRangeArrived()
    {
        if (stage == Page4Stage.OfficerWalkingToRange && gunRangeEscortCompleted)
            gunRangeEntered = true;
    }

    public void CorridorArrived()
    {
        if (corridorEntered || stage != Page4Stage.CorridorWaiting ||
            HospitalCompletionCount != 1 || !player.CanControlPlayer) return;
        officerStartPosition = player.transform.position;
        officerStartRotation = player.transform.rotation;
        officerStartCamera = playerCamera.ThirdPersonTarget.rotation;
        officerSceneOwnsRestore = true;
        // 잠금 뒤에는 Animator 파라미터 갱신도 차단된다. 이동 중 남은 Walk 값을 먼저 정리한다.
        PlayerAnimatorController animator = player.GetComponent<PlayerAnimatorController>();
        animator.CurrentSpeed = 0f;
        animator.MoveHorizontal = 0f;
        animator.MoveVertical = 0f;
        player.AcquireControlLock(this);
        player.AcquireMovementSimulationLock(this);
        corridorEntered = true;
    }
    public void TimelineOfficerLock()
    {
        if (Application.isPlaying && stage == Page4Stage.OfficerCutscene) Block();
    }
    public void TimelineWaitForCorridorDialogue()
    {
        if (Application.isPlaying && stage == Page4Stage.OfficerCutscene)
            officerLockerTimeline.WaitFor(WaitForCorridorDialogue());
    }
    private IEnumerator WaitForCorridorDialogue()
    {
        // 마지막 복도 발화가 끝나야 선반 방향으로 이동을 재개한다.
        while (officialDialogue.IsBusy || officerLockerTimeline.IsDialoguePresentationBusy) yield return null;
    }
    public void TimelineOfficerFinished()
    {
        if (Application.isPlaying && stage == Page4Stage.OfficerCutscene && officerLockerTimeline.IsRunning)
            officerSignalCompleted = true;
    }
    private void Update()
    {
        if (stage == Page4Stage.RiflePickupTutorial || stage == Page4Stage.OfficerWalkingToRange ||
            stage == Page4Stage.RangeBriefing || stage == Page4Stage.FreePractice) RefreshPickups();
    }
    private void RefreshPickups()
    {
        PlayerEquipment equipment = player.GetComponent<PlayerEquipment>();
        RefreshPickup(riflePickup, equipment.HasOwnership(PlayerEquipmentKind.AssaultRifle));
        // Page4 권총은 전시용이다. 모델은 남기고 상호작용을 계속 차단한다.
        if (pistolPickup != null) pistolPickup.enabled = false;
        RefreshPickup(knifePickup, equipment.HasOwnership(PlayerEquipmentKind.Dagger));
    }
    private void RefreshPickup(InteractableObject pickup, bool owned)
    {
        if (pickup == null) return;
        pickup.enabled = !owned;
        if (owned && pickup.gameObject.activeSelf) pickup.gameObject.SetActive(false);
    }
    private void RestoreOfficerPlayer()
    {
        if (!officerSceneOwnsRestore || player == null) return;
        officerSceneOwnsRestore = false;
        player.GetComponent<PlayerMovement>().Teleport(officerStartPosition, officerStartRotation);
        if (playerCamera != null) playerCamera.SynchronizeDirection(officerStartCamera);
    }

    private void Block()
    {
        player.AcquireControlLock(this);
        player.AcquireMovementSimulationLock(this);
        playerCamera.AcquireTargetControl(this);
    }
    public bool TryBeginS13()
    {
        if (!CanUseExit || DemoCompletionCount != 0) return false;
        // 기존 TutorialUI가 실제 F 상호작용 성공을 받은 뒤 마지막 목표를 완료한다.
        if (rifleTutorial.CurrentStep != null && rifleTutorial.CurrentStep.action == DemoAction.DoorOpened)
        {
            if (rifleTutorial.DisplayState != TutorialManager.TutorialDisplayState.Active) return false;
            rifleTutorial.Report(DemoAction.DoorOpened, this);
            if (rifleTutorial.DisplayState != TutorialManager.TutorialDisplayState.Hiding) return false;
        }
        stage = Page4Stage.S13Starting;
        WeaponAssaultRifle weapon = player.GetComponent<PlayerEquipment>().CurrentWeapon;
        if (weapon != null) weapon.CancelWeaponActions();
        player.GetComponent<PlayerMeleeCombat>().CancelActions();
        player.GetComponent<PlayerCombatActions>().CancelAction();
        Block();
        objective.enabled = false;
        subtitle.text = "";
        return true;
    }
    public IEnumerator WaitForExitTutorial()
    {
        // Hide가 시작된 프레임에 패널을 끄면 기존 UI의 완료 검사가 실패한다.
        while (rifleTutorial.DisplayState == TutorialManager.TutorialDisplayState.Hiding) yield return null;
        if (!rifleTutorial.IsCompleted) { Fail("출입문 안내 완료 실패"); yield break; }
        gameplayUI.AcquireHidden(this);
    }
    public void S13Started()
    {
        if (stage == Page4Stage.S13Starting) { HideRenderers(); stage = Page4Stage.S13Playing; }
    }
    public void S13Ready()
    {
        if (stage != Page4Stage.S13Playing || DemoCompletionCount != 0) return;
        StartCoroutine(CompleteDemo());
    }
    private IEnumerator CompleteDemo()
    {
        if (!effects.TryAcquire(this)) { Fail("종료 화면 효과 소유권 실패"); yield break; }
        ownsEffects = true;
        // 원본 S13이 유지한 백색 화면 위로 검은 화면을 덮은 뒤에만 백색 소유권을 해제한다.
        yield return endingFadeTimeline.Play();
        if (!endingFadeTimeline.Completed) { Fail("종료 Fade Timeline 취소"); yield break; }
        ending.ReleaseWhite();
        effects.Release(this);
        ownsEffects = false;
        stage = Page4Stage.DemoCompleted;
        DemoCompletionCount++;
        gameplayUI.ShowDemoEnd();
        onDemoCompleted.Invoke();
    }
    public void S13Cancelled()
    {
        if (isActiveAndEnabled && (stage == Page4Stage.S13Starting || stage == Page4Stage.S13Playing)) Fail("S13 취소. 데모 완료를 요청하지 않습니다.");
    }
    private void Release()
    {
        if (player != null) { player.ReleaseMovementSimulationLock(this); player.ReleaseControlLock(this); }
        if (playerCamera != null) playerCamera.ReleaseTargetControl(this, false);
        if (gameplayUI != null) gameplayUI.ReleaseHidden(this);
    }
    private void Fail(string message)
    {
        stage = Page4Stage.Failed;
        if (officerLockerTimeline != null && officerLockerTimeline.IsRunning) officerLockerTimeline.Cancel();
        RestoreOfficerPlayer();
        RestoreRenderers();
        Release();
        Debug.LogError("[Page4] " + message, this);
    }
    private void RestoreRenderers()
    {
        if (playerRenderers == null) return;
        for (int i = 0; i < playerRenderers.Length; i++)
            if (playerRenderers[i] != null) playerRenderers[i].forceRenderingOff = hiddenStates[i];
        playerRenderers = null;
    }
    private void HideRenderers()
    {
        if (playerRenderers != null) return;
        playerRenderers = player.GetComponentsInChildren<Renderer>(true);
        hiddenStates = new bool[playerRenderers.Length];
        for (int i = 0; i < playerRenderers.Length; i++)
        {
            hiddenStates[i] = playerRenderers[i].forceRenderingOff;
            playerRenderers[i].forceRenderingOff = true;
        }
    }
    private void OnDisable()
    {
        StopAllCoroutines();
        foreach (PageTimelineSequence timeline in new[] { officerLockerTimeline, hospitalIntroTimeline, hospitalDoorTimeline, nurseEntryTimeline, nurseDialogueTimeline, nurseExitTimeline, officerEntryTimeline, officerGreetingTimeline, lossTimeline, armoryWalkTimeline, armoryDialogueTimeline, equipmentTimeline, rangeWalkTimeline, gunRangeWalkTimeline, rangeDialogueTimeline, rehabilitationTimeline, endingFadeTimeline })
            if (timeline != null) timeline.Cancel();
        RestoreOfficerPlayer();
        if (activeConversation != null) activeConversation.enabled = false;
        if (ownsEffects && effects != null) effects.Release(this);
        if (hospitalCamera != null) hospitalCamera.Priority = 0;
        RestoreRenderers();
        Release();
    }
}
