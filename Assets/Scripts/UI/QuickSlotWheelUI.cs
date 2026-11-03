using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 기존 인벤토리의 퀵슬롯 5개를 표시하고 확정된 슬롯만 기존 아이템 사용 처리에 전달한다.
/// </summary>
public class QuickSlotWheelUI : MonoBehaviour
{
    [Serializable]
    private class WheelSlot
    {
        public RectTransform rect;
        public UnityEngine.UI.Image icon;
        [NonSerialized] public Vector3 originalScale;
    }

    [Header("Canvas 참조: 이 컴포넌트는 Wheel Root 바깥에 배치")]
    [SerializeField] private RectTransform wheelRoot;
    [SerializeField] private Canvas canvas;
    [SerializeField] private WheelSlot[] slots = new WheelSlot[PlayerInventory.EquippedSlotCount];
    [Header("부채꼴 Hover 판정: Wheel Root 로컬 좌표 기준")]
    [Tooltip("확대 전 원의 바깥 반지름. 슬롯의 Scale이나 사각형 크기로 판정하지 않습니다.")]
    [SerializeField, Min(0f)] private float radius = 225f;
    [Tooltip("Slot01 부채꼴의 중심 각도. 오른쪽 0도, 위쪽 90도이며 양수는 반시계 방향입니다.")]
    [SerializeField, Range(0f, 360f)] private float startAngle = 90f;
    [Tooltip("배열 인덱스가 증가하는 방향. 현재 Slot01~05 배치는 반시계 방향이므로 끕니다.")]
    [SerializeField] private bool isClockwise;
    [Tooltip("선택하지 않는 중앙 원의 반지름. 바깥 반지름보다 작아야 합니다.")]
    [SerializeField, Min(0f)] private float centerDeadZone = 36f;
    [Header("Hover 확대")]
    [SerializeField, Min(1f)] private float hoverScale = 1.2f;
    [SerializeField, Min(0.1f)] private float hoverSpeed = 12f;
    private PlayerController player;
    private PlayerInventory inventory;
    private int closedFrame = -1;
    private CursorLockMode previousCursorLock;
    private bool wasCursorVisible;
    private bool hasCapturedScale;
    public bool IsOpen { get; private set; }
    public int HoveredSlotIndex { get; private set; } = -1;
    public bool WasClosedThisFrame => closedFrame == Time.frameCount;

    private void Awake()
    {
        if (wheelRoot != null && !transform.IsChildOf(wheelRoot)) wheelRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 필수 UI 참조가 있을 때만 자신의 입력 잠금을 얻고 표시한다. UI는 런타임에 생성하지 않는다.
    /// </summary>
    public bool TryOpen(PlayerController owner, PlayerInventory source)
    {
        if (IsOpen || !isActiveAndEnabled || owner == null || source == null || !owner.CanControlPlayer ||
            source.IsUseLocked || !HasValidSetup() || Mouse.current == null) return false;
        if (!hasCapturedScale)
        {
            for (int i = 0; i < slots.Length; i++) slots[i].originalScale = slots[i].rect.localScale;
            hasCapturedScale = true;
        }
        player = owner;
        inventory = source;
        previousCursorLock = Cursor.lockState;
        wasCursorVisible = Cursor.visible;
        HoveredSlotIndex = -1;
        inventory.Changed += Refresh;
        player.AcquireControlLock(this);
        IsOpen = true;
        wheelRoot.gameObject.SetActive(true);
        Refresh();
        Canvas.ForceUpdateCanvases();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // 워프 직후의 이전 마우스 위치로 선택하지 않는다. 첫 프레임은 선택 없음으로 시작한다.
        Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera != null)
            screen = canvas.worldCamera.pixelRect;
        Mouse.current.WarpCursorPosition(screen.center);
        return true;
    }

    private bool HasValidSetup()
    {
        if (wheelRoot == null || canvas == null || canvas.renderMode == RenderMode.WorldSpace ||
            !canvas.isActiveAndEnabled || transform.IsChildOf(wheelRoot) ||
            !wheelRoot.IsChildOf(canvas.transform) || slots == null || slots.Length != PlayerInventory.EquippedSlotCount ||
            (canvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.worldCamera == null)) return false;
        for (int i = 0; i < slots.Length; i++)
        {
            WheelSlot slot = slots[i];
            if (slot == null || slot.rect == null || slot.icon == null || slot.rect.parent != wheelRoot ||
                slot.icon.transform == slot.rect || !slot.icon.transform.IsChildOf(slot.rect)) return false;
            for (int j = 0; j < i; j++)
            {
                if (slots[j].rect == slot.rect) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// 슬롯의 사각형·Scale 대신 원 중심에서의 거리와 각도로 부채꼴을 선택한다.
    /// 중앙과 바깥 영역은 항상 선택 없음이며, Icon의 활성 상태는 검사하지 않는다.
    /// </summary>
    public void UpdatePointer(Vector2 screenPosition)
    {
        if (!IsOpen) return;
        HoveredSlotIndex = -1;
        if (!HasValidSetup() || radius <= centerDeadZone) return;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(wheelRoot, screenPosition, uiCamera, out local)) return;
        Vector2 direction = local - wheelRoot.rect.center;
        float distanceSquared = direction.sqrMagnitude;
        if (distanceSquared <= centerDeadZone * centerDeadZone || distanceSquared > radius * radius) return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float relativeAngle = angle - startAngle;
        if (isClockwise) relativeAngle = startAngle - angle;
        float sectorAngle = 360f / slots.Length;
        // Start Angle은 경계가 아닌 첫 슬롯의 중심이다. 경계는 다음 슬롯에 한 번만 배정한다.
        float wrappedAngle = Mathf.Repeat(relativeAngle + sectorAngle * 0.5f, 360f);
        HoveredSlotIndex = Mathf.FloorToInt(wrappedAngle / sectorAngle);
    }
    /// <summary>
    /// 수량은 기존 인벤토리에서 읽는다. 아이콘이 없거나 빈 슬롯이면 아이콘만 숨긴다.
    /// </summary>
    private void Refresh()
    {
        if (inventory == null || !HasValidSetup()) return;
        for (int i = 0; i < slots.Length; i++)
        {
            InventoryEntry entry = inventory.GetEntry(true, i);
            UnityEngine.UI.Image icon = slots[i].icon;
            icon.sprite = entry != null && entry.Item != null ? entry.Item.Icon : null;
            bool hasIcon = icon.sprite != null;
            icon.gameObject.SetActive(hasIcon);
            icon.enabled = hasIcon;
            icon.raycastTarget = false;
            if (hasIcon)
            {
                // 수동 구성에서 빈 슬롯용으로 투명해진 Icon도 실제 아이콘이 있으면 표시한다.
                Color color = icon.color;
                color.a = 1f;
                icon.color = color;
            }
        }
    }

    private void Update()
    {
        if (!IsOpen) return;
        // 다른 시스템의 잠금·대화가 시작되면 아이템을 사용하지 않고 자기 잠금만 반환한다.
        if (player == null || !player.CanKeepQuickSlotOpen(this) || !HasValidSetup() || !wheelRoot.gameObject.activeInHierarchy)
        {
            Cancel();
            return;
        }
        float blend = 1f - Mathf.Exp(-hoverSpeed * Time.unscaledDeltaTime);
        for (int i = 0; i < slots.Length; i++)
        {
            bool isHovered = i == HoveredSlotIndex;
            Vector3 scale = slots[i].originalScale;
            if (isHovered) scale *= hoverScale;
            slots[i].rect.localScale = Vector3.Lerp(slots[i].rect.localScale, scale, blend);
        }
    }

    /// <summary>
    /// 먼저 세션과 자기 잠금을 닫은 뒤 딱 한 번 사용을 요청한다. 실패하더라도 재시도하지 않는다.
    /// </summary>
    public bool ConfirmAndClose(ItemUseController itemUse)
    {
        if (!IsOpen) return false;
        if (player == null || !player.CanKeepQuickSlotOpen(this))
        {
            Cancel();
            return false;
        }
        int selected = HoveredSlotIndex;
        // Cancel이 Inventory 참조를 해제하므로 실제 보유한 선택 아이템을 먼저 확보한다.
        InventoryEntry entry = inventory != null && selected >= 0 ? inventory.GetEntry(true, selected) : null;
        ItemData selectedItem = entry != null && entry.Quantity > 0 ? entry.Item : null;
        Cancel();
        if (selectedItem != null)
        {
            Debug.Log($"[QuickSlot] 선택한 아이템: {selectedItem.ItemName}", this);
        }
        if (selected < 0 || itemUse == null) return false;
        return itemUse.TryUseEquippedItem(selected);
    }

    /// <summary>
    /// 소비 없이 닫는다. 다른 시스템의 입력 잠금은 해제하지 않는다.
    /// </summary>
    public void Cancel()
    {
        if (!IsOpen) return;
        IsOpen = false;
        closedFrame = Time.frameCount;
        HoveredSlotIndex = -1;
        if (inventory != null) inventory.Changed -= Refresh;
        if (player != null) player.ReleaseControlLock(this);
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            if (slots[i].rect != null) slots[i].rect.localScale = slots[i].originalScale;
        }
        if (wheelRoot != null) wheelRoot.gameObject.SetActive(false);
        Cursor.lockState = previousCursorLock;
        Cursor.visible = wasCursorVisible;
        inventory = null;
        player = null;
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) Cancel();
    }
}
