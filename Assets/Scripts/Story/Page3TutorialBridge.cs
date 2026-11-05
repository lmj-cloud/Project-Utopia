using UnityEngine;

/// <summary>실제 인벤토리 행동을 기존 TutorialManager에 전달한다. 단계·표시 시스템을 별도로 만들지 않는다.</summary>
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
    private bool vehicleTutorialStarted;
    public bool MedicineTutorialCompleted { get { return started && tutorial.StepNumber >= 5; } }
    public void BeginVehicleTutorial() { vehicleTutorialStarted = true; }
    private bool started;
    public bool PickedUp { get; private set; }
    public bool OpenedInventory { get; private set; }
    public bool Registered { get; private set; }
    public bool Used { get; private set; }
    private void OnEnable()
    {
        if (inventory != null) inventory.ItemAdded += Added;
        if (itemUse != null) itemUse.ItemUsed += ItemUsed;
    }
    public void Begin()
    {
        if (started) return;
        started = true;
        tutorial.BeginTutorial();
    }
    private void Added(ItemData item, int count) { if (started && item == medicine && count > 0) PickedUp = true; }
    private void ItemUsed(ItemData item) { if (started && item == medicine) Used = true; }
    private void LateUpdate()
    {
        if (!started || tutorial.IsCompleted) return;
        if (inventoryUI.IsOpen) OpenedInventory = true;
        for (int i = 0; i < PlayerInventory.EquippedSlotCount; i++)
        {
            InventoryEntry entry = inventory.GetEntry(true, i);
            if (entry != null && entry.Item == medicine && entry.Quantity > 0) Registered = true;
        }
        if (tutorial.StepNumber >= 5 && !vehicleTutorialStarted) return;
        if (tutorial.DisplayState == TutorialManager.TutorialDisplayState.WaitingForShow)
            tutorial.RequestShowCurrentTutorial();
        if (tutorial.DisplayState != TutorialManager.TutorialDisplayState.Active || !gameplayUI.CanCountTutorial(tutorial)) return;
        // 인벤토리/컷씬이 HUD를 숨긴 동안에는 관찰 결과를 보관하고 표시가 복구된 뒤 보고한다.
        switch (tutorial.StepNumber)
        {
            case 1: if (PickedUp) tutorial.Report(DemoAction.Pickup, medicine); break;
            case 2: if (OpenedInventory) tutorial.Report(DemoAction.Inspect, inventoryUI); break;
            case 3: if (Registered) tutorial.Report(DemoAction.Equip, medicine); break;
            case 4: if (Used) tutorial.Report(DemoAction.Medication, medicine); break;
            case 5: if (Vector3.Distance(player.transform.position, vehicle.transform.position) < 3 || vehicle.IsOccupied) tutorial.Report(DemoAction.Move, vehicle); break;
            case 6: if (vehicle.IsOccupied && vehicle.Driver == player) tutorial.Report(DemoAction.Equip, vehicle); break;
        }
    }
    private void OnDisable()
    {
        if (inventory != null) inventory.ItemAdded -= Added;
        if (itemUse != null) itemUse.ItemUsed -= ItemUsed;
    }
}
