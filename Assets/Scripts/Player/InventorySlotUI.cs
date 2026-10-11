using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class InventorySlotUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private GameObject highlight;

    private ItemData item;
    private InventoryUI owner;

    public void Setup(InventoryEntry entry, InventoryUI inventoryUI)
    {
        owner = inventoryUI;
        item = entry != null ? entry.Item : null;
        if (iconImage != null)
        {
            iconImage.sprite = item != null ? item.Icon : null;
            iconImage.enabled = iconImage.sprite != null;
            iconImage.raycastTarget = false;
        }
        if (quantityText != null)
        {
            quantityText.gameObject.SetActive(true);
            quantityText.text = entry != null && entry.Quantity > 1 ? entry.Quantity.ToString() : string.Empty;
            quantityText.raycastTarget = false;
        }
        if (highlight != null) highlight.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item == null || owner == null) return;
        if (highlight != null) highlight.SetActive(true);
        owner.ShowDetails(item);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (highlight != null) highlight.SetActive(false);
        if (owner != null) owner.HideDetails(item);
    }

    private void OnDisable()
    {
        if (highlight != null) highlight.SetActive(false);
    }

}
