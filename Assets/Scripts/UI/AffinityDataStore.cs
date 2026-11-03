using System.Collections.Generic;
using UnityEngine;

/// <summary>초기 에셋은 변경하지 않고 인물 ID별 호감도와 UI용 능력치를 독립적으로 유지한다.</summary>
public class AffinityDataStore : MonoBehaviour
{
    public class CharacterState
    {
        public AffinityCharacterData Data { get; private set; }
        public int Affinity { get; private set; }
        public bool IsUnlocked { get; private set; }
        private int health, attack, defense, agility;

        public CharacterState(AffinityCharacterData data)
        {
            Data = data;
            Affinity = data.InitialAffinity;
            health = data.GetInitialStatus(PersonnelStatKind.Health);
            attack = data.GetInitialStatus(PersonnelStatKind.Attack);
            defense = data.GetInitialStatus(PersonnelStatKind.Defense);
            agility = data.GetInitialStatus(PersonnelStatKind.Agility);
        }

        public int GetStatus(PersonnelStatKind kind)
        {
            switch (kind)
            {
                case PersonnelStatKind.Health: return health;
                case PersonnelStatKind.Attack: return attack;
                case PersonnelStatKind.Defense: return defense;
                case PersonnelStatKind.Agility: return agility;
                default: return 0;
            }
        }

        internal void Unlock() { IsUnlocked = true; }

        internal void SetAffinity(int value) { Affinity = value; }
        internal void SetStatus(PersonnelStatKind kind, int value)
        {
            switch (kind)
            {
                case PersonnelStatKind.Health: health = value; break;
                case PersonnelStatKind.Attack: attack = value; break;
                case PersonnelStatKind.Defense: defense = value; break;
                case PersonnelStatKind.Agility: agility = value; break;
            }
        }
    }

    [SerializeField] private AffinityCharacterData[] characters = new AffinityCharacterData[0];
    private readonly List<CharacterState> states = new List<CharacterState>();
    private bool hasInitialized;
    public event System.Action<string> Changed;
    public event System.Action<AffinityCharacterData> CharacterUnlocked;
    public int CharacterCount => characters.Length;

    private void Awake() { Initialize(); }

    /// <summary>비활성 페이지에서는 Awake보다 조회가 먼저 올 수 있다. 최초 한 번만 초기 데이터를 복사한다.</summary>
    private void Initialize()
    {
        if (hasInitialized) return;
        hasInitialized = true;
        foreach (AffinityCharacterData data in characters)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.CharacterId)) continue;
            bool hasDuplicate = false;
            foreach (CharacterState state in states)
                if (state.Data.CharacterId == data.CharacterId) hasDuplicate = true;
            if (hasDuplicate)
            {
                Debug.LogWarning("AffinityDataStore: 중복 인물 ID " + data.CharacterId, this);
                continue;
            }
            states.Add(new CharacterState(data));
        }
    }

    public AffinityCharacterData GetCharacter(int index)
    {
        if (index < 0 || index >= characters.Length) return null;
        return characters[index];
    }

    public CharacterState GetState(string characterId)
    {
        Initialize();
        foreach (CharacterState state in states)
            if (state.Data.CharacterId == characterId) return state;
        return null;
    }

    /// <summary>페이지를 닫아도 유지되는 이번 플레이의 인물 해금 여부를 조회한다.</summary>
    public bool IsUnlocked(string characterId)
    {
        CharacterState state = GetState(characterId);
        return state != null && state.IsUnlocked;
    }

    /// <summary>상태를 먼저 확정한 뒤 알린다. 같은 ID의 중복 요청은 보상과 알림을 발생시키지 않는다.</summary>
    public bool UnlockCharacter(string characterId)
    {
        CharacterState state = GetState(characterId);
        if (state == null || state.IsUnlocked) return false;
        state.Unlock();
        CharacterUnlocked?.Invoke(state.Data);
        Changed?.Invoke(characterId);
        return true;
    }

    /// <summary>호감도만 갱신한다. HP·ATK·DEF·AGI와 원본 ScriptableObject는 변경하지 않는다.</summary>
    public bool SetAffinity(string characterId, int value)
    {
        CharacterState state = GetState(characterId);
        if (state == null) return false;
        state.SetAffinity(Mathf.Clamp(value, 0, state.Data.AffinityMaximum));
        Changed?.Invoke(characterId);
        return true;
    }

    public bool AddAffinity(string characterId, int amount)
    {
        CharacterState state = GetState(characterId);
        if (state == null) return false;
        long next = (long)state.Affinity + amount;
        if (next < 0) next = 0;
        if (next > state.Data.AffinityMaximum) next = state.Data.AffinityMaximum;
        return SetAffinity(characterId, (int)next);
    }

    /// <summary>UI용 능력치만 갱신한다. 실제 전투 체력·공격력이나 호감도에는 영향을 주지 않는다.</summary>
    public bool SetStatus(string characterId, PersonnelStatKind kind, int value)
    {
        CharacterState state = GetState(characterId);
        if (state == null || !System.Enum.IsDefined(typeof(PersonnelStatKind), kind)) return false;
        state.SetStatus(kind, Mathf.Clamp(value, 0, state.Data.StatusMaximum));
        Changed?.Invoke(characterId);
        return true;
    }
}
