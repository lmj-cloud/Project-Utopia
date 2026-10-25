using UnityEngine;
using Unity.Cinemachine;
namespace Capstone.Direction
{
    /// <summary>
    /// 연출 이벤트에서 효과음·음성·섬광·카메라 흔들림과 화면 효과를 호출할 수 있게 제공한다.
    /// </summary>
    public sealed class DirectionCue : MonoBehaviour
    {
        public DirectionAudio audioSystem;
        public DirectionScreen screen;
        public DirectionVolume volume;
        public CinemachineImpulseSource impulse;
        public AudioClip effect,voice;
        /// <summary>
        /// 연결된 연출 효과음을 재생한다.
        /// </summary>
        public void PlayEffect(){if(audioSystem)audioSystem.Play(effect);}
        /// <summary>
        /// 연결된 음성 Clip을 Voice 버스로 재생한다.
        /// </summary>
        public void PlayVoice(){if(audioSystem)audioSystem.Play(voice,DirectionBus.Voice);}
        /// <summary>
        /// 연결된 Cinemachine Impulse를 통해 흔들림을 요청한다.
        /// </summary>
        public void Shake(){if(impulse)impulse.GenerateImpulse();}
        /// <summary>
        /// 충격 효과음과 흔들림, 연출 Volume 충격 효과를 함께 요청한다.
        /// </summary>
        public void Hit(){PlayEffect();Shake();if(volume)volume.SetTrauma(0.4f);}
        /// <summary>
        /// 연출 Volume의 효과 가중치를 초기화한다.
        /// </summary>
        public void ClearEffects(){if(volume)volume.ResetEffects();}
        /// <summary>
        /// 연결된 화면 덮개가 있을 때 섬광 Coroutine을 시작한다.
        /// </summary>
        public void Flash(){if(screen)StartCoroutine(FlashRoutine());}
        /// <summary>
        /// 짧은 백색 화면 덮기와 다시 드러내기를 순서대로 실행한다.
        /// </summary>
        System.Collections.IEnumerator FlashRoutine()
        {
            yield return screen.Fade(Color.white,1,0.08f);
            yield return screen.Fade(Color.white,0,0.25f);
        }
        /// <summary>
        /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
        /// </summary>
        void OnDisable(){StopAllCoroutines();if(screen)screen.Clear();}
    }
}
