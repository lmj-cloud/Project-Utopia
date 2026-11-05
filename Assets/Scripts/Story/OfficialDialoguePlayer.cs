using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>공식 원문을 기존 DialogueUI로 재생한다. 이동 중 대사도 같은 UI를 사용한다.</summary>
public class OfficialDialoguePlayer : MonoBehaviour
{
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private OfficialStoryScript script;
    public OfficialStoryScript Script { get { return script; } }
    public bool LastCompleted { get; private set; }
    public bool IsPlaying { get; private set; }
    private int pendingCount;
    public bool IsBusy { get { return IsPlaying || pendingCount > 0; } }
    public event Action<OfficialStoryScript.Line> LinePresented;
    private bool ownsDialogue;
    private OfficialStoryScript.Line[] current;
    private UnityEngine.Object activeOwner;
    private int playbackRevision;

    private void OnEnable()
    {
        if (dialogueUI != null) dialogueUI.LinePresented += Presented;
    }
    private void Presented(int index, string speaker, string text)
    {
        if (!ownsDialogue || current == null || index < 0 || index >= current.Length) return;
        LinePresented?.Invoke(current[index]);
    }
    public IEnumerator Play(int section, bool blocksGameplay = true, int first = 0, int count = int.MaxValue, UnityEngine.Object owner = null)
    {
        if (dialogueUI == null || script == null)
        {
            LastCompleted = false;
            Debug.LogError("공식 대본 또는 기존 DialogueUI 참조가 없습니다.", this);
            yield break;
        }
        pendingCount++;
        while (IsPlaying || dialogueUI.IsOpen || dialogueUI.WasClosedThisFrame) yield return null;
        pendingCount--;
        var selected = new List<OfficialStoryScript.Line>();
        int index = 0;
        foreach (OfficialStoryScript.Line line in script.lines)
        {
            if (line.section != section) continue;
            if (index++ < first || selected.Count >= count) continue;
            selected.Add(line);
        }
        LastCompleted = false;
        if (selected.Count == 0) yield break;
        current = selected.ToArray();
        var texts = new string[current.Length];
        var names = new string[current.Length];
        var speakers = new DialogueSpeaker[current.Length];
        for (int i = 0; i < current.Length; i++) { texts[i] = current[i].text; names[i] = current[i].speaker; speakers[i] = current[i].speakerKind; }
        IsPlaying = true;
        ownsDialogue = true;
        activeOwner = owner;
        int revision = ++playbackRevision;
        bool finished = false;
        bool opened = dialogueUI.TryOpenNamedSequence(texts, names, blocksGameplay,
            () => { LastCompleted = true; finished = true; }, () => { finished = true; }, speakers);
        if (!opened) finished = true;
        while (!finished) yield return null;
        if (revision != playbackRevision) yield break;
        ownsDialogue = false;
        IsPlaying = false;
        current = null;
        activeOwner = null;
    }
    /// <summary>Timeline 취소 시 해당 요청이 연 대화만 닫고 다른 발화는 보존한다.</summary>
    public void CancelOwned(UnityEngine.Object owner)
    {
        if (owner == null || activeOwner != owner || !ownsDialogue) return;
        playbackRevision++;
        if (dialogueUI != null) dialogueUI.CancelDialogue();
        LastCompleted = false;
        ownsDialogue = false;
        IsPlaying = false;
        current = null;
        activeOwner = null;
    }
    private void OnDisable()
    {
        if (dialogueUI != null)
        {
            dialogueUI.LinePresented -= Presented;
            if (ownsDialogue) dialogueUI.CancelDialogue();
        }
        StopAllCoroutines();
        IsPlaying = ownsDialogue = false;
        pendingCount = 0;
        current = null;
        activeOwner = null;
        playbackRevision++;
    }
}
