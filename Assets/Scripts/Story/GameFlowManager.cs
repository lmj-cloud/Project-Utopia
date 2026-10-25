using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 현재 게임의 스토리·훈련 진행 단계를 구분한다. 기존 숫자 값은 유지한다.
/// </summary>
public enum GameFlowStage
{
    WaitingForFamilyDialogue = 0,
    WaitingForFrontDoor = 1,
    WaitingForArenaIntro = 2,
    ArenaPlay = 3,
    BedroomWake = 4,
    Hallucination = 5,
    Medication = 6,
    FamilyDeparture = 7,
    TravelToHospital = 8,
    HospitalDialogue = 9,
    LockerEquipment = 10,
    CombatTraining = 11,
    BunkerExit = 12,
    DemoEnded = 13
}

/// <summary>
/// 스토리 단계와 완료 요청의 출처·버전을 검사하여 진행 이벤트와 재시작 초기화를 관리한다.
/// </summary>
public class GameFlowManager : MonoBehaviour
{
    public const string FamilyTalkedFlag = "FamilyTalked";
    public const string FrontDoorInteractedFlag = "FrontDoorInteracted";
    public const string ArenaIntroCompletedFlag = "ArenaIntroCompleted";

    [Header("Story Progress")]
    [SerializeField] private StoryProgress storyProgress;
    // Start 시점에 기본 진행 흐름을 자동으로 시작할지 선택한다.
    [SerializeField] private bool beginOnStart = true;

    [Header("Current Stage")]
    [SerializeField] private GameFlowStage currentStage = GameFlowStage.WaitingForFamilyDialogue;

    [Header("Stage Entry Events")]
    [SerializeField] private UnityEvent onWaitingForFamilyDialogue = new();
    [SerializeField] private UnityEvent onWaitingForFrontDoor = new();
    [SerializeField] private UnityEvent onWaitingForArenaIntro = new();
    [SerializeField] private UnityEvent onArenaPlay = new();

    /// <summary>
    /// 진행 단계별 완료 출처, 컷신, 목표 문구와 진입·종료 이벤트를 연결한다.
    /// </summary>
    [System.Serializable]
    public class StageBinding
    {
        public GameFlowStage stage;
        // 해당 진행 단계의 완료 요청을 허용할 대상 오브젝트.
        public UnityEngine.Object completionSource;
        public CutsceneController timeline;
        public string objective;
        public UnityEvent onEnter = new();
        public UnityEvent onExit = new();
    }
    // 진행 단계별 컷신과 역할 연결이 준비된 경우에만 확장 진행 구성을 활성화한다.
    [Header("Optional Storyboard (disabled until roles/assets are connected)")]
    [SerializeField] private bool storyboardEnabled;
    [SerializeField] private StageBinding[] stages = new StageBinding[0];
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerCameraController cameraController;
    [SerializeField] private PlayerStatus status;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private ItemUseController itemUse;
    [SerializeField] private ScreenEffectController screenEffects;
    [SerializeField] private SceneTransitionController transition;
    [SerializeField] private CutsceneController[] cutscenes = new CutsceneController[0];
    [SerializeField] private VideoCutsceneController[] videos = new VideoCutsceneController[0];
    [SerializeField] private WeaponAssaultRifle[] weapons = new WeaponAssaultRifle[0];
    [SerializeField] private CombatTarget[] targets = new CombatTarget[0];
    [SerializeField] private EnemyHealth[] enemies = new EnemyHealth[0];
    [SerializeField] private SparringOpponent[] opponents = new SparringOpponent[0];
    [SerializeField] private DoorController[] doors = new DoorController[0];
    [SerializeField] private GameEventTrigger[] triggers = new GameEventTrigger[0];
    [SerializeField] private InteractableObject[] interactables = new InteractableObject[0];
    [SerializeField] private InventoryUI inventoryUI;
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private System.Collections.Generic.List<InventoryEntry> initialInventory;
    private static System.Collections.Generic.List<string> restartFlags;
    private static string restartScenePath;
    private bool authorizedCompletion;
    // 진행 단계의 버전. 이전 단계에서 뒤늦게 온 완료 요청을 거부하는 데 사용한다.
    private int revision;
    private bool restarting;
    public int Revision => revision;
    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (restartFlags != null && storyProgress != null && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == restartScenePath)
        {
            storyProgress.RemoveFlag(FamilyTalkedFlag);
            storyProgress.RemoveFlag(FrontDoorInteractedFlag);
            storyProgress.RemoveFlag(ArenaIntroCompletedFlag);
            foreach (string flag in restartFlags) storyProgress.SetFlag(flag);
            restartFlags = null;
            restartScenePath = null;
        }
        if (player != null) { initialPosition = player.transform.position; initialRotation = player.transform.rotation; }
        if (inventory != null) initialInventory = inventory.CaptureInventory();
    }
    private bool hasBegun;
    private bool invokingStageEvent;
    public GameFlowStage CurrentStage => currentStage;
    public bool HasBegun => hasBegun;

    /// <summary>
    /// 컴포넌트의 초기 실행 시점에 설정된 시작 동작을 요청한다.
    /// </summary>
    private void Start()
    {
        if (beginOnStart) BeginFlow();
    }

    /// <summary>
    /// 기존 기본 진행 조건 또는 연결된 스토리보드에 따라 첫 진행 단계를 시작한다.
    /// </summary>
    [ContextMenu("Flow/Begin Flow")]
    public void BeginFlow()
    {
        if (hasBegun || !isActiveAndEnabled) return;
        if (storyProgress == null)
        {
            Debug.LogWarning("GameFlowManager: Connect Story Progress before beginning the flow.", this);
            return;
        }

        bool family = storyProgress.HasFlag(FamilyTalkedFlag);
        bool door = storyProgress.HasFlag(FrontDoorInteractedFlag);
        bool intro = storyProgress.HasFlag(ArenaIntroCompletedFlag);
        // Refuse invalid records instead of inventing completion of earlier actions.
        if ((door && !family) || (intro && !door))
        {
            Debug.LogWarning("GameFlowManager: Story flags are out of order. Check the three flow flags.", this);
            return;
        }

        if (storyboardEnabled && FindBinding(GameFlowStage.BedroomWake) == null && !family && !door && !intro) return;
        hasBegun = true;
        if (storyboardEnabled && !family && !door && !intro) { EnterStage(GameFlowStage.BedroomWake); return; }
        EnterStage(intro ? GameFlowStage.ArenaPlay :
            door ? GameFlowStage.WaitingForArenaIntro :
            family ? GameFlowStage.WaitingForFrontDoor : GameFlowStage.WaitingForFamilyDialogue);
    }

    /// <summary>
    /// 가족 대화 완료를 진행 플래그와 다음 단계 요청에 반영한다.
    /// </summary>
    [ContextMenu("Flow/Notify Family Dialogue Completed")]
    public void OnFamilyDialogueCompleted()
    {
        if (storyboardEnabled && !authorizedCompletion) return;
        if (!CanComplete(GameFlowStage.WaitingForFamilyDialogue)) return;
        storyProgress.SetFlag(FamilyTalkedFlag);
        EnterStage(GameFlowStage.WaitingForFrontDoor);
    }

    /// <summary>
    /// 현관 상호작용 완료가 현재 단계와 맞는지 검사하여 다음 진행을 요청한다.
    /// </summary>
    [ContextMenu("Flow/Notify Front Door Interacted")]
    public void OnFrontDoorInteracted()
    {
        if (storyboardEnabled && !authorizedCompletion) return;
        if (!CanComplete(GameFlowStage.WaitingForFrontDoor) || !storyProgress.HasFlag(FamilyTalkedFlag)) return;
        storyProgress.SetFlag(FrontDoorInteractedFlag);
        EnterStage(GameFlowStage.WaitingForArenaIntro);
    }

    /// <summary>
    /// 경기장 도입 완료를 확인하고 실제 플레이 단계 진입을 요청한다.
    /// </summary>
    [ContextMenu("Flow/Notify Arena Intro Completed")]
    public void OnArenaIntroCompleted()
    {
        if (storyboardEnabled && !authorizedCompletion) return;
        if (!CanComplete(GameFlowStage.WaitingForArenaIntro) ||
            !storyProgress.HasFlag(FamilyTalkedFlag) || !storyProgress.HasFlag(FrontDoorInteractedFlag)) return;
        storyProgress.SetFlag(ArenaIntroCompletedFlag);
        EnterStage(GameFlowStage.ArenaPlay);
    }

    /// <summary>
    /// 현재 단계에서 해당 완료 요청을 수락할 수 있는지 확인한다.
    /// </summary>
    private bool CanComplete(GameFlowStage expected)
    {
        return isActiveAndEnabled && hasBegun && !invokingStageEvent &&
            storyProgress != null && currentStage == expected;
    }

    /// <summary>
    /// 이전 단계의 종료와 새 단계의 목표·진입 이벤트 및 연결 동작을 적용한다.
    /// </summary>
    private void EnterStage(GameFlowStage stage)
    {
        StageBinding previous = hasEnteredStage ? FindBinding(currentStage) : null;
        invokingStageEvent = true;
        try { if (previous != null) previous.onExit.Invoke(); }
        finally { invokingStageEvent = false; }
        if (tutorial != null && !tutorial.IsStandaloneTest) tutorial.StopTutorial();
        currentStage = stage;
        hasEnteredStage = true;
        revision++;
        // Entry requests must not synchronously complete further stages in the same event chain.
        invokingStageEvent = true;
        try
        {
            StageBinding binding = FindBinding(stage);
            if (binding != null)
            {
                if (gameplayUI != null) gameplayUI.SetObjective(binding.objective);
                binding.onEnter.Invoke();
            }
            if (stage == GameFlowStage.DemoEnded)
            {
                if (player != null) player.AcquireControlLock(this);
            }
            switch (stage)
            {
                case GameFlowStage.WaitingForFamilyDialogue: onWaitingForFamilyDialogue.Invoke(); break;
                case GameFlowStage.WaitingForFrontDoor: onWaitingForFrontDoor.Invoke(); break;
                case GameFlowStage.WaitingForArenaIntro: onWaitingForArenaIntro.Invoke(); break;
                case GameFlowStage.ArenaPlay: onArenaPlay.Invoke(); break;
            }
        }
        finally
        {
            invokingStageEvent = false;
        }
    }
    /// <summary>
    /// 현재 진행 단계에 연결된 실제 컷신의 재생을 요청한다.
    /// </summary>
    public void PlayCurrentStageTimeline()
    {
        StageBinding binding = FindBinding(currentStage);
        if (hasBegun && binding != null && binding.timeline != null) binding.timeline.PlayCutscene();
    }
    private bool hasEnteredStage;
    /// <summary>
    /// 주어진 진행 단계와 일치하는 연결 데이터를 찾는다.
    /// </summary>
    private StageBinding FindBinding(GameFlowStage stage)
    {
        if (stages == null) return null;
        foreach (StageBinding binding in stages)
            if (binding != null && binding.stage == stage) return binding;
        return null;
    }
    /// <summary>
    /// 단계·버전·완료 출처를 확인해 오래되거나 잘못된 완료 요청을 거부한다.
    /// </summary>
    public bool TryCompleteStage(GameFlowStage expected, int requestRevision, UnityEngine.Object source)
    {
        if (!storyboardEnabled || !CanComplete(expected) || requestRevision != revision || source == null) return false;
        StageBinding binding = FindBinding(expected);
        if (binding == null || binding.completionSource == null || binding.completionSource != source) return false;
        authorizedCompletion = true;
        try
        {
        switch (expected)
        {
            case GameFlowStage.BedroomWake: EnterStage(GameFlowStage.WaitingForFamilyDialogue); break;
            case GameFlowStage.WaitingForFamilyDialogue: OnFamilyDialogueCompleted(); break;
            case GameFlowStage.WaitingForFrontDoor: OnFrontDoorInteracted(); break;
            case GameFlowStage.WaitingForArenaIntro: OnArenaIntroCompleted(); break;
            case GameFlowStage.ArenaPlay: EnterStage(GameFlowStage.Hallucination); break;
            case GameFlowStage.Hallucination: EnterStage(GameFlowStage.Medication); break;
            case GameFlowStage.Medication: EnterStage(GameFlowStage.FamilyDeparture); break;
            case GameFlowStage.FamilyDeparture: EnterStage(GameFlowStage.TravelToHospital); break;
            case GameFlowStage.TravelToHospital: EnterStage(GameFlowStage.HospitalDialogue); break;
            case GameFlowStage.HospitalDialogue: EnterStage(GameFlowStage.LockerEquipment); break;
            case GameFlowStage.LockerEquipment: EnterStage(GameFlowStage.CombatTraining); break;
            case GameFlowStage.CombatTraining: EnterStage(GameFlowStage.BunkerExit); break;
            case GameFlowStage.BunkerExit: EnterStage(GameFlowStage.DemoEnded); break;
            default: return false;
        }
        return revision != requestRevision;
        }
        finally { authorizedCompletion = false; }
    }
    /// <summary>
    /// 진행 중 연출과 UI·전투를 정리한 뒤 기존 초기 상태 및 재시작 정책을 적용한다.
    /// </summary>
    [ContextMenu("Flow/Restart Demo")]
    public void RestartDemo()
    {
        if (restarting || player == null || movement == null || storyProgress == null) return;
        restarting = true;
        hasBegun = false;
        revision++; // Invalidate earlier action tickets before cancelling callbacks.
        if (tutorial != null) tutorial.StopTutorial();
        if (dialogueUI != null) dialogueUI.CancelDialogue();
        if (inventoryUI != null) inventoryUI.Close();
        if (itemUse != null) itemUse.CancelUse();
        if (transition != null) transition.CancelTransition();
        if (screenEffects != null) screenEffects.CancelEffect();
        foreach (CutsceneController cutscene in cutscenes) if (cutscene != null) cutscene.StopCutscene();
        foreach (VideoCutsceneController video in videos) if (video != null) video.CancelVideo();
        foreach (SparringOpponent opponent in opponents) if (opponent != null) opponent.EndTraining();
        UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            restartFlags = storyProgress.CaptureFlags();
            restartFlags.Remove(FamilyTalkedFlag);
            restartFlags.Remove(FrontDoorInteractedFlag);
            restartFlags.Remove(ArenaIntroCompletedFlag);
            restartScenePath = scene.path;
            UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(scene.buildIndex);
            return;
        }
        foreach (WeaponAssaultRifle weapon in weapons) if (weapon != null) weapon.ResetAmmoForDemo();
        foreach (CombatTarget target in targets) if (target != null) target.ResetTarget();
        foreach (EnemyHealth enemy in enemies) if (enemy != null) enemy.ResetHealth();
        foreach (DoorController door in doors) if (door != null) door.ResetDoor();
        foreach (GameEventTrigger trigger in triggers) if (trigger != null) trigger.ResetTrigger();
        foreach (InteractableObject interactable in interactables)
            if (interactable != null) { interactable.ResetInteraction(); interactable.gameObject.SetActive(true); }
        storyProgress.RemoveFlag(FamilyTalkedFlag);
        storyProgress.RemoveFlag(FrontDoorInteractedFlag);
        storyProgress.RemoveFlag(ArenaIntroCompletedFlag);
        if (inventory != null) inventory.RestoreInventory(initialInventory);
        if (status != null) status.RestoreFullStatus();
        player.ReleaseControlLock(this);
        movement.Teleport(initialPosition, initialRotation);
        if (player != null)
        {
            StoryFirstPersonController storyView = player.GetComponent<StoryFirstPersonController>();
            if (storyView != null) storyView.ResetPresentation();
        }
        if (cameraController != null) { cameraController.SetView(true); cameraController.SynchronizeDirection(initialRotation); }
        if (equipment != null) equipment.ResetEquipment();
        if (gameplayUI != null) gameplayUI.ResetDisplay();
        hasEnteredStage = false;
        restarting = false;
        BeginFlow();
    }

}
