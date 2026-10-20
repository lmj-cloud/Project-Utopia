using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DialogueUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Typewriter Settings")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 30f;

    private Coroutine typingCoroutine;
    private string[] lines;
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
    }

    // 기존 NPCDialogue나 Inspector의 UnityEvent에서도 호출할 수 있는 메서드.
    public void OpenDialogue(string text)
    {
        TryOpenSequence(new[] { text }, null, null);
    }

    public bool TryOpenSequence(string[] dialogueLines, Action onCompleted, Action onCancelled)
    {
        if (!IsReady || IsOpen || dialogueLines == null) return false;

        List<string> validLines = new();
        foreach (string line in dialogueLines)
            if (!string.IsNullOrWhiteSpace(line)) validLines.Add(line);
        if (validLines.Count == 0) return false;

        lines = validLines.ToArray();
        lineIndex = 0;
        completedCallback = onCompleted;
        cancelledCallback = onCancelled;
        isOpen = true;
        openedFrame = Time.frameCount;
        dialoguePanel.SetActive(true);
        ShowCurrentLine();
        return true;
    }

    // 키 입력은 PlayerController에서 받아 이 메서드를 호출한다.
    public void AdvanceDialogue()
    {
        if (!IsOpen || Time.frameCount <= openedFrame) return;
        if (isTyping)
        {
            StopTyping();
            dialogueText.maxVisibleCharacters = int.MaxValue;
            return;
        }

        lineIndex++;
        if (lineIndex >= lines.Length) CloseDialogue();
        else ShowCurrentLine();
    }

    public void CloseDialogue() => FinishDialogue(true);
    public void CancelDialogue() => FinishDialogue(false);

    private void ShowCurrentLine()
    {
        StopTyping();
        dialogueText.text = lines[lineIndex];
        dialogueText.maxVisibleCharacters = 0;
        dialogueText.ForceMeshUpdate();
        isTyping = true;
        typingCoroutine = StartCoroutine(TypeText(dialogueText.textInfo.characterCount));
    }

    private IEnumerator TypeText(int characterCount)
    {
        float visibleCharacters = 0f;
        while (visibleCharacters < characterCount)
        {
            visibleCharacters += Mathf.Max(1f, charactersPerSecond) * Time.unscaledDeltaTime;
            dialogueText.maxVisibleCharacters = Mathf.Min(characterCount, (int)visibleCharacters);
            yield return null;
        }
        dialogueText.maxVisibleCharacters = int.MaxValue;
        isTyping = false;
        typingCoroutine = null;
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
        Action callback = completed ? completedCallback : cancelledCallback;
        completedCallback = null;
        cancelledCallback = null;
        StopTyping();
        isOpen = false;
        closedFrame = Time.frameCount;
        lines = null;
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
        // 먼저 UI를 정리한 뒤, 이 대화에만 연결된 완료 동작을 실행한다.
        callback?.Invoke();
    }

    private void OnDisable() => CancelDialogue();
}
