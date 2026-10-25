using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// NPC 대화의 거리·선행 조건을 검사하고 대화 완료에 따른 진행 플래그와 이벤트를 전달한다.
/// </summary>
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
    // 행동 시작 시 보관한 진행 버전. 완료 시 현재 단계와 함께 검증한다.
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

    /// <summary>
    /// NPC의 선행 조건과 대화 준비 여부를 검사한 뒤 대화를 연다.
    /// </summary>
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
    /// <summary>
    /// NPC 대화 성공을 확정하고 연결된 진행 플래그와 후속 이벤트를 알린다.
    /// </summary>
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

    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (dialogueInProgress && dialogueUI != null) dialogueUI.CancelDialogue();
        dialogueInProgress = false;
    }
}
