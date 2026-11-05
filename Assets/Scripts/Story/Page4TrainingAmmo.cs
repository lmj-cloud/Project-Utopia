using UnityEngine;

/// <summary>Page4의 지정 사격장 안에 있는 플레이어 소총에만 예비 탄약 권한을 예약한다.</summary>
[RequireComponent(typeof(Collider))]
public class Page4TrainingAmmo : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private WeaponAssaultRifle rifle;
    private bool acquired;
    private void OnTriggerStay(Collider other)
    {
        if (acquired || other.GetComponentInParent<PlayerController>() != player) return;
        acquired = rifle.AcquireTrainingReserve(this);
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() == player) Release();
    }
    private void Release()
    {
        if (!acquired) return;
        acquired = false;
        if (rifle != null) rifle.ReleaseTrainingReserve(this);
    }
    private void OnDisable() { Release(); }
}
