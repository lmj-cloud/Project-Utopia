using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    private readonly List<InventorySlotUI> slots = new();
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

    public void Close()
    {
        if (IsOpen) closedFrame = Time.frameCount;
        ClearDetails();
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
    }

    private void Refresh()
    {
        if (!IsOpen || inventory == null || slotParent == null || slotPrefab == null) return;
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
            slots[i].Setup(entry, this);
        }
    }

    public void ShowDetails(ItemData item)
    {
        if (!IsOpen || item == null) return;
        shownItem = item;
        if (detailPanel != null) detailPanel.SetActive(true);
        if (itemNameText != null) itemNameText.text = item.ItemName;
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
        if (itemNameText != null) itemNameText.text = string.Empty;
        if (descriptionText != null) descriptionText.text = string.Empty;
        if (detailImage != null)
        {
            detailImage.sprite = null;
            detailImage.enabled = false;
        }
        if (detailPanel != null) detailPanel.SetActive(false);
    }
}
