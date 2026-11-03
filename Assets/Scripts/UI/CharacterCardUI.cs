using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>카드의 표시와 포인터 효과를 관리한다. Grid가 소유하는 루트의 크기·Scale은 변경하지 않는다.</summary>
public class CharacterCardUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Button button;
    [SerializeField] private Image portrait;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private Image cardImage;
    [SerializeField] private GameObject hoverBorder;
    [SerializeField, Min(0.01f)] private float hoverScale = 1.05f;
    [SerializeField, Min(0.01f)] private float pressedScale = 0.96f;
    [SerializeField, Min(0.01f)] private float interactionDuration = 0.18f;
    private AffinityCharacterData data;
    private AffinityPanelUI owner;
    private bool hasBoundButton;
    private bool isHovered;
    private bool isPressed;
    private bool hasCapturedScale;
    private Vector3 originalScale = Vector3.one;
    private float currentScale = 1f;
    private float startScale = 1f;
    private float targetScale = 1f;
    private float scaleElapsed;
    public string CharacterId => data != null ? data.CharacterId : null;
    public RectTransform TransitionRect => visualRoot != null ? visualRoot : transform as RectTransform;

    private void Awake() { BindButton(); ResetInteraction(); }
    private void OnEnable() { ResetInteraction(); }
    private void OnDisable() { ResetInteraction(); }

    private void BindButton()
    {
        if (hasBoundButton || button == null) return;
        button.onClick.AddListener(HandleClick);
        hasBoundButton = true;
    }

    /// <summary>완성 카드와 상세 초상화는 별도 참조다. 완성 카드가 없으면 기존 사진 영역과 이름을 유지한다.</summary>
    public void Bind(AffinityCharacterData character, AffinityPanelUI panel)
    {
        data = character;
        owner = panel;
        BindButton();
        ResetInteraction();
        bool hasCard = data != null && data.CharacterCardImage != null && cardImage != null;
        if (cardImage != null)
        {
            cardImage.sprite = hasCard ? data.CharacterCardImage : null;
            cardImage.preserveAspect = true;
            cardImage.gameObject.SetActive(hasCard);
        }
        if (characterNameText != null)
        {
            characterNameText.text = data != null ? data.CharacterName : string.Empty;
            characterNameText.gameObject.SetActive(!hasCard);
        }
        if (portrait != null)
        {
            portrait.sprite = data != null ? data.Portrait : null;
            portrait.preserveAspect = true;
            // 사진이 없으면 남색 자리표시자 대신 이름만 표시한다.
            portrait.enabled = portrait.sprite != null;
            portrait.color = portrait.sprite != null ? Color.white : new Color(0.2f, 0.27f, 0.32f, 1f);
            portrait.gameObject.SetActive(!hasCard);
        }
    }

    private void HandleClick()
    {
        if (data != null && owner != null && owner.CanSelectCards)
            owner.ShowCharacter(data.CharacterId, TransitionRect);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = owner != null && owner.CanSelectCards;
        RefreshBorder();
    }
    public void OnPointerExit(PointerEventData eventData) { isHovered = false; RefreshBorder(); }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && owner != null && owner.CanSelectCards) isPressed = true;
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left) isPressed = false;
    }

    private void Update()
    {
        if (owner == null || !owner.CanSelectCards) { ResetInteraction(); return; }
        float nextScale = 1f;
        if (isPressed) nextScale = pressedScale;
        else if (isHovered) nextScale = hoverScale;
        if (!Mathf.Approximately(nextScale, targetScale))
        {
            startScale = currentScale;
            targetScale = nextScale;
            scaleElapsed = 0f;
        }
        // 인벤토리의 시간 정지 여부와 관계없이 약 0.18초 동안 내부 시각 요소만 보간한다.
        scaleElapsed += Time.unscaledDeltaTime;
        float progress = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(scaleElapsed / interactionDuration));
        currentScale = Mathf.Lerp(startScale, targetScale, progress);
        if (visualRoot != null) visualRoot.localScale = originalScale * currentScale;
    }

    private void ResetInteraction()
    {
        // 비활성 카드에서는 Awake보다 Bind가 먼저 실행될 수도 있다.
        if (!hasCapturedScale && visualRoot != null)
        {
            originalScale = visualRoot.localScale;
            hasCapturedScale = true;
        }
        isHovered = false;
        isPressed = false;
        currentScale = startScale = targetScale = 1f;
        scaleElapsed = 0f;
        if (visualRoot != null) visualRoot.localScale = originalScale;
        RefreshBorder();
    }
    private void RefreshBorder()
    {
        if (hoverBorder != null) hoverBorder.SetActive(isHovered && owner != null && owner.CanSelectCards);
    }
    private void OnDestroy()
    {
        if (hasBoundButton && button != null) button.onClick.RemoveListener(HandleClick);
    }
}
