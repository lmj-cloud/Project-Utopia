using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>기존 튜토리얼에 실제 수행 결과만 전달한다. 이동·시점·F 입력 실행은 PlayerController에 그대로 둔다.</summary>
[DefaultExecutionOrder(200)]
public class Page1TutorialBridge : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerStatus status;
    [SerializeField] private PlayerCameraController playerCamera;
    [SerializeField] private PlayerInteraction interaction;
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private InteractableObject familyPhoto;
    [SerializeField] private StoryProgress story;
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private TMPro.TMP_Text objectiveText;
    [Header("성공 판정")]
    [SerializeField, Min(0.05f)] private float movementDistance = 0.3f;
    [SerializeField, Min(1f)] private float lookDegrees = 12f;
    [SerializeField, Range(0.5f, 1f)] private float runningSpeedFraction = 0.85f;
    [SerializeField] private Transform[] actionTargets = new Transform[6];
    private TutorialManager.Step observedStep;
    private Vector3 lastPosition;
    private Quaternion lastCameraRotation;
    private float progress;
    private bool started;
    private bool photoInspected;
    private bool photoReportPending;
    private bool wifeTalked;
    private bool hasArrived;
    private bool livingRoomArrived;
    private bool sonTalked;
    private bool daughterTalked;
    [SerializeField] private Transform livingRoomTarget;
    public void LivingRoomArrived() { if (started && player.CanControlPlayer) livingRoomArrived = true; }
    public void SonDialogueCompleted() { sonTalked = true; }
    public void DaughterDialogueCompleted() { daughterTalked = true; }
    public bool HasPhoto { get { return photoInspected; } }
    public bool HasWifeDialogue { get { return wifeTalked; } }

    public void BeginAfterWake()
    {
        if (started || tutorial == null || !isActiveAndEnabled) return;
        started = true;
        tutorial.BeginTutorial();
        RefreshObjective();
    }

    private void LateUpdate()
    {
        if (!started || player == null || tutorial == null || movement == null || playerCamera == null) return;
        TutorialManager.Step step = tutorial.CurrentStep;
        if (observedStep != step)
        {
            observedStep = step;
            progress = 0f;
            lastPosition = player.transform.position;
            lastCameraRotation = playerCamera.ThirdPersonTarget.rotation;
        }
        if (tutorial.DisplayState == TutorialManager.TutorialDisplayState.WaitingForShow && player.CanControlPlayer)
            tutorial.RequestShowCurrentTutorial();
        Vector3 position = player.transform.position;
        Quaternion rotation = playerCamera.ThirdPersonTarget.rotation;
        if (player.CanControlPlayer && tutorial.DisplayState == TutorialManager.TutorialDisplayState.Active)
            ObserveSuccess(position, rotation);
        // 잠금 중 이동이나 컷씬 회전이 다음 단계의 성공으로 누적되지 않도록 항상 기준값을 갱신한다.
        lastPosition = position;
        lastCameraRotation = rotation;
        // 대화 완료 콜백 때 HUD가 아직 숨겨져 있을 수 있어, 다시 표시된 뒤 같은 성공을 전달한다.
        if (photoReportPending && gameplayUI != null && gameplayUI.CanCountTutorial(tutorial) &&
            tutorial.CurrentStep != null && tutorial.CurrentStep.action == DemoAction.Inspect)
        {
            tutorial.Report(DemoAction.Inspect, familyPhoto);
            if (tutorial.DisplayState == TutorialManager.TutorialDisplayState.Hiding) photoReportPending = false;
        }
        RefreshObjective();
    }

    private void ObserveSuccess(Vector3 position, Quaternion rotation)
    {
        TutorialManager.Step step = tutorial.CurrentStep;
        if (step == null) return;
        if (step.keys == TutorialKeyLayout.WASD)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            Vector3 displacement = position - lastPosition;
            displacement.y = 0;
            Key[] keys = { Key.W, Key.A, Key.S, Key.D };
            Vector2[] directions = { Vector2.up, Vector2.left, Vector2.down, Vector2.right };
            for (int i = 0; i < keys.Length; i++)
                if (keyboard[keys[i]].isPressed && displacement.sqrMagnitude > .000001f &&
                    Vector3.Dot(displacement, playerCamera.GetMoveDirection(directions[i])) > .00001f)
                    tutorial.RecordMovementKey(i);
        }
        else if (step.action == DemoAction.Inspect && step.target == livingRoomTarget && livingRoomArrived)
            tutorial.Report(DemoAction.Inspect, livingRoomTarget);
        else if (step.action == DemoAction.Dialogue && wifeTalked && sonTalked && daughterTalked)
            tutorial.Report(DemoAction.Dialogue, this);
        else if (step.action == DemoAction.DoorOpened && hasArrived)
            tutorial.Report(DemoAction.DoorOpened, this);
    }

    public void TutorialCompleted()
    {
        if (story != null) story.SetFlag("Page1ControlsLearned");
        RefreshObjective();
    }

    public void FamilyPhotoCompleted()
    {
        photoInspected = true;
        photoReportPending = true;
        if (story != null) story.SetFlag("Page1FamilyPhotoInspected");
        RefreshObjective();
    }

    public void WifeDialogueCompleted()
    {
        wifeTalked = true;
        if (story != null) story.SetFlag("Page1WifeDialogueCompleted");
        RefreshObjective();
    }

    public void ArenaArrived()
    {
        hasArrived = true;
        // 문 성공은 S03의 실제 정상 도착으로만 인정한다. HUD 복구 뒤 현재 단계에 전달한다.
        if (dialogueUI != null && dialogueUI.IsOpen) dialogueUI.CancelDialogue();
        RefreshObjective();
    }

    private void RefreshObjective()
    {
        if (objectiveText == null) return;
        if (hasArrived) { objectiveText.text = "경기장 도착 · 링으로 이동하세요."; return; }
        objectiveText.text = tutorial != null && tutorial.CurrentStep != null ? tutorial.CurrentStep.instruction : "집을 둘러보세요.";
        if (interaction != null && interaction.SelectedTarget != null && player.CanControlPlayer)
            objectiveText.text += "\n[F] " + interaction.SelectedTarget.name;
    }
}
