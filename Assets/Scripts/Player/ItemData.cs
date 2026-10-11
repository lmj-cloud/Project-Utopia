using UnityEngine;

[CreateAssetMenu(fileName = "NewItem", menuName = "Utopia/Item Data")]
public class ItemData : ScriptableObject
{
    [SerializeField] private string itemName;
    [SerializeField, TextArea(3, 10)] private string description;
    [SerializeField] private Sprite icon;

    public string ItemName => itemName;
    public string Description => description;
    public Sprite Icon => icon;
}
