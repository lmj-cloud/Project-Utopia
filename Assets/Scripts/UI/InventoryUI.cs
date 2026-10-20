using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class InventoryUI : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private PlayerInventory inventory;

    [Header("List UI")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private Transform slotParent;
    [SerializeField] private InventorySlotUI slotPrefab;

    [Header("Detail UI")]
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private Image detailImage;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Equipped and Drag UI")]
    [SerializeField] private InventorySlotUI[] equippedSlots = new InventorySlotUI[5];
    [SerializeField] private ScrollRect storageScroll;
    [SerializeField, Min(1f)] private float storageWheelPixels = 80f;
    [SerializeField] private Image dragIcon;
    private InventorySlotUI dragSource;
    private ItemData dragItem;
    private int dragQuantity;
    private bool originalScrollEnabled;
    private readonly List<InventorySlotUI> slots = new();
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private Image detailBackground;
    private InventorySlotUI shownSlot;
    private ItemData shownItem;
    private int closedFrame = -1;

    public bool IsOpen => inventoryPanel != null && inventoryPanel.activeSelf;
    public bool WasClosedThisFrame => closedFrame == Time.frameCount;

    private void Awake()
    {
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (detailImage != null) detailImage.raycastTarget = false;
        if (itemNameText != null) itemNameText.raycastTarget = false;
        if (descriptionText != null) descriptionText.raycastTarget = false;
        if (detailBackground != null) detailBackground.raycastTarget = false;
        if (quantityText != null) quantityText.raycastTarget = false;
        if (dragIcon != null) { dragIcon.raycastTarget = false; dragIcon.gameObject.SetActive(false); }
        ClearDetails();
    }

    private void OnEnable()
    {
        if (inventory != null) inventory.Changed += Refresh;
    }

    private void OnDisable()
    {
        if (inventory != null) inventory.Changed -= Refresh;
        Close();
    }

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    [ContextMenu("Inventory/Open UI Preview")]
    public void Open()
    {
        if (inventory == null || inventoryPanel == null || slotParent == null || slotPrefab == null)
        {
            Debug.LogWarning("InventoryUI의 Inventory / Panel / Slot Parent / Slot Prefab을 연결하세요.", this);
            return;
        }
        inventoryPanel.SetActive(true);
        Refresh();
    }

    [ContextMenu("Inventory/Close UI Preview")]
    public void Close()
    {
        CancelSlotDrag();
        if (IsOpen) closedFrame = Time.frameCount;
        ClearDetails();
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
    }

    private void Refresh()
    {
        if (!IsOpen || inventory == null || slotParent == null || slotPrefab == null) return;
        CancelSlotDrag();
        ClearDetails();

        // 슬롯을 재사용하여 갱신할 때마다 Destroy/Instantiate 하지 않는다.
        int slotCount = Mathf.Max(inventory.SlotCapacity, inventory.Entries.Count);
        while (slots.Count < slotCount)
        {
            InventorySlotUI slot = Instantiate(slotPrefab, slotParent);
            slots.Add(slot);
        }
        for (int i = 0; i < slots.Count; i++)
        {
            slots[i].gameObject.SetActive(i < slotCount);
            if (i >= slotCount) continue;
            InventoryEntry entry = i < inventory.Entries.Count ? inventory.Entries[i] : null;
            slots[i].Setup(entry, this, false, i);
        }
        for (int i = 0; equippedSlots != null && i < equippedSlots.Length && i < PlayerInventory.EquippedSlotCount; i++)
            if (equippedSlots[i] != null) equippedSlots[i].Setup(inventory.GetEntry(true, i), this, true, i);
    }

    public void ScrollStorage(float wheel)
    {
        if (!IsOpen || dragSource != null || storageScroll == null || storageScroll.content == null || storageScroll.viewport == null) return;
        float overflow = storageScroll.content.rect.height - storageScroll.viewport.rect.height;
        if (overflow > 0f) storageScroll.verticalNormalizedPosition = Mathf.Clamp01(storageScroll.verticalNormalizedPosition + wheel / 120f * storageWheelPixels / overflow);
    }
    public void BeginSlotDrag(InventorySlotUI source, PointerEventData data)
    {
        if (!IsOpen || inventory == null || inventory.IsUseLocked || source.Item == null || dragIcon == null) return;
        CancelSlotDrag(); dragSource = source; dragItem = source.Item; dragQuantity = source.Quantity;
        dragIcon.sprite = dragItem.Icon; dragIcon.enabled = dragIcon.sprite != null;
        dragIcon.gameObject.SetActive(true); dragIcon.raycastTarget = false;
        if (storageScroll != null) { originalScrollEnabled = storageScroll.enabled; storageScroll.StopMovement(); storageScroll.enabled = false; }
        UpdateSlotDrag(data);
    }
    public void UpdateSlotDrag(PointerEventData data)
    {
        if (dragSource == null || dragIcon == null) return;
        RectTransform parent = dragIcon.rectTransform.parent as RectTransform;
        if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out Vector2 point))
            dragIcon.rectTransform.anchoredPosition = point;
    }
    public void DropSlot(InventorySlotUI destination)
    {
        if (dragSource == null || !IsOpen || inventory == null) return;
        inventory.TryMove(dragSource.IsEquipped, dragSource.SlotIndex, destination.IsEquipped, destination.SlotIndex, dragItem, dragQuantity);
        CancelSlotDrag();
    }
    public void CancelSlotDrag(InventorySlotUI source) { if (dragSource == source) CancelSlotDrag(); }
    public void CancelSlotDrag()
    {
        if (dragSource != null && storageScroll != null) storageScroll.enabled = originalScrollEnabled;
        dragSource = null; dragItem = null; dragQuantity = 0;
        if (dragIcon != null) dragIcon.gameObject.SetActive(false);
    }
    public void ShowSlotDetails(InventorySlotUI slot, ItemData item, int quantity)
    {
        if (!IsOpen) return;
        if (item == null || quantity <= 0) { ClearDetails(); return; }
        ShowDetails(item, quantity);
        shownSlot = slot;
    }

    public void HideSlotDetails(InventorySlotUI slot)
    {
        if (shownSlot == slot) ClearDetails();
    }

    public void ShowDetails(ItemData item)
    {
        int quantity = inventory != null ? (int)System.Math.Min(inventory.GetTotalQuantity(item), int.MaxValue) : 0;
        ShowDetails(item, quantity);
    }

    public void ShowDetails(ItemData item, int quantity)
    {
        if (!IsOpen) return;
        if (item == null || quantity <= 0) { ClearDetails(); return; }
        shownSlot = null;
        shownItem = item;
        if (itemNameText != null) itemNameText.text = quantityText != null ? item.ItemName : $"{item.ItemName} x{quantity}";
        if (quantityText != null) quantityText.text = quantity.ToString();
        if (descriptionText != null) descriptionText.text = item.Description;
        if (detailImage != null)
        {
            detailImage.sprite = item.Icon;
            detailImage.enabled = item.Icon != null;
        }
    }

    public void HideDetails(ItemData item)
    {
        if (shownItem == item) ClearDetails();
    }

    private void ClearDetails()
    {
        shownItem = null;
        shownSlot = null;
        if (quantityText != null) quantityText.text = string.Empty;
        if (itemNameText != null) itemNameText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (detailImage != null)
        {
            detailImage.sprite = null;
            detailImage.enabled = false;
        }
    }
}
