using UnityEngine;

/// <summary>Kevin's Humanoid LookAt target and weights, evaluated by his Animator's IK pass.</summary>
public sealed class BoxingOpponentLook : MonoBehaviour
{
    [SerializeField] private SparringOpponent opponent;
    [SerializeField] private Animator boxer;
    [SerializeField] private Animator player;
    [SerializeField, Range(0f, 80f)] private float maxYaw = 45f;
    [SerializeField, Range(0f, 45f)] private float maxPitch = 20f;
    [SerializeField, Range(0f, 25f)] private float maxBodyYaw = 12f;
    [SerializeField, Min(0.1f)] private float response = 10f;
    [SerializeField, Range(0f, 1f)] private float spineWeight = .03f;
    [SerializeField, Range(0f, 1f)] private float chestWeight = .05f;
    [SerializeField, Range(0f, 1f)] private float upperChestWeight = .06f;
    [SerializeField, Range(0f, 1f)] private float neckWeight = .17f;
    [SerializeField, Range(0f, 1f)] private float headWeight = .69f;
    [SerializeField, Range(0f, 1f)] private float attackBodyWeight = .25f;

    private Transform head;
    private Transform target;
    private PlayerController playerController;
    private Vector2 angles;
    private float weight;
    private float bodyBlend = 1f;

    private void Awake()
    {
        if (boxer == null || !boxer.isHuman || player == null || !player.isHuman) return;
        head = boxer.GetBoneTransform(HumanBodyBones.Head);
        target = player.GetBoneTransform(HumanBodyBones.Head);
        playerController = player.GetComponentInParent<PlayerController>();
    }

    private void OnDisable()
    {
        angles = Vector2.zero;
        weight = 0f;
        bodyBlend = 1f;
    }

    internal void ApplyAnimatorIK(Animator source)
    {
        if (!isActiveAndEnabled || source != boxer || head == null || target == null || opponent == null) return;

        Vector2 desired = angles;
        float targetWeight = 0f;
        if (opponent.IsTraining && playerController != null && playerController.IsAlive &&
            !playerController.HasAnimationControlLock)
        {
            Vector3 direction = transform.InverseTransformDirection(target.position - head.position);
            if (direction.sqrMagnitude > .001f)
            {
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Atan2(-direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
                desired = new Vector2(Mathf.Clamp(pitch, -maxPitch, maxPitch), Mathf.Clamp(yaw, -maxYaw, maxYaw));
                // Keep the target on the front hemisphere and release the gaze behind the boxer.
                targetWeight = Mathf.InverseLerp(150f, 75f, Mathf.Abs(yaw));
            }
        }

        float blend = 1f - Mathf.Exp(-response * Time.deltaTime);
        angles = Vector2.Lerp(angles, desired, blend);
        weight = Mathf.Lerp(weight, targetWeight, blend);
        bodyBlend = Mathf.Lerp(bodyBlend, opponent.IsAttacking ? attackBodyWeight : 1f, blend);

        // The Humanoid solver distributes body and head weights through the spine and neck.
        float bodyWeight = Mathf.Clamp01(spineWeight + chestWeight + upperChestWeight);
        bodyWeight = Mathf.Min(bodyWeight, maxBodyYaw / Mathf.Max(1f, maxYaw));
        Vector3 gaze = transform.rotation * Quaternion.Euler(angles.x, angles.y, 0f) * Vector3.forward;
        source.SetLookAtPosition(head.position + gaze * 10f);
        source.SetLookAtWeight(weight, bodyWeight * bodyBlend, Mathf.Clamp01(headWeight + neckWeight), 0f, .3f);
    }
}
