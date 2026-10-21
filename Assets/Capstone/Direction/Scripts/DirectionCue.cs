using UnityEngine;
using Unity.Cinemachine;
namespace Capstone.Direction
{
    public sealed class DirectionCue : MonoBehaviour
    {
        public DirectionAudio audioSystem;
        public DirectionScreen screen;
        public DirectionVolume volume;
        public CinemachineImpulseSource impulse;
        public AudioClip effect,voice;
        public void PlayEffect(){if(audioSystem)audioSystem.Play(effect);}
        public void PlayVoice(){if(audioSystem)audioSystem.Play(voice,DirectionBus.Voice);}
        public void Shake(){if(impulse)impulse.GenerateImpulse();}
        public void Hit(){PlayEffect();Shake();if(volume)volume.SetTrauma(0.4f);}
        public void ClearEffects(){if(volume)volume.ResetEffects();}
        public void Flash(){if(screen)StartCoroutine(FlashRoutine());}
        System.Collections.IEnumerator FlashRoutine()
        {
            yield return screen.Fade(Color.white,1,0.08f);
            yield return screen.Fade(Color.white,0,0.25f);
        }
        void OnDisable(){StopAllCoroutines();if(screen)screen.Clear();}
    }
}
