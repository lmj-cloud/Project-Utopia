using UnityEngine;

/// <summary>기존 TutorialManager의 실제 전투 성공 단계와 공식 코치 발화를 연결한다.</summary>
public class Page2OfficialTutorial : MonoBehaviour
{
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private OfficialDialoguePlayer dialogue;
    [SerializeField] private PlayerController player;
    private TutorialManager.Step observed;
    private void OnEnable() { if (tutorial != null) tutorial.Changed += Changed; }
    private void Changed()
    {
        if (!tutorial.IsRunning || tutorial.CurrentStep == observed) return;
        observed = tutorial.CurrentStep;
        int index = tutorial.StepNumber - 1;
        if (index >= 0 && index < 3) StartCoroutine(dialogue.Play(5, false, index, 1));
    }
    private void LateUpdate()
    {
        if (tutorial.IsRunning && player.CanControlPlayer && tutorial.DisplayState == TutorialManager.TutorialDisplayState.WaitingForShow)
            tutorial.RequestShowCurrentTutorial();
    }
    private void OnDisable() { if (tutorial != null) tutorial.Changed -= Changed; }
}
