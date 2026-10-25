using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보관 중인 아이템 스택과 수량 및 저장·복원용 슬롯 정보를 보관한다.
/// </summary>
[Serializable]
public class InventoryEntry
{
    [SerializeField] private ItemData item;
    [SerializeField] private int quantity;
    // Snapshot metadata; live entries remain single stacks, not HUD copies.
    // 저장·복원용 원래 보관 슬롯 번호. 일반 UI 표시 복제본의 소유 수량과 구분한다.
    internal int snapshotSlot = -1;
    // 저장 시 퀵슬롯 배치 정보가 포함되어 있는지 구분한다.
    internal bool snapshotEquipped;
    // 저장 시 선택된 퀵슬롯 정보를 전달하는 메타데이터.
    internal int snapshotSelection = -2;
    public ItemData Item => item;
    public int Quantity => quantity;
    internal ItemData snapshotAssignment;
    /// <summary>
    /// 필요한 인자와 기본 상태로 아이템 스택을 구성한다.
    /// </summary>
    public InventoryEntry(ItemData item, int quantity) { this.item = item; this.quantity = quantity; }
    /// <summary>
    /// 해당 인벤토리 스택의 수량을 지정 증감량으로 변경한다.
    /// </summary>
    internal void ChangeQuantity(int amount) => quantity += amount;
}

/// <summary>
/// 보관 스택, 퀵슬롯 등록과 선택 상태를 관리하며 아이템 사용 예약 및 저장·복원을 지원한다.
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    public const int EquippedSlotCount = 5;
    // 기본 보관 슬롯 수. 초기 슬롯 보정과 획득 가능 여부에 사용한다.
    [SerializeField, Min(1)] private int slotCapacity = 24;
    // 아이템을 실제로 소유하는 보관 스택 목록.
    [SerializeField] private List<InventoryEntry> entries = new();
    // 이전 직렬화 장착 스택 정보. 초기화 시 현재 보관·퀵슬롯 정책으로 이전한다.
    [SerializeField] private InventoryEntry[] equippedEntries = new InventoryEntry[EquippedSlotCount];
    // 현재 사용 대상으로 선택된 퀵슬롯 번호. 선택이 없으면 음수다.
    [SerializeField] private int selectedEquippedSlot = -1;
    // 보관 수량과 별개인 퀵슬롯 아이템 등록 정보. 소유 수량을 중복 보관하지 않는다.
    [SerializeField] private ItemData[] quickItems = new ItemData[EquippedSlotCount];
    // 인벤토리 사용 예약의 소유자. 사용 중 아이템 수량과 슬롯을 보호한다.
    private UnityEngine.Object useOwner;
    // 사용 시작 시 예약한 실제 보관 스택 참조.
    private InventoryEntry reservedEntry;
    private int reservedSlot = -1;
    public event Action Changed;
    public event Action<ItemData, int> ItemAdded;
    public IReadOnlyList<InventoryEntry> Entries => entries;
    public IReadOnlyList<InventoryEntry> EquippedEntries
    { get { InventoryEntry[] view = new InventoryEntry[EquippedSlotCount]; for (int i = 0; i < view.Length; i++) view[i] = GetEntry(true, i); return view; } }
    public int SlotCapacity => slotCapacity;
    public int SelectedEquippedSlot => selectedEquippedSlot;
    public bool IsUseLocked => useOwner != null;
    public InventoryEntry SelectedEntry => GetEntry(true, selectedEquippedSlot);
    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake() { EnsureSlots(); CorrectSelection(); }
    /// <summary>
    /// 보관·퀵슬롯 배열 크기를 보정하고 이전 직렬화 장착 스택을 유실 없이 이전한다.
    /// </summary>
    private void EnsureSlots()
    {
        slotCapacity = Mathf.Max(1, slotCapacity);
        while (entries.Count < slotCapacity) entries.Add(null);
        if (equippedEntries == null) equippedEntries = new InventoryEntry[EquippedSlotCount];
        else if (equippedEntries.Length != EquippedSlotCount) Array.Resize(ref equippedEntries, EquippedSlotCount);
        if (quickItems == null) quickItems = new ItemData[EquippedSlotCount];
        else if (quickItems.Length != EquippedSlotCount) Array.Resize(ref quickItems, EquippedSlotCount);
        // 이전 장착칸의 소유 수량은 보관칸으로 한 번만 이전한다. 수용량 초과도 버리지 않는다.
        for (int i = 0; i < EquippedSlotCount; i++)
        {
            InventoryEntry legacy = equippedEntries[i];
            if (!HasStack(legacy)) { equippedEntries[i] = null; continue; }
            int empty = entries.FindIndex(e => !HasStack(e));
            if (empty < 0) { empty = entries.Count; entries.Add(null); }
            entries[empty] = legacy;
            if (quickItems[i] == null && legacy.Item.CanUse) quickItems[i] = legacy.Item;
            equippedEntries[i] = null;
        }
    }
    /// <summary>
    /// 보관 스택 또는 퀵슬롯에 등록된 아이템의 현재 총량을 반환한다.
    /// </summary>
    public InventoryEntry GetEntry(bool equipped, int index)
    {
        EnsureSlots();
        if (equipped)
        {
            if (index < 0 || index >= EquippedSlotCount || quickItems[index] == null) return null;
            ItemData item = quickItems[index];
            long total = GetTotalQuantity(item);
            return total > 0 ? new InventoryEntry(item, (int)Math.Min(int.MaxValue, total)) : null;
        }
        return index >= 0 && index < entries.Count ? entries[index] : null;
    }

    /// <summary>
    /// 아이템 참조와 양수 수량이 있는 유효한 스택인지 확인한다.
    /// </summary>
    private static bool HasStack(InventoryEntry entry) => entry != null && entry.Item != null && entry.Quantity > 0;
    /// <summary>
    /// 같은 아이템의 모든 보관 스택 수량을 합산한다.
    /// </summary>
    public long GetTotalQuantity(ItemData item)
    {
        if (item == null) return 0;
        EnsureSlots();
        long total = 0;
        foreach (InventoryEntry e in entries) if (HasStack(e) && e.Item == item) total += e.Quantity;

        return total;
    }
    // Acquisition always goes to storage, never directly into equipped slots.
    /// <summary>
    /// 기존 스택 또는 빈 보관칸에 아이템을 추가하고 수량·선택 변경을 알린다.
    /// </summary>
    public bool TryAdd(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return false;
        EnsureSlots();
        for (int i = 0; i < entries.Count; i++)
            if (HasStack(entries[i]) && entries[i].Item == item)
            {
                if (entries[i].Quantity > int.MaxValue - quantity) return false;
                entries[i].ChangeQuantity(quantity); CorrectSelection(); Changed?.Invoke(); ItemAdded?.Invoke(item, quantity); return true;
            }
        for (int i = 0; i < entries.Count; i++)
            if (!HasStack(entries[i]))
            { entries[i] = new InventoryEntry(item, quantity); CorrectSelection(); Changed?.Invoke(); ItemAdded?.Invoke(item, quantity); return true; }
        return false;
    }
    /// <summary>
    /// 해당 아이템의 실제 총 보유량이 필요한 수량 이상인지 확인한다.
    /// </summary>
    public bool HasItem(ItemData item, int quantity = 1) => quantity > 0 && GetTotalQuantity(item) >= quantity;
    /// <summary>
    /// 사용 예약과 수량 조건을 확인한 뒤 실제 보관 스택에서 요청 수량을 제거한다.
    /// </summary>
    public bool TryRemove(ItemData item, int quantity = 1)
    {
        if (IsUseLocked || !HasItem(item, quantity)) return false;
        int remaining = quantity;
        for (int i = 0; i < entries.Count && remaining > 0; i++)
            {
                InventoryEntry e = GetEntry(false, i);
                if (!HasStack(e) || e.Item != item) continue;
                int take = Math.Min(remaining, e.Quantity); e.ChangeQuantity(-take); remaining -= take;
                if (e.Quantity == 0) SetEntry(false, i, null);
            }
        CorrectSelection(); Changed?.Invoke(); return true;
    }
    /// <summary>
    /// 지정된 보관 또는 이전 장착 슬롯의 스택 참조를 설정한다.
    /// </summary>
    private void SetEntry(bool equipped, int index, InventoryEntry value)
    { if (equipped) equippedEntries[index] = value; else entries[index] = value; }
    /// <summary>
    /// 해당 종류의 슬롯 인덱스가 허용 범위에 속하는지 검사한다.
    /// </summary>
    private bool ValidSlot(bool equipped, int index) => index >= 0 && index < (equipped ? EquippedSlotCount : entries.Count);
    /// <summary>
    /// 슬롯 종류에 해당 아이템을 배치할 수 있는지 확인한다.
    /// </summary>
    private static bool CanPlace(bool equipped, InventoryEntry entry) => !equipped || !HasStack(entry) || entry.Item.CanUse;
    /// <summary>
    /// 드래그 시작 시 아이템·수량이 그대로인지 확인한 뒤 스택 이동·병합 또는 퀵슬롯 등록을 처리한다.
    /// </summary>
    public bool TryMove(bool fromEquipped, int from, bool toEquipped, int to, ItemData expectedItem, int expectedQuantity)
    {
        EnsureSlots();
        if (IsUseLocked || !ValidSlot(fromEquipped, from) || !ValidSlot(toEquipped, to) || (fromEquipped == toEquipped && from == to)) return false;
        InventoryEntry source = GetEntry(fromEquipped, from), destination = GetEntry(toEquipped, to);
        if (!HasStack(source) || source.Item != expectedItem || source.Quantity != expectedQuantity || !CanPlace(toEquipped, source)) return false;
        if (fromEquipped || toEquipped)
        {
            if (fromEquipped && toEquipped)
            { ItemData item = quickItems[from]; quickItems[from] = quickItems[to]; quickItems[to] = item; }
            else if (toEquipped)
            {
                for (int i = 0; i < EquippedSlotCount; i++) if (quickItems[i] == source.Item) quickItems[i] = null;
                quickItems[to] = source.Item;
            }
            else
            {
                if (HasStack(destination) && destination.Item.Category != source.Item.Category) return false;
                quickItems[from] = null;
            }
            CorrectSelection(); Changed?.Invoke(); return true;
        }
        if (HasStack(destination) && destination.Item.Category != source.Item.Category) return false;
        if (HasStack(destination) && destination.Item == source.Item)
        {
            int moved = Math.Min(source.Quantity, int.MaxValue - destination.Quantity);
            if (moved <= 0) return false;
            destination.ChangeQuantity(moved); source.ChangeQuantity(-moved);
            if (source.Quantity == 0) SetEntry(fromEquipped, from, null);
        }
        else
        {
            if (!CanPlace(fromEquipped, destination)) return false;
            SetEntry(toEquipped, to, source); SetEntry(fromEquipped, from, HasStack(destination) ? destination : null);
        }
        CorrectSelection(); Changed?.Invoke(); return true;
    }
    /// <summary>
    /// 퀵슬롯 등록 아이템이 사용 가능하고 실제 보유량이 남아 있는지 확인한다.
    /// </summary>
    private bool Selectable(int i) => quickItems[i] != null && quickItems[i].CanUse && GetTotalQuantity(quickItems[i]) > 0;
    /// <summary>
    /// 아이템 수량을 버리지 않고 해당 퀵슬롯의 등록만 해제한다.
    /// </summary>
    public bool UnassignQuickSlot(int index)
    {
        EnsureSlots();
        if (IsUseLocked || index < 0 || index >= EquippedSlotCount || quickItems[index] == null) return false;
        quickItems[index] = null; CorrectSelection(); Changed?.Invoke(); return true;
    }
    /// <summary>
    /// 현재 퀵슬롯 선택이 유효하지 않으면 가능한 선택 상태로 보정한다.
    /// </summary>
    private void CorrectSelection()
    {
        if (selectedEquippedSlot >= 0 && selectedEquippedSlot < EquippedSlotCount && Selectable(selectedEquippedSlot)) return;
        selectedEquippedSlot = -1;
        for (int i = 0; i < EquippedSlotCount; i++) if (Selectable(i)) { selectedEquippedSlot = i; break; }
    }
    /// <summary>
    /// 방향 입력에 따라 사용 가능한 퀵슬롯을 순환 선택한다.
    /// </summary>
    public void SelectEquipped(int direction)
    {
        EnsureSlots(); if (IsUseLocked || direction == 0) return;
        CorrectSelection(); if (selectedEquippedSlot < 0) return;
        int old = selectedEquippedSlot;
        for (int distance = 1; distance <= EquippedSlotCount; distance++)
        {
            int next = (old + (direction > 0 ? distance : -distance) + EquippedSlotCount) % EquippedSlotCount;
            if (!Selectable(next)) continue;
            selectedEquippedSlot = next; if (next != old) Changed?.Invoke(); return;
        }
    }
    /// <summary>
    /// 선택된 아이템 스택과 슬롯을 지정 소유자에게 예약하여 사용 도중 변경을 막는다.
    /// </summary>
    public bool BeginEquippedUse(UnityEngine.Object owner, int slot, ItemData item, int quantity)
    {
        InventoryEntry entry = GetEntry(true, slot);
        if (owner == null || IsUseLocked || !HasStack(entry) || entry.Item != item || !item.CanUse || quantity <= 0 || entry.Quantity < quantity) return false;
        useOwner = owner; reservedEntry = entry; reservedSlot = slot; Changed?.Invoke(); return true;
    }
    /// <summary>
    /// 같은 사용 소유자와 아이템·슬롯인지 확인한 뒤 예약 수량을 실제 소비한다.
    /// </summary>
    public bool CompleteEquippedUse(UnityEngine.Object owner, int slot, ItemData item, int consume)
    {
        if (owner == null || useOwner != owner || slot != reservedSlot || reservedEntry == null ||
            reservedEntry.Item != item || quickItems[slot] != item || consume < 0 || GetTotalQuantity(item) < consume) return false;
        int remaining = consume;
        for (int i = 0; i < entries.Count && remaining > 0; i++)
        {
            InventoryEntry e = entries[i];
            if (!HasStack(e) || e.Item != item) continue;
            int take = Math.Min(remaining, e.Quantity); e.ChangeQuantity(-take); remaining -= take;
            if (e.Quantity == 0) entries[i] = null;
        }
        ReleaseUse(owner, false); CorrectSelection(); Changed?.Invoke(); return true;
    }
    /// <summary>
    /// 지정 소유자의 아이템 사용 예약만 해제한다.
    /// </summary>
    public void ReleaseUse(UnityEngine.Object owner, bool notify = true)
    { if (useOwner != owner) return; useOwner = null; reservedEntry = null; reservedSlot = -1; if (notify) Changed?.Invoke(); }
    /// <summary>
    /// 사용 예약을 고려하여 인벤토리와 퀵슬롯 상태를 초기화한다.
    /// </summary>
    public void ClearInventory()
    {
        entries.Clear(); equippedEntries = new InventoryEntry[EquippedSlotCount]; quickItems = new ItemData[EquippedSlotCount]; selectedEquippedSlot = -1;
        useOwner = null; reservedEntry = null; reservedSlot = -1; EnsureSlots(); Changed?.Invoke();
    }
    /// <summary>
    /// 스택 수량과 퀵슬롯 배치·선택을 독립된 저장 목록으로 복사한다.
    /// </summary>
    public List<InventoryEntry> CaptureInventory()
    {
        EnsureSlots(); List<InventoryEntry> snapshot = new();
        snapshot.Add(new InventoryEntry(null, 0) { snapshotSelection = selectedEquippedSlot });
        for (int i = 0; i < entries.Count; i++)
            {
                InventoryEntry e = GetEntry(false, i);
                if (HasStack(e)) snapshot.Add(new InventoryEntry(e.Item, e.Quantity) { snapshotSlot = i, snapshotEquipped = false });
            }
        for (int i = 0; i < EquippedSlotCount; i++)
            if (quickItems[i] != null) snapshot.Add(new InventoryEntry(null, 0) { snapshotSlot = i, snapshotAssignment = quickItems[i] });
        return snapshot;
    }
    /// <summary>
    /// 저장 스택과 메타데이터로 보관·퀵슬롯·선택을 복원하고 변경을 알린다.
    /// </summary>
    public void RestoreInventory(List<InventoryEntry> snapshot)
    {
        entries.Clear(); equippedEntries = new InventoryEntry[EquippedSlotCount]; quickItems = new ItemData[EquippedSlotCount]; selectedEquippedSlot = -1;
        useOwner = null; reservedEntry = null; reservedSlot = -1; EnsureSlots();
        if (snapshot != null) foreach (InventoryEntry e in snapshot)
        {
            if (e == null) continue;
            if (e.snapshotSelection != -2) { selectedEquippedSlot = e.snapshotSelection; continue; }
            if (e.snapshotAssignment != null)
            { if (e.snapshotSlot >= 0 && e.snapshotSlot < EquippedSlotCount) quickItems[e.snapshotSlot] = e.snapshotAssignment; continue; }
            if (!HasStack(e)) continue;
            int index = e.snapshotSlot;
            if (index < 0) index = entries.FindIndex(x => !HasStack(x)); // legacy snapshots
            if (!e.snapshotEquipped && index >= entries.Count) while (entries.Count <= index) entries.Add(null);
            if (!ValidSlot(e.snapshotEquipped, index)) continue;
            SetEntry(e.snapshotEquipped, index, new InventoryEntry(e.Item, e.Quantity));
        }
        EnsureSlots(); CorrectSelection(); Changed?.Invoke();
    }
}
