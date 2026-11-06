using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 튜토리얼에 보고할 이동·전투·상호작용 성공 행동을 구분한다.
/// </summary>
public enum DemoAction { Move, Inspect, Jump, Crouch, Dodge, EvadeHit, PunchHit, DaggerHit, BlockHit, Grab, Assassination, ShotHit, Reload, Equip, Pickup, Medication, Dialogue, DoorOpened, ShotFired, MeleeAttackStarted, CombatStance }

/// <summary>
/// 게임플레이 성공 알림을 현재 튜토리얼 단계의 대상·행동 조건과 대조하고 안내 진행을 관리한다.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    /// <summary>
    /// 튜토리얼 안내 표시의 진행 상태를 구분한다.
    /// </summary>
    public enum TutorialDisplayState { Stopped, WaitingForShow, Showing, Active, Hiding, Completed }
    [SerializeField] private TutorialDisplayState displayState;
    public TutorialDisplayState DisplayState => displayState;
    public bool IsPresenting => displayState == TutorialDisplayState.Showing ||
        displayState == TutorialDisplayState.Active || displayState == TutorialDisplayState.Hiding;
    private int presentationRequest;
    private bool warnedMissingUI;
    private bool warnedMissingSteps;
    private bool IsCurrentFlow => standaloneTest || (flow != null && flow.HasBegun &&
        flow.Revision == revision && flow.CurrentStage == requestedStage);

    /// <summary>
    /// 튜토리얼 단계의 행동, 대상과 안내 조건을 보관한다.
    /// </summary>
    [Serializable]
    public class Step
    {
        public GameFlowStage stage;
        public DemoAction action;
        public UnityEngine.Object target;
        public bool requireTarget;
        public bool checkEquipmentKind;
        public PlayerEquipmentKind equipmentKind;
        [Min(1)] public int requiredCount = 1;
        public string title;
        public string instruction;
        public TutorialKeyLayout keys;
        public UnityEvent onStarted = new();
        public UnityEvent onCompleted = new();
    }
    [SerializeField] private GameFlowManager flow;
    [Header("Independent Test (does not complete story stages)")]
    [SerializeField] private bool standaloneTest;
    // Start 시점에 기본 진행 흐름을 자동으로 시작할지 선택한다.
    [SerializeField] private bool beginOnStart;
    public bool IsStandaloneTest => standaloneTest;
    /// <summary>
    /// 컴포넌트의 초기 실행 시점에 설정된 시작 동작을 요청한다.
    /// </summary>
    private void Start() { if (beginOnStart) BeginTutorial(); }
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerMeleeCombat melee;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private ItemUseController itemUse;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private Step[] steps = new Step[0];
    [SerializeField] private UnityEvent onSequenceCompleted = new();
    [SerializeField] private int currentStep;
    [SerializeField] private int currentCount;
    // 진행 단계의 버전. 이전 단계에서 뒤늦게 온 완료 요청을 거부하는 데 사용한다.
    private int revision;
    private bool running;
    private bool changingStep;
    private bool sequencePending;
    private bool foundStep;
    private int lastCountedFrame = -1;
    private int movementKeyMask;
    public int MovementKeyMask { get { return movementKeyMask; } }
    private GameFlowStage requestedStage;
    private WeaponAssaultRifle weapon;
    public event Action Changed;
    public bool IsCompleted { get; private set; }
    public Step CurrentStep => steps != null && currentStep >= 0 && currentStep < steps.Length ? steps[currentStep] : null;
    public int StepNumber => IsCompleted ? StepTotal : currentStep + 1;
    public int StepTotal
    {
        get
        {
            int total = 0;
            if (steps != null) foreach (Step step in steps)
                if (step != null && (standaloneTest || step.stage == requestedStage)) total++;
            return total;
        }
    }
    public int CurrentCount => currentCount;
    public bool IsRunning => running;
    /// <summary>
    /// 컴포넌트 활성화 시 필요한 알림과 초기 표시를 연결한다.
    /// </summary>
    private void OnEnable()
    {
        if (movement != null) movement.Succeeded += Report;
        if (melee != null) melee.Succeeded += Report;
        if (equipment != null) { equipment.OnWeaponChanged += WeaponChanged; equipment.Succeeded += Report; WeaponChanged(equipment.CurrentWeapon); }
        if (inventory != null) inventory.ItemAdded += ItemAdded;
        if (itemUse != null) itemUse.Succeeded += Report;
    }

    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        StopTutorial();
        if (movement != null) movement.Succeeded -= Report;
        if (melee != null) melee.Succeeded -= Report;
        if (equipment != null) { equipment.OnWeaponChanged -= WeaponChanged; equipment.Succeeded -= Report; }
        if (inventory != null) inventory.ItemAdded -= ItemAdded;
        if (itemUse != null) itemUse.Succeeded -= Report;
        WeaponChanged(null);
    }
    /// <summary>
    /// 이전 무기의 알림 연결을 정리하고 현재 무기의 상태 알림을 연결한다.
    /// </summary>
    private void WeaponChanged(WeaponAssaultRifle next)
    {
        if (weapon != null) { weapon.OnDamageApplied -= Hit; weapon.OnReloadCompleted -= Reloaded; weapon.OnFired -= Fired; }
        weapon = next;
        if (weapon != null) { weapon.OnDamageApplied += Hit; weapon.OnReloadCompleted += Reloaded; weapon.OnFired += Fired; }
    }
    /// <summary>
    /// 현재 훈련 대상에 대한 실제 피해 성공을 튜토리얼에 보고한다.
    /// </summary>
    private void Hit(UnityEngine.Object target) => Report(DemoAction.ShotHit, target);
    private void Fired(Vector3 position) => Report(DemoAction.ShotFired, weapon);
    /// <summary>
    /// 실제 재장전 완료를 현재 튜토리얼 조건에 보고한다.
    /// </summary>
    private void Reloaded(WeaponAssaultRifle target) => Report(DemoAction.Reload, target);
    /// <summary>
    /// 실제 아이템 획득 알림을 현재 튜토리얼 또는 UI 표시로 전달한다.
    /// </summary>
    private void ItemAdded(ItemData item, int quantity) => Report(DemoAction.Pickup, item);
    /// <summary>
    /// 연결된 대상과 안내 준비 여부를 확인하고 지정 튜토리얼 단계를 시작한다.
    /// </summary>
    public void BeginTutorial()
    {
        if (!isActiveAndEnabled || running || IsCompleted || (!standaloneTest && (flow == null || !flow.HasBegun))) return;
        if (steps == null || steps.Length == 0)
        {
            if (!warnedMissingSteps) Debug.LogWarning("TutorialManager: 표시할 Steps를 설정하세요.", this);
            warnedMissingSteps = true;
            return;
        }
        presentationRequest++;
        currentStep = -1;
        revision = standaloneTest ? revision + 1 : flow.Revision;
        requestedStage = standaloneTest ? default : flow.CurrentStage;
        foundStep = false;
        lastCountedFrame = -1;
        IsCompleted = false;
        running = true;
        NextStep();
    }
    /// <summary>
    /// 현재 단계의 안내 상태를 정리하고 다음 튜토리얼 조건으로 이동한다.
    /// </summary>
    private void NextStep()
    {
        currentCount = 0;
        movementKeyMask = 0;
        do { currentStep++; } while (currentStep < steps.Length && (steps[currentStep] == null || (!standaloneTest && steps[currentStep].stage != flow.CurrentStage)));
        if (currentStep >= steps.Length)
        {
            running = false;
            sequencePending = foundStep;
            IsCompleted = foundStep;
            displayState = foundStep ? TutorialDisplayState.Completed : TutorialDisplayState.Stopped;
            Changed?.Invoke();
            return;
        }
        foundStep = true;
        displayState = TutorialDisplayState.WaitingForShow;
        Changed?.Invoke();
    }

    // T와 이후 Timeline/Signal은 준비된 단계의 표시만 요청한다.
    /// <summary>
    /// 현재 단계에 맞는 안내 표시를 UI에 요청한다.
    /// </summary>
    [ContextMenu("Tutorial/Request Show Current")]
    public void RequestShowCurrentTutorial()
    {
        if (!isActiveAndEnabled || !running || changingStep || !IsCurrentFlow ||
            displayState != TutorialDisplayState.WaitingForShow) return;
        if (gameplayUI == null)
        {
            if (!warnedMissingUI) Debug.LogWarning("TutorialManager: GameplayUI를 연결하세요.", this);
            warnedMissingUI = true;
            return;
        }
        int request = ++presentationRequest;
        displayState = TutorialDisplayState.Showing;
        Changed?.Invoke();
        if (!running || request != presentationRequest || displayState != TutorialDisplayState.Showing) return;
        if (!gameplayUI.TryShowTutorial(this, request)) OnTutorialPresentationCancelled(request);
    }

    /// <summary>
    /// 요청한 튜토리얼 표시가 실제 준비되었을 때 입력·행동 집계를 허용한다.
    /// </summary>
    internal void OnTutorialShown(int request)
    {
        if (!running || request != presentationRequest || displayState != TutorialDisplayState.Showing || !IsCurrentFlow) return;
        displayState = TutorialDisplayState.Active;
        lastCountedFrame = Time.frameCount;
        changingStep = true;
        try { CurrentStep?.onStarted.Invoke(); }
        finally { changingStep = false; }
        if (request == presentationRequest && running) Changed?.Invoke();
    }

    /// <summary>
    /// 성공 행동과 대상이 현재 단계의 조건에 일치하는지 확인해 진행을 요청한다.
    /// </summary>
    public void Report(DemoAction action, UnityEngine.Object target)
    {
        // Show 완료 이후에만 실제 성공을 센다. Hide/대기/같은 프레임의 늦은 신호는 무시한다.
        if (!running || changingStep || displayState != TutorialDisplayState.Active ||
            Time.frameCount == lastCountedFrame || !IsCurrentFlow || currentStep < 0 || currentStep >= steps.Length ||
            gameplayUI == null || !gameplayUI.CanCountTutorial(this)) return;
        Step step = steps[currentStep];
        if (step.keys == TutorialKeyLayout.WASD && movementKeyMask != 15) return;
        if ((!standaloneTest && step.stage != flow.CurrentStage) || step.action != action ||
            (step.requireTarget && step.target == null) || (step.target != null && step.target != target)) return;
        if (step.checkEquipmentKind && (equipment == null || equipment.CurrentKind != step.equipmentKind)) return;
        currentCount++;
        lastCountedFrame = Time.frameCount;
        bool cleared = currentCount >= Mathf.Max(1, step.requiredCount);
        if (cleared) displayState = TutorialDisplayState.Hiding;
        int request = presentationRequest;
        Changed?.Invoke();
        if (!cleared || !running || request != presentationRequest || displayState != TutorialDisplayState.Hiding) return;
        request = ++presentationRequest;
        if (!gameplayUI.TryHideTutorial(this, request)) OnTutorialPresentationCancelled(request);
    }

    /// <summary>
    /// 안내 숨김이 완료되었음을 확인하여 진행 상태를 갱신한다.
    /// </summary>
    internal void OnTutorialHidden(int request)
    {
        if (!running || request != presentationRequest || displayState != TutorialDisplayState.Hiding || !IsCurrentFlow) return;
        changingStep = true;
        try { CurrentStep?.onCompleted.Invoke(); }
        finally { changingStep = false; }
        if (running && request == presentationRequest && IsCurrentFlow) NextStep();
    }

    /// <summary>
    /// 안내 표시가 취소되면 현재 표시 요청과 진행 상태를 정리한다.
    /// </summary>
    internal void OnTutorialPresentationCancelled(int request)
    {
        if (!running || request != presentationRequest || !IsPresenting) return;
        presentationRequest++;
        currentCount = 0;
        lastCountedFrame = Time.frameCount;
        displayState = TutorialDisplayState.WaitingForShow;
        Changed?.Invoke();
    }
    /// <summary>
    /// 지정 조사 대상의 상호작용 성공을 튜토리얼에 보고한다.
    /// </summary>
    public void OnInspected(UnityEngine.Object target) => Report(DemoAction.Inspect, target);
    /// <summary>Bridge가 실제 이동을 확인한 키만 기록한다. 같은 키와 UI/컷씬 중 입력은 성공시키지 않는다.</summary>
    public void RecordMovementKey(int index)
    {
        if (index < 0 || index > 3 || !running || !IsCurrentFlow || CurrentStep == null ||
            CurrentStep.keys != TutorialKeyLayout.WASD || displayState != TutorialDisplayState.Active ||
            gameplayUI == null || !gameplayUI.CanCountTutorial(this)) return;
        int bit = 1 << index;
        if ((movementKeyMask & bit) != 0) return;
        movementKeyMask |= bit;
        Changed?.Invoke();
        if (movementKeyMask == 15) Report(DemoAction.Move, CurrentStep.target);
    }
    /// <summary>
    /// 지정 대화 대상의 실제 완료를 튜토리얼에 보고한다.
    /// </summary>
    public void OnDialogueCompleted(UnityEngine.Object target) => Report(DemoAction.Dialogue, target);
    /// <summary>
    /// 지정 문의 실제 열림 완료를 튜토리얼에 보고한다.
    /// </summary>
    public void OnDoorOpened(UnityEngine.Object target) => Report(DemoAction.DoorOpened, target);
    /// <summary>
    /// 일반 Update 이후 최종 상태에 맞추어 표시나 자세를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (!running && !sequencePending) return;
        if (!IsCurrentFlow) { StopTutorial(); return; }
        if (!sequencePending) return;
        sequencePending = false;
        GameFlowStage stage = requestedStage;
        int completedRevision = revision;
        int request = presentationRequest;
        onSequenceCompleted.Invoke();
        if (request == presentationRequest && IsCompleted && !standaloneTest && flow != null)
            flow.TryCompleteStage(stage, completedRevision, this);
    }
    /// <summary>
    /// 훈련 대상의 실제 사망을 현재 튜토리얼 조건에 전달한다.
    /// </summary>
    public void OnTrainingTargetDied()
    {
        if (!standaloneTest || !running || currentStep < 0 || currentStep >= steps.Length) return;
        Step step = steps[currentStep];
        if (step.action != DemoAction.ShotHit) return;
        if (step.target is EnemyHealth enemy) enemy.ResetHealth();
        else if (step.target is CombatTarget target) target.ResetTarget();
    }
    /// <summary>
    /// 진행 중 안내와 훈련 상태를 정리하고 기존 독립 시험 초기화 경로를 실행한다.
    /// </summary>
    [ContextMenu("Tutorial/Restart Independent Test")]
    public void RestartIndependentTest()
    {
        if (!standaloneTest) return;
        StopTutorial();
        if (equipment != null)
        {
            if (equipment.CurrentWeapon != null) equipment.CurrentWeapon.ResetAmmoForDemo();
            equipment.ResetEquipment();
        }
        if (steps != null) foreach (Step step in steps)
        {
            if (step == null) continue;
            if (step.target is EnemyHealth enemy) enemy.ResetHealth();
            else if (step.target is CombatTarget target) target.ResetTarget();
        }
        BeginTutorial();
    }
    /// <summary>
    /// 진행 중 안내와 단계 상태를 중단하고 남은 훈련 동작을 정리한다.
    /// </summary>
    public void StopTutorial()
    {
        presentationRequest++;
        running = false;
        IsCompleted = false;
        sequencePending = false;
        currentCount = 0;
        displayState = TutorialDisplayState.Stopped;
        if (gameplayUI != null) gameplayUI.CancelTutorialPresentation(this);
        Changed?.Invoke();
    }
}
