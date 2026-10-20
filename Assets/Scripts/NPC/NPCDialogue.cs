using UnityEngine;
using UnityEngine.Events;

public class NPCDialogue : MonoBehaviour
{
    [SerializeField] private DialogueUI dialogueUI;

    [Header("Dialogue Sequence")]
    [SerializeField, TextArea(2, 5)] private string[] dialogueLines;
    [SerializeField] private string npcName = "";
    [SerializeField] private DialogueSpeaker[] lineSpeakers = new DialogueSpeaker[0];
    [Header("Completion")]
    [SerializeField] private UnityEvent onDialogueCompleted = new();
    private bool dialogueInProgress;
    [SerializeField] private GameFlowManager flow;
    [SerializeField] private TutorialManager tutorial;
    private GameFlowStage requestedStage;
    private int flowRevision;

    public bool CanStartDialogue
    {
        get
        {
            if (!isActiveAndEnabled || dialogueInProgress || dialogueUI == null ||
                !dialogueUI.IsReady || dialogueUI.IsOpen || dialogueUI.WasClosedThisFrame)
                return false;
            if (dialogueLines != null)
                foreach (string line in dialogueLines)
                    if (!string.IsNullOrWhiteSpace(line)) return true;
            return false;
        }
    }

    public void StartDialogue()
    {
        if (!isActiveAndEnabled || dialogueInProgress) return;
        if (dialogueUI == null)
        {
            Debug.LogWarning($"{name}의 Dialogue UI가 연결되지 않았습니다.");
            return;
        }

        // 대화를 닫은 프레임에는 F 상호작용으로 다시 열지 않는다.
        if (dialogueUI.IsOpen || dialogueUI.WasClosedThisFrame)
        {
            return;
        }

        if (!CanStartDialogue) return;
        if (flow != null) { requestedStage = flow.CurrentStage; flowRevision = flow.Revision; }
        dialogueInProgress = dialogueUI.TryOpenSequence(
            dialogueLines, lineSpeakers, npcName,
            () => { if (this != null) CompleteDialogue(); },
            () => { if (this != null) dialogueInProgress = false; });
    }
    private void CompleteDialogue()
    {
        if (!dialogueInProgress) return;
        dialogueInProgress = false;
        GameFlowStage stage = requestedStage;
        int revision = flowRevision;
        if (isActiveAndEnabled)
        {
            if (tutorial != null) tutorial.OnDialogueCompleted(this);
            onDialogueCompleted.Invoke();
            if (flow != null) flow.TryCompleteStage(stage, revision, this);
        }
    }

    private void OnDisable()
    {
        if (dialogueInProgress && dialogueUI != null) dialogueUI.CancelDialogue();
        dialogueInProgress = false;
    }
}
