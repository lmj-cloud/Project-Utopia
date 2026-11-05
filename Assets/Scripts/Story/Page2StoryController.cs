using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.AI;
using Unity.Cinemachine;

public enum Page2Stage { Arrival, Intro, Combat, TraumaSequence, GlitchStunned, PlayerDefeated, FadeToBlack, Completed, Failed }

/// <summary>기존 MMA와 S06의 성공 이벤트를 Page2 스토리 패배 흐름으로 연결한다. 피해는 기존 AI 타격만 적용한다.</summary>
public class Page2StoryController : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerStatus status;
    [SerializeField] private PlayerMeleeCombat melee;
    [SerializeField] private PlayerCombatActions combatActions;
    [SerializeField] private PlayerAnimatorController playerAnimator;
    [SerializeField] private PlayerCameraController playerCamera;
    [SerializeField] private GameplayUI gameplayUI;
    [SerializeField] private MMATestSceneStarter combatStarter;
    [SerializeField] private SparringOpponent opponent;
    [SerializeField] private EnemyHealth opponentHealth;
    [SerializeField] private NavMeshAgent opponentAgent;
    [SerializeField] private CutsceneController intro;
    [SerializeField] private CinemachineCamera storyCamera;
    [SerializeField] private Page2S06Adapter trauma;
    [SerializeField] private Page2GlitchUI glitch;
    [SerializeField] private ScreenEffectController screenEffects;
    [SerializeField] private CanvasGroup blackOverlay;
    [SerializeField] private StoryProgress story;
    [SerializeField] private TMPro.TMP_Text statusText;
    [SerializeField] private TMPro.TMP_Text healthText;
    [SerializeField] private UnityEngine.UI.Image playerHealthFill;
    [SerializeField] private UnityEngine.UI.Image kevinHealthFill;
    [Header("기존 쓰러짐 모션 / 내일 교체 가능")]
    [SerializeField] private AnimationClip defeatClip;
    [SerializeField, Min(.01f)] private float defeatPlaybackSpeed = 1f;
    [SerializeField, Min(1f)] private float readinessTimeout = 10f;
    [SerializeField] private UnityEvent onPage2DefeatStarted = new UnityEvent();
    [SerializeField] private UnityEvent onDefeatAnimationRequested = new UnityEvent();
    [SerializeField] private UnityEvent onPage2FadeCompleted = new UnityEvent();
    [SerializeField] private UnityEvent onPage2Completed = new UnityEvent();
    [SerializeField] private Page2Stage stage = Page2Stage.Arrival;
    [SerializeField] private OfficialDialoguePlayer officialDialogue;
    [SerializeField] private TutorialManager combatTutorial;
    public Page2Stage Stage { get { return stage; } }
    public UnityEvent OnPage2DefeatStarted { get { return onPage2DefeatStarted; } }
    public UnityEvent OnPage2FadeCompleted { get { return onPage2FadeCompleted; } }
    public UnityEvent OnPage2Completed { get { return onPage2Completed; } }
    public UnityEvent OnDefeatAnimationRequested { get { return onDefeatAnimationRequested; } }
    public int TraumaCount { get; private set; }
    public int DefeatCount { get; private set; }
    public int CompletionCount { get; private set; }
    public int DefeatStartedFrame { get; private set; } = -1;
    public int FadeStartedFrame { get; private set; } = -1;
    public bool DefeatAnimationStarted { get; private set; }
    public float BlackAlpha { get { return blackOverlay != null ? blackOverlay.alpha : 0; } }
    private const int TraumaPauseRequest = 1;
    private bool ownsEffects;
    private bool arrivalNotified;
    private Coroutine defeatRoutine;
    [Header("Page2 편집용 Timeline")]
    [SerializeField] private PageTimelineSequence arrivalTimeline;
    [SerializeField] private PageTimelineSequence introTimeline;
    [SerializeField] private PageTimelineSequence glitchTimeline;
    [SerializeField] private PageTimelineSequence defeatTimeline;

    private void Awake()
    {
        stage = Page2Stage.Arrival;
        BlockGameplay();
    }

    private void OnEnable()
    {
        if (status != null) status.Died += PlayerDied;
        if (opponentHealth != null) opponentHealth.Damaged += KevinThresholdReached;
        if (intro != null) { intro.Completed += IntroCompleted; intro.Cancelled += IntroCancelled; }
        if (trauma != null) { trauma.Completed += TraumaCompleted; trauma.Cancelled += TraumaCancelled; }
    }

    private IEnumerator Start()
    {
        // Player/Animator/NavMesh 및 기존 Starter의 초기화를 먼저 끝낸다.
        yield return null;
        BlockGameplay();
        float deadline = Time.realtimeSinceStartup + readinessTimeout;
        while (!ReferencesReady() && Time.realtimeSinceStartup < deadline) yield return null;
        if (!ReferencesReady()) { Fail("Player/Camera/NavMesh/S06 준비가 확인되지 않았습니다."); yield break; }
        if (Page1RingTransition.Active != null)
        {
            blackOverlay.alpha = 0;
            if (!Page1RingTransition.Active.NotifyPage2Ready(this)) Fail("Page1의 씬 인계 요청이 일치하지 않습니다.");
            yield break;
        }
        // Page2를 직접 실행할 때도 도착 화면을 검정에서 공개한다.
        if (!screenEffects.TryAcquire(this)) { Fail("도착 Fade 소유권을 얻지 못했습니다."); yield break; }
        ownsEffects = true;
        yield return arrivalTimeline.Play();
        if (!arrivalTimeline.Completed) { Fail("도착 Timeline 취소"); yield break; }
        screenEffects.Release(this);
        ownsEffects = false;
        ArrivalRevealed();
    }

    private bool ReferencesReady()
    {
        return player != null && player.IsAlive && status != null && melee != null && combatActions != null &&
            playerAnimator != null && playerAnimator.CombatAnimator != null && playerAnimator.CombatAnimator.isInitialized &&
            playerCamera != null && combatStarter != null && opponent != null && opponentHealth != null &&
            opponentAgent != null && opponentAgent.enabled && opponentAgent.isOnNavMesh && trauma != null && trauma.IsReady &&
            arrivalTimeline != null && introTimeline != null && glitchTimeline != null && defeatTimeline != null &&
            intro != null && intro.CanPlay() && screenEffects != null && screenEffects.IsReady && blackOverlay != null;
    }

    public void ArrivalRevealed()
    {
        if (stage != Page2Stage.Arrival || arrivalNotified || !ReferencesReady()) return;
        arrivalNotified = true;
        SetStage(Page2Stage.Intro);
        if (!intro.TryPlay(intro, out _)) Fail("경기 소개 시간축을 시작하지 못했습니다.");
    }

    private void IntroCompleted(CutsceneController source, int request)
    {
        if (stage != Page2Stage.Intro || source != intro) return;
        if (officialDialogue != null) StartCoroutine(BeginOfficialBout());
        else BeginBout();
    }
    private IEnumerator BeginOfficialBout()
    {
        // 대사 시작은 Intro Signal이 담당한다. 여기서는 실제 완료만 확인한다.
        if (introTimeline == null) { Fail("Intro Timeline 연결 누락"); yield break; }
        if (!officialDialogue.LastCompleted) { Fail("경기 시작 대화 취소"); yield break; }
        yield return null;
        BeginBout();
        if (stage == Page2Stage.Combat && combatTutorial != null) combatTutorial.BeginTutorial();
    }
    private void BeginBout()
    {
        storyCamera.Priority = 0;
        ReleaseGameplayLocks();
        combatStarter.BeginCombat();
        if (!combatStarter.IsCombatActive || !opponent.IsTraining) { BlockGameplay(); Fail("기존 MMA 전투가 시작되지 않았습니다."); return; }
        SetStage(Page2Stage.Combat);
        // 구독 전에 임계값에 도달했더라도 현재 실제 체력으로 한 번 확인한다.
        KevinThresholdReached();
    }

    private void IntroCancelled(CutsceneController source, int request)
    {
        if (stage == Page2Stage.Intro) Fail("경기 소개가 취소되었습니다.");
    }

    public void KevinThresholdReached()
    {
        // 튜토리얼 완료 여부는 스토리 임계값과 별개다. 실제 Kevin의 피해 이벤트와
        // 기존 임계값 UnityEvent가 겹쳐도 먼저 바뀐 Stage가 중복 실행을 막는다.
        if (gameObject.scene.name != "Page2" || stage != Page2Stage.Combat ||
            opponent == null || !opponent.IsTraining || !opponent.IsStoryMode ||
            opponentHealth == null || opponent.GetComponent<EnemyHealth>() != opponentHealth ||
            opponentHealth.IsDead || opponentHealth.MaxHealth <= 0f ||
            opponentHealth.CurrentHealth > opponentHealth.MaxHealth * .25f || TraumaCount != 0) return;
        SetStage(Page2Stage.TraumaSequence);
        TraumaCount++;
        if (combatTutorial != null && combatTutorial.IsRunning) combatTutorial.StopTutorial();
        // 이 연출은 플레이어 Animator를 재생하지 않는다. 잠금 전에 기존 취소 API로
        // 공격 자세까지 복구해야 마지막 펀치 상태가 패닉 구간에 남지 않는다.
        if (combatActions != null) combatActions.CancelAction();
        if (melee != null) melee.CancelActions();
        BlockGameplay();
        if (!opponent.PauseForExecution(this, TraumaPauseRequest)) { Fail("Kevin의 진행 중 공격을 안전하게 정지하지 못했습니다."); return; }
        if (!trauma.Begin()) Fail("S06 Timeline 실행을 시작하지 못했습니다.");
    }

    private void TraumaCompleted()
    {
        if (stage != Page2Stage.TraumaSequence || trauma.CompletionCount != 1) return;
        if (!melee.TryBeginScriptedMmaVictim(this)) { Fail("입력 잠금과 스토리 피격 소유권이 일치하지 않습니다."); return; }
        SetStage(Page2Stage.GlitchStunned);
        glitchTimeline.Begin(true);
        if (glitch != null) glitch.SetGlitch(true);
        // 전투 소유권과 Kevin의 체력 하한을 유지한 채 실제 추적·타격만 다시 허용한다.
        opponent.ReleaseExecution(this, TraumaPauseRequest);
        // Play(7)은 Glitch Timeline의 Signal로 연결한다.
    }

    private void TraumaCancelled()
    {
        if (stage == Page2Stage.TraumaSequence) Fail("S06이 취소되었거나 마지막 Signal까지 실행되지 않았습니다.");
    }

    private void PlayerDied()
    {
        if (status == null || !status.IsDead || status.CurrentHealth > 0 || stage >= Page2Stage.PlayerDefeated) return;
        BlockGameplay();
        if (!melee.OwnsScriptedMmaVictim(this)) melee.TryBeginScriptedMmaVictim(this);
        SetStage(Page2Stage.PlayerDefeated);
        DefeatCount++;
        opponent.PauseForExecution(this, TraumaPauseRequest + 1);
        opponent.EndTraining();
        glitchTimeline.StopVisuals();
        if (glitch != null) glitch.SetGlitch(false);
        DefeatStartedFrame = Time.frameCount;
        // 요청 이벤트와 검은 Fade는 같은 프레임에 시작한다. 완료 뒤에는 그래프의 마지막 자세를 유지한다.
        onPage2DefeatStarted.Invoke();
        DefeatAnimationStarted = defeatTimeline != null && defeatClip != null && defeatPlaybackSpeed > 0;
        onDefeatAnimationRequested.Invoke();
        if (!DefeatAnimationStarted) Debug.LogWarning("Page2: 호환되는 뒤로 쓰러짐 클립을 재생하지 못했습니다. 연결 이벤트와 Fade는 유지합니다.", this);
        defeatRoutine = StartCoroutine(FinishDefeat());
    }

    private IEnumerator FinishDefeat()
    {
        if (!screenEffects.TryAcquire(this)) { Fail("패배 Fade 소유권을 얻지 못했습니다."); yield break; }
        ownsEffects = true;
        SetStage(Page2Stage.FadeToBlack);
        FadeStartedFrame = Time.frameCount;
        yield return defeatTimeline.Play();
        if (!defeatTimeline.Completed) { Fail("패배 Timeline 취소"); yield break; }
        if (BlackAlpha < .999f) { Fail("패배 화면이 완전히 검어지지 않았습니다."); yield break; }
        onPage2FadeCompleted.Invoke();
        // 패배가 빠르게 발생해도 이미 시작한 공식 발화를 다음 Scene 로딩으로 끊지 않는다.
        if (officialDialogue != null) while (officialDialogue.IsBusy) yield return null;
        SetStage(Page2Stage.Completed);
        CompletionCount++;
        if (story != null) story.SetFlag("Page2DefeatCompleted");
        onPage2Completed.Invoke();
        Debug.Log("[Page2] 패배 연출 완료. 검은 화면을 유지하며 S07/Page3 연결을 기다립니다.", this);
        defeatRoutine = null;
        // 다음 구간의 준비 알림 전에는 입력 잠금과 검은 화면을 해제하지 않는다.
    }

    private void BlockGameplay()
    {
        if (player != null) { player.AcquireControlLock(this); player.AcquireMovementSimulationLock(this); }
        if (playerCamera != null) playerCamera.AcquireTargetControl(this);
        if (combatActions != null) combatActions.CancelAction();
        if (melee != null) melee.CancelActions();
    }

    private void ReleaseGameplayLocks()
    {
        if (player != null) { player.ReleaseMovementSimulationLock(this); player.ReleaseControlLock(this); }
        if (playerCamera != null) playerCamera.ReleaseTargetControl(this, true);
    }

    private void SetStoryCamera(bool visible)
    {
        // 실제 Shot과 카메라 위치/회전은 Page2 Timeline Track에서 편집한다.
        if (!visible && storyCamera != null) storyCamera.Priority = 0;
    }

    private void SetStage(Page2Stage next)
    {
        if (next <= stage) return;
        stage = next;
        if (statusText != null) statusText.text = "PAGE 2 · " + stage;
    }

    private void Fail(string reason)
    {
        SetStage(Page2Stage.Failed);
        if (opponent != null) opponent.PauseForExecution(this, TraumaPauseRequest + 2);
        Debug.LogError("Page2 진행 중단: " + reason, this);
    }

    private void Update()
    {
        if (status == null || opponentHealth == null) return;
        if (healthText != null) healthText.text = "PLAYER " + Mathf.CeilToInt(status.CurrentHealth) + " / " + Mathf.CeilToInt(status.MaxHealth) +
            "     KEVIN " + Mathf.CeilToInt(opponentHealth.CurrentHealth) + " / " + Mathf.CeilToInt(opponentHealth.MaxHealth);
        if (playerHealthFill != null) playerHealthFill.fillAmount = status.HealthNormalized;
        if (kevinHealthFill != null) kevinHealthFill.fillAmount = opponentHealth.CurrentHealth / opponentHealth.MaxHealth;
    }

    private void OnDisable()
    {
        if (status != null) status.Died -= PlayerDied;
        if (opponentHealth != null) opponentHealth.Damaged -= KevinThresholdReached;
        if (intro != null) { intro.Completed -= IntroCompleted; intro.Cancelled -= IntroCancelled; }
        if (trauma != null) { trauma.Completed -= TraumaCompleted; trauma.Cancelled -= TraumaCancelled; trauma.Cancel(); }
        if (defeatRoutine != null) StopCoroutine(defeatRoutine);
        if (arrivalTimeline != null) arrivalTimeline.Cancel();
        if (introTimeline != null) introTimeline.Cancel();
        if (glitchTimeline != null) glitchTimeline.Cancel();
        if (defeatTimeline != null) defeatTimeline.Cancel();
        if (ownsEffects && screenEffects != null) screenEffects.Release(this);
        if (opponent != null) { opponent.ReleaseExecution(this, TraumaPauseRequest); opponent.ReleaseExecution(this, TraumaPauseRequest + 1); }
        if (melee != null) melee.EndScriptedMmaVictim(this);
        ReleaseGameplayLocks();
    }
}

