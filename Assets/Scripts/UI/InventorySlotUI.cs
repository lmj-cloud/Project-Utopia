using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 슬롯의 아이템과 수량을 표시하고 포인터·드래그 이벤트를 인벤토리 UI에 전달한다.
/// </summary>
[RequireComponent(typeof(Image))]
public class InventorySlotUI : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private GameObject highlight;

    [SerializeField] private GameObject selectedItem;
    [Header("Slot Visual Animation")]
    [SerializeField] private RectTransform visualRoot;
    [SerializeField, Min(0.01f)] private float normalScale = 1f;
    [SerializeField, Min(0.01f)] private float hoverScale = 1.07f;
    [SerializeField, Min(0.01f)] private float pressedScale = 0.95f;
    [SerializeField, Min(0f)] private float transitionSpeed = 12f;
    private Vector3 originalVisualScale = Vector3.one;
    private bool hasCapturedVisualScale;
    private float currentScale = 1f;
    private bool isPressed;
    private bool isDragging;
    private bool dragged;
    public ItemCategory Category { get; private set; }
    /// <summary>
    /// 이 슬롯이 속한 인벤토리 페이지와 소유 UI를 연결한다.
    /// </summary>
    public void SetupPage(InventoryEntry entry, InventoryUI inventoryUI, bool isEquipped, int index, ItemCategory category)
    { Category = category; Setup(entry, inventoryUI, isEquipped, index); }
    /// <summary>
    /// 드래그 판정을 초기화하고 왼쪽 버튼을 누르는 동안 축소 상태를 유지한다.
    /// </summary>
    public void OnPointerDown(PointerEventData data)
    {
        dragged = false;
        if (data.button == PointerEventData.InputButton.Left && owner != null && owner.InteractionAllowed)
            isPressed = true;
    }

    /// <summary>버튼을 놓으면 현재 Hover 여부에 맞는 크기로 복귀한다.</summary>
    public void OnPointerUp(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left) isPressed = false;
    }
    /// <summary>
    /// 슬롯 클릭에 맞는 선택·상세 표시 요청을 전달한다.
    /// </summary>
    public void OnPointerClick(PointerEventData data)
    {
        if (owner == null || !owner.InteractionAllowed || dragged) return;
        if (data.button == PointerEventData.InputButton.Right && equipped) owner.UnassignQuickSlot(this);
        else if (data.button == PointerEventData.InputButton.Left && !data.dragging && item != null)
        {
            owner.ShowSlotDetails(this, item, quantity);
            if (data.clickCount == 2 && !equipped) owner.EquipStorageSlot(this);
        }
    }
    /// <summary>
    /// Hover와 실제 퀵슬롯 등록 상태를 서로 독립적으로 표시한다.
    /// </summary>
    public void RefreshHighlight()
    {
        if (highlight != null) highlight.SetActive(item != null && hovered && !isDragging);
        // Detail 선택과 별개로, 실제 퀵슬롯 등록 및 보유 수량만 읽는다.
        if (selectedItem != null) selectedItem.SetActive(item != null && quantity > 0 &&
            owner != null && owner.IsQuickSlotAssigned(item));
    }
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
        if (equipped != isEquipped || slotIndex != index) ResetPointerVisuals();
        equipped = isEquipped; slotIndex = index; Setup(entry, inventoryUI);
    }
    /// <summary>
    /// 선택한 슬롯 아이템의 드래그 시작을 인벤토리 UI에 전달한다.
    /// </summary>
    public void OnBeginDrag(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left && owner != null)
        {
            dragged = true;
            isDragging = true;
            isPressed = false;
            hovered = false;
            RefreshHighlight();
            owner.BeginSlotDrag(this, data);
        }
    }
    /// <summary>
    /// 포인터 이동에 따른 드래그 아이콘 위치를 갱신한다.
    /// </summary>
    public void OnDrag(PointerEventData data) { if (owner != null) owner.UpdateSlotDrag(data); }
    /// <summary>
    /// 드래그 종료 시 남은 드래그 표시와 예약 데이터를 정리한다.
    /// </summary>
    public void OnEndDrag(PointerEventData data)
    {
        isDragging = false;
        isPressed = false;
        if (owner != null) owner.CancelSlotDrag();
        RefreshHighlight();
    }
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
        ResetPointerVisuals();
    }

    private void OnEnable()
    {
        ResetPointerVisuals();
        RefreshHighlight();
    }

    private void Update()
    {
        // 카테고리 전환 중에는 이전 포인터 상태를 남기지 않는다. 배치용 루트는 건드리지 않는다.
        if (owner == null || !owner.InteractionAllowed)
        {
            if (hovered || isPressed || isDragging) ResetPointerVisuals();
        }
        float targetScale = normalScale;
        if (item != null && !isDragging)
        {
            if (isPressed) targetScale = pressedScale;
            else if (hovered) targetScale = hoverScale;
        }
        // 비율 차이에 따라 보간하므로 작은 확대량도 한 프레임에 즉시 끝나지 않는다.
        float blend = 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime);
        currentScale = Mathf.Lerp(currentScale, targetScale, blend);
        if (Mathf.Abs(currentScale - targetScale) < 0.0001f) currentScale = targetScale;
        if (visualRoot != null) visualRoot.localScale = originalVisualScale * currentScale;
    }

    /// <summary>슬롯 재사용·비활성화 시 포인터 효과만 초기화하고 등록 정보는 변경하지 않는다.</summary>
    private void ResetPointerVisuals()
    {
        // 비활성 페이지에서는 Awake보다 Setup이 먼저 호출될 수 있으므로 최초 사용 시 캡처한다.
        if (!hasCapturedVisualScale && visualRoot != null)
        {
            originalVisualScale = visualRoot.localScale;
            hasCapturedVisualScale = true;
        }
        hovered = false;
        isPressed = false;
        isDragging = false;
        currentScale = normalScale;
        if (visualRoot != null) visualRoot.localScale = originalVisualScale * normalScale;
        if (highlight != null) highlight.SetActive(false);
    }

    /// <summary>
    /// 슬롯의 소유 UI와 아이템·수량·인덱스를 연결하여 표시와 입력을 준비한다.
    /// </summary>
    public void Setup(InventoryEntry entry, InventoryUI inventoryUI)
    {
        ItemData nextItem = entry != null ? entry.Item : null;
        if (owner != inventoryUI || item != nextItem) ResetPointerVisuals();
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
    /// 상호작용 가능한 슬롯에 포인터가 들어오면 Hover를 시작한다.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = owner != null && owner.InteractionAllowed;
        RefreshHighlight();
    }

    /// <summary>
    /// 포인터가 떠나면 Hover만 해제하고 등록 표시는 유지한다.
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
        ResetPointerVisuals();
        if (owner != null) { owner.CancelSlotDrag(this); owner.HideSlotDetails(this); }
        if (selectedItem != null) selectedItem.SetActive(false);
    }

}
