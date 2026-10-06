using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class DialogueUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private TMP_Text dialogueText;

    [Header("Typewriter Settings")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 30f;

    private Coroutine typingCoroutine;
    private int openedFrame = -1;
    private int closedFrame = -1;

    public bool IsOpen => dialoguePanel != null && dialoguePanel.activeSelf;
    public bool WasClosedThisFrame => closedFrame == Time.frameCount;

    private void Awake()
    {
        dialoguePanel.SetActive(false);
        dialogueText.text = string.Empty;
    }

    private void Update()
    {
        if (!IsOpen || Time.frameCount <= openedFrame)
        {
            return;
        }

        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            CloseDialogue();
        }
    }

    public void OpenDialogue(string text)
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
        }

        dialoguePanel.SetActive(true);
        openedFrame = Time.frameCount;
        typingCoroutine = StartCoroutine(TypeText(text));
    }

    public void CloseDialogue()
    {
        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        dialoguePanel.SetActive(false);
        closedFrame = Time.frameCount;
    }

    private IEnumerator TypeText(string text)
    {
        dialogueText.text = string.Empty;
        float delay = 1f / charactersPerSecond;

        foreach (char character in text)
        {
            dialogueText.text += character;
            yield return new WaitForSeconds(delay);
        }

        typingCoroutine = null;
    }
}
