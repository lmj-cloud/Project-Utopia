using UnityEngine;
using UnityEngine.Events;

public enum InteractionType
{
    Inspect,
    Pickup
}

public class InteractableObject : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private string interactionText = "조사하기";

    [Header("Interaction Type")]
    [SerializeField] private InteractionType interactionType = InteractionType.Inspect;
    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int itemQuantity = 1;

    [Header("Settings")]
    [SerializeField] private bool interactOnlyOnce;
    [SerializeField] private bool disableAfterInteraction;
    [Tooltip("대사가 끝난 뒤 다시 조사할 수 있을 때까지의 시간. 0이면 즉시 재조사 가능.")]
    [SerializeField, Min(0f)] private float interactionCooldown = 2f;

    [Header("Dialogue")]
    [SerializeField, TextArea(2, 6)] private string[] dialogueLines;

    [Header("Optional Conditions")]
    [SerializeField] private ItemData requiredItem;
    [SerializeField] private string requiredStoryFlag;

    [Header("Response")]
    [Tooltip("상호작용이 시작된 순간 실행. 기존 이벤트 연결을 유지한다.")]
    [SerializeField] private UnityEvent onInteracted = new();
    [Tooltip("이 오브젝트의 대사가 끝나면 실행. 대사가 없으면 바로 실행.")]
    [SerializeField] private UnityEvent onInteractionCompleted = new();
    [SerializeField] private UnityEvent onConditionFailed = new();
    [SerializeField] private UnityEvent onInventoryFull = new();

    private bool hasInteracted;
    private bool interactionInProgress;
    private float nextInteractionTime;

    public string InteractionText => interactionText;
    public bool CanInteract => isActiveAndEnabled && !interactionInProgress &&
        !(hasInteracted && (interactOnlyOnce || interactionType == InteractionType.Pickup)) &&
        Time.unscaledTime >= nextInteractionTime;

    // 기존 Interact() UnityEvent 연결을 위한 호환 메서드.
    // 인벤토리/대사가 필요한 상호작용은 PlayerInteraction을 통해 실행한다.
    public void Interact() => TryInteract(null, null, null);

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

        if (!dialogueOpened) CompleteInteraction();

        // 아이템은 저장에 성공한 경우에만 숨긴다. 대화 콜백은 UI에서 계속 관리한다.
        if (interactionType == InteractionType.Pickup && this != null)
            gameObject.SetActive(false);

        return true;
    }

    private bool HasDialogue()
    {
        if (dialogueLines == null) return false;
        foreach (string line in dialogueLines)
            if (!string.IsNullOrWhiteSpace(line)) return true;
        return false;
    }

    private void CompleteInteraction()
    {
        if (!interactionInProgress) return;
        interactionInProgress = false;
        nextInteractionTime = Time.unscaledTime + Mathf.Max(0f, interactionCooldown);
        onInteractionCompleted.Invoke();
        if (this != null && disableAfterInteraction) gameObject.SetActive(false);
    }

    private void CancelInteraction()
    {
        interactionInProgress = false;
        nextInteractionTime = Time.unscaledTime + Mathf.Max(0f, interactionCooldown);
        if (disableAfterInteraction) gameObject.SetActive(false);
    }
}
