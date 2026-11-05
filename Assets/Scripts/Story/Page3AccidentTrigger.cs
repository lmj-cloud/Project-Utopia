using UnityEngine;

/// <summary>실제 탑승 차량의 진입만 Page3 사고 Adapter에 전달한다.</summary>
[RequireComponent(typeof(Collider))]
public class Page3AccidentTrigger : MonoBehaviour
{
    [SerializeField] private VehicleController vehicle;
    [SerializeField] private Page3S10Adapter accident;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<VehicleController>() != vehicle) return;
        accident.RequestAccident(vehicle);
    }
}
