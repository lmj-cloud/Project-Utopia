using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 기존 상호작용 선정 결과에 맞추어 개별 대상의 월드 UI와 실제 입력 키를 표시한다.
/// </summary>
public class InteractionPrompt : MonoBehaviour
{
    [SerializeField] private Transform interactionTarget;
    [SerializeField] private PlayerInteraction playerInteraction;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private Animator panelAnimator;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text keyText;
    private bool isVisible;
    private Key displayedKey = Key.None;

    public bool IsVisible => isVisible;

    private void Awake()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void LateUpdate()
    {
        if (viewCamera != null) transform.rotation = viewCamera.transform.rotation;
        UpdateKeyText();
        bool canShow = playerController != null && playerController.CanControlPlayer &&
            playerInteraction != null && interactionTarget != null &&
            playerInteraction.SelectedTarget == interactionTarget;
        if (canShow == isVisible)
        {
            // 종료 전이가 마지막 샘플보다 먼저 끝나도 미세한 잔여 Alpha가 남지 않게 확정한다.
            if (panelAnimator != null && canvasGroup != null)
            {
                AnimatorStateInfo state = panelAnimator.GetCurrentAnimatorStateInfo(0);
                if (state.IsName("Base Layer.Hidden")) canvasGroup.alpha = 0f;
                else if (state.IsName("Base Layer.Visible")) canvasGroup.alpha = 1f;
            }
            return;
        }
        isVisible = canShow;
        if (panelAnimator == null || canvasGroup == null) return;
        // 반대 동작의 현재 Alpha부터 이어 재생하여 빠른 접근·이탈에도 순간적으로 튀지 않는다.
        float progress = Mathf.Clamp01(canvasGroup.alpha);
        if (!isVisible) progress = 1f - progress;
        panelAnimator.Play(isVisible ? "Base Layer.Showing" : "Base Layer.Hiding", 0, progress);
    }

    /// <summary>
    /// 대상이 비활성화되면 표시 상태를 초기화하여 재활성화 때 등장 동작을 다시 시작한다.
    /// </summary>
    private void OnDisable()
    {
        isVisible = false;
        if (!Application.isPlaying) return;
        if (panelAnimator != null && panelAnimator.isActiveAndEnabled && panelAnimator.isInitialized && panelAnimator.runtimeAnimatorController != null)
            panelAnimator.Play("Base Layer.Hidden", 0, 0f);
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// PlayerController가 실제 검사하는 Key 설정에서 표시 이름을 얻는다.
    /// </summary>
    private void UpdateKeyText()
    {
        if (keyText == null || playerController == null) return;
        Key key = playerController.InteractionKey;
        if (displayedKey == key) return;
        displayedKey = key;
        if (key == Key.None)
        {
            keyText.text = string.Empty;
            return;
        }
        if (Keyboard.current != null) keyText.text = Keyboard.current[key].displayName;
        else keyText.text = key.ToString();
    }
}
