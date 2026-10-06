using UnityEngine;

public class NPCDialogue : MonoBehaviour
{
    [SerializeField] private DialogueUI dialogueUI;

    [TextArea(2, 5)]
    [SerializeField] private string dialogue = "열심히 해라";

    public void StartDialogue()
    {
        if (dialogueUI == null)
        {
            Debug.LogWarning($"{name}의 Dialogue UI가 연결되지 않았습니다.");
            return;
        }

        // 대화를 닫는 데 사용한 E 키가 같은 프레임에 다시 대화를 열지 않게 한다.
        if (dialogueUI.IsOpen || dialogueUI.WasClosedThisFrame)
        {
            return;
        }

        dialogueUI.OpenDialogue(dialogue);
    }
}
