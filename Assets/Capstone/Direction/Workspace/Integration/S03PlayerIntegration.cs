using System;
using System.Collections;
using Capstone.Direction;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Playables;

/// <summary>
/// Page1의 실제 문 상호작용을 기존 S03 연출에 연결하고 자기 소유의 제어 잠금만 복구한다.
/// </summary>
public sealed class S03PlayerIntegration : MonoBehaviour
{
    [Header("Existing gameplay")]
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerCameraController playerCamera;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private Camera outputCamera;
    [SerializeField] private InteractableObject doorInteraction;

    [Header("Existing S03 Timeline")]
    [SerializeField] private DirectionSequence sequence;
    [SerializeField] private DirectionScreen screen;
    [SerializeField] private CinemachineCamera cinematicCamera;
    [SerializeField] private Transform doorHinge;
    [SerializeField] private Transform doorFocus;
    [SerializeField] private Transform doorDollyEnd;
    [SerializeField] private Transform destination;
    [SerializeField] private Transform arenaShotStart;
    [SerializeField] private Transform arenaShotEnd;
    [SerializeField] private Transform arenaLookTarget;
    [SerializeField] private AudioClip doorSound;
    [SerializeField] private float doorOpenAngle = -95f;
    [SerializeField, Min(0.1f)] private float cameraReturnTimeout = 3f;

    [Header("Successful arrival only")]
    [SerializeField] private UnityEvent onCompleted = new UnityEvent();

    private bool isRunning;
    private bool hasArrived;
    private bool isPanning;
    private bool ownsLocks;
    private bool receivedEndSignal;
    private float startedAt;
    private Vector3 originalPlayerPosition;
    private Quaternion originalPlayerRotation;
    private Vector3 originalCameraPosition;
    private Quaternion originalCameraRotation;
    private LensSettings originalLens;
    private Quaternion originalDoorRotation;
    private Coroutine returning;

    public bool IsRunning => isRunning;
    public bool HasArrived => hasArrived;
    public bool HasCompleted { get; private set; }
    public int CompletionCount { get; private set; }
    public float CoverageAtTransfer { get; private set; }
    public string LastFailure { get; private set; }

    /// <summary>InteractableObject의 onInteracted에서만 시작하며 실패하면 문을 재시도 가능하게 돌린다.</summary>
    public void BeginFromDoor()
    {
        if (isRunning || HasCompleted)
            return;

        if (!ReferencesReady() || !player.CanControlPlayer || playerCamera.HasTargetControl ||
            sequence.IsRunning || sequence.director.state == PlayState.Playing)
        {
            if (doorInteraction != null)
                doorInteraction.CancelExternalInteraction();
            return;
        }

        LastFailure = null;
        hasArrived = false;
        receivedEndSignal = false;
        CoverageAtTransfer = 0f;
        originalPlayerPosition = player.transform.position;
        originalPlayerRotation = player.transform.rotation;
        originalCameraPosition = cinematicCamera.transform.position;
        originalCameraRotation = cinematicCamera.transform.rotation;
        originalLens = cinematicCamera.Lens;
        originalDoorRotation = doorHinge.localRotation;

        // 같은 Director의 실행 주체는 DirectionSequence 하나로 유지한다.
        sequence.Play();
        if (!sequence.IsRunning)
        {
            LastFailure = "S03 DirectionSequence is already owned or could not start.";
            doorInteraction.CancelExternalInteraction();
        }
    }

    private bool ReferencesReady()
    {
        return isActiveAndEnabled && player != null && movement != null && playerCamera != null &&
            outputCamera != null && doorInteraction != null && sequence != null &&
            sequence.director != null && sequence.director.playableAsset != null &&
            screen != null && screen.overlay != null && cinematicCamera != null &&
            doorHinge != null && doorFocus != null && doorDollyEnd != null &&
            destination != null && arenaShotStart != null && arenaShotEnd != null && arenaLookTarget != null;
    }

    /// <summary>DirectionSequence가 시작에 성공한 뒤 이동·입력·카메라·HUD 소유권을 함께 취득한다.</summary>
    public void SequenceStarted()
    {
        if (!ReferencesReady())
            throw new InvalidOperationException("Page1 S03 references are incomplete.");

        isRunning = true;
        startedAt = Time.unscaledTime;
        ownsLocks = true;
        player.AcquireControlLock(this);
        player.AcquireMovementSimulationLock(this);
        playerCamera.AcquireTargetControl(this);
        if (gameplayUI != null)
            gameplayUI.AcquireHidden(this);

        cinematicCamera.transform.SetPositionAndRotation(outputCamera.transform.position, outputCamera.transform.rotation);
        originalCameraPositionForShot = outputCamera.transform.position;
        LensSettings lens = cinematicCamera.Lens;
        lens.FieldOfView = outputCamera.fieldOfView;
        cinematicCamera.Lens = lens;
        if (sequence.audioSystem != null && doorSound != null)
            sequence.audioSystem.Play(doorSound);
    }

    private void Update()
    {
        if (!isRunning || returning != null)
            return;

        if (player == null || !player.IsAlive || sequence == null || cinematicCamera == null)
        {
            Cancel();
            return;
        }

        if (!hasArrived)
        {
            // 공통 DirectionScreen의 백색 전환 동안만 문과 진입 카메라를 보간한다.
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - startedAt) / 0.5f));
            doorHinge.localRotation = originalDoorRotation * Quaternion.Euler(0f, doorOpenAngle * t, 0f);
            cinematicCamera.transform.position = Vector3.Lerp(originalCameraPositionForShot, doorDollyEnd.position, t);
            Vector3 direction = doorFocus.position - cinematicCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
                cinematicCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
        else if (isPanning && sequence.director.state == PlayState.Playing)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((float)(sequence.director.time / sequence.director.duration)));
            cinematicCamera.transform.position = Vector3.Lerp(arenaShotStart.position, arenaShotEnd.position, t);
            cinematicCamera.transform.LookAt(arenaLookTarget.position);
        }
    }

    private Vector3 originalCameraPositionForShot;

    /// <summary>완전히 덮인 화면에서만 CharacterController를 보호하는 기존 Teleport API를 사용한다.</summary>
    public void TransferWhileCovered()
    {
        if (!isRunning || hasArrived)
            return;
        if (screen == null || screen.overlay == null || screen.overlay.color.a < 0.999f)
            throw new InvalidOperationException("Page1 S03 transfer requires full white coverage.");

        CoverageAtTransfer = screen.overlay.color.a;
        Vector3 delta = destination.position - player.transform.position;
        movement.Teleport(destination.position, destination.rotation);
        playerCamera.SynchronizeDirection(destination.rotation);
        NotifyCameraWarp(delta);
        doorHinge.localRotation = originalDoorRotation * Quaternion.Euler(0f, doorOpenAngle, 0f);
        cinematicCamera.transform.SetPositionAndRotation(arenaShotStart.position, arenaShotStart.rotation);
        cinematicCamera.transform.LookAt(arenaLookTarget.position);
        hasArrived = true;
    }

    private void NotifyCameraWarp(Vector3 delta)
    {
        if (playerCamera.ThirdPersonTarget != null)
            CinemachineCore.OnTargetObjectWarped(playerCamera.ThirdPersonTarget, delta);
        if (playerCamera.FirstPersonTarget != null)
            CinemachineCore.OnTargetObjectWarped(playerCamera.FirstPersonTarget, delta);
    }

    /// <summary>기존 S03 시작 Signal을 받는다. SignalAsset과 Timeline은 수정하지 않는다.</summary>
    public void StartArenaShot()
    {
        if (isRunning && hasArrived)
            isPanning = true;
    }

    public void EndArenaShot()
    {
        if (!isRunning)
            return;
        isPanning = false;
        receivedEndSignal = true;
    }

    /// <summary>정상 종료 Signal까지 받은 요청만 카메라 복귀를 거쳐 완료 이벤트를 발생시킨다.</summary>
    public void SequenceCompleted()
    {
        if (!isRunning)
            return;
        if (!hasArrived || !receivedEndSignal)
        {
            SequenceCancelled();
            return;
        }
        returning = StartCoroutine(ReturnToGameplay());
    }

    private IEnumerator ReturnToGameplay()
    {
        // Priority 복원 이후 Brain이 실제 게임 카메라로 전환할 시간을 보장한다.
        yield return null;
        CinemachineBrain brain = outputCamera.GetComponent<CinemachineBrain>();
        float elapsed = 0f;
        while (brain != null && brain.IsBlending && elapsed < cameraReturnTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        returning = null;
        isRunning = false;
        RestoreCameraPose();
        ReleaseLocks();
        HasCompleted = true;
        CompletionCount++;
        doorInteraction.CompleteExternalInteraction();
        onCompleted.Invoke();
    }

    public void Cancel()
    {
        if (sequence != null && sequence.IsRunning && sequence.isActiveAndEnabled)
            sequence.Cancel();
        else
            SequenceCancelled();
    }

    /// <summary>취소는 진행 이벤트 없이 원래 위치로 복구하고 자기 소유권만 해제한다.</summary>
    public void SequenceCancelled()
    {
        if (!isRunning && !ownsLocks)
            return;
        if (returning != null)
            StopCoroutine(returning);
        returning = null;
        isRunning = false;
        isPanning = false;
        if (hasArrived && movement != null && player != null && playerCamera != null)
        {
            Vector3 delta = originalPlayerPosition - player.transform.position;
            movement.Teleport(originalPlayerPosition, originalPlayerRotation);
            playerCamera.SynchronizeDirection(originalPlayerRotation);
            NotifyCameraWarp(delta);
        }
        hasArrived = false;
        if (doorHinge != null)
            doorHinge.localRotation = originalDoorRotation;
        if (screen != null)
            screen.Clear();
        RestoreCameraPose();
        ReleaseLocks();
        if (doorInteraction != null)
            doorInteraction.CancelExternalInteraction();
    }

    private void RestoreCameraPose()
    {
        if (cinematicCamera == null)
            return;
        cinematicCamera.transform.SetPositionAndRotation(originalCameraPosition, originalCameraRotation);
        cinematicCamera.Lens = originalLens;
    }

    private void ReleaseLocks()
    {
        if (!ownsLocks)
            return;
        ownsLocks = false;
        if (playerCamera != null)
            playerCamera.ReleaseTargetControl(this, false);
        if (player != null)
        {
            player.ReleaseMovementSimulationLock(this);
            player.ReleaseControlLock(this);
        }
        if (gameplayUI != null)
            gameplayUI.ReleaseHidden(this);
    }

    private void OnDisable()
    {
        Cancel();
    }
}
