using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Capstone.Direction
{
    public sealed class DirectionVolume : MonoBehaviour
    {
        Volume volume;
        VolumeProfile profile;
        Bloom bloom; Vignette vignette; ColorAdjustments color;
        LensDistortion distortion; ChromaticAberration aberration;
        void Awake()
        {
            volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;
            profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;
            bloom=profile.Add<Bloom>();vignette=profile.Add<Vignette>();color=profile.Add<ColorAdjustments>();
            distortion=profile.Add<LensDistortion>();aberration=profile.Add<ChromaticAberration>();
            volume.weight=0;
        }
        public void SetTrauma(float amount)
        {
            amount=Mathf.Clamp01(amount);volume.weight=amount;
            vignette.intensity.Override(0.45f);color.saturation.Override(-65);
            distortion.intensity.Override(-0.15f);aberration.intensity.Override(0.7f);
        }
        public void SetBloom(float intensity){volume.weight=1;bloom.intensity.Override(Mathf.Max(0,intensity));}
        public void SetTint(Color tint){volume.weight=1;color.colorFilter.Override(tint);}
        public void ResetEffects(){if(volume)volume.weight=0;}
        void OnDisable(){ResetEffects();}
        void OnDestroy()
        {
            if(profile){foreach(var c in profile.components)if(c)Destroy(c);Destroy(profile);}
            if(volume)Destroy(volume);
        }
    }
}
