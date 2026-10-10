using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;

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

    private void Awake()
    {
        CacheMaterials();

        if (itemNameText != null)
        {
            itemNameText.SetActive(false);
        }
    }

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
