using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Capstone.Direction.Workspace.S03
{
    public sealed class S03CinematicLight : MonoBehaviour
    {
        public Volume volume;
        public Light doorwayLight;
        public Renderer portal;
        VolumeProfile runtimeProfile;
        Bloom bloom;
        ColorAdjustments color;
        Material portalInstance;
        float baselineBloom, baselineExposure;
        public float Amount { get; private set; }
        public float Exposure => color ? color.postExposure.value : 0;

        void Awake()
        {
            if (volume && volume.sharedProfile)
            {
                runtimeProfile = volume.profile; // Clone: never animate the saved profile asset.
                runtimeProfile.TryGet(out bloom);
                runtimeProfile.TryGet(out color);
                baselineBloom = bloom ? bloom.intensity.value : 0;
                baselineExposure = color ? color.postExposure.value : 0;
            }
            if (portal) portalInstance = portal.material;
            ResetLight();
        }

        public void SetAmount(float amount)
        {
            Amount = Mathf.Clamp01(amount);
            if (bloom) bloom.intensity.Override(Mathf.Lerp(baselineBloom, 3.5f, Amount));
            if (color) color.postExposure.Override(Mathf.Lerp(baselineExposure, 1.65f, Amount));
            if (doorwayLight) doorwayLight.intensity = 10f * Amount * Amount;
            if (portal)
            {
                portal.enabled = Amount > 0.001f;
                if (portalInstance) portalInstance.SetColor("_EmissionColor", Color.white * (0.5f + 10f * Amount));
            }
        }

        public void ResetLight() => SetAmount(0);
        void OnDisable() => ResetLight();
        void OnDestroy()
        {
            if (portalInstance) Destroy(portalInstance);
            if (runtimeProfile)
            {
                foreach (var component in runtimeProfile.components) if (component) Destroy(component);
                Destroy(runtimeProfile);
            }
        }
    }
}
