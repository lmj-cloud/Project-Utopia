using System.Collections;
using UnityEngine;

/// <summary>기존 Page2 도착과 경기 시작 사이의 가족 발견/링 입장만 연결한다.</summary>
public class Page2ArenaEntrance : MonoBehaviour
{
    [SerializeField] private Page2StoryController story;
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerCameraController playerCamera;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private PageTimelineSequence familySpotting;
    [SerializeField] private PageTimelineSequence ringEntrance;
    [SerializeField] private ScreenEffectController screenEffects;
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField] private Transform combatSpawn;
    [SerializeField] private Collider ringTrigger;
    [Header("실제 링 내부에서만 상대 활성화")]
    [SerializeField] private GameObject ringOpponent;
    [SerializeField] private BoxCollider opponentActivationArea;
    public int OpponentActivationCount { get; private set; }

    private void Awake()
    {
        if (ringOpponent != null) ringOpponent.SetActive(false);
    }

    /// <summary>기존 Player 전용 Trigger와 링 이동 Signal이 같은 일회성 활성화 경로를 사용한다.</summary>
    public void ActivateOpponentInsideRing()
    {
        if (OpponentActivationCount != 0 || (!AwaitingRing && !IsRunning) || ringOpponent == null ||
            opponentActivationArea == null || player == null || !FamilyCompleted) return;
        Vector3 point = player.transform.position + Vector3.up * .8f;
        if (!opponentActivationArea.bounds.Contains(point)) return;
        OpponentActivationCount++;
        ringOpponent.SetActive(true);
    }

    public bool FamilyCompleted { get; private set; }
    public bool AwaitingRing { get; private set; }
    public bool RingCompleted { get; private set; }
    public bool IsRunning { get; private set; }
    public int RingStartCount { get; private set; }
    private bool coveredArrival;
    private bool ownsEffects;
    private Vector3 entryPosition;
    private Quaternion entryRotation;
    private Quaternion cameraRotation;

    public void BeginArrival()
    {
        if (!isActiveAndEnabled || IsRunning || FamilyCompleted) return;
        Snapshot();
        Acquire();
        StartCoroutine(SpotFamily());
    }
    private IEnumerator SpotFamily()
    {
        yield return familySpotting.Play();
        if (!familySpotting.Completed) { Fail("가족 발견 Timeline 취소"); yield break; }
        FamilyCompleted = true;
        AwaitingRing = true;
        Release();
    }
    public void RingEntered()
    {
        if (!isActiveAndEnabled || !AwaitingRing || IsRunning || RingCompleted || !player.CanControlPlayer) return;
        Snapshot();
        // 입력 잠금을 얻기 전에 이전 이동 파라미터를 정리한다.
        PlayerAnimatorController animator = player.GetComponent<PlayerAnimatorController>();
        animator.CurrentSpeed = 0f;
        animator.MoveHorizontal = 0f;
        animator.MoveVertical = 0f;
        if (!screenEffects.TryAcquire(this)) return;
        ownsEffects = true;
        AwaitingRing = false;
        RingStartCount++;
        ringTrigger.enabled = false;
        Acquire();
        StartCoroutine(EnterRing());
    }
    private IEnumerator EnterRing()
    {
        yield return ringEntrance.Play();
        if (!ringEntrance.Completed || !coveredArrival) { Fail("링 입장 정상 인계 실패"); yield break; }
        // Timeline 그래프 해제 뒤 Humanoid Animator가 다시 평가된 프레임에 인계한다.
        yield return null;
        // 원래 경기 소개가 자기 잠금을 얻은 뒤 이 어댑터의 잠금만 해제한다.
        if (!story.TryBeginArenaIntro()) { Fail("기존 경기 소개 시작 실패"); yield break; }
        RingCompleted = true;
        Release();
    }
    public void TimelineEnterCombat()
    {
        if (!IsRunning || !ownsEffects || blackOverlay.alpha < .999f) return;
        movement.Teleport(combatSpawn.position, combatSpawn.rotation);
        playerCamera.SynchronizeDirection(combatSpawn.rotation);
        ActivateOpponentInsideRing();
        coveredArrival = true;
    }
    private void Snapshot()
    {
        entryPosition = player.transform.position;
        entryRotation = player.transform.rotation;
        cameraRotation = playerCamera.ThirdPersonTarget.rotation;
    }
    private void Acquire()
    {
        IsRunning = true;
        player.AcquireControlLock(this);
        player.AcquireMovementSimulationLock(this);
        playerCamera.AcquireTargetControl(this);
        gameplayUI.AcquireHidden(this);
    }
    private void Release()
    {
        if (player != null)
        {
            player.ReleaseMovementSimulationLock(this);
            player.ReleaseControlLock(this);
        }
        if (playerCamera != null) playerCamera.ReleaseTargetControl(this, true);
        if (gameplayUI != null) gameplayUI.ReleaseHidden(this);
        if (ownsEffects && screenEffects != null) screenEffects.Release(this);
        ownsEffects = false;
        IsRunning = false;
    }
    private void Fail(string reason)
    {
        if (familySpotting != null) familySpotting.Cancel();
        if (ringEntrance != null) ringEntrance.Cancel();
        if (movement != null) movement.Teleport(entryPosition, entryRotation);
        if (playerCamera != null) playerCamera.SynchronizeDirection(cameraRotation);
        if (ownsEffects && blackOverlay != null) blackOverlay.alpha = 0;
        Release();
        Debug.LogError("[Page2 입장] " + reason, this);
    }
    private void OnDisable()
    {
        if (!IsRunning) return;
        StopAllCoroutines();
        if (familySpotting != null) familySpotting.Cancel();
        if (ringEntrance != null) ringEntrance.Cancel();
        if (movement != null) movement.Teleport(entryPosition, entryRotation);
        if (playerCamera != null) playerCamera.SynchronizeDirection(cameraRotation);
        if (ownsEffects && blackOverlay != null) blackOverlay.alpha = 0;
        Release();
    }
}
