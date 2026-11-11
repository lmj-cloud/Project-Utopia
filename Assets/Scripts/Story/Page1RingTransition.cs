using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>기존 링 진입 또는 Page1 현관 Timeline Signal을 Page2 비동기 전환으로 연결한다.</summary>
[RequireComponent(typeof(BoxCollider), typeof(Rigidbody))]
public class Page1RingTransition : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerCameraController playerCamera;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private StoryProgress story;
    [SerializeField] private S03PlayerIntegration s03;
    [SerializeField] private ScreenEffectController screenEffects;
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField] private string destinationScene = "Assets/Scenes/Page2.unity";
    [SerializeField, Min(1f)] private float readinessTimeout = 15f;
    [Header("현관 Timeline의 완전 백색 인계")]
    [SerializeField] private bool useDoorTimeline;
    [SerializeField] private Capstone.Direction.DirectionSequence doorSequence;
    [SerializeField] private Capstone.Direction.DirectionScreen doorScreen;
    [SerializeField] private UnityEngine.UI.Image transitionImage;
    private bool isWhiteHandoffArmed;
    private bool usesWhiteHandoff;

    public static Page1RingTransition Active { get; private set; }
    public bool IsTransitioning { get; private set; }
    public int TriggerCount { get; private set; }
    public int LoadCount { get; private set; }
    public float CoverageAtLoad { get; private set; }
    private Page2StoryController receiver;
    private bool ownsEffects;

    private void OnTriggerEnter(Collider other) { TryBegin(other); }
    private void OnTriggerStay(Collider other) { TryBegin(other); }

    private void TryBegin(Collider other)
    {
        if (useDoorTimeline || IsTransitioning || Active != null || other == null || player == null ||
            other.GetComponentInParent<PlayerController>() != player || !player.CanControlPlayer ||
            s03 == null || !s03.HasCompleted || story == null || !story.HasFlag("ArenaIntroCompleted")) return;
        BeginSceneTransition(false);
    }

    /// <summary>기존 완전 가림 이벤트에서만 화면을 유지한다. Timeline Signal 전에는 씬을 로드하지 않는다.</summary>
    public void HoldDoorWhiteCoverage()
    {
        if (!useDoorTimeline || IsTransitioning || isWhiteHandoffArmed || s03 == null || !s03.IsRunning)
            return;
        if (doorSequence == null || !doorSequence.IsRunning || doorScreen == null || doorScreen.overlay == null ||
            doorScreen.overlay.color.a < .999f || transitionImage == null || doorSequence.screen != doorScreen)
            throw new System.InvalidOperationException("Page1 현관 전환: 완전 백색 화면과 기존 화면 참조가 필요합니다.");

        // DirectionSequence의 후속 Reveal만 보류한다. 원본 화면·Signal·카메라 연출은 바꾸지 않는다.
        isWhiteHandoffArmed = true;
        doorSequence.screen = null;
    }

    /// <summary>기존 StartArenaShot Signal의 Page1 전용 수신점. 시간은 Timeline에서 편집한다.</summary>
    public void LoadPage2FromTimeline()
    {
        if (!useDoorTimeline || !isWhiteHandoffArmed || IsTransitioning || Active != null ||
            doorSequence == null || !doorSequence.IsRunning || s03 == null || !s03.IsRunning)
            return;
        if (doorScreen == null || doorScreen.overlay == null || doorScreen.overlay.color.a < .999f)
        {
            CancelDoorHandoff();
            return;
        }
        BeginSceneTransition(true);
    }

    /// <summary>실제 로드 전 취소는 기존 S03 취소 경로와 함께 보류한 화면 참조만 복구한다.</summary>
    public void CancelDoorHandoff()
    {
        if (IsTransitioning) return;
        RestoreDoorScreen();
    }

    private void RestoreDoorScreen()
    {
        if (isWhiteHandoffArmed && doorSequence != null && doorSequence.screen == null)
            doorSequence.screen = doorScreen;
        isWhiteHandoffArmed = false;
    }

    private void BeginSceneTransition(bool whiteHandoff)
    {
        if (IsTransitioning || Active != null) return;
        if (screenEffects == null || !screenEffects.IsReady || blackOverlay == null ||
            player == null || playerCamera == null || (whiteHandoff && transitionImage == null) ||
            !Application.CanStreamedLevelBeLoaded(destinationScene))
        {
            Debug.LogError("Page1 링 전환: Fade 참조와 Page2 Build 등록을 확인하세요.", this);
            return;
        }
        if (!screenEffects.TryAcquire(this)) return;
        ownsEffects = true;
        usesWhiteHandoff = whiteHandoff;
        if (usesWhiteHandoff)
        {
            // 같은 Sprite·색의 기존 인계용 화면을 먼저 덮어 씬 해제 중에도 Page1이 노출되지 않게 한다.
            transitionImage.sprite = doorScreen.overlay.sprite;
            transitionImage.material = doorScreen.overlay.material;
            transitionImage.color = doorScreen.overlay.color;
            blackOverlay.alpha = 1f;
            blackOverlay.blocksRaycasts = true;
        }
        Active = this;
        IsTransitioning = true;
        TriggerCount++;
        player.AcquireControlLock(this);
        player.AcquireMovementSimulationLock(this);
        playerCamera.AcquireTargetControl(this);
        if (gameplayUI != null) gameplayUI.AcquireHidden(this);
        GetComponent<Collider>().enabled = false;
        // 기존 인계용 화면만 씬 경계를 넘긴다. 기존 Player/Camera는 Page1과 함께 해제된다.
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        if (usesWhiteHandoff)
        {
            // 인계 화면과 자기 잠금을 확보한 뒤, 아직 활성 상태인 원본 연출을 정리한다.
            // 씬 언로드의 OnDisable에서 오디오 Coroutine을 시작하는 것을 방지한다.
            RestoreDoorScreen();
            s03.Cancel();
        }
        StartCoroutine(LoadPage2());
    }

    private IEnumerator LoadPage2()
    {
        // Signal 인계는 이미 완전히 흰 상태이므로 추가 고정 시간이나 Fade를 기다리지 않는다.
        if (!usesWhiteHandoff) yield return screenEffects.Fade(Color.black, 1f);
        CoverageAtLoad = blackOverlay.alpha;
        if (usesWhiteHandoff) CoverageAtLoad *= transitionImage.color.a;
        if (CoverageAtLoad < 0.999f) { Fail("화면이 완전히 덮이지 않아 로드를 중단했습니다."); yield break; }
        AsyncOperation loading = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
        if (loading == null) { Fail("Page2 비동기 로드를 시작하지 못했습니다."); yield break; }
        LoadCount++;
        yield return loading;
        float deadline = Time.realtimeSinceStartup + readinessTimeout;
        while (receiver == null && Time.realtimeSinceStartup < deadline) yield return null;
        if (receiver == null) { Fail("Page2 Player/Camera/NavMesh 준비가 확인되지 않았습니다. 덮인 화면을 유지합니다."); yield break; }
        yield return screenEffects.Fade(usesWhiteHandoff ? Color.white : Color.black, 0f);
        Page2StoryController readyReceiver = receiver;
        ReleaseOwnLocks();
        screenEffects.Release(this);
        ownsEffects = false;
        Active = null;
        readyReceiver.ArrivalRevealed();
        Destroy(gameObject);
    }

    public bool NotifyPage2Ready(Page2StoryController ready)
    {
        if (!IsTransitioning || receiver != null || ready == null || ready.gameObject.scene.path != destinationScene) return false;
        receiver = ready;
        return true;
    }

    private void Fail(string reason)
    {
        Debug.LogError("Page1 → Page2: " + reason, this);
        // 실패를 성공 이벤트로 바꾸지 않는다. 로드 후 실패라면 덮인 화면을 그대로 유지한다.
        ReleaseOwnLocks();
    }

    private void ReleaseOwnLocks()
    {
        if (player != null) { player.ReleaseMovementSimulationLock(this); player.ReleaseControlLock(this); }
        if (playerCamera != null) playerCamera.ReleaseTargetControl(this, false);
        if (gameplayUI != null) gameplayUI.ReleaseHidden(this);
    }

    private void OnDisable()
    {
        RestoreDoorScreen();
        ReleaseOwnLocks();
        if (ownsEffects && screenEffects != null) screenEffects.Release(this);
        if (Active == this) Active = null;
    }
}
