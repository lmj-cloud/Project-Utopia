using System.Collections;
using UnityEngine;

/// <summary>원본 S03을 수정하지 않고 공식 출발 대사와 도착 대사를 문 이벤트 앞뒤에 연결한다.</summary>
public class Page1OfficialStory : MonoBehaviour
{
    [SerializeField] private OfficialDialoguePlayer dialogue;
    [SerializeField] private S03PlayerIntegration arrival;
    [SerializeField] private InteractableObject door;
    private bool departing;
    public void UseDoor()
    {
        if (departing || arrival.IsRunning || arrival.HasCompleted || !door.IsExternalInteractionPending) return;
        departing = true;
        StartCoroutine(Depart());
    }
    private IEnumerator Depart()
    {
        yield return dialogue.Play(3, true, 0, 2);
        if (!dialogue.LastCompleted) { departing = false; door.CancelExternalInteraction(); yield break; }
        // DialogueUI가 닫힌 프레임의 입력 보호가 끝난 뒤 기존 S03 진입 API를 호출한다.
        yield return null;
        arrival.BeginFromDoor();
        if (!arrival.IsRunning) { departing = false; door.CancelExternalInteraction(); }
    }
    public void Arrived()
    {
        if (arrival.HasCompleted) StartCoroutine(dialogue.Play(3, true, 2, 1));
    }
}
