using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 슬롯의 아이템과 수량을 표시하고 포인터·드래그 이벤트를 인벤토리 UI에 전달한다.
/// </summary>
[RequireComponent(typeof(Image))]
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private GameObject highlight;

    private bool dragged;
    public ItemCategory Category { get; private set; }
    /// <summary>
    /// 이 슬롯이 속한 인벤토리 페이지와 소유 UI를 연결한다.
    /// </summary>
    public void SetupPage(InventoryEntry entry, InventoryUI inventoryUI, bool isEquipped, int index, ItemCategory category)
    { Category = category; Setup(entry, inventoryUI, isEquipped, index); }
    /// <summary>
    /// 포인터 누름을 해당 슬롯 또는 분류 선택 처리에 전달한다.
    /// </summary>
    public void OnPointerDown(PointerEventData data) { dragged = false; }
    /// <summary>
    /// 슬롯 클릭에 맞는 선택·상세 표시 요청을 전달한다.
    /// </summary>
    public void OnPointerClick(PointerEventData data)
    {
        if (owner == null || !owner.InteractionAllowed || dragged) return;
        if (data.button == PointerEventData.InputButton.Right && equipped) owner.UnassignQuickSlot(this);
        else if (data.button == PointerEventData.InputButton.Left && !data.dragging && item != null) owner.ShowSlotDetails(this, item, quantity);
    }
    /// <summary>
    /// 현재 선택 여부에 맞는 슬롯 강조 표시를 적용한다.
    /// </summary>
    public void RefreshHighlight()
    { if (highlight != null) highlight.SetActive(item != null && (hovered || (owner != null && owner.IsSelected(this)))); }
    private bool equipped;
    private int slotIndex = -1;
    public ItemData Item => item;
    public int Quantity => quantity;
    public bool IsEquipped => equipped;
    public int SlotIndex => slotIndex;
    /// <summary>
    /// 슬롯의 소유 UI와 아이템·수량·인덱스를 연결하여 표시와 입력을 준비한다.
    /// </summary>
    public void Setup(InventoryEntry entry, InventoryUI inventoryUI, bool isEquipped, int index)
    {
        equipped = isEquipped; slotIndex = index; Setup(entry, inventoryUI);
    }
    /// <summary>
    /// 선택한 슬롯 아이템의 드래그 시작을 인벤토리 UI에 전달한다.
    /// </summary>
    public void OnBeginDrag(PointerEventData data)
    { if (data.button == PointerEventData.InputButton.Left && owner != null) { dragged = true; owner.BeginSlotDrag(this, data); } }
    /// <summary>
    /// 포인터 이동에 따른 드래그 아이콘 위치를 갱신한다.
    /// </summary>
    public void OnDrag(PointerEventData data) { if (owner != null) owner.UpdateSlotDrag(data); }
    /// <summary>
    /// 드래그 종료 시 남은 드래그 표시와 예약 데이터를 정리한다.
    /// </summary>
    public void OnEndDrag(PointerEventData data) { if (owner != null) owner.CancelSlotDrag(); }
    /// <summary>
    /// 현재 슬롯을 드롭 대상으로 전달하여 아이템 이동 또는 퀵슬롯 등록을 요청한다.
    /// </summary>
    public void OnDrop(PointerEventData data)
    { if (data.button == PointerEventData.InputButton.Left && owner != null) owner.DropSlot(this); }
    private Image pointerImage;
    private int quantity;
    private bool hovered;
    private ItemData item;
    private InventoryUI owner;

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        pointerImage = GetComponent<Image>();
    }

    /// <summary>
    /// 슬롯의 소유 UI와 아이템·수량·인덱스를 연결하여 표시와 입력을 준비한다.
    /// </summary>
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
        RefreshHighlight();
    }

    /// <summary>
    /// 포인터가 올라온 슬롯의 상세 정보 표시를 요청한다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        RefreshHighlight();
    }

    /// <summary>
    /// 포인터가 떠난 슬롯의 상세 정보 표시를 정리한다.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        RefreshHighlight();
    }

    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        hovered = false;
        if (owner != null) { owner.CancelSlotDrag(this); owner.HideSlotDetails(this); }
        if (highlight != null) highlight.SetActive(false);
    }

}
