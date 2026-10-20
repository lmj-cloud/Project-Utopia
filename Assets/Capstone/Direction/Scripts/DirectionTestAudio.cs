using UnityEngine;
namespace Capstone.Direction
{
    public sealed class DirectionTestAudio : MonoBehaviour
    {
        public DirectionSequence sequence;
        AudioClip transition,crowd;
        void Awake()
        {
            transition=Tone("TEST transition tone",0.2f,440);crowd=Tone("TEST ambient tone",2,110);
            if(sequence){sequence.transitionSound=transition;sequence.crowdLoop=crowd;}
        }
        static AudioClip Tone(string label,float seconds,float hz)
        {
            int n=(int)(44100*seconds);var data=new float[n];
            for(int i=0;i<n;i++)data[i]=0.08f*Mathf.Sin(2*Mathf.PI*hz*i/44100)*Mathf.Min(1,i/441f)*Mathf.Min(1,(n-i)/441f);
            var clip=AudioClip.Create(label,n,1,44100,false);clip.SetData(data,0);return clip;
        }
        void OnDestroy(){if(transition)Destroy(transition);if(crowd)Destroy(crowd);}
    }
}
