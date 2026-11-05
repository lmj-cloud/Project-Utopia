using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>정상 S03 이후 링의 실제 Trigger 진입을 한 번만 Page2 비동기 전환으로 연결한다.</summary>
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
        if (IsTransitioning || Active != null || other == null || player == null ||
            other.GetComponentInParent<PlayerController>() != player || !player.CanControlPlayer ||
            s03 == null || !s03.HasCompleted || story == null || !story.HasFlag("ArenaIntroCompleted")) return;
        if (screenEffects == null || !screenEffects.IsReady || blackOverlay == null ||
            !Application.CanStreamedLevelBeLoaded(destinationScene))
        {
            Debug.LogError("Page1 링 전환: Fade 참조와 Page2 Build 등록을 확인하세요.", this);
            return;
        }
        if (!screenEffects.TryAcquire(this)) return;
        ownsEffects = true;
        Active = this;
        IsTransitioning = true;
        TriggerCount++;
        player.AcquireControlLock(this);
        player.AcquireMovementSimulationLock(this);
        playerCamera.AcquireTargetControl(this);
        if (gameplayUI != null) gameplayUI.AcquireHidden(this);
        GetComponent<Collider>().enabled = false;
        // 검은 화면만 씬 경계를 넘긴다. 기존 Player/Camera는 Page1과 함께 해제된다.
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        StartCoroutine(LoadPage2());
    }

    private IEnumerator LoadPage2()
    {
        yield return screenEffects.Fade(Color.black, 1f);
        CoverageAtLoad = blackOverlay.alpha;
        if (CoverageAtLoad < 0.999f) { Fail("검은 화면이 완전히 덮이지 않아 로드를 중단했습니다."); yield break; }
        AsyncOperation loading = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
        if (loading == null) { Fail("Page2 비동기 로드를 시작하지 못했습니다."); yield break; }
        LoadCount++;
        yield return loading;
        float deadline = Time.realtimeSinceStartup + readinessTimeout;
        while (receiver == null && Time.realtimeSinceStartup < deadline) yield return null;
        if (receiver == null) { Fail("Page2 Player/Camera/NavMesh 준비가 확인되지 않았습니다. 검은 화면을 유지합니다."); yield break; }
        yield return screenEffects.Fade(Color.black, 0f);
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
        ReleaseOwnLocks();
        if (ownsEffects && screenEffects != null) screenEffects.Release(this);
        if (Active == this) Active = null;
    }
}
