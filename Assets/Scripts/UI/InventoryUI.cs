using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 인벤토리 열기·닫기, 분류·페이지 선택, 슬롯 드래그와 상세 패널 애니메이션을 관리한다.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    [Header("Inventory")]
    [SerializeField] private PlayerInventory inventory;

    [Header("List UI")]
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private GameObject playerInterface;
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
    private ScrollRect dragScroll;
    private readonly List<InventorySlotUI> slots = new();
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private Image detailBackground;
    private InventorySlotUI shownSlot;
    private ItemData shownItem;
    private int closedFrame = -1;

    /// <summary>
    /// 아이템 분류와 연결된 보관 페이지의 UI 정보를 보관한다.
    /// </summary>
    [System.Serializable]
    private class CategoryPage
    {
        public RectTransform panel;
        public CanvasGroup group;
        public GameObject selectedTab;
        public ItemCategory category;
        public Transform content;
        public ScrollRect scroll;
        [System.NonSerialized] public readonly List<InventorySlotUI> slots = new();
        [System.NonSerialized] public Vector2 origin;
        [System.NonSerialized] public bool active;
        [System.NonSerialized] public float alpha;
        [System.NonSerialized] public bool interactable, blocksRaycasts;
        [System.NonSerialized] public int layoutChildCount = -1;
    }
    [Header("Categories: 장비 / 소모품 / 정복 / 스토리 물품 / 호감도")]
    [SerializeField] private CategoryPage[] pages = new CategoryPage[5];
    [SerializeField] private RectTransform pageViewport;
    /// <summary>
    /// 상세 패널 애니메이션 전후에 복원할 RectTransform 배치를 보관한다.
    /// </summary>
    [System.Serializable]
    private class DetailLayout
    {
        public RectTransform target;
        public Vector2 positionOffset;
        public Vector2 sizeDeltaOffset;
        [System.NonSerialized] public Vector2 position, size;
    }
    [Header("Detail: 끊긴 리스트 경로만 보완 (Animator 대상 제외)")]
    [SerializeField] private DetailLayout[] detailLayouts = new DetailLayout[0];
    [SerializeField] private RectTransform categoryTabs;
    [SerializeField] private RectTransform[] categoryTabRects = new RectTransform[0];
    private readonly Dictionary<RectTransform, Vector2[]> animatedLayout = new();
    private float detailProgress;
    private bool captured;
    private bool capturedTabLayout;
    private AnimatorUpdateMode detailUpdateMode;
    [SerializeField, Min(0.01f)] private float adjacentDuration = 0.25f;
    [SerializeField, Min(0.01f)] private float distantSegmentDuration = 0.1f;
    [SerializeField] private CanvasGroup quickSlotGroup;
    [Header("Detail Animation")]
    [SerializeField] private Animator detailAnimator;
    [SerializeField] private AnimationClip detailOpenClip;
    [SerializeField] private AnimationClip detailCloseClip;
    [SerializeField] private string detailOpenState = "Base Layer.InventoryDetailOpen";
    [SerializeField] private string detailCloseState = "Base Layer.InventoryDetailClose";
    [SerializeField] private CanvasGroup detailGroup;
    [SerializeField, Min(1f)] private float animationTimeout = 5f;
    [Header("Equipment Page")]
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private RawImage playerPreview;
    [SerializeField] private RenderTexture previewTexture;
    [SerializeField] private RawImage primaryWeaponIcon;
    [SerializeField] private RawImage secondaryWeaponIcon;
    [SerializeField] private TMP_Text primaryWeaponText;
    [SerializeField] private TMP_Text pistolWeaponText;
    [SerializeField] private TMP_Text meleeWeaponText;
    /// <summary>
    /// 인벤토리 상세 패널이 표시·전환 중인지 구분한다.
    /// </summary>
    private enum DetailState { Closed, Opening, Open, Closing }
    private DetailState detailState;
    private int detailSelectionFrame = -1;
    private EventSystem detailEventSystem;
    private PointerEventData detailPointer;
    private readonly List<RaycastResult> detailRaycastResults = new();
    private Coroutine categoryRoutine;
    private Canvas.WillRenderCanvases categoryPresentation;
    private Coroutine detailRoutine;
    private int currentCategory;
    private int requestedCategory;
    private int displayRevision;
    private int detailRevision;
    private bool changingCategory;
    private bool warnedSetup;
    private bool warnedAnimation;
    private bool warnedCategory;

    public int CurrentCategory => currentCategory;
    public bool InteractionAllowed => Application.isPlaying && IsOpen && !changingCategory && detailState != DetailState.Closing;
    /// <summary>기존 인벤토리의 유효한 퀵슬롯 등록 상태만 조회한다. 별도 선택 데이터를 저장하지 않는다.</summary>
    public bool IsQuickSlotAssigned(ItemData item)
    {
        if (inventory == null || item == null || inventory.GetTotalQuantity(item) <= 0) return false;
        for (int i = 0; i < PlayerInventory.EquippedSlotCount; i++)
        {
            InventoryEntry entry = inventory.GetEntry(true, i);
            if (entry != null && entry.Item == item && entry.Quantity > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// 해당 종류와 인덱스의 슬롯이 현재 선택된 슬롯인지 확인한다.
    /// </summary>
    public bool IsSelected(InventorySlotUI slot) => shownItem != null && slot != null && slot.Item == shownItem && (shownSlot == null || slot == shownSlot);


    public bool IsOpen => inventoryPanel != null && inventoryPanel.activeSelf;
    public bool WasClosedThisFrame => closedFrame == Time.frameCount;

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (inventoryPanel != null)
        {
            inventoryPanel.SetActive(false);
            if (playerInterface != null) playerInterface.SetActive(true);
        }
        if (detailImage != null) detailImage.raycastTarget = false;
        if (itemNameText != null) itemNameText.raycastTarget = false;
        if (descriptionText != null) descriptionText.raycastTarget = false;
        if (detailBackground != null) detailBackground.raycastTarget = false;
        if (quantityText != null) quantityText.raycastTarget = false;
        if (dragIcon != null) { dragIcon.raycastTarget = false; dragIcon.gameObject.SetActive(false); }
        CapturePresentation();
        if (playerPreview != null) playerPreview.raycastTarget = false;
        if (detailAnimator != null) detailAnimator.enabled = false;
        SetDetailInput(false);
        ClearDetails();
    }
    /// <summary>
    /// 애니메이션 전의 RectTransform 배치 값을 보관한다.
    /// </summary>
    private void CaptureRect(RectTransform rect)
    {
        if (rect != null && !animatedLayout.ContainsKey(rect))
            animatedLayout.Add(rect, new[] { rect.anchoredPosition, rect.sizeDelta });
    }
    /// <summary>
    /// UI의 초기 활성·표시·배치 상태를 복구용으로 보관한다.
    /// </summary>
    private void CapturePresentation()
    {
        if (captured) return;
        captured = true;
        foreach (CategoryPage page in pages ?? System.Array.Empty<CategoryPage>())
            if (page != null && page.panel != null)
            {
                page.origin = page.panel.anchoredPosition; page.active = page.panel.gameObject.activeSelf;
                if (page.group != null)
                { page.alpha = page.group.alpha; page.interactable = page.group.interactable; page.blocksRaycasts = page.group.blocksRaycasts; }
            }
        CaptureRect(detailPanel != null ? detailPanel.transform as RectTransform : null);
        CaptureRect(categoryTabs);
        foreach (RectTransform rect in categoryTabRects ?? System.Array.Empty<RectTransform>()) CaptureRect(rect);
        foreach (DetailLayout layout in detailLayouts ?? System.Array.Empty<DetailLayout>())
            if (layout != null && layout.target != null)
            { layout.position = layout.target.anchoredPosition; layout.size = layout.target.sizeDelta; }
        if (detailAnimator != null)
        { detailUpdateMode = detailAnimator.updateMode; }
    }

    /// <summary>
    /// 컴포넌트 활성화 시 필요한 알림과 초기 표시를 연결한다.
    /// </summary>
    private void OnEnable()
    {
        if (inventory != null) inventory.Changed += Refresh;
        if (equipment != null) equipment.OnWeaponChanged += EquipmentChanged;
    }

    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        ClearCategoryPresentation();
        if (inventory != null) inventory.Changed -= Refresh;
        if (equipment != null) equipment.OnWeaponChanged -= EquipmentChanged;
        if (Application.isPlaying) Close();
    }

    /// <summary>
    /// 장비 변경에 맞추어 인벤토리 또는 HUD의 선택 표시를 갱신한다.
    /// </summary>
    private void EquipmentChanged(WeaponAssaultRifle weapon) { if (IsOpen) RefreshEquipment(); }

    /// <summary>
    /// 현재 열림 상태에 따라 인벤토리 열기 또는 닫기를 요청한다.
    /// </summary>
    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    /// <summary>
    /// 인벤토리 사용 조건을 검사하고 표시와 플레이어 입력 잠금을 적용한다.
    /// </summary>
    [ContextMenu("Inventory/Open UI Preview")]
    public void Open()
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return;
        if (inventory == null || inventoryPanel == null || slotPrefab == null || !ValidRoute(0, 4))
        {
            if (!warnedSetup) Debug.LogWarning("InventoryUI: Inventory, Panel, Slot Prefab과 카테고리 5개를 연결하세요.", this);
            warnedSetup = true;
            return;
        }
        CapturePresentation();
        ResetPresentation();
        inventoryPanel.SetActive(true);
        RebuildCategoryTabs();
        if (!capturedTabLayout)
        {
            // 비활성 상태의 직렬화 좌표 대신 닫힌 UI의 실제 LayoutGroup 배치를 저장한다.
            foreach (RectTransform rect in categoryTabRects ?? System.Array.Empty<RectTransform>())
                if (rect != null && animatedLayout.TryGetValue(rect, out Vector2[] original))
                { original[0] = rect.anchoredPosition; original[1] = rect.sizeDelta; }
            capturedTabLayout = true;
        }
        Refresh();
        // 열기 준비가 끝난 뒤 HUD만 숨긴다. 인벤토리와 데이터는 별도 오브젝트에서 계속 동작한다.
        if (IsOpen && playerInterface != null) playerInterface.SetActive(false);
    }

    /// <summary>
    /// 열린 인벤토리를 닫고 드래그·상세 표시 및 자기 입력 잠금을 정리한다.
    /// </summary>
    [ContextMenu("Inventory/Close UI Preview")]
    public void Close()
    {
        if (!Application.isPlaying || (!IsOpen && categoryRoutine == null && detailRoutine == null && dragSource == null)) return;
        CancelSlotDrag();
        if (IsOpen) closedFrame = Time.frameCount;
        ResetPresentation();
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        RestorePresentation();
        // Tab·Esc·직접 호출 모두 같은 종료 경로에서 HUD를 복구한다.
        if (inventoryPanel != null && !IsOpen && playerInterface != null) playerInterface.SetActive(true);
    }

    /// <summary>
    /// 현재 분류와 슬롯 데이터로 아이템·수량·선택 표시를 다시 구성한다.
    /// </summary>
    private void Refresh()
    {
        if (!Application.isPlaying || !IsOpen || inventory == null || slotPrefab == null) return;
        CancelSlotDrag();
        if (!warnedCategory) foreach (InventoryEntry entry in inventory.Entries)
            if (entry != null && entry.Item != null && entry.Quantity > 0 && entry.Item.Category == ItemCategory.Unclassified && !entry.Item.IsFixedWeapon)
            { Debug.LogWarning("InventoryUI: 미분류 ItemData는 보관 수량을 유지하지만 페이지에는 표시하지 않습니다. Category를 지정하세요.", this); warnedCategory = true; break; }
        foreach (CategoryPage page in pages ?? System.Array.Empty<CategoryPage>())
        {
            if (page == null || page.content == null || page.category == ItemCategory.Unclassified) continue;
            List<int> indices = new();
            for (int i = 0; i < inventory.Entries.Count; i++)
            {
                InventoryEntry entry = inventory.GetEntry(false, i);
                if (entry == null || entry.Item == null || entry.Quantity <= 0 || entry.Item.Category == page.category) indices.Add(i);
            }
            while (page.slots.Count < indices.Count) page.slots.Add(Instantiate(slotPrefab, page.content));
            for (int i = 0; i < page.slots.Count; i++)
            {
                InventorySlotUI slot = page.slots[i];
                slot.gameObject.SetActive(i < indices.Count);
                if (i < indices.Count) slot.SetupPage(inventory.GetEntry(false, indices[i]), this, false, indices[i], page.category);
            }
        }
        for (int i = 0; equippedSlots != null && i < equippedSlots.Length && i < PlayerInventory.EquippedSlotCount; i++)
            if (equippedSlots[i] != null) equippedSlots[i].Setup(inventory.GetEntry(true, i), this, true, i);
        if (shownItem != null)
        {
            long total = inventory.GetTotalQuantity(shownItem);
            if (total <= 0) CloseDetails();
            else SetDetailText(shownItem, (int)System.Math.Min(total, int.MaxValue));
        }
        RefreshEquipment();
    }

    /// <summary>
    /// 현재 보관 분류의 표시 위치 또는 페이지를 변경한다.
    /// </summary>
    public void ScrollStorage(float wheel)
    {
        ScrollRect scroll = pages != null && currentCategory < pages.Length && pages[currentCategory] != null ? pages[currentCategory].scroll : storageScroll;
        if (!InteractionAllowed || dragSource != null || scroll == null || scroll.content == null || scroll.viewport == null) return;
        float overflow = scroll.content.rect.height - scroll.viewport.rect.height;
        if (overflow > 0f) scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + wheel / 120f * storageWheelPixels / overflow);
    }
    /// <summary>
    /// 드래그 출발 슬롯의 아이템·수량을 기록하고 드래그 표시를 준비한다.
    /// </summary>
    public void BeginSlotDrag(InventorySlotUI source, PointerEventData data)
    {
        if (!InteractionAllowed || inventory == null || inventory.IsUseLocked || source == null || source.Item == null || dragIcon == null) return;
        CancelSlotDrag(); dragSource = source; dragItem = source.Item; dragQuantity = source.Quantity;
        dragIcon.sprite = dragItem.Icon; dragIcon.enabled = dragIcon.sprite != null;
        dragIcon.gameObject.SetActive(true); dragIcon.raycastTarget = false;
        dragScroll = pages[currentCategory].scroll;
        if (dragScroll != null) { originalScrollEnabled = dragScroll.enabled; dragScroll.StopMovement(); dragScroll.enabled = false; }
        UpdateSlotDrag(data);
    }
    /// <summary>
    /// 현재 포인터 위치에 맞추어 드래그 아이콘을 이동한다.
    /// </summary>
    public void UpdateSlotDrag(PointerEventData data)
    {
        if (dragSource == null || dragIcon == null) return;
        RectTransform parent = dragIcon.rectTransform.parent as RectTransform;
        if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, data.position, data.pressEventCamera, out Vector2 point))
            dragIcon.rectTransform.anchoredPosition = point;
    }
    /// <summary>
    /// 출발 데이터가 여전히 유효한지 확인하고 인벤토리 이동·등록을 요청한다.
    /// </summary>
    public void DropSlot(InventorySlotUI destination)
    {
        if (dragSource == null || !InteractionAllowed || inventory == null || destination == null) return;
        if (!destination.IsEquipped && destination.Category != dragItem.Category) { CancelSlotDrag(); return; }
        inventory.TryMove(dragSource.IsEquipped, dragSource.SlotIndex, destination.IsEquipped, destination.SlotIndex, dragItem, dragQuantity);
        CancelSlotDrag();
    }
    /// <summary>
    /// 드래그 소유권과 일치하는 진행 표시 및 임시 데이터를 정리한다.
    /// </summary>
    public void CancelSlotDrag(InventorySlotUI source) { if (dragSource == source) CancelSlotDrag(); }
    /// <summary>
    /// 드래그 소유권과 일치하는 진행 표시 및 임시 데이터를 정리한다.
    /// </summary>
    public void CancelSlotDrag()
    {
        if (dragScroll != null) dragScroll.enabled = originalScrollEnabled;
        dragScroll = null;
        dragSource = null; dragItem = null; dragQuantity = 0;
        if (dragIcon != null) dragIcon.gameObject.SetActive(false);
    }
    /// <summary>
    /// 현재 슬롯의 유효한 아이템을 선택하여 상세 표시를 요청한다.
    /// </summary>
    public void ShowSlotDetails(InventorySlotUI slot, ItemData item, int quantity)
    {
        // 장비 페이지의 등록 슬롯은 상세 표시 없이 기존 슬롯 입력만 처리한다.
        if (currentCategory == 0) return;
        if (!InteractionAllowed || dragSource != null || item == null || quantity <= 0) return;
        ShowDetails(item, quantity);
        shownSlot = slot;
        UpdateSelection();
    }

    /// <summary>
    /// 해당 슬롯에서 시작된 상세 표시를 정리한다.
    /// </summary>
    public void HideSlotDetails(InventorySlotUI slot)
    {
        // hover 종료는 상세를 유지하고, 사라진 아이템만 닫는다.
        if (shownSlot == slot && shownItem != null && inventory != null && inventory.GetTotalQuantity(shownItem) <= 0) CloseDetails();
    }

    /// <summary>
    /// 아이템 정보를 표시하고 해당 분류의 상세 패널 전환을 요청한다.
    /// </summary>
    public void ShowDetails(ItemData item)
    {
        int quantity = inventory != null ? (int)System.Math.Min(inventory.GetTotalQuantity(item), int.MaxValue) : 0;
        ShowDetails(item, quantity);
    }

    /// <summary>
    /// 아이템 정보를 표시하고 해당 분류의 상세 패널 전환을 요청한다.
    /// </summary>
    public void ShowDetails(ItemData item, int quantity)
    {
        // 슬롯 클릭 외의 직접 호출도 장비 페이지에서는 Detail을 열지 못하게 한다.
        if (currentCategory == 0) return;
        if (!InteractionAllowed || item == null || quantity <= 0) return;
        // EventSystem의 슬롯 클릭 처리와 같은 프레임에는 외부 클릭으로 다시 닫지 않는다.
        detailSelectionFrame = Time.frameCount;
        shownSlot = null;
        shownItem = item;
        SetDetailText(item, inventory != null ? (int)System.Math.Min(inventory.GetTotalQuantity(item), int.MaxValue) : quantity);
        UpdateSelection();
        if (detailState == DetailState.Closed) StartDetail(true);
    }
    /// <summary>
    /// 아이템 이름과 설명 등 상세 텍스트를 연결된 UI에 반영한다.
    /// </summary>
    private void SetDetailText(ItemData item, int quantity)
    {
        if (itemNameText != null) itemNameText.text = quantityText != null ? item.ItemName : $"{item.ItemName} x{quantity}";
        if (quantityText != null) quantityText.text = quantity.ToString();
        if (descriptionText != null) descriptionText.text = item.Description;
        if (detailImage != null)
        {
            detailImage.sprite = item.Icon;
            detailImage.enabled = item.Icon != null;
        }
    }

    /// <summary>
    /// 현재 상세 패널을 숨기는 전환을 요청한다.
    /// </summary>
    public void HideDetails(ItemData item)
    {
        if (shownItem == item) CloseDetails();
    }

    /// <summary>
    /// 현재 분류와 상세 상태의 애니메이션 경로가 연결되어 있는지 확인한다.
    /// </summary>
    private bool ValidRoute(int from, int to)
    {
        if (pages == null || pages.Length != 5 || from < 0 || to < 0 || from >= 5 || to >= 5) return false;
        for (int i = Mathf.Min(from, to); i <= Mathf.Max(from, to); i++)
        {
            CategoryPage page = pages[i];
            if (page == null || page.panel == null || page.group == null || page.group.transform != page.panel) return false;
            if (i > 0 && i < 4 && (page.content == null || page.scroll == null ||
                page.scroll.content != page.content || page.category != (ItemCategory)i)) return false;
            for (int j = 0; j < i; j++)
                if (pages[j] != null && (pages[j].panel == page.panel ||
                    (page.content != null && pages[j].content == page.content))) return false;
        }
        return true;
    }
    /// <summary>
    /// 요청한 아이템 분류로 선택을 변경한다.
    /// </summary>
    public void SelectCategory(int index)
    {
        if (!Application.isPlaying || !IsOpen || !ValidRoute(currentCategory, index))
        {
            if (IsOpen && !warnedSetup) Debug.LogWarning("InventoryUI: 페이지 RectTransform/CanvasGroup, 독립 Content/ScrollRect, 카테고리를 연결하세요.", this);
            warnedSetup = true; return;
        }
        requestedCategory = index;
        if (!changingCategory && index == currentCategory) return;
        CancelSlotDrag();
        if (categoryRoutine == null) categoryRoutine = StartCoroutine(ChangeCategory(displayRevision));
    }
    /// <summary>
    /// 분류 전환 중 표시와 이전 상세 애니메이션을 정리하고 새 페이지를 갱신한다.
    /// </summary>
    private IEnumerator ChangeCategory(int revision)
    {
        changingCategory = true;
        SetPageInput(false);
        CloseDetails();
        yield return null;
        while (detailState == DetailState.Closing && revision == displayRevision) yield return null;
        while (revision == displayRevision && currentCategory != requestedCategory)
        {
            // 중간 인덱스를 거치지 않는다. 전환 중 추가 클릭은 마지막 목표 하나만 대기시킨다.
            int next = requestedCategory;
            if (!ValidRoute(currentCategory, next)) break;
            CategoryPage old = pages[currentCategory], incoming = pages[next];
            SetTransitionPages(currentCategory, next);
            int direction = next > currentCategory ? 1 : -1;
            Vector3 worldWidth = old.panel.TransformVector(Vector3.right * old.panel.rect.width);
            if (pageViewport != null) worldWidth = pageViewport.TransformVector(Vector3.right * pageViewport.rect.width);
            float oldWidth = ((RectTransform)old.panel.parent).InverseTransformVector(worldWidth).magnitude;
            float nextWidth = ((RectTransform)incoming.panel.parent).InverseTransformVector(worldWidth).magnitude;
            // 기존 인접 전환 시간을 모든 거리에서 사용한다. 이전 필드는 잘못된 시간 값의 대체값으로 보존한다.
            float duration = adjacentDuration > 0f ? adjacentDuration : distantSegmentDuration;
            float elapsed = 0f;
            float progress = 0f;
            // Detail의 LateUpdate 배치 복원을 유지한 뒤, 전환 위치만 렌더 직전에 적용한다.
            // 새 효과가 아니라 기존 좌우 이동·Fade의 적용 순서를 보장하는 콜백이다.
            categoryPresentation = () =>
            {
                old.panel.anchoredPosition = old.origin + Vector2.left * (direction * oldWidth * progress);
                incoming.panel.anchoredPosition = incoming.origin + Vector2.right * (direction * nextWidth * (1f - progress));
                old.group.alpha = 1f - progress;
                incoming.group.alpha = progress;
            };
            categoryPresentation();
            Canvas.preWillRenderCanvases += categoryPresentation;
            try
            {
                while (revision == displayRevision)
                {
                    progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                    if (progress >= 1f) break;
                    yield return null;
                    elapsed += Time.unscaledDeltaTime;
                }
            }
            finally
            {
                ClearCategoryPresentation();
            }
            if (revision != displayRevision) yield break;
            old.group.alpha = 0f; old.panel.anchoredPosition = old.origin;
            incoming.group.alpha = 1f; incoming.panel.anchoredPosition = incoming.origin;
            currentCategory = next;
            SetTransitionPages(currentCategory, currentCategory);
            UpdateTabs();
        }
        changingCategory = false; categoryRoutine = null;
        SetPageInput(true);
    }

    /// <summary>
    /// 완료·취소·비활성화 때 전환 전용 렌더 콜백을 해제해 다음 UI 표시로 남기지 않는다.
    /// </summary>
    private void ClearCategoryPresentation()
    {
        if (categoryPresentation == null) return;
        Canvas.preWillRenderCanvases -= categoryPresentation;
        categoryPresentation = null;
    }

    /// <summary>
    /// 현재 전환에 필요한 두 페이지만 활성화하고 나머지 패널은 표시와 입력을 모두 중지한다.
    /// </summary>
    private void SetTransitionPages(int from, int to)
    {
        for (int i = 0; pages != null && i < pages.Length; i++)
        {
            CategoryPage page = pages[i];
            if (page == null || page.panel == null || page.group == null) continue;
            bool isVisible = i == from || i == to;
            if (!isVisible)
            {
                page.group.alpha = 0f;
                page.group.interactable = false;
                page.group.blocksRaycasts = false;
                page.panel.anchoredPosition = page.origin;
            }
            if (page.panel.gameObject.activeSelf != isVisible) page.panel.gameObject.SetActive(isVisible);
        }
    }
    /// <summary>
    /// 지정 페이지에서 슬롯 입력을 받을 수 있는지 설정한다.
    /// </summary>
    private void SetPageInput(bool allowed)
    {
        for (int i = 0; pages != null && i < pages.Length; i++)
            if (pages[i] != null && pages[i].group != null)
            { pages[i].group.interactable = allowed && i == currentCategory; pages[i].group.blocksRaycasts = allowed && i == currentCategory; }
        if (quickSlotGroup != null)
        { quickSlotGroup.interactable = allowed && currentCategory == 0; quickSlotGroup.blocksRaycasts = allowed && currentCategory == 0; }
    }
    /// <summary>
    /// 선택된 분류에 맞추어 탭 표시를 갱신한다.
    /// </summary>
    private void UpdateTabs()
    {
        for (int i = 0; pages != null && i < pages.Length; i++)
            if (pages[i] != null && pages[i].selectedTab != null) pages[i].selectedTab.SetActive(i == currentCategory);
    }
    /// <summary>
    /// 현재 상세 요청을 종료하고 패널의 기본 배치를 복구한다.
    /// </summary>
    public void CloseDetails()
    {
        if (!Application.isPlaying || !IsOpen || detailState == DetailState.Closed || detailState == DetailState.Closing) return;
        StartDetail(false);
    }
    /// <summary>
    /// 상세 전환 요청 번호를 갱신하고 실제 애니메이션 Coroutine을 시작한다.
    /// </summary>
    private void StartDetail(bool open)
    {
        detailRevision++;
        if (detailRoutine != null) StopCoroutine(detailRoutine);
        detailState = open ? DetailState.Opening : DetailState.Closing;
        SetDetailInput(false);
        detailRoutine = StartCoroutine(PlayDetail(open, detailRevision));
    }
    /// <summary>
    /// 상세 패널의 Animator·State·Clip 참조가 실제 재생 가능한지 확인한다.
    /// </summary>
    private bool CanPlayDetail(AnimationClip clip, string state)
    {
        if (detailAnimator == null || detailAnimator.runtimeAnimatorController == null || clip == null || clip.isLooping ||
            string.IsNullOrWhiteSpace(state)) return false;
        foreach (AnimationClip candidate in detailAnimator.runtimeAnimatorController.animationClips)
            if (candidate == clip) return true;
        return false;
    }
    /// <summary>
    /// 요청이 유효한 동안 상세 애니메이션의 진입·완료를 감시하고 배치를 적용한다.
    /// </summary>
    private IEnumerator PlayDetail(bool open, int revision)
    {
        yield return null;
        AnimationClip clip = open ? detailOpenClip : detailCloseClip;
        string state = open ? detailOpenState : detailCloseState;
        if (!CanPlayDetail(clip, state)) { FailDetail(); yield break; }
        float from = detailProgress;
        detailAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        detailAnimator.enabled = true;
        yield return null;
        if (revision != detailRevision) yield break;
        int hash = Animator.StringToHash(state);
        if (!detailAnimator.isActiveAndEnabled || !detailAnimator.isInitialized || !detailAnimator.HasState(0, hash))
        { FailDetail(); yield break; }
        float start = open ? 0f : 1f - Mathf.Clamp01(from);
        detailAnimator.Play(hash, 0, start);
        yield return null;
        float elapsed = 0f;
        while (revision == detailRevision && IsOpen)
        {
            AnimatorStateInfo info = detailAnimator.GetCurrentAnimatorStateInfo(0);
            AnimatorClipInfo[] clips = detailAnimator.GetCurrentAnimatorClipInfo(0);
            if (info.fullPathHash != hash || clips.Length == 0 || clips[0].clip != clip) { FailDetail(); yield break; }
            // 위치 갱신은 Animator 평가가 끝난 LateUpdate에서 한 번만 처리한다.
            if (!detailAnimator.IsInTransition(0) && info.normalizedTime >= 1f)
            {
                detailProgress = open ? 1f : 0f; ApplyDetailLayout();
                detailState = open ? DetailState.Open : DetailState.Closed;
                SetDetailInput(open);
                detailRoutine = null; detailAnimator.enabled = false;
                if (!open) { RestoreAnimatedLayout(); ClearDetails(); }
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= Mathf.Max(animationTimeout, clip.length * 4f)) { FailDetail(); yield break; }
            yield return null;
        }
    }
    /// <summary>
    /// 상세 패널의 현재 표시 상태에 맞는 레이아웃을 적용한다.
    /// </summary>
    private void ApplyDetailLayout()
    {
        foreach (DetailLayout layout in detailLayouts ?? System.Array.Empty<DetailLayout>())
            if (layout != null && layout.target != null && !animatedLayout.ContainsKey(layout.target) && !IsDetailContent(layout.target))
            {
                // 현재 Open/Close 클립의 양 끝 접선이 0인 보간과 같은 곡선을 사용한다.
                float progress = Mathf.SmoothStep(0f, 1f, detailProgress);
                Vector2 position = layout.position + layout.positionOffset * progress;
                Vector2 size = layout.size + layout.sizeDeltaOffset * progress;
                if (layout.target.anchoredPosition != position) layout.target.anchoredPosition = position;
                if (layout.target.sizeDelta != size) layout.target.sizeDelta = size;
            }
    }
    /// <summary>
    /// Detail 애니메이션이 바꾼 실제 Viewport 너비에 맞춰 고정 크기 슬롯의 열 수를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (!IsOpen) return;
        if (detailAnimator != null && detailAnimator.enabled && detailAnimator.isInitialized &&
            (detailState == DetailState.Opening || detailState == DetailState.Closing))
        {
            bool isOpening = detailState == DetailState.Opening;
            AnimatorStateInfo state = detailAnimator.GetCurrentAnimatorStateInfo(0);
            int expectedState = Animator.StringToHash(isOpening ? detailOpenState : detailCloseState);
            if (state.fullPathHash == expectedState)
                detailProgress = isOpening ? Mathf.Clamp01(state.normalizedTime) : 1f - Mathf.Clamp01(state.normalizedTime);
        }
        ApplyDetailLayout();
        UpdateResponsiveGrids();
        HandleDetailOutsideClick();
    }

    /// <summary>Alpha 애니메이션과 별개로, 완전히 열린 Detail의 버튼 입력만 허용한다.</summary>
    private void SetDetailInput(bool allowed)
    {
        if (detailGroup == null) return;
        detailGroup.interactable = allowed;
        detailGroup.blocksRaycasts = allowed;
    }

    /// <summary>
    /// EventSystem 처리 후 빈 공간의 새 왼쪽 클릭만 기존 닫기 Coroutine으로 전달한다.
    /// 슬롯·탭·버튼 입력은 가로채지 않으므로 선택 변경과 더블클릭 등록이 그대로 동작한다.
    /// </summary>
    private void HandleDetailOutsideClick()
    {
        if ((detailState != DetailState.Open && detailState != DetailState.Opening) ||
            changingCategory || dragSource != null || detailSelectionFrame == Time.frameCount) return;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        EventSystem events = EventSystem.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || events == null || detailPanel == null) return;
        RectTransform detailRect = detailPanel.transform as RectTransform;
        if (detailRect == null) return;
        Canvas canvas = detailRect.GetComponentInParent<Canvas>();
        Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        Vector2 position = mouse.position.ReadValue();
        if (RectTransformUtility.RectangleContainsScreenPoint(detailRect, position, eventCamera)) return;
        if (categoryTabs != null && RectTransformUtility.RectangleContainsScreenPoint(categoryTabs, position, eventCamera)) return;

        if (detailPointer == null || detailEventSystem != events)
        {
            detailEventSystem = events;
            detailPointer = new PointerEventData(events);
        }
        detailPointer.Reset();
        detailPointer.position = position;
        detailRaycastResults.Clear();
        events.RaycastAll(detailPointer, detailRaycastResults);
        // 실제 입력을 받는 최상위 대상만 검사해 뒤에 가려진 UI로 오판하지 않는다.
        if (detailRaycastResults.Count > 0)
        {
            GameObject target = detailRaycastResults[0].gameObject;
            if (target != null && (target.GetComponentInParent<InventorySlotUI>() != null ||
                target.GetComponentInParent<Selectable>() != null ||
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) != null ||
                ExecuteEvents.GetEventHandler<IPointerDownHandler>(target) != null)) return;
        }
        CloseDetails();
    }

    private void UpdateResponsiveGrids()
    {
        foreach (CategoryPage page in pages ?? System.Array.Empty<CategoryPage>())
        {
            if (page == null || page.content == null || page.scroll == null ||
                !page.content.gameObject.activeInHierarchy) continue;
            RectTransform content = page.content as RectTransform;
            RectTransform viewport = page.scroll.viewport;
            GridLayoutGroup grid = page.content.GetComponent<GridLayoutGroup>();
            if (content == null || viewport == null || grid == null || page.scroll.content != content) continue;

            int childCount = CountGridChildren(content);
            float width = GetGridWidth(page.scroll, viewport, grid, childCount);
            if (width <= 0f || grid.cellSize.x + grid.spacing.x <= 0f) continue;
            int columns = GetGridColumns(grid, width);

            // 가운데 정렬은 열이 줄 때마다 남는 여백을 재분배해 첫 열까지 좌우 왕복시킨다.
            // Grid 자체의 좌상단 정렬로 첫 열을 고정하고 필요한 줄바꿈은 그대로 허용한다.
            if (grid.childAlignment != TextAnchor.UpperLeft) grid.childAlignment = TextAnchor.UpperLeft;
            if (Mathf.Abs(content.rect.width - width) > 0.01f)
                content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            if (grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount)
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            if (grid.constraintCount != columns) grid.constraintCount = columns;

            page.layoutChildCount = childCount;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null || !fitter.isActiveAndEnabled || fitter.verticalFit == ContentSizeFitter.FitMode.Unconstrained)
            {
                float height = Mathf.Max(viewport.rect.height, GetGridHeight(grid, columns, childCount));
                if (Mathf.Abs(content.rect.height - height) > 0.01f)
                {
                    content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
                    // 행 수 감소로 범위를 벗어날 때만 즉시 정리해 Elastic의 뒤늦은 복귀를 막는다.
                    Vector2 position = content.anchoredPosition;
                    float maxY = Mathf.Max(0f, height - viewport.rect.height);
                    float clampedY = Mathf.Clamp(position.y, 0f, maxY);
                    if (dragSource == null && !Mathf.Approximately(position.y, clampedY))
                    {
                        position.y = clampedY;
                        content.anchoredPosition = position;
                        page.scroll.StopMovement();
                    }
                }
            }
        }
    }

    /// <summary>
    /// ScrollRect가 Canvas 레이아웃 단계에서 결정할 최종 폭을 먼저 계산한다.
    /// 이전 프레임의 스크롤바 유무를 읽어 17px가 뒤늦게 반영되는 현상을 방지한다.
    /// </summary>
    private float GetGridWidth(ScrollRect scroll, RectTransform viewport, GridLayoutGroup grid, int childCount)
    {
        RectTransform scrollRect = scroll.transform as RectTransform;
        RectTransform scrollbar = scroll.verticalScrollbar != null ? scroll.verticalScrollbar.transform as RectTransform : null;
        bool canExpand = !scroll.horizontal && scroll.vertical &&
            scroll.verticalScrollbarVisibility == ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport &&
            scrollRect != null && viewport.parent == scroll.transform && scrollbar != null &&
            scrollbar.parent == scroll.transform && scroll.horizontalScrollbar == null;
        if (!canExpand) return viewport.rect.width;

        float width = scrollRect.rect.width;
        int columns = GetGridColumns(grid, width);
        if (GetGridHeight(grid, columns, childCount) > viewport.rect.height + 0.01f)
            width -= scrollbar.rect.width + scroll.verticalScrollbarSpacing;
        return Mathf.Max(0f, width);
    }

    private int GetGridColumns(GridLayoutGroup grid, float width)
    {
        float step = grid.cellSize.x + grid.spacing.x;
        if (step <= 0f) return 1;
        float usableWidth = width - grid.padding.left - grid.padding.right;
        return Mathf.Max(1, Mathf.FloorToInt((usableWidth + grid.spacing.x) / step));
    }

    private float GetGridHeight(GridLayoutGroup grid, int columns, int childCount)
    {
        int rows = Mathf.CeilToInt(childCount / (float)columns);
        return grid.padding.vertical + rows * grid.cellSize.y + Mathf.Max(0, rows - 1) * grid.spacing.y;
    }

    private int CountGridChildren(RectTransform content)
    {
        int count = 0;
        for (int i = 0; i < content.childCount; i++)
        {
            Transform child = content.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;
            if (child.TryGetComponent(out LayoutElement element) && element.ignoreLayout) continue;
            count++;
        }
        return count;
    }

    private bool IsDetailContent(RectTransform target)
    {
        foreach (CategoryPage page in pages ?? System.Array.Empty<CategoryPage>())
        {
            if (page != null && page.content == target) return true;
        }
        return false;
    }

    /// <summary>
    /// 애니메이션이 변경한 RectTransform 배치를 보관한 값으로 되돌린다.
    /// </summary>
    private void RestoreAnimatedLayout()
    {
        foreach (var entry in animatedLayout)
            if (entry.Key != null) { entry.Key.anchoredPosition = entry.Value[0]; entry.Key.sizeDelta = entry.Value[1]; }
        // Animator를 멈추고 크기를 복구한 뒤, 레이아웃이 소유한 탭 위치를 다시 계산한다.
        RebuildCategoryTabs();
        if (detailGroup != null) { detailGroup.alpha = 0f; detailGroup.interactable = false; detailGroup.blocksRaycasts = false; }
    }
    /// <summary>
    /// 현재 카테고리 탭 레이아웃의 갱신을 요청한다.
    /// </summary>
    private void RebuildCategoryTabs()
    {
        if (categoryTabs != null && categoryTabs.gameObject.activeInHierarchy &&
            categoryTabs.TryGetComponent(out LayoutGroup layout) && layout.isActiveAndEnabled)
            LayoutRebuilder.ForceRebuildLayoutImmediate(categoryTabs);
    }
    /// <summary>
    /// 잘못된 상세 전환 요청을 정리하고 기본 표시 상태로 복구한다.
    /// </summary>
    private void FailDetail()
    {
        if (!warnedAnimation) Debug.LogWarning("InventoryUI: Detail Animator와 Open/Close 상태·클립 연결을 확인하세요. Missing 바인딩은 에셋에서 수동 확인이 필요합니다.", this);
        warnedAnimation = true; detailRoutine = null; ResetDetailVisuals(); ClearDetails();
    }
    /// <summary>
    /// 상세 패널의 강조·표시·임시 상태를 초기화한다.
    /// </summary>
    private void ResetDetailVisuals()
    {
        if (detailAnimator != null) detailAnimator.enabled = false;
        detailProgress = 0f; ApplyDetailLayout(); RestoreAnimatedLayout();
        detailState = DetailState.Closed;
    }
    /// <summary>
    /// 기존 표시 스냅샷을 이용해 인벤토리 UI를 초기 상태로 복구한다.
    /// </summary>
    private void RestorePresentation()
    {
        foreach (CategoryPage page in pages ?? System.Array.Empty<CategoryPage>())
            if (page != null && page.panel != null)
            {
                page.panel.anchoredPosition = page.origin;
                if (page.group != null) { page.group.alpha = page.alpha; page.group.interactable = page.interactable; page.group.blocksRaycasts = page.blocksRaycasts; }
                page.panel.gameObject.SetActive(page.active);
            }
        if (detailAnimator != null) { detailAnimator.updateMode = detailUpdateMode; detailAnimator.enabled = false; }
    }
    /// <summary>
    /// 남아 있는 표시 동작을 정리하고 보관한 초기 표시 상태로 복구한다.
    /// </summary>
    private void ResetPresentation()
    {
        ClearCategoryPresentation();
        displayRevision++; detailRevision++;
        if (categoryRoutine != null) StopCoroutine(categoryRoutine);
        if (detailRoutine != null) StopCoroutine(detailRoutine);
        categoryRoutine = null; detailRoutine = null; changingCategory = false;
        CancelSlotDrag(); currentCategory = requestedCategory = 0;
        ResetDetailVisuals(); ClearDetails();
        for (int i = 0; pages != null && i < pages.Length; i++)
            if (pages[i] != null && pages[i].panel != null && pages[i].group != null)
            { pages[i].panel.gameObject.SetActive(i == 0); pages[i].panel.anchoredPosition = pages[i].origin; pages[i].group.alpha = i == 0 ? 1f : 0f; }
        SetPageInput(true); UpdateTabs();
    }
    /// <summary>
    /// 현재 선택한 슬롯이 여전히 유효한지 확인하고 선택 표시를 갱신한다.
    /// </summary>
    private void UpdateSelection()
    {
        foreach (CategoryPage page in pages ?? System.Array.Empty<CategoryPage>())
            if (page != null) foreach (InventorySlotUI slot in page.slots) slot.RefreshHighlight();
        foreach (InventorySlotUI slot in equippedSlots ?? System.Array.Empty<InventorySlotUI>()) if (slot != null) slot.RefreshHighlight();
    }
    /// <summary>
    /// 더블클릭한 보관 슬롯을 빈 퀵슬롯에 등록한다. 드래그 및 사용 예약 중에는 변경하지 않는다.
    /// </summary>
    public bool EquipStorageSlot(InventorySlotUI slot)
    {
        if (!InteractionAllowed || dragSource != null || inventory == null || slot == null || slot.IsEquipped) return false;
        InventoryEntry entry = inventory.GetEntry(false, slot.SlotIndex);
        if (entry == null || entry.Item != slot.Item || entry.Quantity != slot.Quantity) return false;
        return inventory.TryAssignFirstEmpty(slot.SlotIndex);
    }

    /// <summary>
    /// 선택 아이템을 지정 퀵슬롯에 등록하도록 인벤토리에 요청한다.
    /// </summary>
    public void AssignSelectedToQuickSlot(int index)
    {
        if (!InteractionAllowed || inventory == null || shownItem == null || !shownItem.CanEquipToQuickSlot) return;
        for (int i = 0; i < inventory.Entries.Count; i++)
        {
            InventoryEntry entry = inventory.GetEntry(false, i);
            if (entry != null && entry.Item == shownItem && entry.Quantity > 0)
            { inventory.TryMove(false, i, true, index, entry.Item, entry.Quantity); return; }
        }
    }
    /// <summary>
    /// 아이템 수량을 버리지 않고 해당 퀵슬롯의 등록만 해제한다.
    /// </summary>
    public void UnassignQuickSlot(InventorySlotUI slot)
    { if (InteractionAllowed && slot != null && slot.IsEquipped && inventory != null) inventory.UnassignQuickSlot(slot.SlotIndex); }
    /// <summary>
    /// 현재 장비 선택에 맞는 인벤토리 표시를 갱신한다.
    /// </summary>
    private void RefreshEquipment()
    {
        RefreshWeaponIcon(primaryWeaponIcon, PlayerEquipmentKind.AssaultRifle);
        RefreshWeaponIcon(secondaryWeaponIcon, PlayerEquipmentKind.Pistol);
        WeaponText(primaryWeaponText, PlayerEquipmentKind.AssaultRifle, "주무기");
        WeaponText(pistolWeaponText, PlayerEquipmentKind.Pistol, "권총");
        WeaponText(meleeWeaponText, PlayerEquipmentKind.Dagger, "근접 무기");
    }
    /// <summary>
    /// 현재 장비 종류를 표시용 무기 이름으로 변환한다.
    /// </summary>
    private void WeaponText(TMP_Text text, PlayerEquipmentKind kind, string label)
    {
        if (text == null) return;
        if (kind == PlayerEquipmentKind.AssaultRifle || kind == PlayerEquipmentKind.Pistol)
        {
            ItemData item = equipment != null ? equipment.GetRequiredItem(kind) : null;
            bool hasWeapon = equipment != null && equipment.HasOwnership(kind);
            text.text = hasWeapon ? item.ItemName + (equipment.CurrentKind == kind ? " · 장착됨" : " · 보유") : label + " · 미보유";
            return;
        }
        text.text = label + (equipment == null || !equipment.IsAssigned(kind) ? " · 준비되지 않음" : equipment.CurrentKind == kind ? " · 장착됨" : " · 배정됨");
    }
    /// <summary>
    /// 소유 중인 총기의 아이콘을 기존 장비 슬롯에 표시한다.
    /// </summary>
    private void RefreshWeaponIcon(RawImage icon, PlayerEquipmentKind kind)
    {
        if (icon == null) return;
        bool hasWeapon = equipment != null && equipment.HasOwnership(kind);
        // Texture는 Inspector 설정을 그대로 사용하고 소유 여부에 따른 표시만 변경한다.
        icon.enabled = hasWeapon;
    }

    /// <summary>
    /// 연결된 상세 이름·설명 등 텍스트를 비운다.
    /// </summary>
    private void ClearDetails()
    {
        shownItem = null;
        shownSlot = null;
        UpdateSelection();
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
