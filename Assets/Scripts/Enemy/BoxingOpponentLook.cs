using UnityEngine;

/// <summary>Applies a bounded gaze offset after Kevin's boxing pose has been evaluated.</summary>
[DefaultExecutionOrder(100)]
public sealed class BoxingOpponentLook : MonoBehaviour
{
    [SerializeField] private SparringOpponent opponent;
    [SerializeField] private Animator boxer;
    [SerializeField] private Animator player;
    [SerializeField, Range(0f, 80f)] private float maxYaw = 38f;
    [SerializeField, Range(0f, 45f)] private float maxPitch = 20f;
    [SerializeField, Min(0.1f)] private float response = 10f;
    [SerializeField, Range(0f, 1f)] private float spineWeight = .03f;
    [SerializeField, Range(0f, 1f)] private float chestWeight = .05f;
    [SerializeField, Range(0f, 1f)] private float upperChestWeight = .06f;
    [SerializeField, Range(0f, 1f)] private float neckWeight = .17f;
    [SerializeField, Range(0f, 1f)] private float headWeight = .69f;
    [SerializeField, Range(0f, 1f)] private float attackBodyWeight = .25f;

    private Transform[] bones;
    private Transform target;
    private PlayerController playerController;
    private readonly Quaternion[] pose = new Quaternion[5];
    private readonly Quaternion[] applied = new Quaternion[5];
    private Vector2 angles;
    private bool hasOffset;

    private void Awake()
    {
        if (boxer == null || !boxer.isHuman || player == null || !player.isHuman) return;
        bones = new[] {
            boxer.GetBoneTransform(HumanBodyBones.Spine),
            boxer.GetBoneTransform(HumanBodyBones.Chest),
            boxer.GetBoneTransform(HumanBodyBones.UpperChest),
            boxer.GetBoneTransform(HumanBodyBones.Neck),
            boxer.GetBoneTransform(HumanBodyBones.Head)
        };
        target = player.GetBoneTransform(HumanBodyBones.Head);
        playerController = player.GetComponentInParent<PlayerController>();
    }

    private void Update() => RemoveOffset();
    private void OnDisable()
    {
        RemoveOffset();
        angles = Vector2.zero;
    }

    private void RemoveOffset()
    {
        if (!hasOffset) return;
        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];
            if (bone != null && Quaternion.Angle(bone.localRotation, applied[i]) < .01f)
                bone.localRotation = pose[i];
        }
        hasOffset = false;
    }

    private void LateUpdate()
    {
        if (opponent == null || bones == null || bones[4] == null || target == null || !boxer.isActiveAndEnabled)
            return;

        Vector2 desired = Vector2.zero;
        if (opponent.IsTraining && playerController != null && !playerController.HasAnimationControlLock)
        {
            Vector3 direction = transform.InverseTransformDirection(target.position - bones[4].position);
            if (direction.sqrMagnitude > .001f)
            {
                float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Atan2(-direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
                // Ease out when the target crosses behind the boxer, where yaw changes sign.
                float frontWeight = Mathf.InverseLerp(150f, 75f, Mathf.Abs(yaw));
                desired = new Vector2(Mathf.Clamp(pitch, -maxPitch, maxPitch), Mathf.Clamp(yaw, -maxYaw, maxYaw)) * frontWeight;
            }
        }
        angles = Vector2.Lerp(angles, desired, 1f - Mathf.Exp(-response * Time.deltaTime));
        if (angles.sqrMagnitude < .0001f) return;

        float bodyWeight = opponent.IsAttacking ? attackBodyWeight : 1f;
        Apply(0, spineWeight * bodyWeight);
        Apply(1, chestWeight * bodyWeight);
        Apply(2, upperChestWeight * bodyWeight);
        Apply(3, neckWeight);
        Apply(4, headWeight);
        hasOffset = true;
    }

    private void Apply(int index, float weight)
    {
        Transform bone = bones[index];
        if (bone == null) return;
        pose[index] = bone.localRotation;
        Quaternion offset = Quaternion.Euler(angles.x * weight, angles.y * weight, 0f);
        bone.rotation = transform.rotation * offset * Quaternion.Inverse(transform.rotation) * bone.rotation;
        applied[index] = bone.localRotation;
    }
}
