using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>인물 목록과 파일 상세 화면을 전환한다. 기존 아이템 DetailPanel과는 독립적인 표시 컴포넌트다.</summary>
public class AffinityPanelUI : MonoBehaviour
{
    [System.Serializable]
    private class StatusRow
    {
        public PersonnelStatKind kind;
        public TMP_Text label;
        public TMP_Text value;
        public Image fill;
    }

    [SerializeField] private AffinityDataStore dataStore;
    [SerializeField] private GameObject characterListPage;
    [SerializeField] private GameObject characterDetailPage;
    [SerializeField] private Button utopiaButton;
    [SerializeField] private Button dystopiaButton;
    [SerializeField] private TMP_Text listTitle;
    [SerializeField] private ScrollRect listScroll;
    [SerializeField] private Transform cardContent;
    [SerializeField] private CharacterCardUI cardPrefab;
    [SerializeField] private Image characterPortrait;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private TMP_Text characterGroupText;
    [SerializeField] private TMP_Text characterDescriptionText;
    [SerializeField] private TMP_Text affinityValueText;
    [SerializeField] private Image affinityFill;
    [SerializeField] private StatusRow[] statusRows = new StatusRow[0];
    [SerializeField] private Color selectedGroupColor = new Color(0.15f, 0.4f, 0.52f, 1f);
    [SerializeField] private Color normalGroupColor = new Color(0.15f, 0.19f, 0.24f, 1f);
    [Header("Card to Personnel File Transition")]
    [SerializeField] private RectTransform transitionOverlay;
    [SerializeField] private RectTransform movingCharacterFile;
    [SerializeField] private CanvasGroup movingFileGroup;
    [SerializeField] private CanvasGroup movingCardLayer;
    [SerializeField] private CanvasGroup movingDocumentLayer;
    [SerializeField] private Image movingCardImage;
    [SerializeField] private Image movingPortrait;
    [SerializeField] private TMP_Text movingName;
    [SerializeField] private RectTransform filePortraitFrame;
    [SerializeField] private CanvasGroup portraitGroup;
    [SerializeField] private CanvasGroup detailInputGroup;
    [SerializeField, Min(0.01f)] private float cardToFileDuration = 0.18f;
    [SerializeField, Min(0.01f)] private float fileMoveDuration = 0.5f;
    [SerializeField, Min(0.01f)] private float portraitFadeDuration = 0.2f;
    private Coroutine transitionRoutine;
    private int transitionRevision;
    private bool isTransitioning;
    public bool IsTransitioning => isTransitioning;
    public bool CanSelectCards => isActiveAndEnabled && !isTransitioning && characterListPage != null && characterListPage.activeInHierarchy;

    private readonly List<CharacterCardUI> cards = new List<CharacterCardUI>();
    private AffinityGroup currentGroup;
    private string selectedCharacterId;
    private float savedScrollPosition = 1f;
    public AffinityGroup CurrentGroup => currentGroup;
    public string SelectedCharacterId => selectedCharacterId;

    private void OnEnable()
    {
        // 표시 중인 인물의 변경만 반영한다. 페이지 비활성화 시 아래 구독을 해제한다.
        if (dataStore != null)
        {
            dataStore.Changed += HandleRecordChanged;
            dataStore.CharacterUnlocked += HandleCharacterUnlocked;
        }
        // 페이지를 다시 열어도 데이터는 유지하고 화면만 기본 목록으로 되돌린다.
        selectedCharacterId = null;
        ShowUtopia();
    }

    private void OnDisable()
    {
        CancelTransition();
        if (dataStore != null)
        {
            dataStore.Changed -= HandleRecordChanged;
            dataStore.CharacterUnlocked -= HandleCharacterUnlocked;
        }
        selectedCharacterId = null;
    }

    public void ShowUtopia() { ShowGroup(AffinityGroup.Utopia); }
    public void ShowDystopia() { ShowGroup(AffinityGroup.Dystopia); }

    private void ShowGroup(AffinityGroup group)
    {
        CancelTransition();
        currentGroup = group;
        selectedCharacterId = null;
        if (characterDetailPage != null) characterDetailPage.SetActive(false);
        if (characterListPage != null) characterListPage.SetActive(true);
        if (utopiaButton != null) utopiaButton.image.color = group == AffinityGroup.Utopia ? selectedGroupColor : normalGroupColor;
        if (dystopiaButton != null) dystopiaButton.image.color = group == AffinityGroup.Dystopia ? selectedGroupColor : normalGroupColor;
        if (listTitle != null) listTitle.text = group.ToString().ToUpperInvariant() + " / 임시 인물 기록";
        RefreshCards();
        if (listScroll != null)
        {
            listScroll.StopMovement();
            listScroll.verticalNormalizedPosition = 1f;
        }
        savedScrollPosition = 1f;
    }

    /// <summary>현재 그룹과 스크롤 위치를 유지하며 해금된 카드만 재사용해 표시한다.</summary>
    private void RefreshCards()
    {
        if (dataStore == null || cardPrefab == null || cardContent == null) return;
        int count = 0;
        for (int i = 0; i < dataStore.CharacterCount; i++)
        {
            AffinityCharacterData data = dataStore.GetCharacter(i);
            if (data == null || data.Group != currentGroup || !dataStore.IsUnlocked(data.CharacterId)) continue;
            if (count >= cards.Count) cards.Add(Instantiate(cardPrefab, cardContent));
            cards[count].Bind(data, this);
            cards[count].gameObject.SetActive(true);
            count++;
        }
        for (int i = count; i < cards.Count; i++) cards[i].gameObject.SetActive(false);
    }

    private void HandleCharacterUnlocked(AffinityCharacterData data)
    {
        if (data != null && data.Group == currentGroup && characterListPage != null && characterListPage.activeSelf)
        {
            RefreshCards();
        }
    }

    public void ShowCharacter(string characterId)
    {
        RectTransform source = null;
        foreach (CharacterCardUI card in cards)
        {
            if (card != null && card.isActiveAndEnabled && card.CharacterId == characterId)
            {
                source = card.TransitionRect;
                break;
            }
        }
        ShowCharacter(characterId, source);
    }

    /// <summary>클릭한 카드의 현재 화면 영역을 먼저 복사한다. 전환 중 다른 선택은 받지 않는다.</summary>
    public void ShowCharacter(string characterId, RectTransform source)
    {
        if (isTransitioning || !isActiveAndEnabled || dataStore == null || !dataStore.IsUnlocked(characterId)) return;
        selectedCharacterId = characterId;
        if (listScroll != null)
        {
            listScroll.StopMovement();
            savedScrollPosition = listScroll.verticalNormalizedPosition;
        }
        RefreshCharacter();
        if (source == null || !HasTransitionSetup())
        {
            // 외부 API로 보이지 않는 인물을 지정했거나 필수 참조가 없으면 임시 UI를 남기지 않는다.
            if (characterListPage != null) characterListPage.SetActive(false);
            if (characterDetailPage != null) characterDetailPage.SetActive(true);
            return;
        }
        Vector2 position, size;
        if (!TryGetOverlayRect(source, out position, out size)) return;
        isTransitioning = true;
        transitionRevision++;
        transitionRoutine = StartCoroutine(PlayCharacterTransition(characterId, position, size, transitionRevision));
    }

    private bool HasTransitionSetup()
    {
        return transitionOverlay != null && movingCharacterFile != null && movingFileGroup != null &&
            movingCardLayer != null && movingDocumentLayer != null && movingCardImage != null &&
            movingPortrait != null && filePortraitFrame != null && portraitGroup != null && detailInputGroup != null;
    }

    /// <summary>CanvasScaler와 각 RectTransform의 부모 차이를 화면 좌표를 경유해 Overlay 로컬 좌표로 통일한다.</summary>
    private bool TryGetOverlayRect(RectTransform source, out Vector2 position, out Vector2 size)
    {
        position = Vector2.zero;
        size = Vector2.zero;
        if (source == null || transitionOverlay == null) return false;
        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        Camera sourceCamera = sourceCanvas != null && sourceCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? sourceCanvas.worldCamera : null;
        Canvas overlayCanvas = transitionOverlay.GetComponentInParent<Canvas>();
        Camera overlayCamera = overlayCanvas != null && overlayCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? overlayCanvas.worldCamera : null;
        Vector3[] corners = new Vector3[4];
        source.GetWorldCorners(corners);
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (Vector3 corner in corners)
        {
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(sourceCamera, corner);
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(transitionOverlay, screen, overlayCamera, out local)) return false;
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }
        position = (min + max) * 0.5f;
        size = max - min;
        return size.x > 0f && size.y > 0f;
    }

    /// <summary>하나의 인물 ID를 유지하며 카드→파일 이동→실제 초상화 Fade를 Unscaled Time으로 실행한다.</summary>
    private IEnumerator PlayCharacterTransition(string characterId, Vector2 startPosition, Vector2 startSize, int revision)
    {
        AffinityDataStore.CharacterState state = dataStore.GetState(characterId);
        AffinityCharacterData data = state.Data;
        movingCardImage.sprite = data.CharacterCardImage != null ? data.CharacterCardImage : data.Portrait;
        movingCardImage.preserveAspect = true;
        movingCardImage.color = movingCardImage.sprite != null ? Color.white : new Color(0.2f, 0.27f, 0.32f, 1f);
        movingPortrait.sprite = data.Portrait;
        movingPortrait.preserveAspect = true;
        movingPortrait.color = data.Portrait != null ? Color.white : new Color(0.2f, 0.27f, 0.32f, 1f);
        if (movingName != null) movingName.text = "PERSONNEL FILE";
        portraitGroup.alpha = 0f;
        detailInputGroup.interactable = false;
        detailInputGroup.blocksRaycasts = false;
        movingFileGroup.alpha = 1f;
        movingCardLayer.alpha = 1f;
        movingDocumentLayer.alpha = 0f;
        movingCharacterFile.anchoredPosition = startPosition;
        movingCharacterFile.sizeDelta = startSize;
        transitionOverlay.gameObject.SetActive(true);
        transitionOverlay.SetAsLastSibling();
        // 클릭한 카드와 같은 위치에서 최소 한 프레임 표시한 뒤 상세 페이지를 준비한다.
        yield return null;
        if (revision != transitionRevision) yield break;
        if (characterListPage != null) characterListPage.SetActive(false);
        if (characterDetailPage != null) characterDetailPage.SetActive(true);
        yield return null;
        Vector2 targetPosition, targetSize;
        if (!TryGetOverlayRect(filePortraitFrame, out targetPosition, out targetSize))
        {
            FinishTransition();
            yield break;
        }
        // 먼저 제자리에서 카드가 실제 파일 이미지로 변한 뒤 이동한다.
        // 두 단계를 분리하여 이동 중 카드 잔상이 계속 겹쳐 보이지 않게 한다.
        float elapsed = 0f;
        while (elapsed < cardToFileDuration && revision == transitionRevision)
        {
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / cardToFileDuration));
            movingCardLayer.alpha = 1f - progress;
            movingDocumentLayer.alpha = progress;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (revision != transitionRevision) yield break;
        movingCardLayer.alpha = 0f;
        movingDocumentLayer.alpha = 1f;

        elapsed = 0f;
        while (elapsed < fileMoveDuration && revision == transitionRevision)
        {
            float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fileMoveDuration));
            movingCharacterFile.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, progress);
            movingCharacterFile.sizeDelta = Vector2.Lerp(startSize, targetSize, progress);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (revision != transitionRevision) yield break;
        movingCharacterFile.anchoredPosition = targetPosition;
        movingCharacterFile.sizeDelta = targetSize;
        movingCardLayer.alpha = 0f;
        movingDocumentLayer.alpha = 1f;
        elapsed = 0f;
        while (elapsed < portraitFadeDuration && revision == transitionRevision)
        {
            float progress = Mathf.Clamp01(elapsed / portraitFadeDuration);
            movingFileGroup.alpha = 1f - progress;
            portraitGroup.alpha = progress;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        if (revision == transitionRevision) FinishTransition();
    }

    private void FinishTransition()
    {
        if (transitionOverlay != null) transitionOverlay.gameObject.SetActive(false);
        if (movingFileGroup != null) movingFileGroup.alpha = 0f;
        if (portraitGroup != null) portraitGroup.alpha = 1f;
        if (detailInputGroup != null)
        {
            detailInputGroup.interactable = true;
            detailInputGroup.blocksRaycasts = true;
        }
        isTransitioning = false;
        transitionRoutine = null;
    }

    private void CancelTransition()
    {
        transitionRevision++;
        if (transitionRoutine != null) StopCoroutine(transitionRoutine);
        FinishTransition();
    }

    public void CloseCharacter()
    {
        CancelTransition();
        if (characterDetailPage != null) characterDetailPage.SetActive(false);
        if (characterListPage != null) characterListPage.SetActive(true);
        selectedCharacterId = null;
        RefreshCards();
        if (listScroll != null)
        {
            listScroll.StopMovement();
            listScroll.verticalNormalizedPosition = savedScrollPosition;
        }
    }

    private void HandleRecordChanged(string characterId)
    {
        if (selectedCharacterId == characterId) RefreshCharacter();
    }

    private void RefreshCharacter()
    {
        AffinityDataStore.CharacterState state = dataStore.GetState(selectedCharacterId);
        if (state == null) return;
        AffinityCharacterData data = state.Data;
        if (characterNameText != null) characterNameText.text = data.CharacterName;
        if (characterGroupText != null) characterGroupText.text = "소속: " + data.Group.ToString().ToUpperInvariant();
        if (characterDescriptionText != null) characterDescriptionText.text = data.Description;
        if (characterPortrait != null)
        {
            characterPortrait.sprite = data.Portrait;
            characterPortrait.preserveAspect = true;
            characterPortrait.color = data.Portrait != null ? Color.white : new Color(0.2f, 0.27f, 0.32f, 1f);
        }
        if (affinityValueText != null) affinityValueText.text = "호감도 " + state.Affinity + " / " + data.AffinityMaximum;
        SetBar(affinityFill, state.Affinity, data.AffinityMaximum);
        foreach (StatusRow row in statusRows)
        {
            if (row == null) continue;
            int value = state.GetStatus(row.kind);
            if (row.label != null) row.label.text = GetStatusLabel(row.kind);
            if (row.value != null) row.value.text = value + " / " + data.StatusMaximum;
            SetBar(row.fill, value, data.StatusMaximum);
        }
    }

    /// <summary>Sprite 생성 없이 Image의 가로 표시 영역만 조절한다.</summary>
    private void SetBar(Image fill, int value, int maximum)
    {
        if (fill == null) return;
        float ratio = Mathf.Clamp01((float)value / Mathf.Max(1, maximum));
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = new Vector2(ratio, 1f);
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;
    }

    private string GetStatusLabel(PersonnelStatKind kind)
    {
        switch (kind)
        {
            case PersonnelStatKind.Health: return "체력 (HP)";
            case PersonnelStatKind.Attack: return "공격력 (ATK)";
            case PersonnelStatKind.Defense: return "방어력 (DEF)";
            case PersonnelStatKind.Agility: return "민첩성 (AGI)";
            default: return string.Empty;
        }
    }
}
