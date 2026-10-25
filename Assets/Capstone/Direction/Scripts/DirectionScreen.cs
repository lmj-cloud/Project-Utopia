using System.Collections;
using UnityEngine;
using UnityEngine.UI;
namespace Capstone.Direction
{
    /// <summary>
    /// UI 오버레이의 색과 투명도를 시간에 따라 변경하여 연출 화면 전환을 표시한다.
    /// </summary>
    public sealed class DirectionScreen : MonoBehaviour
    {
        public Image overlay;
        /// <summary>
        /// 실제 시간을 사용하여 연출의 투명도 또는 음량을 목표값으로 보간한다.
        /// </summary>
        public IEnumerator Fade(Color color,float alpha,float seconds)
        {
            if(!overlay) yield break;
            float start=overlay.color.a,t=0;
            while(t<seconds){t+=Time.unscaledDeltaTime;color.a=Mathf.Lerp(start,alpha,seconds<=0?1:t/seconds);overlay.color=color;yield return null;}
            color.a=alpha; overlay.color=color;
        }
        /// <summary>
        /// 화면 덮개를 제거하여 다시 게임 화면이 보이게 한다.
        /// </summary>
        public void Clear(){if(overlay) overlay.color=Color.clear;}
    }
}
