using UnityEngine;
namespace Capstone.Direction.Workspace.S10
{
    [CreateAssetMenu(menuName="Capstone/Direction/S10 Timing")]
    public sealed class S10Timing : ScriptableObject
    {
        [Min(.2f)] public float normalDrive=3, truckApproach=3, danger=1.5f;
        [Min(.05f)] public float impact=.25f;
        [Min(.2f)] public float confusion=3, fadeToBlack=1.5f, blackHold=.75f;
        public bool proposedHorn=true, proposedBrake=true, proposedGlass=true, proposedTinnitus=true;
        [Min(.05f)] public float blink1Close=.22f,blink1Hold=.28f,blink1Open=.42f,blink1Rest=.45f;
        [Min(.05f)] public float blink2Close=.55f,blink2Hold=.85f,blink2Open=.95f,blink2Rest=.55f;
        [Range(0,.25f)] public float tinnitusMaximum=.18f;
        public AnimationCurve tinnitusGrowth=new AnimationCurve(new Keyframe(0,.12f),new Keyframe(.35f,.35f),new Keyframe(.7f,.7f),new Keyframe(1,1));
        [HideInInspector] public int driverVersion;
        [HideInInspector] public string appliedTimingSignature;
        public string TimingSignature=>string.Join("|",System.Array.ConvertAll(new[]{normalDrive,truckApproach,danger,impact,confusion,fadeToBlack,blackHold,blink1Close,blink1Hold,blink1Open,blink1Rest,blink2Close,blink2Hold,blink2Open,blink2Rest},v=>v.ToString("R",System.Globalization.CultureInfo.InvariantCulture)));
        public float ApproachAt=>normalDrive;
        public float DangerAt=>ApproachAt+truckApproach;
        public float ImpactAt=>DangerAt+danger;
        public float ConfusionAt=>ImpactAt+impact;
        public float BlackAt=>ConfusionAt+confusion;
        public float FirstClosedAt=>BlackAt+blink1Close;
        public float FirstOpenAt=>FirstClosedAt+blink1Hold;
        public float FirstReopenedAt=>FirstOpenAt+blink1Open;
        public float SecondAt=>FirstReopenedAt+blink1Rest;
        public float SecondClosedAt=>SecondAt+blink2Close;
        public float SecondOpenAt=>SecondClosedAt+blink2Hold;
        public float SecondReopenedAt=>SecondOpenAt+blink2Open;
        public float ThirdAt=>SecondReopenedAt+blink2Rest;
        public float CoveredAt=>ThirdAt+fadeToBlack;
        public float FinishAt=>CoveredAt+blackHold;
        public bool Valid=>normalDrive>=.2f&&truckApproach>=.2f&&danger>=.2f&&impact>=.05f&&confusion>=.2f&&blink1Close>0&&blink1Hold>0&&blink1Open>0&&blink1Rest>0&&blink2Close>blink1Close&&blink2Hold>blink1Hold&&blink2Open>blink1Open&&blink2Rest>0&&fadeToBlack>blink2Close&&blackHold>blink2Hold&&FinishAt<120;
        public float EyeAlpha(float t)
        {
            if(t<BlackAt)return 0;
            if(t<FirstClosedAt)return Smooth(BlackAt,FirstClosedAt,t);
            if(t<FirstOpenAt)return 1;
            if(t<FirstReopenedAt)return 1-Smooth(FirstOpenAt,FirstReopenedAt,t);
            if(t<SecondAt)return 0;
            if(t<SecondClosedAt)return Smooth(SecondAt,SecondClosedAt,t);
            if(t<SecondOpenAt)return 1;
            if(t<SecondReopenedAt)return 1-Smooth(SecondOpenAt,SecondReopenedAt,t);
            if(t<ThirdAt)return 0;
            return Smooth(ThirdAt,CoveredAt,t);
        }
        static float Smooth(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
    }
}
