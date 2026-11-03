using UnityEngine;

public enum AffinityGroup { Utopia = 0, Dystopia = 1 }
public enum PersonnelStatKind { Health = 0, Attack = 1, Defense = 2, Agility = 3 }

/// <summary>인물의 기본 정보와 초기 표시 값. 전투 컴포넌트와 연결되지 않는 UI용 데이터다.</summary>
[CreateAssetMenu(menuName = "UTOPIA/Affinity Character")]
public class AffinityCharacterData : ScriptableObject
{
    [SerializeField] private string characterId;
    [SerializeField] private string characterName;
    [SerializeField] private AffinityGroup group;
    [SerializeField] private Sprite portrait;
    [SerializeField] private Sprite characterCardImage;
    [SerializeField, TextArea(3, 6)] private string description;
    [Header("Relationship — independent of combat status")]
    [SerializeField, Min(1)] private int affinityMaximum = 100;
    [SerializeField, Min(0)] private int initialAffinity;
    [Header("Display-only HP / ATK / DEF / AGI")]
    [SerializeField, Min(1)] private int statusMaximum = 100;
    [SerializeField, Min(0)] private int health;
    [SerializeField, Min(0)] private int attack;
    [SerializeField, Min(0)] private int defense;
    [SerializeField, Min(0)] private int agility;

    public string CharacterId => characterId;
    public string CharacterName => characterName;
    public AffinityGroup Group => group;
    public Sprite Portrait => portrait;
    public Sprite CharacterCardImage => characterCardImage;
    public string Description => description;
    public int AffinityMaximum => Mathf.Max(1, affinityMaximum);
    public int StatusMaximum => Mathf.Max(1, statusMaximum);
    public int InitialAffinity => Mathf.Clamp(initialAffinity, 0, AffinityMaximum);

    public int GetInitialStatus(PersonnelStatKind kind)
    {
        int value = 0;
        switch (kind)
        {
            case PersonnelStatKind.Health: value = health; break;
            case PersonnelStatKind.Attack: value = attack; break;
            case PersonnelStatKind.Defense: value = defense; break;
            case PersonnelStatKind.Agility: value = agility; break;
        }
        return Mathf.Clamp(value, 0, StatusMaximum);
    }
}
