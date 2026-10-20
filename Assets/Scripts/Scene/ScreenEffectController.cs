using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ScreenEffectController : MonoBehaviour
{
    [SerializeField] private Image fadeImage;
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private AudioSource ambience;
    [SerializeField] private AudioSource cueSource;
    [SerializeField] private AudioClip cueClip;
    [SerializeField] private Color tintColor = new Color(0.3f, 0.4f, 0.6f, 1f);
    [SerializeField, Range(0f, 1f)] private float tintOpacity = 0.2f;
    [SerializeField] private Transform dizzinessTarget;
    [SerializeField, Min(0f)] private float fadeDuration = 0.5f;
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
    private void Awake() { if (fadeImage != null) originalColor = fadeImage.color; }
    public void TintScreen() => Request(tintColor, tintOpacity);
    public void PlayCue() { if (cueSource != null && cueClip != null) cueSource.PlayOneShot(cueClip); }
    private Object leaseOwner;
    public bool TryAcquire(Object owner)
    {
        if (!IsReady || running || owner == null || leaseOwner != null) return false;
        leaseOwner = owner;
        running = true;
        return true;
    }
    public void Release(Object owner)
    {
        if (leaseOwner != owner) return;
        leaseOwner = null;
        running = false;
    }
    public bool IsRunning => running;
    public bool IsReady => isActiveAndEnabled && fadeImage != null && fadeGroup != null;

    public void FadeToBlack() => Request(Color.black, 1f);
    public void FadeToWhite() => Request(Color.white, 1f);
    public void Reveal() => Request(fadeImage != null ? fadeImage.color : Color.black, 0f);
    private void Request(Color color, float alpha)
    {
        if (running) return; // Busy requests are rejected, never reported as success.
        if (!IsReady) { onFailed.Invoke(); return; }
        running = true;
        routine = StartCoroutine(RunFade(color, alpha));
    }
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
    private IEnumerator RunFade(Color color, float alpha)
    {
        yield return Fade(color, alpha);
        if (!IsReady || !running) { CancelEffect(); yield break; }
        routine = null;
        running = false;
        leaseOwner = null;
        onCompleted.Invoke();
    }
    public void Blink()
    {
        if (running) return;
        if (!IsReady) { onFailed.Invoke(); return; }
        running = true;
        routine = StartCoroutine(RunBlink());
    }
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
    private void LateUpdate()
    {
        if (hasRotationSnapshot && dizzinessTarget != null)
            dizzinessTarget.localRotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 3f) * dizzinessDegrees);
    }
    public void SetAmbienceVolume(float value)
    {
        if (ambience == null) return;
        if (!hasAudioSnapshot) { originalVolume = ambience.volume; hasAudioSnapshot = true; }
        ambience.volume = Mathf.Clamp01(value);
    }
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
    private void OnDisable() => CancelEffect();
}
