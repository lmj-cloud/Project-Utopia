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
        int step = tutorial.StepNumber - 1;
        if (step < 0 || step >= 6 || actionTargets == null || actionTargets.Length <= step || actionTargets[step] == null) return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        Vector3 displacement = position - lastPosition;
        displacement.y = 0f;
        if (step < 4)
        {
            Key[] keys = { Key.W, Key.A, Key.S, Key.D };
            Vector2[] directions = { Vector2.up, Vector2.left, Vector2.down, Vector2.right };
            bool correctKey = keyboard[keys[step]].isPressed;
            for (int i = 0; i < 4; i++)
                if (i != step && keyboard[keys[i]].isPressed) correctKey = false;
            Vector3 expected = playerCamera.GetMoveDirection(directions[step]);
            if (correctKey && Vector3.Dot(displacement, expected) > 0f) progress += displacement.magnitude;
            if (progress >= movementDistance) tutorial.Report(DemoAction.Move, actionTargets[step]);
        }
        else if (step == 4)
        {
            if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0f && playerCamera.LookEnabled)
                progress += Quaternion.Angle(lastCameraRotation, rotation);
            if (progress >= lookDegrees) tutorial.Report(DemoAction.Move, actionTargets[step]);
        }
        else if (keyboard.leftShiftKey.isPressed && status != null && status.RunSpeed > status.WalkSpeed &&
                 !movement.IsCrouching && movement.TargetSpeed > status.WalkSpeed &&
                 movement.CurrentSpeed >= status.RunSpeed * runningSpeedFraction && displacement.magnitude > 0.001f)
        {
            progress += displacement.magnitude;
            if (progress >= movementDistance) tutorial.Report(DemoAction.Move, actionTargets[step]);
        }
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
        if (tutorial != null && tutorial.IsRunning) tutorial.StopTutorial();
        if (dialogueUI != null && dialogueUI.IsOpen) dialogueUI.CancelDialogue();
        RefreshObjective();
    }

    private void RefreshObjective()
    {
        if (objectiveText == null) return;
        if (hasArrived)
        {
            objectiveText.text = "경기장 도착 · Page1 완료";
            return;
        }
        string controls = "진행 중";
        if (tutorial != null && tutorial.IsCompleted) controls = "완료";
        string photo = photoInspected ? "완료" : "가족사진 조사";
        string wife = wifeTalked ? "완료" : "아내와 대화";
        objectiveText.text = "집 탐색  |  조작: " + controls + "  ·  사진: " + photo + "  ·  대화: " + wife;
        if (tutorial != null && tutorial.IsCompleted && photoInspected && wifeTalked)
            objectiveText.text += "\n준비가 되면 출입문에서 F · 경기장으로 이동";
        if (interaction != null && interaction.SelectedTarget != null && player.CanControlPlayer)
            objectiveText.text += "\n[F] " + interaction.SelectedTarget.name;
    }
}
