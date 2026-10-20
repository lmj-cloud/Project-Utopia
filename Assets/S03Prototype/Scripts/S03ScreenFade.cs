using System.Collections;
using UnityEngine;

namespace S03Prototype
{
    public sealed class S03ScreenFade : MonoBehaviour
    {
        [SerializeField] private CanvasGroup overlay;
        public float Opacity => overlay != null ? overlay.alpha : 0f;

        private void Awake()
        {
            if (overlay == null)
            {
                Debug.LogError("S03 fade needs an overlay CanvasGroup.", this);
                enabled = false;
                return;
            }
            Clear();
        }

        public IEnumerator FadeTo(float targetOpacity, float duration)
        {
            if (overlay == null) yield break;
            targetOpacity = Mathf.Clamp01(targetOpacity);
            float initial = overlay.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float fraction = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
                overlay.alpha = Mathf.Lerp(initial, targetOpacity, fraction);
                yield return null;
            }
            overlay.alpha = targetOpacity;
        }

        public void Clear()
        {
            if (overlay != null) overlay.alpha = 0f;
        }

        public void SetOpacity(float opacity)
        {
            if (overlay != null) overlay.alpha = Mathf.Clamp01(opacity);
        }
    }
}
