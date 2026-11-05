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
    // Page1 가족처럼 가까운 NPC는 시선을 우선한다. 다른 씬은 기존 거리 우선을 유지한다.
    [SerializeField] private bool preferNpcInView;

    [SerializeField] private Transform selectionView;
    [SerializeField, Range(1f, 180f)] private float maxViewAngle = 65f;
    private HighlightableObject currentTarget;
    private NPCDialogue currentNpcTarget;
    private bool interactionEnabled = true;
    private int interactionBlockedFrame = -1;

    public HighlightableObject CurrentTarget => currentTarget;
    public Transform SelectedTarget
    {
        get
        {
            if (!interactionEnabled || interactionBlockedFrame == Time.frameCount) return null;
            if (currentNpcTarget != null) return currentNpcTarget.transform;
            if (currentTarget != null) return currentTarget.transform;
            return null;
        }
    }

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
        if (!isActiveAndEnabled || !interactionEnabled || interactionBlockedFrame == Time.frameCount) return;
        if (dialogueUI != null && (dialogueUI.IsOpen || dialogueUI.WasClosedThisFrame)) return;

        // Update 호출 순서에 관계없이 현재 위치에서 대상을 다시 확인한다.
        RefreshTargets();
        if (currentNpcTarget != null)
        {
            currentNpcTarget.StartDialogue();
            return;
        }
        if (currentTarget == null) return;

        // 차량도 기존 거리·우선순위 선정 결과를 그대로 사용한다.
        VehicleController vehicle = currentTarget.GetComponent<VehicleController>();
        if (vehicle != null)
        {
            vehicle.TryEnter(GetComponent<PlayerController>());
            RefreshTargets();
            return;
        }
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
    /// 하차에 사용한 입력이 같은 프레임에 재탑승이나 다른 상호작용으로 다시 소비되지 않게 한다.
    /// </summary>
    public void SuppressInteractionForCurrentFrame()
    {
        interactionBlockedFrame = Time.frameCount;
        ChangeTarget(null);
        currentNpcTarget = null;
    }

    /// <summary>
    /// 현재 위치에서 기존 거리·우선순위 규칙으로 상호작용 대상을 다시 선택한다.
    /// </summary>
    private void RefreshTargets()
    {
        if (interactionBlockedFrame == Time.frameCount) return;
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
        float nearestViewAngle = float.MaxValue;
        foreach (Collider nearbyCollider in nearbyColliders)
        {
            NPCDialogue candidate = nearbyCollider.GetComponentInParent<NPCDialogue>();
            if (candidate == null || !candidate.CanStartDialogue) continue;
            float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
            float viewAngle = 0f;
            bool useView = preferNpcInView && selectionView != null;
            if (useView)
            {
                viewAngle = Vector3.Angle(selectionView.forward, nearbyCollider.bounds.center - selectionView.position);
                if (viewAngle > maxViewAngle) continue;
            }
            bool better = distanceSquared < nearestDistanceSquared;
            if (useView)
                better = viewAngle < nearestViewAngle ||
                    (Mathf.Approximately(viewAngle, nearestViewAngle) && distanceSquared < nearestDistanceSquared);
            if (better ||
                (Mathf.Approximately(distanceSquared, nearestDistanceSquared) &&
                 (!useView || Mathf.Approximately(viewAngle, nearestViewAngle)) && candidate == currentNpcTarget))
            {
                nearestDistanceSquared = distanceSquared;
                nearestViewAngle = viewAngle;
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
        float nearestViewAngle = float.MaxValue;
        foreach (Collider nearbyCollider in nearbyColliders)
        {
            HighlightableObject candidate = nearbyCollider.GetComponentInParent<HighlightableObject>();
            if (candidate == null || !candidate.isActiveAndEnabled) continue;
            float viewAngle = 0f;
            if (selectionView != null)
            {
                Vector3 direction = nearbyCollider.bounds.center - selectionView.position;
                viewAngle = Vector3.Angle(selectionView.forward, direction);
                if (viewAngle > maxViewAngle) continue;
            }
            InteractableObject interactable = candidate.GetComponent<InteractableObject>();
            VehicleController vehicle = candidate.GetComponent<VehicleController>();
            if (vehicle != null)
            {
                if (!vehicle.CanEnter) continue;
            }
            else if (interactable == null || !interactable.CanInteract) continue;
            float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
            bool isBetter = distanceSquared < nearestDistanceSquared;
            if (selectionView != null)
                isBetter = viewAngle < nearestViewAngle ||
                    (Mathf.Approximately(viewAngle, nearestViewAngle) && distanceSquared < nearestDistanceSquared);
            if (isBetter || (Mathf.Approximately(viewAngle, nearestViewAngle) &&
                Mathf.Approximately(distanceSquared, nearestDistanceSquared) && candidate == currentTarget))
            {
                nearestDistanceSquared = distanceSquared;
                nearestViewAngle = viewAngle;
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
