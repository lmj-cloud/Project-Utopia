using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventoryEntry
{
    [SerializeField] private ItemData item;
    [SerializeField] private int quantity;
    // Snapshot metadata; live entries remain single stacks, not HUD copies.
    internal int snapshotSlot = -1;
    internal bool snapshotEquipped;
    internal int snapshotSelection = -2;
    public ItemData Item => item;
    public int Quantity => quantity;
    public InventoryEntry(ItemData item, int quantity) { this.item = item; this.quantity = quantity; }
    internal void ChangeQuantity(int amount) => quantity += amount;
}

public class PlayerInventory : MonoBehaviour
{
    public const int EquippedSlotCount = 5;
    [SerializeField, Min(1)] private int slotCapacity = 24;
    [SerializeField] private List<InventoryEntry> entries = new();
    [SerializeField] private InventoryEntry[] equippedEntries = new InventoryEntry[EquippedSlotCount];
    [SerializeField] private int selectedEquippedSlot = -1;
    private UnityEngine.Object useOwner;
    private InventoryEntry reservedEntry;
    private int reservedSlot = -1;
    public event Action Changed;
    public event Action<ItemData, int> ItemAdded;
    public IReadOnlyList<InventoryEntry> Entries => entries;
    public IReadOnlyList<InventoryEntry> EquippedEntries => equippedEntries;
    public int SlotCapacity => slotCapacity;
    public int SelectedEquippedSlot => selectedEquippedSlot;
    public bool IsUseLocked => useOwner != null;
    public InventoryEntry SelectedEntry => GetEntry(true, selectedEquippedSlot);
    private void Awake() { EnsureSlots(); CorrectSelection(); }
    private void EnsureSlots()
    {
        slotCapacity = Mathf.Max(1, slotCapacity);
        while (entries.Count < slotCapacity) entries.Add(null);
        if (equippedEntries == null) equippedEntries = new InventoryEntry[EquippedSlotCount];
        else if (equippedEntries.Length != EquippedSlotCount) Array.Resize(ref equippedEntries, EquippedSlotCount);
    }
    public InventoryEntry GetEntry(bool equipped, int index)
    {
        EnsureSlots();
        return index >= 0 && index < (equipped ? EquippedSlotCount : entries.Count)
            ? equipped ? equippedEntries[index] : entries[index] : null;
    }
    private static bool HasStack(InventoryEntry entry) => entry != null && entry.Item != null && entry.Quantity > 0;
    public long GetTotalQuantity(ItemData item)
    {
        if (item == null) return 0;
        EnsureSlots();
        long total = 0;
        foreach (InventoryEntry e in entries) if (HasStack(e) && e.Item == item) total += e.Quantity;
        foreach (InventoryEntry e in equippedEntries) if (HasStack(e) && e.Item == item) total += e.Quantity;
        return total;
    }
    // Acquisition always goes to storage, never directly into equipped slots.
    public bool TryAdd(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return false;
        EnsureSlots();
        for (int i = 0; i < entries.Count; i++)
            if (HasStack(entries[i]) && entries[i].Item == item)
            {
                if (entries[i].Quantity > int.MaxValue - quantity) return false;
                entries[i].ChangeQuantity(quantity); Changed?.Invoke(); ItemAdded?.Invoke(item, quantity); return true;
            }
        for (int i = 0; i < entries.Count; i++)
            if (!HasStack(entries[i]))
            { entries[i] = new InventoryEntry(item, quantity); Changed?.Invoke(); ItemAdded?.Invoke(item, quantity); return true; }
        return false;
    }
    public bool HasItem(ItemData item, int quantity = 1) => quantity > 0 && GetTotalQuantity(item) >= quantity;
    public bool TryRemove(ItemData item, int quantity = 1)
    {
        if (IsUseLocked || !HasItem(item, quantity)) return false;
        int remaining = quantity;
        for (int area = 0; area < 2 && remaining > 0; area++)
            for (int i = 0; i < (area == 0 ? entries.Count : EquippedSlotCount) && remaining > 0; i++)
            {
                InventoryEntry e = GetEntry(area == 1, i);
                if (!HasStack(e) || e.Item != item) continue;
                int take = Math.Min(remaining, e.Quantity); e.ChangeQuantity(-take); remaining -= take;
                if (e.Quantity == 0) SetEntry(area == 1, i, null);
            }
        CorrectSelection(); Changed?.Invoke(); return true;
    }
    private void SetEntry(bool equipped, int index, InventoryEntry value)
    { if (equipped) equippedEntries[index] = value; else entries[index] = value; }
    private bool ValidSlot(bool equipped, int index) => index >= 0 && index < (equipped ? EquippedSlotCount : entries.Count);
    private static bool CanPlace(bool equipped, InventoryEntry entry) => !equipped || !HasStack(entry) || entry.Item.CanUse;
    public bool TryMove(bool fromEquipped, int from, bool toEquipped, int to, ItemData expectedItem, int expectedQuantity)
    {
        EnsureSlots();
        if (IsUseLocked || !ValidSlot(fromEquipped, from) || !ValidSlot(toEquipped, to) || (fromEquipped == toEquipped && from == to)) return false;
        InventoryEntry source = GetEntry(fromEquipped, from), destination = GetEntry(toEquipped, to);
        if (!HasStack(source) || source.Item != expectedItem || source.Quantity != expectedQuantity || !CanPlace(toEquipped, source)) return false;
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
    private bool Selectable(int i) => HasStack(equippedEntries[i]) && equippedEntries[i].Item.CanUse;
    private void CorrectSelection()
    {
        if (selectedEquippedSlot >= 0 && selectedEquippedSlot < EquippedSlotCount && Selectable(selectedEquippedSlot)) return;
        selectedEquippedSlot = -1;
        for (int i = 0; i < EquippedSlotCount; i++) if (Selectable(i)) { selectedEquippedSlot = i; break; }
    }
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
    public bool BeginEquippedUse(UnityEngine.Object owner, int slot, ItemData item, int quantity)
    {
        InventoryEntry entry = GetEntry(true, slot);
        if (owner == null || IsUseLocked || !HasStack(entry) || entry.Item != item || !item.CanUse || quantity <= 0 || entry.Quantity < quantity) return false;
        useOwner = owner; reservedEntry = entry; reservedSlot = slot; Changed?.Invoke(); return true;
    }
    public bool CompleteEquippedUse(UnityEngine.Object owner, int slot, ItemData item, int consume)
    {
        if (owner == null || useOwner != owner || slot != reservedSlot || GetEntry(true, slot) != reservedEntry || reservedEntry.Item != item || consume < 0 || reservedEntry.Quantity < consume) return false;
        reservedEntry.ChangeQuantity(-consume);
        if (reservedEntry.Quantity == 0) equippedEntries[slot] = null;
        ReleaseUse(owner, false); CorrectSelection(); Changed?.Invoke(); return true;
    }
    public void ReleaseUse(UnityEngine.Object owner, bool notify = true)
    { if (useOwner != owner) return; useOwner = null; reservedEntry = null; reservedSlot = -1; if (notify) Changed?.Invoke(); }
    public void ClearInventory()
    {
        entries.Clear(); equippedEntries = new InventoryEntry[EquippedSlotCount]; selectedEquippedSlot = -1;
        useOwner = null; reservedEntry = null; reservedSlot = -1; EnsureSlots(); Changed?.Invoke();
    }
    public List<InventoryEntry> CaptureInventory()
    {
        EnsureSlots(); List<InventoryEntry> snapshot = new();
        snapshot.Add(new InventoryEntry(null, 0) { snapshotSelection = selectedEquippedSlot });
        for (int area = 0; area < 2; area++)
            for (int i = 0; i < (area == 0 ? entries.Count : EquippedSlotCount); i++)
            {
                InventoryEntry e = GetEntry(area == 1, i);
                if (HasStack(e)) snapshot.Add(new InventoryEntry(e.Item, e.Quantity) { snapshotSlot = i, snapshotEquipped = area == 1 });
            }
        return snapshot;
    }
    public void RestoreInventory(List<InventoryEntry> snapshot)
    {
        entries.Clear(); equippedEntries = new InventoryEntry[EquippedSlotCount]; selectedEquippedSlot = -1;
        useOwner = null; reservedEntry = null; reservedSlot = -1; EnsureSlots();
        if (snapshot != null) foreach (InventoryEntry e in snapshot)
        {
            if (e == null) continue;
            if (e.snapshotSelection != -2) { selectedEquippedSlot = e.snapshotSelection; continue; }
            if (!HasStack(e)) continue;
            int index = e.snapshotSlot;
            if (index < 0) index = entries.FindIndex(x => !HasStack(x)); // legacy snapshots
            if (!ValidSlot(e.snapshotEquipped, index)) continue;
            SetEntry(e.snapshotEquipped, index, new InventoryEntry(e.Item, e.Quantity));
        }
        CorrectSelection(); Changed?.Invoke();
    }
}
