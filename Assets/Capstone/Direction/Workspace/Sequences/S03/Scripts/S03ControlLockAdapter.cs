using UnityEngine;
using S03Prototype;

namespace Capstone.Direction.Workspace.S03
{
    // Only one of these players is normally assigned. The generated test uses prototypePlayer.
    public sealed class S03ControlLockAdapter : MonoBehaviour
    {
        public S03FirstPersonPlayer prototypePlayer;
        public PlayerController gamePlayer;
        bool acquired;
        bool previousPrototypeLock;

        public bool IsAcquired => acquired;

        public void Acquire()
        {
            if (acquired) return;
            acquired = true;
            if (prototypePlayer)
            {
                previousPrototypeLock = prototypePlayer.ControlsLocked;
                prototypePlayer.SetControlsLocked(true);
            }
            if (gamePlayer) gamePlayer.AcquireControlLock(this);
        }

        public void Release()
        {
            if (!acquired) return;
            acquired = false;
            if (gamePlayer) gamePlayer.ReleaseControlLock(this);
            if (prototypePlayer) prototypePlayer.SetControlsLocked(previousPrototypeLock);
        }

        void OnDisable() => Release();
        void OnDestroy() => Release();
    }
}
