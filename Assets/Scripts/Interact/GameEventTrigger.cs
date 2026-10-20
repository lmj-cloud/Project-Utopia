using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameEventTrigger : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private GameFlowManager flow;
    [SerializeField] private StoryProgress storyProgress;
    [SerializeField] private GameFlowStage requiredStage;
    [SerializeField] private string requiredFlag;
    [SerializeField] private bool onlyOnce = true;
    [SerializeField] private UnityEvent onRequested = new();
    private readonly HashSet<Collider> inside = new();
    private bool pending;
    private bool completed;
    private int revision;
    public void ConfirmCompleted()
    {
        if (!pending || flow == null || flow.Revision != revision || flow.CurrentStage != requiredStage) return;
        pending = false;
        completed = true;
    }
    public void RequestFailedOrCancelled() => pending = false;
    private void OnTriggerEnter(Collider other)
    {
        if (player == null || other.GetComponentInParent<PlayerController>() != player || !inside.Add(other)) return;
        if (inside.Count == 1) Request();
    }
    private void OnTriggerExit(Collider other) => inside.Remove(other);
    public void RetryWhileInside() { if (inside.Count > 0) Request(); }
    private void Request()
    {
        if (!isActiveAndEnabled || onRequested.GetPersistentEventCount() == 0 || pending || (onlyOnce && completed) || flow == null || !flow.HasBegun || flow.CurrentStage != requiredStage ||
            (!string.IsNullOrWhiteSpace(requiredFlag) && (storyProgress == null || !storyProgress.HasFlag(requiredFlag)))) return;
        pending = true;
        revision = flow.Revision;
        onRequested.Invoke();
    }
    public void ResetTrigger() { pending = false; completed = false; inside.Clear(); }
    private void OnDisable() { pending = false; inside.Clear(); }
}
