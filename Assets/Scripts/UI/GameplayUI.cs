using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameplayUI : MonoBehaviour
{
    [Header("Tutorial")]
    [SerializeField] private TutorialManager tutorial;
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TMP_Text tutorialTitleText;
    [SerializeField] private Image tutorialBackground;
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private InventoryUI inventoryUI;
    [SerializeField, Min(0f)] private float stepFeedbackDuration = 0.35f;
    private float stepFeedbackUntil;
    private int displayedStep = -1;
    private bool tutorialWasVisible;
    private bool displayedCompletion;
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
    private void Awake()
    {
        if (hudGroup == null) return;
        originalHudAlpha = hudGroup.alpha;
        originalHudInteractable = hudGroup.interactable;
        originalHudBlocksRaycasts = hudGroup.blocksRaycasts;
    }
    private void OnEnable()
    {
        if (tutorial != null) tutorial.Changed += RefreshTutorial;
        RefreshTutorial();
        if (equipment != null) { equipment.OnWeaponChanged += WeaponChanged; equipment.Succeeded += EquipmentChanged; WeaponChanged(equipment.CurrentWeapon); }
        if (inventory != null) { inventory.ItemAdded += ItemAdded; inventory.Changed += RefreshEquippedItems; }
        RefreshEquippedItems();
        UpdateVisibility();
    }
    private void OnDisable()
    {
        if (tutorial != null) tutorial.Changed -= RefreshTutorial;
        displayedStep = -1;
        stepFeedbackUntil = 0f;
        tutorialWasVisible = false;
        if (equipment != null) { equipment.OnWeaponChanged -= WeaponChanged; equipment.Succeeded -= EquipmentChanged; }
        if (inventory != null) { inventory.ItemAdded -= ItemAdded; inventory.Changed -= RefreshEquippedItems; }
        WeaponChanged(null);
    }
    private void WeaponChanged(WeaponAssaultRifle next)
    {
        if (weapon != null) { weapon.onAmmoEvent.RemoveListener(AmmoChanged); weapon.onReserveAmmoEvent.RemoveListener(ReserveChanged); }
        weapon = next;
        if (weapon != null) { weapon.onAmmoEvent.AddListener(AmmoChanged); weapon.onReserveAmmoEvent.AddListener(ReserveChanged); }
        RefreshAmmo();
    }
    private void AmmoChanged(int loaded, int capacity) => RefreshAmmo();
    private void ReserveChanged(int reserve) => RefreshAmmo();
    private void RefreshAmmo() { if (ammoText != null) ammoText.text = weapon != null ? $"{weapon.CurrentAmmo} / {weapon.ReserveAmmo} ({weapon.TotalAmmo})" : ""; }
    private void EquipmentChanged(DemoAction action, Object target) { if (equipmentText != null && equipment != null) equipmentText.text = equipment.CurrentKind.ToString(); }
    private void ItemAdded(ItemData item, int quantity)
    {
        if (acquisitionText != null) acquisitionText.text = $"{item.ItemName} +{quantity}";
        noticeUntil = Time.unscaledTime + noticeDuration;
    }
    public void SetObjective(string text) { if (objectiveText != null) objectiveText.text = text; }
    public void SetInstruction(string text, int count, int required)
    {
        if (instructionText != null) instructionText.text = text;
        if (countText != null) countText.text = $"{count}/{required}";
    }
    public void SetCoachText(string text)
    {
        if (coachText != null) coachText.text = text;
        coachUntil = Time.unscaledTime + noticeDuration;
    }
    public void ShowDemoEnd() { IsDemoEndVisible = true; if (endPanel != null) endPanel.SetActive(true); }
    public void ResetDisplay()
    {
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
    public void RestartDemo() { if (flow != null) flow.RestartDemo(); }
    public void AcquireHidden(Object owner) { if (owner != null) hiddenOwners.Add(owner); UpdateVisibility(); }
    public void ReleaseHidden(Object owner) { if (owner != null) hiddenOwners.Remove(owner); UpdateVisibility(); }
    private void UpdateVisibility()
    {
        if (hudGroup == null) return;
        hudGroup.alpha = hiddenOwners.Count > 0 ? 0f : originalHudAlpha;
        hudGroup.blocksRaycasts = hiddenOwners.Count == 0 && originalHudBlocksRaycasts;
        hudGroup.interactable = hiddenOwners.Count == 0 && originalHudInteractable;
    }
    public void RefreshTutorial()
    {
        if (tutorial == null) return;
        int step = tutorial.StepNumber;
        if (step < displayedStep || (!tutorial.IsRunning && !tutorial.IsCompleted)) stepFeedbackUntil = 0f;
        if (tutorial.IsCompleted && !displayedCompletion) stepFeedbackUntil = Time.unscaledTime + stepFeedbackDuration;
        displayedCompletion = tutorial.IsCompleted;
        if (displayedStep > 0 && step > displayedStep) stepFeedbackUntil = Time.unscaledTime + stepFeedbackDuration;
        displayedStep = step;
        if (tutorial.IsCompleted)
        {
            if (tutorialTitleText != null) tutorialTitleText.text = "<color=#91EDB2>훈련 완료 ✓</color>";
            if (instructionText != null) instructionText.text = "모든 훈련을 완료했습니다.";
            if (countText != null) countText.text = $"단계 {tutorial.StepTotal} / {tutorial.StepTotal} · 완료";
        }
        else if (tutorial.CurrentStep != null)
        {
            TutorialManager.Step current = tutorial.CurrentStep;
            if (tutorialTitleText != null) tutorialTitleText.text = current.title;
            if (instructionText != null) instructionText.text = current.instruction;
            string countLabel = current.action == DemoAction.ShotHit ? "명중" : "성공";
            if (countText != null) countText.text = $"단계 {step} / {tutorial.StepTotal} · {countLabel} {tutorial.CurrentCount} / {Mathf.Max(1, current.requiredCount)}";
        }
    }
    public void RestartTutorial() { if (tutorial != null) tutorial.RestartIndependentTest(); }
    private void UpdateTutorialVisibility()
    {
        bool visible = tutorial != null && (tutorial.IsRunning || tutorial.IsCompleted) && hiddenOwners.Count == 0 &&
            (dialogueUI == null || !dialogueUI.IsOpen) && (inventoryUI == null || !inventoryUI.IsOpen);
        if (visible && !tutorialWasVisible) RefreshTutorial();
        tutorialWasVisible = visible;
        if (tutorialPanel != null && tutorialPanel.activeSelf != visible) tutorialPanel.SetActive(visible);
        if (tutorialBackground != null) tutorialBackground.color = Time.unscaledTime < stepFeedbackUntil
            ? new Color(0.08f, 0.3f, 0.19f, 0.88f) : new Color(0.025f, 0.035f, 0.055f, 0.82f);
    }
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
    private void Update()
    {
        UpdateTutorialVisibility();
        if (equippedItemPanel != null) equippedItemPanel.SetActive(hiddenOwners.Count == 0 && !IsDemoEndVisible &&
            (status == null || !status.IsDead) && (dialogueUI == null || !dialogueUI.IsOpen) && (inventoryUI == null || !inventoryUI.IsOpen));
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
