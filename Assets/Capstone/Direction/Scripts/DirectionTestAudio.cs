using UnityEngine;
namespace Capstone.Direction
{
    /// <summary>
    /// 연출 연결을 시험할 임시 오디오를 생성하여 테스트 시퀀스에 제공한다.
    /// </summary>
    public sealed class DirectionTestAudio : MonoBehaviour
    {
        public DirectionSequence sequence;
        AudioClip transition,crowd;
        /// <summary>
        /// 다른 실행 로직이 시작되기 전에 필요한 참조와 초기 상태를 준비한다.
        /// </summary>
        void Awake()
        {
            transition=Tone("TEST transition tone",0.2f,440);crowd=Tone("TEST ambient tone",2,110);
            if(sequence){sequence.transitionSound=transition;sequence.crowdLoop=crowd;}
        }
        /// <summary>
        /// 연출 연결 시험에 사용할 임시 파형 Clip을 생성한다. 실제 제작된 효과음과 구분한다.
        /// </summary>
        static AudioClip Tone(string label,float seconds,float hz)
        {
            int n=(int)(44100*seconds);var data=new float[n];
            for(int i=0;i<n;i++)data[i]=0.08f*Mathf.Sin(2*Mathf.PI*hz*i/44100)*Mathf.Min(1,i/441f)*Mathf.Min(1,(n-i)/441f);
            var clip=AudioClip.Create(label,n,1,44100,false);clip.SetData(data,0);return clip;
        }
        /// <summary>
        /// 실행 중 생성하여 소유한 자원을 파괴 시점에 해제한다.
        /// </summary>
        void OnDestroy(){if(transition)Destroy(transition);if(crowd)Destroy(crowd);}
    }
}
