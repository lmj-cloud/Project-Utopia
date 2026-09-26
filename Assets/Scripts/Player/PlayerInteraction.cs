using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Selection Settings")]
    [SerializeField, Min(0.1f)] private float selectionDistance = 3f;
    [SerializeField] private LayerMask selectionLayers = ~0;

    private HighlightableObject currentTarget;

    public HighlightableObject CurrentTarget => currentTarget;

    private void Update()
    {
        ChangeTarget(FindTarget());

        if (currentTarget != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            InteractableObject interactable = currentTarget.GetComponent<InteractableObject>();

            if (interactable != null)
            {
                interactable.Interact();
            }
        }
    }

    private HighlightableObject FindTarget()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(
            transform.position,
            selectionDistance,
            selectionLayers,
            QueryTriggerInteraction.Collide);

        HighlightableObject nearestTarget = null;
        float nearestDistanceSquared = float.MaxValue;

        foreach (Collider nearbyCollider in nearbyColliders)
        {
            HighlightableObject candidate = nearbyCollider.GetComponentInParent<HighlightableObject>();

            if (candidate == null)
            {
                continue;
            }

            InteractableObject interactable = candidate.GetComponent<InteractableObject>();

            if (interactable == null || !interactable.CanInteract)
            {
                continue;
            }

            float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;

            if (distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestTarget = candidate;
            }
        }

        return nearestTarget;
    }

    private void ChangeTarget(HighlightableObject nextTarget)
    {
        if (currentTarget == nextTarget)
        {
            return;
        }

        if (currentTarget != null)
        {
            currentTarget.SetHighlighted(false);
        }

        currentTarget = nextTarget;

        if (currentTarget != null)
        {
            currentTarget.SetHighlighted(true);
        }
    }

    private void OnDisable()
    {
        if (currentTarget != null)
        {
            currentTarget.SetHighlighted(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, selectionDistance);
    }
}
