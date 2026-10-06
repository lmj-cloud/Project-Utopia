using UnityEngine;

public enum FireMode
{
    SemiAuto,
    FullAuto
}

[CreateAssetMenu(fileName = "GunData", menuName = "GunData")]
public class GunData : ScriptableObject
{
    [SerializeField] private string gunName;
    [SerializeField] private FireMode fireMode;

    [SerializeField] private float damage = 10f;
    [SerializeField] private float distance = 50f;
    [SerializeField] private float delaySecond = 0.5f;

    public string GunName => gunName;
    public FireMode FireMode => fireMode;
    public float Damage => damage;
    public float Distance => distance;
    public float DelaySecond => delaySecond;
}
