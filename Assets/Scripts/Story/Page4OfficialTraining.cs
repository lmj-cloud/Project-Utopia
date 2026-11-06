using System.Collections;
using UnityEngine;

/// <summary>기존 성공 이벤트와 실제 장비/조준 상태를 기존 TutorialManager에 연결한다.</summary>
public class Page4OfficialTraining : MonoBehaviour
{
    [SerializeField] private Page4StoryController story;
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private OfficialDialoguePlayer dialogue;
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private WeaponAssaultRifle rifle;
    [SerializeField, Min(.1f)] private float aimHoldSeconds = .2f;
    [SerializeField] private TMPro.TMP_Text objective;
    private float aimHeld;
    private bool explainingKnife;
    private bool gunDialogueCompleted;
    private TutorialManager.Step observed;
    private void OnEnable() { if (tutorial != null) tutorial.Changed += Changed; }
    private void Changed()
    {
        if (!tutorial.IsRunning || tutorial.CurrentStep == observed) return;
        observed = tutorial.CurrentStep;
        aimHeld = 0;
        if (tutorial.StepNumber == 7 && !gunDialogueCompleted) StartCoroutine(ExplainKnife());
    }
    private IEnumerator ExplainKnife()
    {
        explainingKnife = true;
        yield return dialogue.Play(14, false, 2, 2);
        gunDialogueCompleted = dialogue.LastCompleted;
        explainingKnife = false;
    }
    private void LateUpdate()
    {
        if (!tutorial.IsRunning || tutorial.IsCompleted || explainingKnife || !player.CanControlPlayer) return;
        bool pickupPhase = story.Stage == Page4Stage.RiflePickupTutorial && tutorial.StepNumber <= 3;
        bool corridorPhase = story.Stage == Page4Stage.CorridorWaiting;
        if (!pickupPhase && !corridorPhase && !story.RangeTrainingEnabled) return;
        if (tutorial.DisplayState == TutorialManager.TutorialDisplayState.WaitingForShow) tutorial.RequestShowCurrentTutorial();
        if (tutorial.DisplayState != TutorialManager.TutorialDisplayState.Active) return;
        TutorialManager.Step step = tutorial.CurrentStep;
        if (story.RangeTrainingEnabled && objective != null)
        {
            if (step.action == DemoAction.Inspect && equipment.CurrentWeapon == null)
                objective.text = "무기고에서 소총 또는 권총을 획득한 뒤 조준하세요.";
            else if (step.action == DemoAction.DaggerHit && !equipment.HasOwnership(PlayerEquipmentKind.Dagger))
                objective.text = "무기고에서 단검을 획득한 뒤 지정 표적을 공격하세요.";
            else objective.text = step.instruction;
        }
        // UI Show/Hide 중 이미 성공한 획득·장착은 실제 현재 상태로 확인한다. 아이템을 생성하지 않는다.
        if (step.action == DemoAction.Pickup)
        {
            foreach (PlayerEquipmentKind kind in new[] { PlayerEquipmentKind.AssaultRifle, PlayerEquipmentKind.Pistol, PlayerEquipmentKind.Dagger })
            {
                ItemData item = equipment.GetRequiredItem(kind);
                if (item != null && equipment.HasOwnership(kind)) { tutorial.Report(DemoAction.Pickup, item); break; }
            }
        }
        if (step.action == DemoAction.Equip && equipment.CurrentKind != PlayerEquipmentKind.BareHands &&
            equipment.CurrentKind != PlayerEquipmentKind.Gloves && equipment.HasOwnership(equipment.CurrentKind))
            tutorial.Report(DemoAction.Equip, equipment);
        if (step.action == DemoAction.Inspect && story.RangeTrainingEnabled)
        {
            WeaponAssaultRifle weapon = equipment.CurrentWeapon;
            bool aiming = weapon != null && !weapon.IsReloading && (player.WantsRifleAimCorrection || player.WantsPistolAimCorrection);
            aimHeld = aiming ? aimHeld + Time.deltaTime : 0;
            if (aimHeld >= aimHoldSeconds) tutorial.Report(DemoAction.Inspect, weapon);

        }
        // ShotHit/Reload/DaggerHit는 TutorialManager의 기존 무기·근접 성공 이벤트만 사용한다.
    }
    private void OnDisable() { if (tutorial != null) tutorial.Changed -= Changed; }
}
