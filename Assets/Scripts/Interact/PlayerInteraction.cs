using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 플레이어 주변의 NPC와 조사 대상을 선택하고 조건에 맞는 상호작용을 요청한다.
/// </summary>
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

    /// <summary>
    /// 매 프레임 현재 상태와 입력을 확인하여 이 컴포넌트의 동작을 갱신한다.
    /// </summary>
    private void Update()
    {
        if (!interactionEnabled) return;
        RefreshTargets();
    }

    // 입력 검사는 PlayerController에서만 한다.
    /// <summary>
    /// 거리 또는 선행 아이템·스토리 조건을 확인해 조사·획득·대화 시작을 요청한다.
    /// </summary>
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

    /// <summary>
    /// 상호작용 탐색을 허용하거나 중지하고 남은 선택 표시를 정리한다.
    /// </summary>
    public void SetInteractionEnabled(bool value)
    {
        interactionEnabled = value;
        if (!value)
        {
            ChangeTarget(null);
            currentNpcTarget = null;
        }
    }

    /// <summary>
    /// 현재 위치에서 NPC와 하이라이트 대상을 다시 탐색하여 선택 상태를 맞춘다.
    /// </summary>
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

    /// <summary>
    /// 플레이어 주변의 거리와 조건에 맞는 NPC 대상을 찾는다.
    /// </summary>
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

    /// <summary>
    /// 플레이어의 시야와 탐색 조건에 맞는 조사 대상을 찾는다.
    /// </summary>
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

    /// <summary>
    /// 이전 대상의 하이라이트를 해제하고 새 선택 대상의 표시를 적용한다.
    /// </summary>
    private void ChangeTarget(HighlightableObject nextTarget)
    {
        if (currentTarget == nextTarget) return;
        if (currentTarget != null) currentTarget.SetHighlighted(false);
        currentTarget = nextTarget;
        if (currentTarget != null) currentTarget.SetHighlighted(true);
    }

    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        ChangeTarget(null);
        currentNpcTarget = null;
    }

    /// <summary>
    /// 선택된 오브젝트의 상호작용 탐색 범위를 Editor에서 표시한다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, selectionDistance);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up, npcInteractionDistance);
    }
}
