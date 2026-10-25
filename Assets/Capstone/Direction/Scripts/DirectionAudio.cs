using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Capstone.Direction
{
    /// <summary>
    /// 연출 오디오의 배경음, 효과음, 환경음, 음성 및 UI 버스를 구분한다.
    /// </summary>
    public enum DirectionBus { BGM, SFX, Ambient, Voice, UI }
    /// <summary>
    /// 연출용 버스별 반복음과 제한된 효과음 채널, 음량과 페이드를 관리한다.
    /// </summary>
    public sealed class DirectionAudio : MonoBehaviour
    {
        public AudioMixer mixer;
        public AudioMixerGroup[] groups = new AudioMixerGroup[5];
        [Range(0,1)] public float master = 1;
        readonly AudioSource[] loops = new AudioSource[5];
        readonly Coroutine[] fades = new Coroutine[5];
        readonly float[] levels = {1,1,1,1,1};
        readonly float[] loopGains = new float[5];
        readonly int[] voiceBuses = new int[12];
        readonly AudioSource[] voices = new AudioSource[12];
        /// <summary>
        /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
        /// </summary>
        void Awake()
        {
            for (int i=0;i<5;i++) loops[i]=MakeSource("Loop_"+(DirectionBus)i);
            for (int i=0;i<voices.Length;i++) voices[i]=MakeSource("SFX_"+i);
        }
        /// <summary>
        /// 연출 오디오 재사용 채널로 사용할 AudioSource 오브젝트를 만든다.
        /// </summary>
        AudioSource MakeSource(string label)
        {
            var go=new GameObject(label); go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>(); source.playOnAwake=false;
            return source;
        }
        /// <summary>
        /// 현재 실행 조건에 맞는 연출 또는 오디오 재생을 요청한다.
        /// </summary>
        public void Play(AudioClip clip, DirectionBus bus=DirectionBus.SFX, Vector3? position=null, bool suppressDuplicate=true)
        {
            if (!clip) return;
            if(suppressDuplicate) foreach(var v in voices) if(v.isPlaying && v.clip==clip) return;
            AudioSource free=null;
            foreach(var v in voices) if(!v.isPlaying) {free=v;break;}
            if(!free) return; // Bounded voices: do not interrupt dialogue or create unlimited sources.
            int i=(int)bus;
            free.outputAudioMixerGroup=groups.Length>i ? groups[i] : null;
            free.transform.position=position ?? transform.position;
            free.spatialBlend=position.HasValue ? 1:0;
            voiceBuses[System.Array.IndexOf(voices,free)]=i;
            free.volume=master*levels[i]; free.clip=clip; free.Play();
        }
        /// <summary>
        /// 버스별 반복 Clip을 연결하고 기존 페이드와 겹치지 않게 목표 음량 전환을 시작한다.
        /// </summary>
        public void Loop(AudioClip clip, DirectionBus bus, float target=1, float seconds=1)
        {
            if (!clip) return;
            int i=(int)bus; var s=loops[i];
            if(fades[i]!=null) StopCoroutine(fades[i]);
            s.outputAudioMixerGroup=groups.Length>i ? groups[i] : null;
            s.loop=true; s.spatialBlend=0;
            if(s.clip!=clip) {s.Stop();s.clip=clip;loopGains[i]=0;s.volume=0;s.Play();}
            else if(!s.isPlaying) s.Play();
            fades[i]=StartCoroutine(Fade(i,Mathf.Clamp01(target),seconds));
        }
        /// <summary>
        /// 해당 버스의 기존 페이드를 중단하고 음량을 낮추는 Coroutine을 시작한다.
        /// </summary>
        public void FadeOut(DirectionBus bus,float seconds=1)
        {
            int i=(int)bus; if(fades[i]!=null) StopCoroutine(fades[i]);
            fades[i]=StartCoroutine(Fade(i,0,seconds));
        }
        /// <summary>
        /// 실제 시간을 사용하여 연출의 투명도 또는 음량을 목표값으로 보간한다.
        /// </summary>
        IEnumerator Fade(int i,float target,float seconds)
        {
            float from=loopGains[i], t=0;
            while(t<seconds) {t+=Time.unscaledDeltaTime;loopGains[i]=Mathf.Lerp(from,target,seconds<=0?1:t/seconds);loops[i].volume=loopGains[i]*master*levels[i];yield return null;}
            loopGains[i]=target; loops[i].volume=target*master*levels[i]; if(target==0) loops[i].Stop(); fades[i]=null;
        }
        /// <summary>
        /// 특정 오디오 버스의 음량을 제한 범위로 설정하고 출력 음량을 다시 계산한다.
        /// </summary>
        public void SetBusVolume(DirectionBus bus,float value)
        {
            int i=(int)bus; levels[i]=Mathf.Clamp01(value);
            ApplyVolumes();
        }
        /// <summary>
        /// 연출 전체 음량을 제한 범위로 설정하고 출력 음량을 다시 계산한다.
        /// </summary>
        public void SetMasterVolume(float value)
        {
            master=Mathf.Clamp01(value); ApplyVolumes();
        }
        /// <summary>
        /// 마스터·버스·반복음 가중치를 합산하여 현재 채널 음량을 갱신한다.
        /// </summary>
        void ApplyVolumes(){for(int i=0;i<5;i++)if(loops[i])loops[i].volume=loopGains[i]*master*levels[i];for(int i=0;i<voices.Length;i++)if(voices[i])voices[i].volume=master*levels[voiceBuses[i]];}
        /// <summary>
        /// 비활성화 시 이 컴포넌트가 남긴 진행 상태나 이벤트 연결을 정리한다.
        /// </summary>
        void OnDisable(){StopAllCoroutines();foreach(var s in loops) if(s)s.Stop();foreach(var s in voices) if(s)s.Stop();}
    }
}

