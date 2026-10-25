using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 화면 페이드, 깜빡임, 어지럼 및 환경음 조절의 실행 소유권과 원래 상태 복구를 관리한다.
/// </summary>
public class ScreenEffectController : MonoBehaviour
{
    [SerializeField] private Image fadeImage;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private AudioSource ambience;
    [SerializeField] private AudioSource cueSource;
    [SerializeField] private AudioClip cueClip;
    [SerializeField] private Color tintColor = new Color(0.3f, 0.4f, 0.6f, 1f);
    [SerializeField, Range(0f, 1f)] private float tintOpacity = 0.2f;
    // 어지럼 회전 효과를 적용하고 원래 회전을 복원할 Transform.
    [SerializeField] private Transform dizzinessTarget;
    // 화면이 목표 투명도에 도달하기까지의 전환 시간.
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;
    // 화면을 가린 뒤 다시 드러내기 전 잠깐 유지하는 시간.
    [SerializeField, Min(0f)] private float blinkInterval = 0.15f;
    [SerializeField, Min(0f)] private float dizzinessDegrees = 2f;
    [SerializeField] private UnityEvent onCompleted = new();
    [SerializeField] private UnityEvent onCancelled = new();
    [SerializeField] private UnityEvent onFailed = new();
    private Coroutine routine;
    private float originalVolume;
    private Quaternion originalRotation;
    private bool hasAudioSnapshot;
    private bool hasRotationSnapshot;
    private bool running;
    private Color originalColor;
    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake() { if (fadeImage != null) originalColor = fadeImage.color; }
    /// <summary>
    /// 설정된 색과 투명도로 화면 색조 전환을 요청한다.
    /// </summary>
    public void TintScreen() => Request(tintColor, tintOpacity);
    /// <summary>
    /// 연결된 효과음 소스와 Clip이 있을 때 한 번 재생한다.
    /// </summary>
    public void PlayCue() { if (cueSource != null && cueClip != null) cueSource.PlayOneShot(cueClip); }
    // 화면 효과 실행을 예약한 소유자. 다른 시스템의 연출을 임의 해제하지 않게 구분한다.
    private Object leaseOwner;
    /// <summary>
    /// 다른 화면 연출이 없고 참조가 준비된 경우 지정 소유자의 실행 권한을 예약한다.
    /// </summary>
    public bool TryAcquire(Object owner)
    {
        if (!IsReady || running || owner == null || leaseOwner != null) return false;
        leaseOwner = owner;
        running = true;
        return true;
    }
    /// <summary>
    /// 해당 소유자의 화면 연출 권한만 해제한다.
    /// </summary>
    public void Release(Object owner)
    {
        if (leaseOwner != owner) return;
        leaseOwner = null;
        running = false;
    }
    public bool IsRunning => running;
    public bool IsReady => isActiveAndEnabled && fadeImage != null && fadeGroup != null;

    /// <summary>
    /// 화면을 검은색으로 가리는 페이드를 요청한다.
    /// </summary>
    public void FadeToBlack() => Request(Color.black, 1f);
    /// <summary>
    /// 화면을 흰색으로 가리는 페이드를 요청한다.
    /// </summary>
    public void FadeToWhite() => Request(Color.white, 1f);
    /// <summary>
    /// 현재 화면 덮개의 투명도를 낮추어 다시 화면을 드러낸다.
    /// </summary>
    public void Reveal() => Request(fadeImage != null ? fadeImage.color : Color.black, 0f);
    /// <summary>
    /// 중복 화면 연출을 거부하고 준비된 경우 페이드 Coroutine을 시작한다.
    /// </summary>
    private void Request(Color color, float alpha)
    {
        if (running) return; // Busy requests are rejected, never reported as success.
        if (!IsReady) { onFailed.Invoke(); return; }
        running = true;
        routine = StartCoroutine(RunFade(color, alpha));
    }
    /// <summary>
    /// 실제 시간으로 화면 투명도를 보간한다. 이 메서드 자체가 실행 소유권을 획득하지는 않는다.
    /// </summary>
    public IEnumerator Fade(Color color, float alpha)
    {
        if (!IsReady || !running) yield break;
        fadeImage.color = color;
        float start = fadeGroup.alpha;
        float elapsed = 0f;
        do
        {
            if (!IsReady || !running) yield break;
            elapsed += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, alpha, fadeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeDuration));
            fadeGroup.blocksRaycasts = fadeGroup.alpha > 0f;
            yield return null;
        } while (elapsed < fadeDuration);
        if (!IsReady || !running) yield break;
        fadeGroup.alpha = alpha;
        fadeGroup.blocksRaycasts = alpha > 0f;
    }
    /// <summary>
    /// 페이드를 기다린 뒤 실행 상태를 정리하고 완료 또는 취소를 알린다.
    /// </summary>
    private IEnumerator RunFade(Color color, float alpha)
    {
        yield return Fade(color, alpha);
        if (!IsReady || !running) { CancelEffect(); yield break; }
        routine = null;
        running = false;
        leaseOwner = null;
        onCompleted.Invoke();
    }
    /// <summary>
    /// 다른 연출과 겹치지 않을 때 눈 깜빡임 Coroutine을 시작한다.
    /// </summary>
    public void Blink()
    {
        if (running) return;
        if (!IsReady) { onFailed.Invoke(); return; }
        running = true;
        routine = StartCoroutine(RunBlink());
    }
    /// <summary>
    /// 화면 가리기, 잠깐 대기, 다시 드러내기를 순서대로 수행한다.
    /// </summary>
    private IEnumerator RunBlink()
    {
        yield return Fade(Color.black, 1f);
        yield return new WaitForSecondsRealtime(blinkInterval);
        yield return Fade(Color.black, 0f);
        if (!IsReady || !running) { CancelEffect(); yield break; }
        routine = null;
        running = false;
        onCompleted.Invoke();
    }
    /// <summary>
    /// 카메라 기준 회전을 보관하여 어지럼 효과를 켜거나 원래 회전으로 복구한다.
    /// </summary>
    public void SetDizziness(bool value)
    {
        if (dizzinessTarget == null) return;
        if (value && !hasRotationSnapshot)
        {
            originalRotation = dizzinessTarget.localRotation;
            hasRotationSnapshot = true;
        }
        else if (!value && hasRotationSnapshot)
        {
            dizzinessTarget.localRotation = originalRotation;
            hasRotationSnapshot = false;
        }
    }
    /// <summary>
    /// 일반 Update 이후 최종 상태에 맞추어 표시나 자세를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if (hasRotationSnapshot && dizzinessTarget != null)
            dizzinessTarget.localRotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 3f) * dizzinessDegrees);
    }
    /// <summary>
    /// 기존 환경음 음량을 한 번 보관한 뒤 지정 음량을 적용한다.
    /// </summary>
    public void SetAmbienceVolume(float value)
    {
        if (ambience == null) return;
        if (!hasAudioSnapshot) { originalVolume = ambience.volume; hasAudioSnapshot = true; }
        ambience.volume = Mathf.Clamp01(value);
    }
    /// <summary>
    /// 진행 중 연출을 취소하고 화면·음향·회전 스냅샷을 복구한다.
    /// </summary>
    public void CancelEffect()
    {
        bool cancelled = running;
        leaseOwner = null;
        if (routine != null) StopCoroutine(routine);
        routine = null;
        running = false;
        if (fadeGroup != null) { fadeGroup.alpha = 0f; fadeGroup.blocksRaycasts = false; }
        if (fadeImage != null) fadeImage.color = originalColor;
        if (cueSource != null && cueSource != ambience) cueSource.Stop();
        if (hasAudioSnapshot && ambience != null) ambience.volume = originalVolume;
        hasAudioSnapshot = false;
        SetDizziness(false);
        if (cancelled) onCancelled.Invoke();
    }
    /// <summary>
    /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
    /// </summary>
    private void OnDisable() => CancelEffect();
}
