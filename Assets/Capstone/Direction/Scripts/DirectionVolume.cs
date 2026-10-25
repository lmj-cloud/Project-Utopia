using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace Capstone.Direction
{
    /// <summary>
    /// 연출용 URP Volume 효과를 실행 중 구성하고 강도 조절 및 생성 자원 해제를 담당한다.
    /// </summary>
    public sealed class DirectionVolume : MonoBehaviour
    {
        Volume volume;
        VolumeProfile profile;
        Bloom bloom; Vignette vignette; ColorAdjustments color;
        LensDistortion distortion; ChromaticAberration aberration;
        /// <summary>
        /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
        /// </summary>
        void Awake()
        {
            volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;
            profile=ScriptableObject.CreateInstance<VolumeProfile>();volume.sharedProfile=profile;
            bloom=profile.Add<Bloom>();vignette=profile.Add<Vignette>();color=profile.Add<ColorAdjustments>();
            distortion=profile.Add<LensDistortion>();aberration=profile.Add<ChromaticAberration>();
            volume.weight=0;
        }
        /// <summary>
        /// 충격 강도에 맞추어 연출 Volume 가중치와 왜곡·색·비네트 값을 적용한다.
        /// </summary>
        public void SetTrauma(float amount)
        {
            amount=Mathf.Clamp01(amount);volume.weight=amount;
            vignette.intensity.Override(0.45f);color.saturation.Override(-65);
            distortion.intensity.Override(-0.15f);aberration.intensity.Override(0.7f);
        }
        /// <summary>
        /// 연출 Volume을 활성화하고 Bloom 강도를 적용한다.
        /// </summary>
        public void SetBloom(float intensity){volume.weight=1;bloom.intensity.Override(Mathf.Max(0,intensity));}
        /// <summary>
        /// 연출 Volume을 활성화하고 지정 색조를 적용한다.
        /// </summary>
        public void SetTint(Color tint){volume.weight=1;color.colorFilter.Override(tint);}
        /// <summary>
        /// 실행 중 연출 Volume의 가중치를 내려 효과를 숨긴다.
        /// </summary>
        public void ResetEffects(){if(volume)volume.weight=0;}
        /// <summary>
        /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
        /// </summary>
        void OnDisable(){ResetEffects();}
        /// <summary>
        /// 실행 중 생성하여 소유한 자원을 파괴 시점에 해제한다.
        /// </summary>
        void OnDestroy()
        {
            if(profile){foreach(var c in profile.components)if(c)Destroy(c);Destroy(profile);}
            if(volume)Destroy(volume);
        }
    }
}
