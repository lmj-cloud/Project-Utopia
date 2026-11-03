using UnityEngine;
using Capstone.Direction.Workspace.S06;
namespace Capstone.Direction.Workspace.S07
{
    // Put this in the integration scene; do not edit the S06 source prefab or script.
    public sealed class S07S06Adapter : MonoBehaviour
    {
        public S06BoutSequence source;
        public S07AwardSequence target;
        S06BoutSequence subscribed;
        void OnEnable() { if(source) { subscribed=source; subscribed.onAwardReady.AddListener(ReceiveAward); } }
        void OnDisable() { if(subscribed)subscribed.onAwardReady.RemoveListener(ReceiveAward); subscribed=null; }
        public void ReceiveAward()
        {
            if(source && target && source.Completed && source.CompletionCount==1) target.AcceptAward(source.awardState);
        }
    }
}
