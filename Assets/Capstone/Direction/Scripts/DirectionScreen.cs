using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace Capstone.Direction
{
    public sealed class DirectionScreen : MonoBehaviour
    {
        public Image overlay;
        public IEnumerator Fade(Color color,float alpha,float seconds)
        {
            if(!overlay) yield break;
            float start=overlay.color.a,t=0;
            while(t<seconds){t+=Time.unscaledDeltaTime;color.a=Mathf.Lerp(start,alpha,seconds<=0?1:t/seconds);overlay.color=color;yield return null;}
            color.a=alpha; overlay.color=color;
        }
        public void Clear(){if(overlay) overlay.color=Color.clear;}
    }
}
