using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Utopia/Item Data")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string itemName;
    [SerializeField, TextArea(3, 10)] private string description;
    [SerializeField] private Sprite icon;

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
