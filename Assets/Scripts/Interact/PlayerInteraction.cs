using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Selection Settings")]
    [SerializeField, Min(0.1f)] private float selectionDistance = 3f;
    [SerializeField] private LayerMask selectionLayers = ~0;

    [Header("NPC Settings")]
    [SerializeField, Min(0.1f)] private float npcInteractionDistance = 3f;
    [SerializeField] private LayerMask npcLayers = ~0;

    private HighlightableObject currentTarget;
    private NPCDialogue currentNpcTarget;
    private bool interactionEnabled = true;

    public HighlightableObject CurrentTarget => currentTarget;

    private void Update()
    {
        if (!interactionEnabled) return;
        RefreshTargets();
    }

    // 입력 검사는 PlayerController에서만 한다.
    public void TryInteract(PlayerInventory inventory, DialogueUI dialogueUI, StoryProgress story)
    {
        if (!isActiveAndEnabled || !interactionEnabled) return;
        if (dialogueUI != null && (dialogueUI.IsOpen || dialogueUI.WasClosedThisFrame)) return;

        // Update 호출 순서에 관계없이 현재 위치에서 대상을 다시 확인한다.
        RefreshTargets();
        if (currentNpcTarget != null)
        {
            currentNpcTarget.StartDialogue();
            return;
        }
        if (currentTarget == null) return;

        InteractableObject interactable = currentTarget.GetComponent<InteractableObject>();
        if (interactable != null) interactable.TryInteract(inventory, dialogueUI, story);
        RefreshTargets();
    }

    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;
        if (!value)
        {
            ChangeTarget(null);
            currentNpcTarget = null;
        }
    }

    private void RefreshTargets()
    {
        HighlightableObject objectTarget = FindTarget();
        NPCDialogue npcTarget = FindNpcTarget();
        if (objectTarget != null && npcTarget != null)
        {
            float objectDistance = (objectTarget.transform.position - transform.position).sqrMagnitude;
            float npcDistance = (npcTarget.transform.position - transform.position).sqrMagnitude;
            bool keepNpc = Mathf.Approximately(npcDistance, objectDistance)
                ? currentNpcTarget == npcTarget
                : npcDistance < objectDistance;
            if (keepNpc) objectTarget = null;
            else npcTarget = null;
        }
        ChangeTarget(objectTarget);
        currentNpcTarget = npcTarget;
    }

    private NPCDialogue FindNpcTarget()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(
            transform.position + Vector3.up, npcInteractionDistance,
            npcLayers, QueryTriggerInteraction.Collide);

        NPCDialogue nearestNpc = null;
        float nearestDistanceSquared = float.MaxValue;
        foreach (Collider nearbyCollider in nearbyColliders)
        {
            NPCDialogue candidate = nearbyCollider.GetComponentInParent<NPCDialogue>();
            if (candidate == null || !candidate.CanStartDialogue) continue;
            float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared ||
                (Mathf.Approximately(distanceSquared, nearestDistanceSquared) && candidate == currentNpcTarget))
            {
                nearestDistanceSquared = distanceSquared;
                nearestNpc = candidate;
            }
        }
        return nearestNpc;
    }

    private HighlightableObject FindTarget()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(
            transform.position + Vector3.up, selectionDistance,
            selectionLayers, QueryTriggerInteraction.Collide);

        HighlightableObject nearestTarget = null;
        float nearestDistanceSquared = float.MaxValue;
        foreach (Collider nearbyCollider in nearbyColliders)
        {
            HighlightableObject candidate = nearbyCollider.GetComponentInParent<HighlightableObject>();
            if (candidate == null || !candidate.isActiveAndEnabled) continue;
            InteractableObject interactable = candidate.GetComponent<InteractableObject>();
            if (interactable == null || !interactable.CanInteract) continue;
            float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distanceSquared < nearestDistanceSquared ||
                (Mathf.Approximately(distanceSquared, nearestDistanceSquared) && candidate == currentTarget))
            {
                nearestDistanceSquared = distanceSquared;
                nearestTarget = candidate;
            }
        }
        return nearestTarget;
    }

    private void ChangeTarget(HighlightableObject nextTarget)
    {
        if (currentTarget == nextTarget) return;
        if (currentTarget != null) currentTarget.SetHighlighted(false);
        currentTarget = nextTarget;
        if (currentTarget != null) currentTarget.SetHighlighted(true);
    }

    private void OnDisable()
    {
        ChangeTarget(null);
        currentNpcTarget = null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, selectionDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, npcInteractionDistance);
    }
}
