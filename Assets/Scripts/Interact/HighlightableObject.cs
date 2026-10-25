using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 조사 대상으로 선택된 오브젝트의 Renderer 머티리얼 표시를 하이라이트 상태로 갱신한다.
/// </summary>
public class HighlightableObject : MonoBehaviour
{

    [Header("Name Label")]
    [SerializeField] private GameObject itemNameText;

    [Header("Camera")]
    [SerializeField] private PlayerCameraController playerCameraController;
    [SerializeField] private Transform firstPersonCamera;
    [SerializeField] private Transform thirdPersonCamera;

    [Header("Highlight")]
    [SerializeField, ColorUsage(true, true)] private Color highlightColor = Color.yellow;
    [SerializeField, Min(0f)] private float highlightIntensity = 3f;

    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    private readonly List<Material> materials = new();
    private readonly List<Color> originalEmissionColors = new();

    /// <summary>
    /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
    /// </summary>
    private void Awake()
    {
        CacheMaterials();

        if (itemNameText != null)
        {
            itemNameText.SetActive(false);
        }
    }

    /// <summary>
    /// 일반 Update 이후 최종 상태에 맞추어 표시나 자세를 갱신한다.
    /// </summary>
    private void LateUpdate()
    {
        if(itemNameText == null || !itemNameText.activeInHierarchy || playerCameraController == null)
        {
            return;
        }

        Transform targetCamera = playerCameraController.IsThirdPerson ? thirdPersonCamera : firstPersonCamera;

        if(targetCamera == null)
        {
            return;
        }

        Transform labelTransform = itemNameText.transform;

        if((targetCamera.position - labelTransform.position).sqrMagnitude < 0.0001f)
        {
            return;
        }

        labelTransform.LookAt(targetCamera.position, Vector3.up);

        labelTransform.Rotate(0f, 180f, 0f, Space.Self);
    }

    /// <summary>
    /// 하이라이트 변경 전 Renderer별 원래 머티리얼 정보를 보관한다.
    /// </summary>
    private void CacheMaterials()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        foreach (Renderer targetRenderer in renderers)
        {
            foreach (Material material in targetRenderer.materials)
            {
                if (!material.HasProperty(EmissionColor))
                {
                    continue;
                }

                material.EnableKeyword("_EMISSION");
                materials.Add(material);
                originalEmissionColors.Add(material.GetColor(EmissionColor));
            }
        }
    }

    /// <summary>
    /// 선택 여부에 따라 하이라이트 표시 상태를 변경한다.
    /// </summary>
    public void SetHighlighted(bool isHighlighted)
    {
        for (int i = 0; i < materials.Count; i++)
        {
            Color emissionColor = isHighlighted
                ? highlightColor * highlightIntensity
                : originalEmissionColors[i];

            materials[i].SetColor(EmissionColor, emissionColor);
        }

        if (itemNameText != null)
        {
            itemNameText.SetActive(isHighlighted);
        }
    }
}
