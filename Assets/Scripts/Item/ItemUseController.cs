using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public enum ActionTimingMode { Timer = 0, AnimationEvent = 1 }

public enum MedicineSource { Inventory = 0, World = 1 }

public class ItemUseController : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private StoryProgress storyProgress;
    [SerializeField, HideInInspector] private PlayerAnimatorController animator;
    [SerializeField] private ItemData medicine;
    [SerializeField] private string requiredFlag;
    [SerializeField, HideInInspector] private ActionTimingMode timing;
    [SerializeField, HideInInspector] private float useDuration;
    [SerializeField, HideInInspector] private float eventTimeout;
    [SerializeField] private UnityEvent onStarted = new();
    [SerializeField] private UnityEvent onCompleted = new();
    [SerializeField] private UnityEvent onCancelled = new();
    [SerializeField] private UnityEvent onFailed = new();
    [SerializeField] private CutsceneController useTimeline;
    [SerializeField] private InteractableObject worldMedicine;
    [Serializable]
    public class ItemUseBinding
    {
        public ItemData item;
        public CutsceneController timeline;
        public UnityEvent onStarted = new();
        public UnityEvent onCompleted = new();
        public UnityEvent onCancelled = new();
        public UnityEvent onFailed = new();
    }
    [Header("Equipped Item Actions (scene references)")]
    [SerializeField] private ItemUseBinding[] itemActions = new ItemUseBinding[0];
    private CutsceneController activeTimeline;
    private ItemUseBinding activeBinding;
    private ItemData activeItem;
    private bool equippedUse;
    private int activeSlot = -1;
    private int activeConsume;
    private bool activeMedication;
    public event Action<ItemData> ItemUsed;
    private MedicineSource activeSource;
    private int timelineId;
    private bool timelineConfirmed;
    private int confirmedRequestId = -1;
    private bool usingItem;
    [SerializeField] private GameFlowManager flow;
    private GameFlowStage requestedStage;
    private int flowRevision;
    private int requestId;
    public int RequestId => requestId;
    public bool IsUsing => usingItem;
    public event Action<DemoAction, UnityEngine.Object> Succeeded;
    public void UseMedicine() => TryUseMedicine();
    public bool TryUseMedicine()
    {
        return TryUse(MedicineSource.Inventory);
    }
    public void UseWorldMedicine() => TryUseWorldMedicine();
    public bool TryUseWorldMedicine() => TryUse(MedicineSource.World);
    public void UseSelectedItem() => TryUseSelectedItem();
    public bool TryUseSelectedItem()
    {
        if (usingItem || !isActiveAndEnabled || playerController == null || !playerController.CanControlPlayer || inventory == null) return false;
        InventoryEntry entry = inventory.SelectedEntry;
        if (entry == null || entry.Item == null || !entry.Item.CanUse) return false;
        ItemData item = entry.Item;
        ItemUseBinding binding = null;
        if (itemActions != null) foreach (ItemUseBinding candidate in itemActions)
            if (candidate != null && candidate.item == item) { binding = candidate; break; }
        CutsceneController timeline = binding != null ? binding.timeline : item == medicine ? useTimeline : null;
        bool medication = item.IsMedication || item == medicine;
        int required = item.IsConsumable ? item.ConsumePerUse : 1;
        if (entry.Quantity < required || timeline == null || timeline.IsPlaying || !timeline.CanPlay() ||
            (medication && !string.IsNullOrWhiteSpace(requiredFlag) && (storyProgress == null || !storyProgress.HasFlag(requiredFlag))))
        { binding?.onFailed.Invoke(); onFailed.Invoke(); return false; }
        int slot = inventory.SelectedEquippedSlot;
        if (!inventory.BeginEquippedUse(this, slot, item, required)) return false;
        equippedUse = true; activeSlot = slot; activeItem = item; activeConsume = item.IsConsumable ? item.ConsumePerUse : 0;
        activeMedication = medication; activeBinding = binding; activeTimeline = timeline; activeSource = MedicineSource.Inventory;
        usingItem = true; requestId++;
        if (flow != null) { requestedStage = flow.CurrentStage; flowRevision = flow.Revision; }
        timeline.Completed += OnTimelineCompleted; timeline.Cancelled += OnTimelineCancelled;
        if (!timeline.TryPlay(this, out timelineId)) { Finish(); binding?.onFailed.Invoke(); onFailed.Invoke(); return false; }
        binding?.onStarted.Invoke(); if (medication) onStarted.Invoke(); return usingItem;
    }
    private bool TryUse(MedicineSource source)
    {
        if (usingItem) return false;
        bool available = source == MedicineSource.Inventory
            ? inventory != null && medicine != null && inventory.HasItem(medicine)
            : medicine != null && worldMedicine != null && worldMedicine.IsExternalInteractionPending && !worldMedicine.IsPickup;
        if (!isActiveAndEnabled || playerController == null || !playerController.CanControlPlayer || !available ||
            useTimeline == null || useTimeline.IsPlaying || !useTimeline.CanPlay() ||
            (!string.IsNullOrWhiteSpace(requiredFlag) && (storyProgress == null || !storyProgress.HasFlag(requiredFlag))))
        {
            if (source == MedicineSource.World && worldMedicine != null) worldMedicine.CancelExternalInteraction();
            onFailed.Invoke();
            return false;
        }
        activeTimeline = useTimeline; activeBinding = null; activeItem = medicine;
        equippedUse = false; activeMedication = true;
        usingItem = true;
        activeSource = source;
        requestId++;
        if (flow != null) { requestedStage = flow.CurrentStage; flowRevision = flow.Revision; }
        useTimeline.Completed += OnTimelineCompleted;
        useTimeline.Cancelled += OnTimelineCancelled;
        if (!useTimeline.TryPlay(this, out timelineId))
        {
            Finish();
            if (source == MedicineSource.World && worldMedicine != null) worldMedicine.CancelExternalInteraction();
            onFailed.Invoke();
            return false;
        }
        onStarted.Invoke();
        return usingItem;
    }
    private void OnTimelineCompleted(CutsceneController source, int id)
    {
        if (!usingItem || source != activeTimeline || id != timelineId) return;
        if (!isActiveAndEnabled || playerController == null || !playerController.IsAlive) { CancelUse(); return; }
        timelineConfirmed = true;
        confirmedRequestId = requestId;
        try { CompleteUse(requestId); }
        finally { timelineConfirmed = false; }
    }
    private void OnTimelineCancelled(CutsceneController source, int id)
    {
        if (usingItem && source == activeTimeline && id == timelineId) CancelUse();
    }
    public void CompleteUse(int id)
    {
        if (!usingItem || id != requestId || id != confirmedRequestId || !timelineConfirmed) return;
        GameFlowStage stage = requestedStage;
        int revision = flowRevision;
        MedicineSource source = activeSource;
        ItemData item = activeItem;
        InteractableObject world = worldMedicine;
        bool selected = equippedUse;
        int slot = activeSlot, consume = activeConsume;
        bool medication = activeMedication;
        ItemUseBinding binding = activeBinding;
        Finish(selected); // Keep the reservation until the atomic data commit, after clearing callbacks.
        bool success = selected
            ? inventory != null && inventory.CompleteEquippedUse(this, slot, item, consume)
            : source == MedicineSource.Inventory
                ? inventory != null && item != null && inventory.TryRemove(item, 1)
                : world != null && world.TryConsumeExternalInteraction();
        if (success)
        {
            ItemUsed?.Invoke(item);
            if (medication) Succeeded?.Invoke(DemoAction.Medication, item);
            binding?.onCompleted.Invoke();
            if (medication) onCompleted.Invoke();
            if (medication && flow != null) flow.TryCompleteStage(stage, revision, this);
        }
        else
        {
            if (inventory != null) inventory.ReleaseUse(this);
            if (!selected && source == MedicineSource.World && world != null) world.CancelExternalInteraction();
            binding?.onFailed.Invoke(); onFailed.Invoke();
        }
    }
    public void CompleteUseFromAnimation(int id) { if (timing == ActionTimingMode.AnimationEvent) CompleteUse(id); }
    private void Finish(bool keepReservation = false)
    {
        usingItem = false;
        confirmedRequestId = -1;
        requestId++;
        if (activeTimeline != null)
        {
            activeTimeline.Completed -= OnTimelineCompleted;
            activeTimeline.Cancelled -= OnTimelineCancelled;
        }
        activeTimeline = null;
        if (!keepReservation && inventory != null) inventory.ReleaseUse(this);
        if (playerController != null) playerController.ReleaseControlLock(this);
    }
    public void CancelUse()
    {
        if (!usingItem) return;
        CutsceneController source = activeTimeline;
        ItemUseBinding binding = activeBinding;
        bool selected = equippedUse;
        int id = timelineId;
        MedicineSource medicineSource = activeSource;
        Finish();
        if (source != null) source.StopOwned(this, id);
        if (!selected && medicineSource == MedicineSource.World && worldMedicine != null) worldMedicine.CancelExternalInteraction();
        binding?.onCancelled.Invoke();
        onCancelled.Invoke();
    }
    private void Update() { if (usingItem && (playerController == null || !playerController.IsAlive || (equippedUse && (inventory == null || !inventory.IsUseLocked)))) CancelUse(); }
    private void OnDisable() => CancelUse();
}
