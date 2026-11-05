using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// NPC 대화의 거리·선행 조건을 검사하고 대화 완료에 따른 진행 플래그와 이벤트를 전달한다.
/// </summary>
public class NPCDialogue : MonoBehaviour
{
    [SerializeField] private DialogueUI dialogueUI;
    [Header("Official Script (optional)")]
    [SerializeField] private OfficialStoryScript officialScript;
    [SerializeField] private int officialSection;
    [SerializeField, Min(0)] private int officialFirst;
    [SerializeField, Min(1)] private int officialCount = int.MaxValue;

    [Header("First Dialogue Character Unlock")]
    [SerializeField] private AffinityCharacterData affinityCharacter;
    [SerializeField] private AffinityDataStore affinityDataStore;
    [SerializeField, Min(0)] private int firstDialogueAffinityBonus = 5;

    [Header("Dialogue Sequence")]
    [SerializeField, TextArea(2, 5)] private string[] dialogueLines;
    [SerializeField] private string npcName = "";
    [SerializeField] private DialogueSpeaker[] lineSpeakers = new DialogueSpeaker[0];
    [Header("Completion")]
    [SerializeField] private UnityEvent onDialogueCompleted = new();
    private bool dialogueInProgress;
    [Header("Dialogue Animation")]
    [SerializeField] private Animator dialogueAnimator;
    [SerializeField] private NPCLookAtPlayer dialogueLookAt;
    [SerializeField] private string idleState = "Base Layer.NPC_Idle";
    [SerializeField] private string talkingState = "Base Layer.NPC_Talk";
    private bool isTalking;
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
            if (officialScript != null)
                foreach (OfficialStoryScript.Line line in officialScript.lines)
                    if (line.section == officialSection) return true;
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
        // 첫 문장 이벤트가 Open 안에서 발생하므로 소유 상태와 구독을 먼저 준비한다.
        dialogueInProgress = true;
        dialogueUI.SpeakerChanged += OnSpeakerChanged;
        bool opened;
        if (officialScript != null)
        {
            var texts = new System.Collections.Generic.List<string>();
            var names = new System.Collections.Generic.List<string>();
            var speakers = new System.Collections.Generic.List<DialogueSpeaker>();
            int index = 0;
            foreach (OfficialStoryScript.Line line in officialScript.lines)
            {
                if (line.section != officialSection) continue;
                if (index++ < officialFirst || texts.Count >= officialCount) continue;
                texts.Add(line.text); names.Add(line.speaker); speakers.Add(line.speakerKind);
            }
            opened = dialogueUI.TryOpenNamedSequence(texts.ToArray(), names.ToArray(), true,
                () => { if (this != null) CompleteDialogue(); }, () => { if (this != null) CancelDialogueAnimation(); }, speakers.ToArray());
        }
        else
        {
            opened = dialogueUI.TryOpenSequence(dialogueLines, lineSpeakers, npcName,
                () => { if (this != null) CompleteDialogue(); }, () => { if (this != null) CancelDialogueAnimation(); });
        }
        if (!opened)
        {
            CancelDialogueAnimation();
        }
        else if (dialogueInProgress)
        {
            // 첫 문장 이벤트에서 즉시 취소될 수도 있으므로, 성공과 현재 소유 상태를 함께 검사한다.
            StartDialogueLookAt();
            UnlockDialogueCharacter();
        }
    }
    /// <summary>실제 대화 시작 성공 뒤 최초 해금에 성공한 인물에게만 호감도를 한 번 지급한다.</summary>
    private void UnlockDialogueCharacter()
    {
        // 미등록 NPC도 기존 대화는 그대로 진행한다.
        if (affinityCharacter == null || affinityDataStore == null) return;
        if (!affinityDataStore.UnlockCharacter(affinityCharacter.CharacterId)) return;
        affinityDataStore.AddAffinity(affinityCharacter.CharacterId, Mathf.Max(0, firstDialogueAffinityBonus));
    }

    /// <summary>
    /// NPC 대화 성공을 확정하고 연결된 진행 플래그와 후속 이벤트를 알린다.
    /// </summary>
    private void CompleteDialogue()
    {
        if (!dialogueInProgress) return;
        CancelDialogueAnimation();
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
    /// 현재 문장의 발언자가 바뀌면 NPC의 말하기 여부를 갱신한다.
    /// </summary>
    private void OnSpeakerChanged(DialogueSpeaker speaker)
    {
        if (!dialogueInProgress) return;
        SetTalking(speaker == DialogueSpeaker.NPC);
    }

    /// <summary>
    /// 대화 종료·취소 때 구독을 해제하고 이 NPC만 대기 자세로 복귀시킨다.
    /// </summary>
    private void CancelDialogueAnimation()
    {
        StopDialogueLookAt();
        dialogueInProgress = false;
        if (dialogueUI != null) dialogueUI.SpeakerChanged -= OnSpeakerChanged;
        SetTalking(false);
    }

    /// <summary>대화 시작에 성공한 NPC의 시선 추적만 허용한다.</summary>
    private void StartDialogueLookAt()
    {
        if (dialogueLookAt != null)
        {
            dialogueLookAt.BeginDialogueLookAt();
        }
    }

    /// <summary>완료·취소·비활성화가 공유하는 정리 경로에서 추적을 해제한다.</summary>
    private void StopDialogueLookAt()
    {
        if (dialogueLookAt != null)
        {
            dialogueLookAt.EndDialogueLookAt();
        }
    }

    /// <summary>
    /// 준비된 Animator 상태만 재생하여 모델 없는 NPC의 기존 대화도 유지한다.
    /// </summary>
    private void SetTalking(bool value)
    {
        if (dialogueAnimator == null || !dialogueAnimator.isActiveAndEnabled || dialogueAnimator.runtimeAnimatorController == null) return;
        string state = value ? talkingState : idleState;
        int hash = Animator.StringToHash(state);
        if (!dialogueAnimator.HasState(0, hash)) return;
        if (isTalking == value && dialogueAnimator.GetCurrentAnimatorStateInfo(0).fullPathHash == hash) return;
        isTalking = value;
        dialogueAnimator.CrossFadeInFixedTime(hash, 0.12f, 0);
    }

    /// <summary>
    /// 비활성화 시 자신의 대화와 애니메이션 구독을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        if (dialogueInProgress && dialogueUI != null) dialogueUI.CancelDialogue();
        CancelDialogueAnimation();
    }
}
