using System;
using UnityEngine;
using UnityEngine.Events;

public enum DemoAction { Move, Inspect, Jump, Crouch, Dodge, EvadeHit, PunchHit, DaggerHit, BlockHit, Grab, Assassination, ShotHit, Reload, Equip, Pickup, Medication, Dialogue, DoorOpened }

public class TutorialManager : MonoBehaviour
{
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
        public UnityEvent onStarted = new();
        public UnityEvent onCompleted = new();
    }
    [SerializeField] private GameFlowManager flow;
    [Header("Independent Test (does not complete story stages)")]
    [SerializeField] private bool standaloneTest;
    [SerializeField] private bool beginOnStart;
    public bool IsStandaloneTest => standaloneTest;
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
    private int revision;
    private bool running;
    private bool changingStep;
    private bool sequencePending;
    private bool foundStep;
    private int lastCountedFrame = -1;
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
    private void OnEnable()
    {
        if (movement != null) movement.Succeeded += Report;
        if (melee != null) melee.Succeeded += Report;
        if (equipment != null) { equipment.OnWeaponChanged += WeaponChanged; equipment.Succeeded += Report; WeaponChanged(equipment.CurrentWeapon); }
        if (inventory != null) inventory.ItemAdded += ItemAdded;
        if (itemUse != null) itemUse.Succeeded += Report;
    }

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
    private void WeaponChanged(WeaponAssaultRifle next)
    {
        if (weapon != null) { weapon.OnDamageApplied -= Hit; weapon.OnReloadCompleted -= Reloaded; }
        weapon = next;
        if (weapon != null) { weapon.OnDamageApplied += Hit; weapon.OnReloadCompleted += Reloaded; }
    }
    private void Hit(UnityEngine.Object target) => Report(DemoAction.ShotHit, target);
    private void Reloaded(WeaponAssaultRifle target) => Report(DemoAction.Reload, target);
    private void ItemAdded(ItemData item, int quantity) => Report(DemoAction.Pickup, item);
    public void BeginTutorial()
    {
        if (!isActiveAndEnabled || running || IsCompleted || (!standaloneTest && (flow == null || !flow.HasBegun)) || steps == null || steps.Length == 0) return;
        currentStep = -1;
        revision = standaloneTest ? revision + 1 : flow.Revision;
        requestedStage = standaloneTest ? default : flow.CurrentStage;
        foundStep = false;
        lastCountedFrame = -1;
        IsCompleted = false;
        running = true;
        NextStep();
    }
    private void NextStep()
    {
        currentCount = 0;
        do { currentStep++; } while (currentStep < steps.Length && (steps[currentStep] == null || (!standaloneTest && steps[currentStep].stage != flow.CurrentStage)));
        if (currentStep >= steps.Length)
        {
            running = false;
            sequencePending = foundStep;
            IsCompleted = foundStep;
            Changed?.Invoke();
            return;
        }
        Step step = steps[currentStep];
        foundStep = true;
        changingStep = true;
        try
        {
            Changed?.Invoke();
            step.onStarted.Invoke();
        }
        finally { changingStep = false; }
    }
    public void Report(DemoAction action, UnityEngine.Object target)
    {
        if (!running || changingStep || Time.frameCount == lastCountedFrame || (!standaloneTest && (flow == null || flow.Revision != revision)) || currentStep < 0 || currentStep >= steps.Length) return;
        Step step = steps[currentStep];
        if ((!standaloneTest && step.stage != flow.CurrentStage) || step.action != action ||
            (step.requireTarget && step.target == null) || (step.target != null && step.target != target)) return;
        if (step.checkEquipmentKind && (equipment == null || equipment.CurrentKind != step.equipmentKind)) return;
        currentCount++;
        lastCountedFrame = Time.frameCount;
        Changed?.Invoke();
        if (currentCount < Mathf.Max(1, step.requiredCount)) return;
        changingStep = true;
        try { step.onCompleted.Invoke(); }
        finally { changingStep = false; }
        if (running && (standaloneTest || (flow != null && flow.Revision == revision))) NextStep();
    }
    public void OnInspected(UnityEngine.Object target) => Report(DemoAction.Inspect, target);
    public void OnDialogueCompleted(UnityEngine.Object target) => Report(DemoAction.Dialogue, target);
    public void OnDoorOpened(UnityEngine.Object target) => Report(DemoAction.DoorOpened, target);
    private void LateUpdate()
    {
        if (!standaloneTest && (flow == null || flow.Revision != revision)) { StopTutorial(); return; }
        if (!sequencePending) return;
        sequencePending = false;
        GameFlowStage stage = requestedStage;
        int completedRevision = revision;
        onSequenceCompleted.Invoke();
        if (!standaloneTest && flow != null) flow.TryCompleteStage(stage, completedRevision, this);
    }
    public void OnTrainingTargetDied()
    {
        if (!standaloneTest || !running || currentStep < 0 || currentStep >= steps.Length) return;
        Step step = steps[currentStep];
        if (step.action != DemoAction.ShotHit) return;
        if (step.target is EnemyHealth enemy) enemy.ResetHealth();
        else if (step.target is CombatTarget target) target.ResetTarget();
    }
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
        foreach (Step step in steps)
        {
            if (step == null) continue;
            if (step.target is EnemyHealth enemy) enemy.ResetHealth();
            else if (step.target is CombatTarget target) target.ResetTarget();
        }
        BeginTutorial();
    }
    public void StopTutorial() { running = false; IsCompleted = false; sequencePending = false; currentCount = 0; Changed?.Invoke(); }
}
