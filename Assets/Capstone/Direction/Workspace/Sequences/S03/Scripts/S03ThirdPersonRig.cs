using UnityEngine;

namespace Capstone.Direction.Workspace.S03
{
    // Locomotion and mouse pitch/yaw remain owned by the existing prototype player.
    [DefaultExecutionOrder(100)]
    public sealed class S03ThirdPersonRig : MonoBehaviour
    {
        public Transform pivot;
        public Vector3 offset = new Vector3(0, 0.55f, -3.8f);

        void LateUpdate() => Snap();

        public void Snap()
        {
            if (!pivot) return;
            Vector3 direction = pivot.TransformVector(offset);
            Vector3 position = pivot.position + direction;
            if (Physics.SphereCast(pivot.position, 0.15f, direction.normalized,
                    out RaycastHit hit, direction.magnitude, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                position = pivot.position + direction.normalized * Mathf.Max(0.35f, hit.distance - 0.1f);
            transform.SetPositionAndRotation(position,
                Quaternion.LookRotation(pivot.position + pivot.forward * 2f - position, Vector3.up));
        }
    }
}
