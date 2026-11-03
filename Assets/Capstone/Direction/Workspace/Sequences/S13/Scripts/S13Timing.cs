using UnityEngine;
namespace Capstone.Direction.Workspace.S13
{
    [CreateAssetMenu(menuName="Capstone/Direction/S13 Timing")]
    public sealed class S13Timing : ScriptableObject
    {
        [Min(.1f)] public float cameraBlend=1,doorOpening=1.8f,whiteRise=3,whiteHold=.6f;
        [Range(0,130)] public float doorAngle=95;
        [Min(0)] public float lightBoost=10;
        [Range(0,1)] public float windGain=.2f;
        public float OpenAt=>cameraBlend;
        public float WhiteAt=>OpenAt+doorOpening;
        public float CoveredAt=>WhiteAt+whiteRise;
        public float FinishAt=>CoveredAt+whiteHold;
        public bool Valid=>Finite(cameraBlend)&&Finite(doorOpening)&&Finite(whiteRise)&&Finite(whiteHold)&&cameraBlend>=.1f&&doorOpening>=.1f&&whiteRise>=.1f&&whiteHold>=.1f&&FinishAt<120&&Finite(lightBoost)&&lightBoost>=0;
        static bool Finite(float x)=>!float.IsNaN(x)&&!float.IsInfinity(x);
    }
}
