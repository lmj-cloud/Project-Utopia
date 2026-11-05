using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체력·탄약·퀵슬롯과 목표 안내, 튜토리얼 애니메이션 및 데모 종료 화면을 갱신한다.
/// </summary>
public class GameplayUI : MonoBehaviour
{
    [Header("Tutorial")]
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TMP_Text tutorialTitleText;
    [SerializeField] private Animator tutorialAnimator;
    [SerializeField] private CanvasGroup tutorialCanvasGroup;
    [SerializeField] private AnimationClip tutorialShowClip;
    [SerializeField] private AnimationClip tutorialHideClip;
    [SerializeField] private string tutorialShowState = "Base Layer.TutorialShow";
    [SerializeField] private string tutorialHideState = "Base Layer.TutorialHide";
    [SerializeField, Min(1f)] private float tutorialAnimationTimeout = 10f;
    private Coroutine tutorialAnimation;
    private TutorialManager presentationOwner;
    private int presentationRequest;
    private int presentationVersion;
    private bool warnedTutorialAnimation;
    [SerializeField] private Image tutorialBackground;
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField, Min(0f)] private float stepFeedbackDuration = 0.35f;
    private float stepFeedbackUntil;
    private int displayedStep = -1;
    private bool tutorialWasVisible;
    [SerializeField] private PlayerStatus status;
    [SerializeField] private PlayerEquipment equipment;
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private GameFlowManager flow;
    [SerializeField] private CanvasGroup hudGroup;
    [SerializeField] private TMP_Text objectiveText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private TMP_Text coachText;
    [SerializeField] private TMP_Text ammoText;
    [SerializeField] private TMP_Text equipmentText;
    [SerializeField] private TMP_Text acquisitionText;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image staminaFill;
    [SerializeField] private GameObject endPanel;
    [SerializeField, Min(0f)] private float noticeDuration = 2f;
    [Header("Equipped Item HUD")]
    [SerializeField] private GameObject equippedItemPanel;
    [SerializeField] private Image[] equippedItemIcons = new Image[5];
    [SerializeField] private TMP_Text[] equippedItemQuantities = new TMP_Text[5];
    [SerializeField] private Image[] equippedItemBackgrounds = new Image[5];
    [SerializeField] private TMP_Text selectedItemText;
    private readonly HashSet<Object> hiddenOwners = new();
    private WeaponAssaultRifle weapon;
    private float noticeUntil;
    private float coachUntil;
    public bool IsDemoEndVisible { get; private set; }
    private float originalHudAlpha;
    private bool originalHudInteractable;
    private bool originalHudBlocksRaycasts;
    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        if (tutorialCanvasGroup != null)
        {
            tutorialCanvasGroup.interactable = false;
            tutorialCanvasGroup.blocksRaycasts = false;
        }
        if (tutorialAnimator != null) tutorialAnimator.keepAnimatorStateOnDisable = true;
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
        if (hudGroup == null) return;
        originalHudAlpha = hudGroup.alpha;
        originalHudInteractable = hudGroup.interactable;
        originalHudBlocksRaycasts = hudGroup.blocksRaycasts;
    }
    /// <summary>
    /// 컴포넌트 활성화 시 필요한 알림과 초기 표시를 연결한다.
    /// </summary>
    private void OnEnable()
    {
        if (tutorial != null) tutorial.Changed += RefreshTutorial;
        RefreshTutorial();
        if (equipment != null) { equipment.OnWeaponChanged += WeaponChanged; equipment.Succeeded += EquipmentChanged; WeaponChanged(equipment.CurrentWeapon); }
        if (inventory != null) { inventory.ItemAdded += ItemAdded; inventory.Changed += RefreshEquippedItems; }
        RefreshEquippedItems();
        UpdateVisibility();
    }
    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable()
    {
        TutorialManager owner = presentationOwner;
        int request = presentationRequest;
        CancelTutorialPresentation(owner);
        if (owner != null) owner.OnTutorialPresentationCancelled(request);
        if (tutorial != null) tutorial.Changed -= RefreshTutorial;
        displayedStep = -1;
        stepFeedbackUntil = 0f;
        tutorialWasVisible = false;
        if (equipment != null) { equipment.OnWeaponChanged -= WeaponChanged; equipment.Succeeded -= EquipmentChanged; }
        if (inventory != null) { inventory.ItemAdded -= ItemAdded; inventory.Changed -= RefreshEquippedItems; }
        WeaponChanged(null);
    }
    /// <summary>
    /// 이전 무기의 알림 연결을 정리하고 현재 무기의 상태 알림을 연결한다.
    /// </summary>
    private void WeaponChanged(WeaponAssaultRifle next)
    {
        if (weapon != null) { weapon.onAmmoEvent.RemoveListener(AmmoChanged); weapon.onReserveAmmoEvent.RemoveListener(ReserveChanged); }
        weapon = next;
        if (weapon != null) { weapon.onAmmoEvent.AddListener(AmmoChanged); weapon.onReserveAmmoEvent.AddListener(ReserveChanged); }
        RefreshAmmo();
    }
    /// <summary>
    /// 변경된 현재 탄약과 최대 탄약 수를 화면에 갱신한다.
    /// </summary>
    private void AmmoChanged(int loaded, int capacity) => RefreshAmmo();
    /// <summary>
    /// 변경된 예비 탄약 수를 화면에 갱신한다.
    /// </summary>
    private void ReserveChanged(int reserve) => RefreshAmmo();
    /// <summary>
    /// 현재 연결된 무기의 탄약 상태를 다시 읽어 표시한다.
    /// </summary>
    private void RefreshAmmo()
    {
        if (ammoText == null) return;
        if (weapon == null) ammoText.text = "";
        else if (weapon.HasUnlimitedReserve) ammoText.text = $"{weapon.CurrentAmmo} / ∞";
        else ammoText.text = $"{weapon.CurrentAmmo} / {weapon.ReserveAmmo} ({weapon.TotalAmmo})";
    }
    /// <summary>
    /// 장비 변경에 맞추어 인벤토리 또는 HUD의 선택 표시를 갱신한다.
    /// </summary>
    private void EquipmentChanged(DemoAction action, Object target) { if (equipmentText != null && equipment != null) equipmentText.text = equipment.CurrentKind.ToString(); }
    /// <summary>
    /// 실제 아이템 획득 알림을 현재 튜토리얼 또는 UI 표시로 전달한다.
    /// </summary>
    private void ItemAdded(ItemData item, int quantity)
    {
        if (acquisitionText != null) acquisitionText.text = $"{item.ItemName} +{quantity}";
        noticeUntil = Time.unscaledTime + noticeDuration;
    }
    /// <summary>
    /// 현재 게임 목표 문구를 UI에 표시한다.
    /// </summary>
    public void SetObjective(string text) { if (objectiveText != null) objectiveText.text = text; }
    /// <summary>
    /// 현재 조작 안내 문구를 UI에 표시한다.
    /// </summary>
    public void SetInstruction(string text, int count, int required)
    {
        if (instructionText != null) instructionText.text = text;
        if (countText != null) countText.text = $"{count}/{required}";
    }
    /// <summary>
    /// 훈련 코치 안내 문구를 UI에 표시한다.
    /// </summary>
    public void SetCoachText(string text)
    {
        if (coachText != null) coachText.text = text;
        coachUntil = Time.unscaledTime + noticeDuration;
    }
    /// <summary>
    /// 데모 종료 화면 표시를 요청한다.
    /// </summary>
    public void ShowDemoEnd() { IsDemoEndVisible = true; if (endPanel != null) endPanel.SetActive(true); }
    /// <summary>
    /// 남은 HUD·종료·안내 표시를 초기 상태로 되돌린다.
    /// </summary>
    public void ResetDisplay()
    {
        TutorialManager owner = presentationOwner;
        int request = presentationRequest;
        CancelTutorialPresentation(owner);
        if (owner != null) owner.OnTutorialPresentationCancelled(request);
        IsDemoEndVisible = false;
        hiddenOwners.Clear();
        UpdateVisibility();
        if (endPanel != null) endPanel.SetActive(false);
        SetObjective("");
        SetInstruction("", 0, 0);
        if (coachText != null) coachText.text = "";
        if (acquisitionText != null) acquisitionText.text = "";
        noticeUntil = coachUntil = 0f;
        RefreshAmmo();
    }
    /// <summary>
    /// 진행 중 연출과 UI·전투를 정리한 뒤 기존 초기 상태 및 재시작 정책을 적용한다.
    /// </summary>
    public void RestartDemo() { if (flow != null) flow.RestartDemo(); }
    /// <summary>
    /// HUD를 숨기는 요청 소유자를 등록한다.
    /// </summary>
    public void AcquireHidden(Object owner) { if (owner != null) hiddenOwners.Add(owner); UpdateVisibility(); }
    /// <summary>
    /// 지정 소유자의 HUD 숨김 요청만 해제한다.
    /// </summary>
    public void ReleaseHidden(Object owner) { if (owner != null) hiddenOwners.Remove(owner); UpdateVisibility(); }
    /// <summary>
    /// 현재 숨김 소유자와 종료 상태에 맞추어 화면 표시 여부를 적용한다.
    /// </summary>
    private void UpdateVisibility()
    {
        if (hudGroup == null) return;
        hudGroup.alpha = hiddenOwners.Count > 0 ? 0f : originalHudAlpha;
        hudGroup.blocksRaycasts = hiddenOwners.Count == 0 && originalHudBlocksRaycasts;
        hudGroup.interactable = hiddenOwners.Count == 0 && originalHudInteractable;
    }
    /// <summary>
    /// 튜토리얼 안내 문구와 표시 상태를 현재 데이터로 갱신한다.
    /// </summary>
    public void RefreshTutorial()
    {
        // 다음 단계 문구는 다음 Show 요청 전까지 이전 Hide 화면에 쓰지 않는다.
        if (tutorial == null || !tutorial.IsPresenting || tutorial.CurrentStep == null) return;
        int step = tutorial.StepNumber;
        if (step != displayedStep) stepFeedbackUntil = 0f;
        if (tutorial.DisplayState == TutorialManager.TutorialDisplayState.Hiding)
            stepFeedbackUntil = Time.unscaledTime + stepFeedbackDuration;
        displayedStep = step;
        TutorialManager.Step current = tutorial.CurrentStep;
        if (tutorialTitleText != null) tutorialTitleText.text = current.title;
        if (instructionText != null) instructionText.text = current.instruction;
        string countLabel = current.action == DemoAction.ShotHit ? "명중" : "성공";
        if (countText != null) countText.text = $"단계 {step} / {tutorial.StepTotal} · {countLabel} {tutorial.CurrentCount} / {Mathf.Max(1, current.requiredCount)}";
    }
    /// <summary>
    /// 현재 연결된 튜토리얼의 재시작을 요청한다.
    /// </summary>
    public void RestartTutorial() { if (tutorial != null) tutorial.RestartIndependentTest(); }

    private bool TutorialHUDAllowed => isActiveAndEnabled && !IsDemoEndVisible && hiddenOwners.Count == 0 &&
        (status == null || !status.IsDead) && (dialogueUI == null || !dialogueUI.BlocksGameplay) &&
        (inventoryUI == null || !inventoryUI.IsOpen);

    /// <summary>
    /// 현재 안내의 표시 상태에서 행동 성공을 집계해도 되는지 확인한다.
    /// </summary>
    public bool CanCountTutorial(TutorialManager owner) => owner != null && owner == tutorial &&
        presentationOwner == owner && TutorialHUDAllowed && tutorialPanel != null && tutorialPanel.activeInHierarchy;

    /// <summary>
    /// 현재 안내 요청을 표시할 수 있는지 검사하고 애니메이션을 요청한다.
    /// </summary>
    public bool TryShowTutorial(TutorialManager owner, int request)
    {
        if (owner == null || owner != tutorial || owner.DisplayState != TutorialManager.TutorialDisplayState.Showing ||
            !TutorialHUDAllowed || presentationOwner != null) return false;
        return PlayTutorialAnimation(owner, request, true);
    }

    /// <summary>
    /// 현재 안내 요청을 숨길 수 있는지 검사하고 애니메이션을 요청한다.
    /// </summary>
    public bool TryHideTutorial(TutorialManager owner, int request)
    {
        if (owner == null || owner != tutorial || owner.DisplayState != TutorialManager.TutorialDisplayState.Hiding ||
            presentationOwner != owner || tutorialAnimation != null) return false;
        return PlayTutorialAnimation(owner, request, false);
    }

    /// <summary>
    /// 실제 Animator·Clip·상태 연결을 확인한 뒤 안내 애니메이션 대기를 시작한다.
    /// </summary>
    private bool PlayTutorialAnimation(TutorialManager owner, int request, bool show)
    {
        AnimationClip clip = show ? tutorialShowClip : tutorialHideClip;
        string state = show ? tutorialShowState : tutorialHideState;
        if (tutorialPanel == null || tutorialAnimator == null || tutorialCanvasGroup == null ||
            tutorialAnimator.gameObject != tutorialPanel || tutorialCanvasGroup.gameObject != tutorialPanel ||
            !tutorialAnimator.enabled || tutorialAnimator.runtimeAnimatorController == null ||
            tutorialAnimator.speed <= 0f || clip == null || clip.isLooping || string.IsNullOrWhiteSpace(state))
        {
            WarnTutorialAnimation();
            CancelTutorialPresentation(owner);
            return false;
        }
        bool hasClip = false;
        foreach (AnimationClip bound in tutorialAnimator.runtimeAnimatorController.animationClips)
            if (bound == clip) { hasClip = true; break; }
        if (!hasClip)
        {
            WarnTutorialAnimation();
            CancelTutorialPresentation(owner);
            return false;
        }
        // Show 0프레임이 평가되기 전에도 이전 표시가 한 프레임 노출되지 않게 한다.
        if (show) tutorialCanvasGroup.alpha = 0f;
        tutorialCanvasGroup.interactable = false;
        tutorialCanvasGroup.blocksRaycasts = false;
        tutorialPanel.SetActive(true);
        int hash = Animator.StringToHash(state);
        presentationOwner = owner;
        presentationRequest = request;
        int version = ++presentationVersion;
        tutorialAnimation = StartCoroutine(WaitForTutorialAnimation(owner, request, version, hash, clip, show));
        UpdateTutorialVisibility();
        return true;
    }

    /// <summary>
    /// 요청 번호와 실제 재생·종료 상태를 확인하며 안내 애니메이션 완료를 기다린다.
    /// </summary>
    private IEnumerator WaitForTutorialAnimation(TutorialManager owner, int request, int version,
        int stateHash, AnimationClip clip, bool show)
    {
        float elapsed = 0f;
        // 비활성 패널의 Animator가 초기화된 뒤 상태를 검사하고 처음부터 재생한다.
        yield return null;
        if (version != presentationVersion || presentationOwner != owner || presentationRequest != request) yield break;
        if (tutorialAnimator == null || !tutorialAnimator.isActiveAndEnabled || !tutorialAnimator.HasState(0, stateHash))
        {
            FailTutorialAnimation(owner, request);
            yield break;
        }
        tutorialAnimator.Play(stateHash, 0, 0f);
        yield return null;
        while (version == presentationVersion && presentationOwner == owner && presentationRequest == request)
        {
            if (tutorialAnimator == null || tutorialPanel == null || !tutorialAnimator.enabled ||
                tutorialAnimator.runtimeAnimatorController == null)
            {
                FailTutorialAnimation(owner, request);
                yield break;
            }
            // 대화/인벤토리/HUD 잠금 동안에는 상태·재생 시간을 보존하고 성공을 집계하지 않는다.
            if (!TutorialHUDAllowed || !tutorialPanel.activeInHierarchy)
            {
                yield return null;
                continue;
            }
            AnimatorStateInfo state = tutorialAnimator.GetCurrentAnimatorStateInfo(0);
            AnimatorClipInfo[] clips = tutorialAnimator.GetCurrentAnimatorClipInfo(0);
            if (state.fullPathHash != stateHash || clips.Length == 0 || clips[0].clip != clip || state.loop)
            {
                FailTutorialAnimation(owner, request);
                yield break;
            }
            if (!tutorialAnimator.IsInTransition(0) && state.normalizedTime >= 1f)
            {
                tutorialAnimation = null;
                if (show) owner.OnTutorialShown(request);
                else
                {
                    // Hide 마지막 프레임이 평가된 뒤에만 끈다. 이전 실행의 정리는 콜백 전에 끝낸다.
                    presentationOwner = null;
                    tutorialWasVisible = false;
                    tutorialPanel.SetActive(false);
                    owner.OnTutorialHidden(request);
                }
                yield break;
            }
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= tutorialAnimationTimeout)
            {
                // 시간 초과는 실패이며 정상 완료로 취급하지 않는다.
                FailTutorialAnimation(owner, request);
                yield break;
            }
            yield return null;
        }
    }

    /// <summary>
    /// 표시 애니메이션이 실패했을 때 현재 요청을 정리하고 실패를 전달한다.
    /// </summary>
    private void FailTutorialAnimation(TutorialManager owner, int request)
    {
        tutorialAnimation = null;
        WarnTutorialAnimation();
        CancelTutorialPresentation(owner);
        if (owner != null) owner.OnTutorialPresentationCancelled(request);
    }

    /// <summary>
    /// 잘못 연결된 튜토리얼 애니메이션 설정의 경고를 출력한다.
    /// </summary>
    private void WarnTutorialAnimation()
    {
        if (warnedTutorialAnimation) return;
        warnedTutorialAnimation = true;
        Debug.LogWarning("GameplayUI: 튜토리얼 Panel의 Animator/CanvasGroup, Show·Hide 클립과 상태를 확인하세요. 표시 요청을 취소합니다.", this);
    }

    /// <summary>
    /// 진행 중 안내 Coroutine과 표시 요청을 취소 정리한다.
    /// </summary>
    public void CancelTutorialPresentation(TutorialManager owner)
    {
        if (presentationOwner != null && presentationOwner != owner) return;
        if (presentationOwner == null && owner != null && owner != tutorial) return;
        presentationVersion++;
        if (tutorialAnimation != null) StopCoroutine(tutorialAnimation);
        tutorialAnimation = null;
        presentationOwner = null;
        tutorialWasVisible = false;
        stepFeedbackUntil = 0f;
        if (tutorialPanel != null) tutorialPanel.SetActive(false);
    }

    /// <summary>
    /// 현재 안내 상태에 맞추어 표시 오브젝트의 활성 상태를 적용한다.
    /// </summary>
    private void UpdateTutorialVisibility()
    {
        bool visible = tutorial != null && tutorial.IsPresenting && presentationOwner == tutorial && TutorialHUDAllowed;
        if (visible && tutorialWasVisible && tutorialPanel != null && !tutorialPanel.activeSelf)
        {
            TutorialManager owner = presentationOwner;
            int request = presentationRequest;
            CancelTutorialPresentation(owner);
            owner.OnTutorialPresentationCancelled(request);
            return;
        }
        if (visible && !tutorialWasVisible) RefreshTutorial();
        tutorialWasVisible = visible;
        if (tutorialPanel != null && tutorialPanel.activeSelf != visible) tutorialPanel.SetActive(visible);
        if (tutorialBackground != null) tutorialBackground.color = Time.unscaledTime < stepFeedbackUntil
            ? new Color(0.08f, 0.3f, 0.19f, 0.88f) : new Color(0.025f, 0.035f, 0.055f, 0.82f);
    }
    /// <summary>
    /// 퀵슬롯 등록과 실제 아이템 총량·선택을 HUD에 갱신한다.
    /// </summary>
    public void RefreshEquippedItems()
    {
        if (inventory == null) return;
        for (int i = 0; i < PlayerInventory.EquippedSlotCount; i++)
        {
            InventoryEntry entry = inventory.GetEntry(true, i);
            ItemData item = entry != null && entry.Quantity > 0 ? entry.Item : null;
            if (equippedItemIcons != null && i < equippedItemIcons.Length && equippedItemIcons[i] != null)
            {
                equippedItemIcons[i].sprite = item != null ? item.Icon : null;
                equippedItemIcons[i].enabled = item != null && item.Icon != null;
                equippedItemIcons[i].raycastTarget = false;
            }
            if (equippedItemQuantities != null && i < equippedItemQuantities.Length && equippedItemQuantities[i] != null)
                equippedItemQuantities[i].text = item != null ? entry.Quantity.ToString() : "";
            if (equippedItemBackgrounds != null && i < equippedItemBackgrounds.Length && equippedItemBackgrounds[i] != null)
                equippedItemBackgrounds[i].color = inventory.SelectedEquippedSlot == i
                    ? new Color(0.15f, 0.48f, 0.68f, 0.9f) : new Color(0.025f, 0.035f, 0.055f, 0.82f);
        }
        InventoryEntry selected = inventory.SelectedEntry;
        if (selectedItemText != null) selectedItemText.text = selected != null && selected.Item != null
            ? selected.Item.ItemName + (inventory.IsUseLocked ? " · 사용 중" : " · 4 사용") : "";
    }
    /// <summary>
    /// 대화·인벤토리·숨김 소유권에 따라 HUD를 표시하고 체력·스태미나·장비 및 안내 만료를 갱신한다.
    /// </summary>
    private void Update()
    {
        UpdateTutorialVisibility();
        if (equippedItemPanel != null) equippedItemPanel.SetActive(hiddenOwners.Count == 0 && !IsDemoEndVisible &&
            (status == null || !status.IsDead) && (dialogueUI == null || !dialogueUI.BlocksGameplay) && (inventoryUI == null || !inventoryUI.IsOpen));
        if (status != null)
        {
            if (healthFill != null) healthFill.fillAmount = status.HealthNormalized;
            if (staminaFill != null) staminaFill.fillAmount = status.StaminaNormalized;
        }
        if (equipmentText != null && equipment != null) equipmentText.text = equipment.CurrentKind.ToString();
        if (acquisitionText != null && Time.unscaledTime >= noticeUntil) acquisitionText.text = "";
        if (coachText != null && Time.unscaledTime >= coachUntil) coachText.text = "";
    }
}
