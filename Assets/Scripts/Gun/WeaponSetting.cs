
/// <summary>
/// 총기의 소총·권총 종류를 구분하는 기존 식별 값이다.
/// </summary>
public enum WeaponName { AssaultRifle = 0, Pistol = 1 }

/// <summary>
/// 총기 종류와 탄약, 발사 속도, 사거리 및 피해 설정을 함께 전달하는 직렬화 데이터다.
/// </summary>
[System.Serializable]
public struct WeaponSetting
{
    public WeaponName weaponName;
    [UnityEngine.Min(0)] public int initialTotalAmmo;
    [UnityEngine.Min(0)] public int reserveAmmo;
    public int currentAmmo;
    public int maxAmmo;
    public float attackRate;
    public float attackDistance;
    public bool isAutomaticAttack;
    [UnityEngine.Min(0f)] public float attackDamage;
}