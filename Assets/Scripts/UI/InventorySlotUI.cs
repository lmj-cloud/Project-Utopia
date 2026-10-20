using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private GameObject highlight;

    private bool equipped;
    private int slotIndex = -1;
    public ItemData Item => item;
    public int Quantity => quantity;
    public bool IsEquipped => equipped;
    public int SlotIndex => slotIndex;
    public void Setup(InventoryEntry entry, InventoryUI inventoryUI, bool isEquipped, int index)
    {
        equipped = isEquipped; slotIndex = index; Setup(entry, inventoryUI);
    }
    public void OnBeginDrag(PointerEventData data)
    { if (data.button == PointerEventData.InputButton.Left && owner != null) owner.BeginSlotDrag(this, data); }
    public void OnDrag(PointerEventData data) { if (owner != null) owner.UpdateSlotDrag(data); }
    public void OnEndDrag(PointerEventData data) { if (owner != null) owner.CancelSlotDrag(); }
    public void OnDrop(PointerEventData data)
    { if (data.button == PointerEventData.InputButton.Left && owner != null) owner.DropSlot(this); }
    private Image pointerImage;
    private int quantity;
    private bool hovered;
    private ItemData item;
    private InventoryUI owner;

    private void Awake()
    {
        pointerImage = GetComponent<Image>();
    }

    public void Setup(InventoryEntry entry, InventoryUI inventoryUI)
    {
        owner = inventoryUI;
        quantity = entry != null ? entry.Quantity : 0;
        if (pointerImage == null) pointerImage = GetComponent<Image>();
        if (pointerImage != null) { pointerImage.enabled = true; pointerImage.raycastTarget = true; }
        item = entry != null ? entry.Item : null;
        if (iconImage != null)
        {
            iconImage.sprite = item != null ? item.Icon : null;
            // The current prefab uses the root Image as the icon and pointer area.
            iconImage.enabled = iconImage == pointerImage || iconImage.sprite != null;
            iconImage.raycastTarget = iconImage == pointerImage;
        }
        if (quantityText != null)
        {
            quantityText.gameObject.SetActive(true);
            quantityText.text = entry != null && entry.Quantity > 0 ? entry.Quantity.ToString() : string.Empty;
            quantityText.raycastTarget = false;
        }
        if (highlight != null) highlight.SetActive(hovered && item != null);
        if (hovered && owner != null) owner.ShowSlotDetails(this, item, quantity);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (highlight != null) highlight.SetActive(item != null);
        if (owner != null) owner.ShowSlotDetails(this, item, quantity);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlight != null) highlight.SetActive(false);
        hovered = false;
        if (owner != null) owner.HideSlotDetails(this);
    }

    private void OnDisable()
    {
        hovered = false;
        if (owner != null) { owner.CancelSlotDrag(this); owner.HideSlotDetails(this); }
        if (highlight != null) highlight.SetActive(false);
    }

}
