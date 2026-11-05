using UnityEngine;

/// <summary>기존 URP UI Glitch Shader를 전용 머티리얼 인스턴스에만 적용한다. 공유 머티리얼을 변경하지 않는다.</summary>
public class Page2GlitchUI : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Image overlay;
    [SerializeField] private Material materialTemplate;
    [SerializeField, Range(0f, 0.4f)] private float maximumOpacity = 0.2f;
    public float MaximumOpacity { get { return maximumOpacity; } }
    private Material instance;
    public bool IsGlitching { get; private set; }

    private void Awake()
    {
        if (overlay == null || materialTemplate == null) return;
        instance = new Material(materialTemplate);
        instance.name = "Page2 Glitch Runtime";
        instance.SetFloat("_Intensity", 1);
        instance.SetFloat("_NoiseAmount", .45f);
        instance.SetFloat("_ScanlineAmount", .8f);
        overlay.material = instance;
        overlay.enabled = false;
    }

    public void SetGlitch(bool value)
    {
        IsGlitching = value;
        if (overlay != null) overlay.enabled = value;
    }

    private void OnDestroy()
    {
        if (instance != null) Destroy(instance);
    }
}
