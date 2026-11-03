using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기존 상호작용 선택을 받아 개별 Outline 두께와 Light를 표시한다. Emission은 사용하지 않는다.
/// </summary>
public class HighlightableObject : MonoBehaviour
{
    [Header("Name Label")]
    [SerializeField] private GameObject itemNameText;
    [Header("Camera")]
    [SerializeField] private PlayerCameraController playerCameraController;
    [SerializeField] private Transform firstPersonCamera;
    [SerializeField] private Transform thirdPersonCamera;
    // 이전 직렬화 데이터는 보존하지만 Emission 하이라이트에는 더 이상 사용하지 않는다.
    [SerializeField, HideInInspector] private Color highlightColor = Color.yellow;
    [SerializeField, HideInInspector] private float highlightIntensity = 3f;

    [SerializeField] private bool disableMaterialEmission;

    [Header("Optional Outline")]
    [SerializeField] private Renderer[] outlineRenderers = new Renderer[0];
    [SerializeField, Range(0f, 12f)] private float baseOutlineWidth = 1f;
    [SerializeField, Range(0f, 12f)] private float selectedOutlineWidth = 4f;
    [SerializeField, Min(0f)] private float outlineChangeSpeed = 20f;
    [Header("Optional Interaction Light")]
    [SerializeField] private Light interactionLight;
    [SerializeField] private Color lightColor = Color.white;
    [SerializeField, Min(0f)] private float lightIntensity = 1f;
    [SerializeField, Min(0.01f)] private float lightRange = 2f;

    private static readonly int OutlineWidth = Shader.PropertyToID("_OutlineWidth");
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new();
    private readonly List<Material> ownedMaterials = new();
    private MaterialPropertyBlock outlineProperties;
    private bool isHighlighted;
    private float currentWidth;

    private void Awake()
    {
        // 런타임 사본만 수정하여 원본 머티리얼의 색상·텍스처·키워드를 보존한다.
        // 차량처럼 자체 발광 표현을 가진 대상은 선택 효과와 별개로 원본 표현을 유지한다.
        if (!disableMaterialEmission) return;
        foreach (Renderer target in GetComponentsInChildren<Renderer>(true))
        {
            if (System.Array.IndexOf(outlineRenderers, target) >= 0) continue;
            bool hasEmission = false;
            foreach (Material material in target.sharedMaterials)
                if (material != null && material.HasProperty(EmissionColor)) hasEmission = true;
            if (!hasEmission) continue;
            originalMaterials.Add(target, target.sharedMaterials);
            foreach (Material material in target.materials)
            {
                if (material == null) continue;
                ownedMaterials.Add(material);
                if (!material.HasProperty(EmissionColor)) continue;
                material.SetColor(EmissionColor, Color.black);
                material.DisableKeyword("_EMISSION");
            }
        }
    }

    private void OnEnable()
    {
        isHighlighted = false;
        currentWidth = baseOutlineWidth;
        ApplyOutline();
        SetLight(false);
        if (itemNameText != null) itemNameText.SetActive(false);
    }

    private void Update()
    {
        float targetWidth = isHighlighted ? selectedOutlineWidth : baseOutlineWidth;
        currentWidth = Mathf.MoveTowards(currentWidth, targetWidth, outlineChangeSpeed * Time.unscaledDeltaTime);
        ApplyOutline();
    }

    private void ApplyOutline()
    {
        if (outlineProperties == null) outlineProperties = new MaterialPropertyBlock();
        foreach (Renderer outline in outlineRenderers)
        {
            if (outline == null) continue;
            outline.GetPropertyBlock(outlineProperties);
            outlineProperties.SetFloat(OutlineWidth, currentWidth);
            outline.SetPropertyBlock(outlineProperties);
        }
    }

    public void SetHighlighted(bool value)
    {
        isHighlighted = value && isActiveAndEnabled;
        SetLight(isHighlighted);
        if (itemNameText != null) itemNameText.SetActive(isHighlighted);
    }

    private void SetLight(bool value)
    {
        if (interactionLight == null) return;
        interactionLight.color = lightColor;
        interactionLight.intensity = lightIntensity;
        interactionLight.range = lightRange;
        interactionLight.enabled = value;
    }

    private void LateUpdate()
    {
        if (itemNameText == null || !itemNameText.activeInHierarchy || playerCameraController == null) return;
        Transform targetCamera = playerCameraController.IsThirdPerson ? thirdPersonCamera : firstPersonCamera;
        if (targetCamera == null) return;
        Transform label = itemNameText.transform;
        if ((targetCamera.position - label.position).sqrMagnitude < 0.0001f) return;
        label.LookAt(targetCamera.position, Vector3.up);
        label.Rotate(0f, 180f, 0f, Space.Self);
    }

    private void OnDisable()
    {
        isHighlighted = false;
        currentWidth = baseOutlineWidth;
        ApplyOutline();
        SetLight(false);
        if (itemNameText != null) itemNameText.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (var pair in originalMaterials)
            if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        foreach (Material material in ownedMaterials)
            if (material != null) Destroy(material);
    }
}
