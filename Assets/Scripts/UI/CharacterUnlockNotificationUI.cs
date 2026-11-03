using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>최초 인물 해금 알림을 큐에 보관하고 입력을 막지 않는 상단 UI로 순차 표시한다.</summary>
public class CharacterUnlockNotificationUI : MonoBehaviour
{
    [SerializeField] private AffinityDataStore dataStore;
    [SerializeField] private GameObject notificationRoot;
    [SerializeField] private CanvasGroup notificationGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text characterNameText;
    [SerializeField, Min(0.01f)] private float fadeInDuration = 0.25f;
    [SerializeField, Min(0f)] private float holdDuration = 2f;
    [SerializeField, Min(0.01f)] private float fadeOutDuration = 0.5f;

    private readonly Queue<AffinityCharacterData> pendingCharacters = new Queue<AffinityCharacterData>();
    private Coroutine notificationRoutine;

    private void OnEnable()
    {
        // PlayerCanvas에서 구독하므로 PlayerInterface와 인벤토리 페이지가 꺼져도 알림을 받는다.
        HideNotification();
        if (dataStore != null) dataStore.CharacterUnlocked += HandleCharacterUnlocked;
    }

    private void OnDisable()
    {
        if (dataStore != null) dataStore.CharacterUnlocked -= HandleCharacterUnlocked;
        if (notificationRoutine != null) StopCoroutine(notificationRoutine);
        notificationRoutine = null;
        pendingCharacters.Clear();
        HideNotification();
    }

    private void HandleCharacterUnlocked(AffinityCharacterData character)
    {
        if (character == null || notificationRoot == null || notificationGroup == null || characterNameText == null) return;
        pendingCharacters.Enqueue(character);
        if (notificationRoutine == null) notificationRoutine = StartCoroutine(ShowNotifications());
    }

    /// <summary>연속 해금은 한 명씩 표시하며, 시간 정지 중에도 UI가 진행되도록 Unscaled Time을 사용한다.</summary>
    private IEnumerator ShowNotifications()
    {
        while (pendingCharacters.Count > 0)
        {
            AffinityCharacterData character = pendingCharacters.Dequeue();
            characterNameText.text = character.CharacterName;
            if (titleText != null) titleText.text = "새로운 인물 정보가 등록되었습니다.";
            notificationGroup.interactable = false;
            notificationGroup.blocksRaycasts = false;
            notificationRoot.transform.SetAsLastSibling();
            notificationRoot.SetActive(true);
            yield return Fade(0f, 1f, fadeInDuration);
            float elapsed = 0f;
            while (elapsed < holdDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return Fade(1f, 0f, fadeOutDuration);
            HideNotification();
        }
        notificationRoutine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);
        while (elapsed < duration)
        {
            notificationGroup.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        notificationGroup.alpha = to;
    }

    private void HideNotification()
    {
        if (notificationGroup != null)
        {
            notificationGroup.alpha = 0f;
            notificationGroup.interactable = false;
            notificationGroup.blocksRaycasts = false;
        }
        if (notificationRoot != null) notificationRoot.SetActive(false);
    }
}
