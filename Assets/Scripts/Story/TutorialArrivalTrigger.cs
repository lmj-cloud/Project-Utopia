using UnityEngine;
using UnityEngine.Events;

/// <summary>기존 플레이어 또는 실제 탑승 차량의 구역 도착만 Page 진행 Bridge에 알린다.</summary>
[RequireComponent(typeof(Collider))]
public class TutorialArrivalTrigger : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private VehicleController vehicle;
    [SerializeField] private UnityEvent onArrived = new UnityEvent();
    private void OnTriggerEnter(Collider other) { CheckArrival(other); }
    private void OnTriggerStay(Collider other) { CheckArrival(other); }
    private void CheckArrival(Collider other)
    {
        if (vehicle != null)
        {
            if (other.GetComponentInParent<VehicleController>() != vehicle || !vehicle.IsOccupied ||
                vehicle.Driver != player || player.HasOtherControlLock(vehicle)) return;
        }
        else if (player == null || !player.CanControlPlayer || other.GetComponentInParent<PlayerController>() != player) return;
        // 단계·중복 실행 여부는 기존 Story/Bridge가 판단한다. 일찍 진입했어도 조건 충족 후 재시도한다.
        onArrived.Invoke();
    }
}
