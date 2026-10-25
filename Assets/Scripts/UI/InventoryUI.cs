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
    [SerializeField] private TMP_Text primaryWeaponText;
    [SerializeField] private TMP_Text pistolWeaponText;
    [SerializeField] private TMP_Text meleeWeaponText;
    /// <summary>
    /// 인벤토리 상세 패널이 표시·전환 중인지 구분한다.
    /// </summary>
    private enum DetailState { Closed, Opening, Open, Closing }
    private DetailState detailState;
    private Coroutine categoryRoutine;
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
        if (inventoryPanel != null) inventoryPanel.SetActive(false);
        if (detailImage != null) detailImage.raycastTarget = false;
        if (itemNameText != null) itemNameText.raycastTarget = false;
        if (descriptionText != null) descriptionText.raycastTarget = false;
        if (detailBackground != null) detailBackground.raycastTarget = false;
        if (quantityText != null) quantityText.raycastTarget = false;
        if (dragIcon != null) { dragIcon.raycastTarget = false; dragIcon.gameObject.SetActive(false); }
        CapturePresentation();
        if (playerPreview != null && previewTexture != null) playerPreview.texture = previewTexture;
        if (playerPreview != null) playerPreview.raycastTarget = false;
        if (detailAnimator != null) detailAnimator.enabled = false;
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
    }

    /// <summary>
    /// 현재 분류와 슬롯 데이터로 아이템·수량·선택 표시를 다시 구성한다.
    /// </summary>
    private void Refresh()
    {
        if (!Application.isPlaying || !IsOpen || inventory == null || slotPrefab == null) return;
        CancelSlotDrag();
        if (!warnedCategory) foreach (InventoryEntry entry in inventory.Entries)
            if (entry != null && entry.Item != null && entry.Quantity > 0 && entry.Item.Category == ItemCategory.Unclassified)
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
        if (!InteractionAllowed || item == null || quantity <= 0) return;
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
        bool fastRoute = Mathf.Abs(requestedCategory - currentCategory) > 1;
        while (revision == displayRevision && currentCategory != requestedCategory)
        {
            int next = currentCategory + (requestedCategory > currentCategory ? 1 : -1);
            if (!ValidRoute(currentCategory, next)) break;
            CategoryPage old = pages[currentCategory], incoming = pages[next];
            incoming.panel.gameObject.SetActive(true);
            int direction = next > currentCategory ? 1 : -1;
            Vector3 worldWidth = old.panel.TransformVector(Vector3.right * old.panel.rect.width);
            if (pageViewport != null) worldWidth = pageViewport.TransformVector(Vector3.right * pageViewport.rect.width);
            float oldWidth = ((RectTransform)old.panel.parent).InverseTransformVector(worldWidth).magnitude;
            float nextWidth = ((RectTransform)incoming.panel.parent).InverseTransformVector(worldWidth).magnitude;
            fastRoute |= Mathf.Abs(requestedCategory - currentCategory) > 1;
            float duration = fastRoute ? distantSegmentDuration : adjacentDuration;
            float elapsed = 0f;
            while (revision == displayRevision)
            {
                float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
                old.panel.anchoredPosition = old.origin + Vector2.left * (direction * oldWidth * t);
                incoming.panel.anchoredPosition = incoming.origin + Vector2.right * (direction * nextWidth * (1f - t));
                old.group.alpha = 1f - t; incoming.group.alpha = t;
                if (t >= 1f) break;
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            if (revision != displayRevision) yield break;
            old.group.alpha = 0f; old.panel.anchoredPosition = old.origin;
            incoming.group.alpha = 1f; incoming.panel.anchoredPosition = incoming.origin;
            currentCategory = next; UpdateTabs();
        }
        changingCategory = false; categoryRoutine = null;
        SetPageInput(true);
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
            // Animator가 맡지 않는 끊긴 리스트 경로만 같은 normalizedTime으로 보완한다.
            detailProgress = Mathf.Lerp(from, open ? 1f : 0f, Mathf.Clamp01((info.normalizedTime - start) / Mathf.Max(0.0001f, 1f - start)));
            ApplyDetailLayout();
            if (!detailAnimator.IsInTransition(0) && info.normalizedTime >= 1f)
            {
                detailProgress = open ? 1f : 0f; ApplyDetailLayout();
                detailState = open ? DetailState.Open : DetailState.Closed;
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
            if (layout != null && layout.target != null && !animatedLayout.ContainsKey(layout.target))
            {
                layout.target.anchoredPosition = layout.position + layout.positionOffset * detailProgress;
                layout.target.sizeDelta = layout.size + layout.sizeDeltaOffset * detailProgress;
            }
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
        displayRevision++; detailRevision++;
        if (categoryRoutine != null) StopCoroutine(categoryRoutine);
        if (detailRoutine != null) StopCoroutine(detailRoutine);
        categoryRoutine = null; detailRoutine = null; changingCategory = false;
        CancelSlotDrag(); currentCategory = requestedCategory = 0;
        ResetDetailVisuals(); ClearDetails();
        for (int i = 0; pages != null && i < pages.Length; i++)
            if (pages[i] != null && pages[i].panel != null && pages[i].group != null)
            { pages[i].panel.gameObject.SetActive(true); pages[i].panel.anchoredPosition = pages[i].origin; pages[i].group.alpha = i == 0 ? 1f : 0f; }
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
    /// 선택 아이템을 지정 퀵슬롯에 등록하도록 인벤토리에 요청한다.
    /// </summary>
    public void AssignSelectedToQuickSlot(int index)
    {
        if (!InteractionAllowed || inventory == null || shownItem == null || !shownItem.CanUse) return;
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
        text.text = label + (equipment == null || !equipment.IsAssigned(kind) ? " · 준비되지 않음" : equipment.CurrentKind == kind ? " · 장착됨" : " · 배정됨");
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
