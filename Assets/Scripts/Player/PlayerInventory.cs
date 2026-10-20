using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventoryEntry
{
    [SerializeField] private ItemData item;
    [SerializeField] private int quantity;

    public ItemData Item => item;
    public int Quantity => quantity;

    public InventoryEntry(ItemData item, int quantity)
    {
        this.item = item;
        this.quantity = quantity;
    }

    internal void ChangeQuantity(int amount) => quantity += amount;
}

public class PlayerInventory : MonoBehaviour
{
    [SerializeField, Min(1)] private int slotCapacity = 24;
    [SerializeField] private List<InventoryEntry> entries = new();

    public event Action Changed;
    public IReadOnlyList<InventoryEntry> Entries => entries;
    public int SlotCapacity => slotCapacity;

    // 같은 ItemData를 사용하는 아이템은 한 칸에 수량을 합친다.
    public bool TryAdd(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return false;

        InventoryEntry existing = FindEntry(item);
        if (existing != null)
        {
            if (existing.Quantity > int.MaxValue - quantity) return false;
            existing.ChangeQuantity(quantity);
        }
        else
        {
            if (entries.Count >= slotCapacity) return false;
            entries.Add(new InventoryEntry(item, quantity));
        }

        Changed?.Invoke();
        return true;
    }

    public bool HasItem(ItemData item, int quantity = 1)
    {
        if (item == null || quantity <= 0) return false;
        InventoryEntry entry = FindEntry(item);
        return entry != null && entry.Quantity >= quantity;
    }

    public bool TryRemove(ItemData item, int quantity = 1)
    {
        if (!HasItem(item, quantity)) return false;
        InventoryEntry entry = FindEntry(item);
        entry.ChangeQuantity(-quantity);
        if (entry.Quantity == 0) entries.Remove(entry);
        Changed?.Invoke();
        return true;
    }

    public void ClearInventory()
    {
        entries.Clear();
        Changed?.Invoke();
    }

    private InventoryEntry FindEntry(ItemData item)
    {
        foreach (InventoryEntry entry in entries)
            if (entry != null && entry.Item == item) return entry;
        return null;
    }
}
