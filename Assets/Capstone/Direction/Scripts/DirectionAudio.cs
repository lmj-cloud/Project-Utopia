using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace Capstone.Direction
{
    public enum DirectionBus { BGM, SFX, Ambient, Voice, UI }
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
        void Awake()
        {
            for (int i=0;i<5;i++) loops[i]=MakeSource("Loop_"+(DirectionBus)i);
            for (int i=0;i<voices.Length;i++) voices[i]=MakeSource("SFX_"+i);
        }
        AudioSource MakeSource(string label)
        {
            var go=new GameObject(label); go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>(); source.playOnAwake=false;
            return source;
        }
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
        public void FadeOut(DirectionBus bus,float seconds=1)
        {
            int i=(int)bus; if(fades[i]!=null) StopCoroutine(fades[i]);
            fades[i]=StartCoroutine(Fade(i,0,seconds));
        }
        IEnumerator Fade(int i,float target,float seconds)
        {
            float from=loopGains[i], t=0;
            while(t<seconds) {t+=Time.unscaledDeltaTime;loopGains[i]=Mathf.Lerp(from,target,seconds<=0?1:t/seconds);loops[i].volume=loopGains[i]*master*levels[i];yield return null;}
            loopGains[i]=target; loops[i].volume=target*master*levels[i]; if(target==0) loops[i].Stop(); fades[i]=null;
        }
        public void SetBusVolume(DirectionBus bus,float value)
        {
            int i=(int)bus; levels[i]=Mathf.Clamp01(value);
            ApplyVolumes();
        }
        public void SetMasterVolume(float value)
        {
            master=Mathf.Clamp01(value); ApplyVolumes();
        }
        void ApplyVolumes(){for(int i=0;i<5;i++)if(loops[i])loops[i].volume=loopGains[i]*master*levels[i];for(int i=0;i<voices.Length;i++)if(voices[i])voices[i].volume=master*levels[voiceBuses[i]];}
        void OnDisable(){StopAllCoroutines();foreach(var s in loops) if(s)s.Stop();foreach(var s in voices) if(s)s.Stop();}
    }
}

