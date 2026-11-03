using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 공격 판정 시점을 타이머 또는 Animation Event 중 어느 경로로 결정할지 구분한다.
/// </summary>
public enum ActionTimingMode { Timer = 0, AnimationEvent = 1 }

/// <summary>
/// 복약 아이템이 인벤토리 또는 월드 오브젝트에서 제공되는지 구분한다.
/// </summary>
public enum MedicineSource { Inventory = 0, World = 1 }

/// <summary>
/// 아이템 소유와 사용 조건을 검사하고 사용 예약, 컷신 완료 확인, 소비와 취소를 처리한다.
/// </summary>
public class ItemUseController : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private StoryProgress storyProgress;
    [SerializeField, HideInInspector] private PlayerAnimatorController animator;
    [SerializeField] private ItemData medicine;
    // 아이템 사용이나 진행의 선행 조건으로 확인할 스토리 플래그.
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
    /// <summary>
    /// 사용 아이템과 연결된 컷신, 시작·완료·취소 알림을 묶는다.
    /// </summary>
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
    // 행동 시작 시 보관한 진행 버전. 완료 시 현재 단계와 함께 검증한다.
    private int flowRevision;
    // 비동기 타격·완료 콜백이 현재 행동에 속하는지 구분하는 요청 번호.
    private int requestId;
    public int RequestId => requestId;
    public bool IsUsing => usingItem;
    public event Action<DemoAction, UnityEngine.Object> Succeeded;
    /// <summary>
    /// 기존 외부 호출 경로를 통해 인벤토리 복약을 요청한다.
    /// </summary>
    public void UseMedicine() => TryUseMedicine();
    /// <summary>
    /// 인벤토리에 연결된 약의 사용 조건과 컷신 준비 여부를 검사하여 사용을 요청한다.
    /// </summary>
    public bool TryUseMedicine()
    {
        return TryUse(MedicineSource.Inventory);
    }
    /// <summary>
    /// 월드에 놓인 약을 사용하는 기존 호출 경로를 전달한다.
    /// </summary>
    public void UseWorldMedicine() => TryUseWorldMedicine();
    /// <summary>
    /// 월드 약 오브젝트와 복약 조건을 검사하여 사용을 요청한다.
    /// </summary>
    public bool TryUseWorldMedicine() => TryUse(MedicineSource.World);
    /// <summary>
    /// 현재 선택된 퀵슬롯 아이템의 사용을 요청한다.
    /// </summary>
    public void UseSelectedItem() => TryUseSelectedItem();
    /// <summary>
    /// 퀵슬롯 아이템과 사용 연출 연결을 검사하고 실제 사용 예약을 시작한다.
    /// </summary>
    public bool TryUseSelectedItem()
    {
        if (inventory == null) return false;
        return TryUseEquippedItem(inventory.SelectedEquippedSlot);
    }

    /// <summary>
    /// 원형 UI가 고른 슬롯을 직접 사용한다. 기존 예약과 Timeline 완료 후 소비 경로를 공유한다.
    /// </summary>
    public bool TryUseEquippedItem(int slotIndex)
    {
        if (usingItem || !isActiveAndEnabled || playerController == null || !playerController.CanControlPlayer || inventory == null) return false;
        InventoryEntry entry = inventory.GetEntry(true, slotIndex);
        if (entry == null || entry.Item == null) return false;
        // 등록 가능 여부와 실제 사용은 별개다. 사용 불가 설정은 실패 알림만 보내고 소비하지 않는다.
        if (!entry.Item.CanUse)
        {
            onFailed.Invoke();
            return false;
        }
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
        int slot = slotIndex;
        if (!inventory.BeginEquippedUse(this, slot, item, required)) return false;
        equippedUse = true; activeSlot = slot; activeItem = item; activeConsume = item.IsConsumable ? item.ConsumePerUse : 0;
        activeMedication = medication; activeBinding = binding; activeTimeline = timeline; activeSource = MedicineSource.Inventory;
        usingItem = true; requestId++;
        if (flow != null) { requestedStage = flow.CurrentStage; flowRevision = flow.Revision; }
        timeline.Completed += OnTimelineCompleted; timeline.Cancelled += OnTimelineCancelled;
        if (!timeline.TryPlay(this, out timelineId)) { Finish(); binding?.onFailed.Invoke(); onFailed.Invoke(); return false; }
        binding?.onStarted.Invoke(); if (medication) onStarted.Invoke(); return usingItem;
    }
    /// <summary>
    /// 아이템 출처·조건과 Timeline 준비를 확인하고 현재 사용 요청을 예약한다.
    /// </summary>
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
    /// <summary>
    /// 연결된 컷신 완료가 현재 요청과 일치할 때만 후속 완료를 처리한다.
    /// </summary>
    private void OnTimelineCompleted(CutsceneController source, int id)
    {
        if (!usingItem || source != activeTimeline || id != timelineId) return;
        if (!isActiveAndEnabled || playerController == null || !playerController.IsAlive) { CancelUse(); return; }
        timelineConfirmed = true;
        confirmedRequestId = requestId;
        try { CompleteUse(requestId); }
        finally { timelineConfirmed = false; }
    }
    /// <summary>
    /// 현재 요청에 해당하는 컷신 취소를 받아 진행 중 행동을 정리한다.
    /// </summary>
    private void OnTimelineCancelled(CutsceneController source, int id)
    {
        if (usingItem && source == activeTimeline && id == timelineId) CancelUse();
    }
    /// <summary>
    /// 확인된 컷신 완료 요청에 대해서만 아이템 소비와 완료 알림을 확정한다.
    /// </summary>
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
    /// <summary>
    /// 현재 사용 요청 번호와 일치하는 완료 알림인지 검사한다.
    /// </summary>
    public void CompleteUseFromAnimation(int id) { if (timing == ActionTimingMode.AnimationEvent) CompleteUse(id); }
    /// <summary>
    /// 현재 아이템 사용의 Timeline 연결과 예약·잠금 상태를 정리한다.
    /// </summary>
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
    /// <summary>
    /// 진행 중 사용을 취소하고 아이템을 소비하지 않은 채 예약·제어 상태를 복구한다.
    /// </summary>
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
    /// <summary>
    /// 매 프레임 현재 상태와 입력을 확인하여 이 컴포넌트의 동작을 갱신한다.
    /// </summary>
    private void Update() { if (usingItem && (playerController == null || !playerController.IsAlive || (equippedUse && (inventory == null || !inventory.IsUseLocked)))) CancelUse(); }
    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable() => CancelUse();
}
