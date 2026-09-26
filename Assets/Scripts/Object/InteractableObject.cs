using UnityEngine;
using UnityEngine.Events;

public class InteractableObject : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private string interactionText = "조사하기";

    [Header("Settings")]
    [SerializeField] private bool interactOnlyOnce;
    [SerializeField] private bool disableAfterInteraction;

    [Header("Response")]
    [SerializeField] private UnityEvent onInteracted;

    private bool hasInteracted;

    public string InteractionText => interactionText;
    public bool CanInteract => !interactOnlyOnce || !hasInteracted;

    public void Interact()
    {
        if (!CanInteract)
        {
            return;
        }

        hasInteracted = true;
        onInteracted.Invoke();

        if (disableAfterInteraction)
        {
            gameObject.SetActive(false);
        }
    }
}
