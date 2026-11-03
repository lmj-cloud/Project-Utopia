using UnityEngine;

/// <summary>
/// 인벤토리에서 아이템을 분류할 카테고리 값이다.
/// </summary>
public enum ItemCategory { Unclassified = 0, Consumables = 1, Uniform = 2, StoryItems = 3 }

/// <summary>
/// 아이템의 표시 정보, 분류, 사용 가능 여부와 사용 효과 설정을 보관한다.
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "Utopia/Item Data")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string itemName;
    [SerializeField, TextArea(3, 10)] private string description;
    [SerializeField] private Sprite icon;

    [SerializeField] private ItemCategory category = ItemCategory.Unclassified;
    public ItemCategory Category => category;

    [Header("Fixed Equipment")]
    [SerializeField] private bool isFixedWeapon;
    [SerializeField] private PlayerEquipmentKind equipmentKind;
    public bool IsFixedWeapon => isFixedWeapon;
    public PlayerEquipmentKind EquipmentKind => equipmentKind;

    [Header("Quick Slot")]
    [Tooltip("카테고리와 무관한 퀵슬롯 등록 허용 여부. 실제 사용에는 Usable도 필요합니다.")]
    [SerializeField] private bool canEquipToQuickSlot;
    public bool CanEquipToQuickSlot => canEquipToQuickSlot;

    [Header("Use")]
    [SerializeField] private bool usable;
    [SerializeField] private bool consumable = true;
    [SerializeField, Min(1)] private int consumePerUse = 1;
    [SerializeField] private bool medicationItem;
    public bool CanUse => usable;
    public bool IsConsumable => consumable;
    public int ConsumePerUse => Mathf.Max(1, consumePerUse);
    public bool IsMedication => medicationItem;
    public string ItemName => itemName;
    public string Description => description;
    public Sprite Icon => icon;
}
