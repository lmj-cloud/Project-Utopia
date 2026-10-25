using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 오브젝트 상호작용을 조사와 아이템 획득으로 구분한다.
/// </summary>
public enum InteractionType
{
    Inspect,
    Pickup
}

/// <summary>
/// 아이템과 스토리 조건을 검사해 조사·획득·대화를 시작하고 완료 또는 취소를 처리한다.
/// </summary>
public class InteractableObject : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private string interactionText = "조사하기";

    [Header("Interaction Type")]
    [SerializeField] private InteractionType interactionType = InteractionType.Inspect;
    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int itemQuantity = 1;

    // 성공한 상호작용을 다시 시작하지 않도록 제한한다.
    [Header("Settings")]
    [SerializeField] private bool interactOnlyOnce;
    [SerializeField] private bool disableAfterInteraction;
    // 문이나 외부 연출이 완료를 확정할 때까지 상호작용을 진행 중 상태로 유지한다.
    [SerializeField] private bool waitForExternalCompletion;
    // 상호작용 종료 후 재시도 전까지의 실제 시간 기준 대기 시간.
    [Tooltip("대사가 끝난 뒤 다시 조사할 수 있을 때까지의 시간. 0이면 즉시 재조사 가능.")]
    [SerializeField, Min(0f)] private float interactionCooldown = 2f;

    [Header("Dialogue")]
    [SerializeField, TextArea(2, 6)] private string[] dialogueLines;

    // 상호작용 조건으로 보유해야 할 아이템.
    [Header("Optional Conditions")]
    [SerializeField] private ItemData requiredItem;
    // 상호작용을 시작하기 전에 확인할 스토리 플래그.
    [SerializeField] private string requiredStoryFlag;

    [Header("Response")]
    [Tooltip("상호작용이 시작된 순간 실행. 기존 이벤트 연결을 유지한다.")]
    [SerializeField] private UnityEvent onInteracted = new();
    [Tooltip("이 오브젝트의 대사가 끝나면 실행. 대사가 없으면 바로 실행.")]
    [SerializeField] private UnityEvent onInteractionCompleted = new();
    [SerializeField] private UnityEvent onConditionFailed = new();
    [SerializeField] private UnityEvent onInventoryFull = new();

    [SerializeField] private TutorialManager tutorial;
    private bool hasInteracted;
    private bool interactionInProgress;
    private float nextInteractionTime;

    public bool IsPickup => interactionType == InteractionType.Pickup;
    public bool IsExternalInteractionPending => isActiveAndEnabled && waitForExternalCompletion && interactionInProgress;
    /// <summary>
    /// 외부 완료 대기 중인 조사 요청을 확정하고 해당 오브젝트를 비활성화한다.
    /// </summary>
    public bool TryConsumeExternalInteraction()
    {
        if (!IsExternalInteractionPending || IsPickup) return false;
        CompleteInteraction();
        if (this != null) gameObject.SetActive(false);
        return true;
    }
    public string InteractionText => interactionText;
    public bool CanInteract => isActiveAndEnabled && !interactionInProgress &&
        !(hasInteracted && (interactOnlyOnce || interactionType == InteractionType.Pickup)) &&
        Time.unscaledTime >= nextInteractionTime;

    // 기존 Interact() UnityEvent 연결을 위한 호환 메서드.
    // 인벤토리/대사가 필요한 상호작용은 PlayerInteraction을 통해 실행한다.
    /// <summary>
    /// 이전 UnityEvent 연결을 위한 호환 상호작용 호출을 전달한다.
    /// </summary>
    public void Interact() => TryInteract(null, null, null);

    /// <summary>
    /// 거리 또는 선행 아이템·스토리 조건을 확인해 조사·획득·대화 시작을 요청한다.
    /// </summary>
    public bool TryInteract(PlayerInventory inventory, DialogueUI dialogueUI, StoryProgress story)
    {
        if (!CanInteract) return false;

        bool itemConditionFailed = requiredItem != null &&
            (inventory == null || !inventory.HasItem(requiredItem));
        bool storyConditionFailed = !string.IsNullOrWhiteSpace(requiredStoryFlag) &&
            (story == null || !story.HasFlag(requiredStoryFlag));
        if (itemConditionFailed || storyConditionFailed)
        {
            onConditionFailed.Invoke();
            return false;
        }

        bool hasDialogue = HasDialogue();
        // Door completion is delegated; use NPCDialogue separately for conversations.
        if (waitForExternalCompletion && (hasDialogue || interactionType == InteractionType.Pickup)) return false;
        if (hasDialogue && (dialogueUI == null || !dialogueUI.IsReady || dialogueUI.IsOpen))
        {
            Debug.LogWarning($"{name}: PlayerController의 DialogueUI 연결을 확인하세요.", this);
            return false;
        }

        if (interactionType == InteractionType.Pickup)
        {
            if (itemData == null || inventory == null)
            {
                Debug.LogWarning($"{name}: ItemData와 PlayerInventory 연결이 필요합니다.", this);
                return false;
            }
            if (!inventory.TryAdd(itemData, itemQuantity))
            {
                onInventoryFull.Invoke();
                return false;
            }
        }

        hasInteracted = true;
        interactionInProgress = true;

        bool dialogueOpened = hasDialogue && dialogueUI.TryOpenSequence(
            dialogueLines,
            () => { if (this != null) CompleteInteraction(); },
            () => { if (this != null) CancelInteraction(); });

        onInteracted.Invoke();

        if (!dialogueOpened && !waitForExternalCompletion) CompleteInteraction();

        // 아이템은 저장에 성공한 경우에만 숨긴다. 대화 콜백은 UI에서 계속 관리한다.
        if (interactionType == InteractionType.Pickup && this != null)
            gameObject.SetActive(false);

        return true;
    }

    /// <summary>
    /// 외부 동작이 완료된 경우 대기 중 상호작용의 완료를 요청한다.
    /// </summary>
    public void CompleteExternalInteraction()
    {
        if (waitForExternalCompletion) CompleteInteraction();
    }

    /// <summary>
    /// 외부 상호작용 대기를 취소하고 재시도 가능한 상태와 대기 시간을 설정한다.
    /// </summary>
    public void CancelExternalInteraction()
    {
        if (!waitForExternalCompletion || !interactionInProgress) return;
        hasInteracted = false;
        interactionInProgress = false;
        nextInteractionTime = Time.unscaledTime + Mathf.Max(0f, interactionCooldown);
    }

    /// <summary>
    /// 상호작용의 완료·진행·재시도 대기 상태를 초기화한다.
    /// </summary>
    public void ResetInteraction()
    {
        hasInteracted = false;
        interactionInProgress = false;
        nextInteractionTime = 0f;
    }

    /// <summary>
    /// 유효한 대사 문자열이 하나 이상 연결되어 있는지 확인한다.
    /// </summary>
    private bool HasDialogue()
    {
        if (dialogueLines == null) return false;
        foreach (string line in dialogueLines)
            if (!string.IsNullOrWhiteSpace(line)) return true;
        return false;
    }

    /// <summary>
    /// 현재 상호작용 완료를 확정하고 튜토리얼·완료 이벤트 및 표시 상태를 갱신한다.
    /// </summary>
    private void CompleteInteraction()
    {
        if (!interactionInProgress) return;
        interactionInProgress = false;
        nextInteractionTime = Time.unscaledTime + Mathf.Max(0f, interactionCooldown);
        if (tutorial != null && interactionType == InteractionType.Inspect) tutorial.OnInspected(this);
        onInteractionCompleted.Invoke();
        if (this != null && disableAfterInteraction) gameObject.SetActive(false);
    }

    /// <summary>
    /// 대화 등 상호작용이 취소되었을 때 진행 상태와 재시도 대기를 정리한다.
    /// </summary>
    private void CancelInteraction()
    {
        interactionInProgress = false;
        nextInteractionTime = Time.unscaledTime + Mathf.Max(0f, interactionCooldown);
        if (disableAfterInteraction) gameObject.SetActive(false);
    }
}
