
public enum WeaponName { AssaultRifle = 0, Pistol = 1 }

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