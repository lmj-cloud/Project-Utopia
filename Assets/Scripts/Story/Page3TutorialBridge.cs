using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>실제 등록·닫기·사용 이력을 보존하여 한 번의 약 복용을 여러 안내 때문에 반복시키지 않는다.</summary>
[DefaultExecutionOrder(200)]
public class Page3TutorialBridge : MonoBehaviour
{
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField] private ItemUseController itemUse;
    [SerializeField] private ItemData medicine;
    [SerializeField] private VehicleController vehicle;
    [SerializeField] private PlayerController player;
    [SerializeField] private StoryProgress storyProgress;
    [SerializeField] private string registrationFlag = "Page3MedicineRegisteredAndClosed";
    private bool vehicleTutorialStarted;
    private bool started;
    private bool wasInventoryOpen;
    private bool straightArrived;
    private PlayerInteraction playerInteraction;
    // 기존 상호작용 시스템이 선택한 차량만 도착으로 인정한다. 별도 거리 판정을 만들지 않는다.
    public bool VehicleInReach
    {
        get
        {
            if (playerInteraction == null && player != null) playerInteraction = player.GetComponent<PlayerInteraction>();
            return playerInteraction != null && playerInteraction.CurrentTarget != null &&
                playerInteraction.CurrentTarget.GetComponent<VehicleController>() == vehicle && vehicle.CanEnter;
        }
    }
    public bool MedicineTutorialCompleted { get { return started && tutorial.StepNumber >= 4; } }
    public bool PickedUp { get; private set; }
    public bool OpenedInventory { get; private set; }
    public bool Registered { get; private set; }
    public bool ClosedAfterRegistration { get; private set; }
    public bool Used { get; private set; }
    public bool UsedViaQuickSlot { get; private set; }
    public void BeginVehicleTutorial() { vehicleTutorialStarted = true; }
    public void StraightArrived()
    {
        if (vehicleTutorialStarted && vehicle.IsOccupied && vehicle.Driver == player) straightArrived = true;
    }
    private void OnEnable()
    {
        if (inventory != null) inventory.ItemAdded += Added;
        if (itemUse != null) itemUse.ItemUsed += ItemUsed;
        if (inventoryUI != null) inventoryUI.StorageItemEquipped += EquippedByDoubleClick;
    }
    public void Begin()
    {
        if (started) return;
        started = true;
        tutorial.BeginTutorial();
    }
    private void Added(ItemData item, int count) { if (started && item == medicine && count > 0) PickedUp = true; }
    private void EquippedByDoubleClick(ItemData item)
    {
        if (!started || item != medicine || !inventoryUI.IsOpen) return;
        OpenedInventory = true;
        Registered = true;
        ClosedAfterRegistration = false;
        wasInventoryOpen = true;
    }
    private void ItemUsed(ItemData item)
    {
        if (!started || item != medicine) return;
        Used = true;
        UsedViaQuickSlot = itemUse.LastSuccessfulUseWasEquipped;
    }
    private bool HasEquippedMedicine()
    {
        for (int i = 0; i < PlayerInventory.EquippedSlotCount; i++)
        {
            InventoryEntry entry = inventory.GetEntry(true, i);
            if (entry != null && entry.Item == medicine && entry.Quantity > 0) return true;
        }
        return false;
    }
    private void LateUpdate()
    {
        if (!started || tutorial.IsCompleted) return;
        bool open = inventoryUI.IsOpen;
        if (open) OpenedInventory = true;
        if (wasInventoryOpen && !open && Registered && HasEquippedMedicine())
        {
            ClosedAfterRegistration = true;

        }
        wasInventoryOpen = open;
        // 실제 등록·닫기를 마치고 복용 안내 단계에 도달한 뒤에만 기존 약 사용 조건을 연다.
        if (storyProgress != null && ClosedAfterRegistration && Registered &&
            tutorial.StepNumber >= 3 && (Used || HasEquippedMedicine()))
            storyProgress.SetFlag(registrationFlag);
        // 실제 등록을 해제했다면 복용 준비 플래그도 해제한다. 소비 확정 뒤에는 성공 이력을 보존한다.
        if (!Used && !HasEquippedMedicine() && storyProgress != null) storyProgress.RemoveFlag(registrationFlag);
        if (tutorial.StepNumber >= 4 && !vehicleTutorialStarted) return;
        if (tutorial.DisplayState == TutorialManager.TutorialDisplayState.WaitingForShow)
            tutorial.RequestShowCurrentTutorial();
        if (tutorial.DisplayState != TutorialManager.TutorialDisplayState.Active || !gameplayUI.CanCountTutorial(tutorial)) return;
        switch (tutorial.StepNumber)
        {
            case 1: if (PickedUp) tutorial.Report(DemoAction.Pickup, medicine); break;
            case 2: if (OpenedInventory && Registered && (Used || HasEquippedMedicine())) tutorial.Report(DemoAction.Equip, medicine); break;
            case 3: if (UsedViaQuickSlot && ClosedAfterRegistration) tutorial.Report(DemoAction.Medication, medicine); break;
            case 4:
                if (VehicleInReach || (vehicle.IsOccupied && vehicle.Driver == player)) tutorial.Report(DemoAction.Inspect, vehicle);
                break;
            case 5:
                if (vehicle.IsOccupied && vehicle.Driver == player) tutorial.Report(DemoAction.Inspect, vehicle);
                break;
            case 6:
                Rigidbody body = vehicle.GetComponent<Rigidbody>();
                if (vehicle.IsOccupied && vehicle.Driver == player && !player.HasOtherControlLock(vehicle) &&
                    Keyboard.current != null && Keyboard.current.wKey.isPressed && body != null &&
                    Vector3.Dot(body.linearVelocity, vehicle.transform.forward) > .15f)
                    tutorial.Report(DemoAction.Move, vehicle);
                break;
            case 7: if (straightArrived) tutorial.Report(DemoAction.Inspect, vehicle); break;
        }
    }
    private void OnDisable()
    {
        if (inventory != null) inventory.ItemAdded -= Added;
        if (itemUse != null) itemUse.ItemUsed -= ItemUsed;
        if (inventoryUI != null) inventoryUI.StorageItemEquipped -= EquippedByDoubleClick;
    }
}
