using System.Collections;
using UnityEngine;

/// <summary>원본 S03을 수정하지 않고 공식 출발 대사와 도착 대사를 문 이벤트 앞뒤에 연결한다.</summary>
public class Page1OfficialStory : MonoBehaviour
{
    [SerializeField] private OfficialDialoguePlayer dialogue;
    [SerializeField] private S03PlayerIntegration arrival;
    [SerializeField] private InteractableObject door;
    [SerializeField] private Page1TutorialBridge tutorialBridge;
    private bool departing;

    private void Awake()
    {
        // PlayerInteraction의 첫 대상 탐색 전부터 현관 상호작용을 잠근다.
        if (door != null) door.enabled = false;
    }

    private void LateUpdate()
    {
        // 진행 중인 외부 상호작용 토큰은 S03/씬 전환 완료까지 보존한다.
        if (door == null || departing || arrival.IsRunning) return;
        door.enabled = !arrival.HasCompleted && tutorialBridge != null && tutorialBridge.HasCompletedFamilyTutorial;
    }
    public void UseDoor()
    {
        if (tutorialBridge == null || !tutorialBridge.HasCompletedFamilyTutorial)
        {
            if (door != null) door.CancelExternalInteraction();
            return;
        }
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
