using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum DialogueSpeaker { NPC = 0, Player = 1 }

public class DialogueUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Image enterFillImage;
    [SerializeField] private TMP_Text speakerNameText;
    [Header("Speaker")]
    [SerializeField] private string playerName = "플레이어";

    [Header("Typewriter Settings")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 30f;

    [Header("Dialogue Advance Timing")]
    [SerializeField, Min(0f)] private float autoAdvanceDelay = 2f;
    [SerializeField, Min(0.01f)] private float holdThreshold = 1f;
    [SerializeField, Min(0.01f)] private float holdAdvanceInterval = 0.2f;

    private float lineDisplayedAt = -1f;
    private float holdStartedAt = -1f;
    private float nextHoldAdvanceAt;
    private bool isFastAdvancing;
    private int inputFrame = -1;
    private int processedFrame = -1;
    private bool enterPressed;
    private bool enterHeld;
    private bool enterReleased;
    private bool advanceRequested;

    private Coroutine typingCoroutine;
    private string[] lines;
    private string[] speakerNames;
    private int lineIndex;
    private int openedFrame = -1;
    private int closedFrame = -1;
    private bool isTyping;
    private bool isOpen;
    private Action completedCallback;
    private Action cancelledCallback;

    public bool IsOpen => isOpen;
    public bool WasClosedThisFrame => closedFrame == Time.frameCount;
    public bool IsReady => isActiveAndEnabled && dialoguePanel != null && dialogueText != null;

    private void Awake()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        if (dialogueText != null) dialogueText.text = string.Empty;
        if (speakerNameText != null) speakerNameText.text = string.Empty;
        ResetAdvanceState();
    }

    // 기존 NPCDialogue나 Inspector의 UnityEvent에서도 호출할 수 있는 메서드.
    public void OpenDialogue(string text)
    {
        TryOpenSequence(new[] { text }, null, null);
    }

    public bool TryOpenSequence(string[] dialogueLines, Action onCompleted, Action onCancelled)
        => TryOpenSequence(dialogueLines, null, null, DialogueSpeaker.Player, onCompleted, onCancelled);

    public bool TryOpenSequence(string[] dialogueLines, DialogueSpeaker[] speakers, string npcName, Action onCompleted, Action onCancelled)
        => TryOpenSequence(dialogueLines, speakers, npcName, DialogueSpeaker.NPC, onCompleted, onCancelled);

    private bool TryOpenSequence(string[] dialogueLines, DialogueSpeaker[] speakers, string npcName,
        DialogueSpeaker defaultSpeaker, Action onCompleted, Action onCancelled)
    {
        if (!IsReady || IsOpen || dialogueLines == null) return false;
        List<string> validLines = new();
        List<string> validNames = new();
        // 원본 인덱스로 화자를 찾은 뒤 문장과 이름을 함께 거른다.
        for (int i = 0; i < dialogueLines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(dialogueLines[i])) continue;
            DialogueSpeaker speaker = speakers != null && i < speakers.Length ? speakers[i] : defaultSpeaker;
            string displayName = speaker == DialogueSpeaker.Player ? playerName : npcName;
            validLines.Add(dialogueLines[i]);
            validNames.Add(string.IsNullOrWhiteSpace(displayName)
                ? speaker == DialogueSpeaker.Player ? "플레이어" : "NPC" : displayName.Trim());
        }
        if (validLines.Count == 0) return false;

        ResetAdvanceState();
        lines = validLines.ToArray();
        speakerNames = validNames.ToArray();
        lineIndex = 0;
        completedCallback = onCompleted;
        cancelledCallback = onCancelled;
        isOpen = true;
        openedFrame = Time.frameCount;
        dialoguePanel.SetActive(true);
        ShowCurrentLine();
        return true;
    }

    // PlayerController가 전달한 Enter 상태를 저장하고 LateUpdate에서 프레임당 한 번 처리한다.
    public void SetAdvanceInput(bool pressed, bool held, bool released)
    {
        if (!IsOpen || Time.frameCount <= openedFrame || WasClosedThisFrame) return;
        inputFrame = Time.frameCount;
        enterPressed = pressed;
        enterHeld = held;
        enterReleased = released;
    }

    // Preserve existing UnityEvent callers without creating another progression path.
    public void AdvanceDialogue()
    {
        if (IsOpen && Time.frameCount > openedFrame && !WasClosedThisFrame)
            advanceRequested = true;
    }

    private void LateUpdate()
    {
        if (!IsOpen || Time.frameCount <= openedFrame || WasClosedThisFrame || processedFrame == Time.frameCount) return;
        processedFrame = Time.frameCount;
        bool hasInput = inputFrame == Time.frameCount;
        bool pressed = hasInput && enterPressed;
        bool held = hasInput && enterHeld;
        bool released = hasInput && enterReleased;
        bool requested = advanceRequested;
        advanceRequested = false;
        float now = Time.unscaledTime;

        // Missing input also releases the hold, so a disabled input source cannot leave it stuck.
        if (released || !held)
        {
            bool wasHolding = holdStartedAt >= 0f;
            ResetHoldState();
            if (wasHolding && !isTyping) lineDisplayedAt = now;
        }

        // 첫 누름은 타이핑 완성 또는 다음 문장 중 하나만 수행한다.
        if (pressed || (requested && !held))
        {
            if (pressed && held)
            {
                holdStartedAt = now;
                isFastAdvancing = false;
                nextHoldAdvanceAt = 0f;
            }
            UpdateEnterFill(0f);
            AdvanceCurrentLine(false);
            return;
        }

        // Enter 유지 시간으로 채우기를 표시하며, 가득 찬 뒤 반복 간격마다 빠르게 진행한다.
        float holdProgress = held && holdStartedAt >= 0f
            ? Mathf.Clamp01((now - holdStartedAt) / Mathf.Max(0.01f, holdThreshold)) : 0f;
        UpdateEnterFill(holdProgress);
        if (held)
        {
            // A carried-over key needs a fresh press. Auto advance stays paused while held.
            if (holdStartedAt < 0f || holdProgress < 1f) return;
            if (!isFastAdvancing)
            {
                isFastAdvancing = true;
                nextHoldAdvanceAt = now + Mathf.Max(0.01f, holdAdvanceInterval);
                // Render the full indicator before the first additional line can advance.
                return;
            }
            if (now >= nextHoldAdvanceAt)
            {
                nextHoldAdvanceAt = now + Mathf.Max(0.01f, holdAdvanceInterval);
                AdvanceCurrentLine(true);
            }
            return;
        }

        // 입력이 없으면 타이핑 완료 시점부터 자동 진행 대기 시간을 계산한다.
        if (!isTyping && lineDisplayedAt >= 0f && now - lineDisplayedAt >= Mathf.Max(0f, autoAdvanceDelay))
            AdvanceCurrentLine(false);
    }

    private void AdvanceCurrentLine(bool fastAdvance)
    {
        if (isTyping)
        {
            CompleteCurrentLine();
            if (!fastAdvance) return;
        }
        lineIndex++;
        if (lineIndex >= lines.Length) CloseDialogue();
        else ShowCurrentLine();
    }

    private void CompleteCurrentLine()
    {
        StopTyping();
        dialogueText.maxVisibleCharacters = int.MaxValue;
        lineDisplayedAt = Time.unscaledTime;
    }

    private void UpdateEnterFill(float progress)
    {
        if (enterFillImage != null) enterFillImage.fillAmount = progress;
    }

    private void ResetHoldState()
    {
        holdStartedAt = -1f;
        nextHoldAdvanceAt = 0f;
        isFastAdvancing = false;
        if (enterFillImage != null) enterFillImage.fillAmount = 0f;
    }

    private void ResetAdvanceState()
    {
        ResetHoldState();
        lineDisplayedAt = -1f;
        inputFrame = -1;
        enterPressed = false;
        enterHeld = false;
        enterReleased = false;
        advanceRequested = false;
    }

    // 마지막 문장을 넘긴 경우만 정상 완료이고 중간 닫기는 취소다.
    public void CloseDialogue() => FinishDialogue(lines != null && lineIndex >= lines.Length);
    public void CancelDialogue() => FinishDialogue(false);

    private void ShowCurrentLine()
    {
        StopTyping();
        lineDisplayedAt = -1f;
        // 문장이 바뀔 때 같은 인덱스의 화자 이름도 바로 갱신한다.
        if (speakerNameText != null) speakerNameText.text = speakerNames[lineIndex];
        dialogueText.text = lines[lineIndex];
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();
        int characterCount = dialogueText.textInfo.characterCount;
        if (characterCount == 0)
        {
            CompleteCurrentLine();
            return;
        }
        isTyping = true;
        typingCoroutine = StartCoroutine(TypeText(characterCount));
    }

    private IEnumerator TypeText(int characterCount)
    {
        float visibleCharacters = 0f;
        while (visibleCharacters < characterCount)
        {
            visibleCharacters += Mathf.Max(1f, charactersPerSecond) * Time.unscaledDeltaTime;
            dialogueText.maxVisibleCharacters = Mathf.Min(characterCount, (int)visibleCharacters);
            if (visibleCharacters >= characterCount) break;
            yield return null;
        }
        dialogueText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
        typingCoroutine = null;
        lineDisplayedAt = Time.unscaledTime;
    }

    private void StopTyping()
    {
        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        typingCoroutine = null;
        isTyping = false;
    }

    private void FinishDialogue(bool completed)
    {
        if (!IsOpen) return;
        // 입력·타이머·표시를 먼저 정리해 완료 콜백에서 연 새 대화를 지우지 않는다.
        Action callback = completed ? completedCallback : cancelledCallback;
        completedCallback = null;
        cancelledCallback = null;
        StopTyping();
        ResetAdvanceState();
        isOpen = false;
        closedFrame = Time.frameCount;
        lines = null;
        speakerNames = null;
        if (speakerNameText != null) speakerNameText.text = string.Empty;
        if (dialogueText != null) dialogueText.text = string.Empty;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        // 먼저 UI를 정리한 뒤, 이 대화에만 연결된 완료 동작을 실행한다.
        callback?.Invoke();
    }

    private void OnDisable()
    {
        CancelDialogue();
        ResetAdvanceState();
    }
}
