using UnityEngine;

/// <summary>기존 TutorialManager의 실제 전투 성공 단계와 공식 코치 발화를 연결한다.</summary>
public class Page2OfficialTutorial : MonoBehaviour
{
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private OfficialDialoguePlayer dialogue;
    [SerializeField] private PlayerController player;
    private TutorialManager.Step observed;
    [SerializeField] private PlayerCombatActions actions;
    private int lastRequest = -1;
    private float stanceHeld;
    private void OnEnable() { if (tutorial != null) tutorial.Changed += Changed; }
    private void Changed()
    {
        if (!tutorial.IsRunning || tutorial.CurrentStep == observed) return;
        observed = tutorial.CurrentStep;
        int index = tutorial.StepNumber - 1;
        if (index == 0) StartCoroutine(dialogue.Play(5, false, 0, 1));
        else if (index == 1) StartCoroutine(dialogue.Play(5, false, 1, 2));
        stanceHeld = 0;
    }
    private void LateUpdate()
    {
        if (tutorial == null || player == null) return;
        if (tutorial.IsRunning && player.CanControlPlayer && tutorial.DisplayState == TutorialManager.TutorialDisplayState.WaitingForShow)
            tutorial.RequestShowCurrentTutorial();
        bool newAttack = actions != null && actions.IsRunning && actions.RequestId != lastRequest;
        if (actions != null) lastRequest = actions.RequestId;
        if (!player.CanUseMma || tutorial.DisplayState != TutorialManager.TutorialDisplayState.Active) return;
        if (newAttack) tutorial.Report(DemoAction.MeleeAttackStarted, actions);
        bool facing = player.IsMmaCombatStance && Vector3.Angle(player.transform.forward, player.MmaCameraForward) < 12f;
        stanceHeld = facing ? stanceHeld + Time.deltaTime : 0;
        if (stanceHeld >= .2f) tutorial.Report(DemoAction.CombatStance, player);
    }
    private void OnDisable() { if (tutorial != null) tutorial.Changed -= Changed; }
}
