using UnityEngine;
using UnityEngine.Events;
namespace Capstone.Direction.Workspace.S13
{
    [System.Serializable] public sealed class S13EndingState
    {
        public string sourceSequence="S13";
        public bool exitReached,whiteCovered;
        // Deliberately no authored dialogue, ending interpretation or subsequent events.
    }
    public sealed class S13EndingAdapter : MonoBehaviour
    {
        public S13EndingSequence target;
        public S13EndingState endingState=new S13EndingState();
        public UnityEvent onEndingRequested=new UnityEvent();
        void OnEnable(){if(target)target.onEndingReady.AddListener(Forward);}
        void OnDisable(){if(target)target.onEndingReady.RemoveListener(Forward);}
        public bool TryStartFromExit()=>target&&target.BeginFromExit();
        public void StartFromExit(){TryStartFromExit();}
        public bool BindGameplay(PlayerController gamePlayer,Transform playerRoot,Camera camera,Behaviour[] inputComponents,
            DirectionScreen effectScreen,Transform exit,Transform leaf,Transform cameraAtDoor,Transform cameraPastDoor)
        {
            if(!target||target.Active||target.WhiteHeld||!gamePlayer||!playerRoot||!camera||!camera.GetComponent<Unity.Cinemachine.CinemachineBrain>()||!effectScreen||!effectScreen.overlay||!exit||!leaf||!cameraAtDoor||!cameraPastDoor)return false;
            target.player=playerRoot;target.outputCamera=camera;target.screen=effectScreen;
            target.exitAnchor=exit;target.door=leaf;target.doorView=cameraAtDoor;target.exitView=cameraPastDoor;
            target.controlLock.prototypePlayer=null;target.controlLock.gamePlayer=gamePlayer;
            target.sequence.inputComponents=inputComponents??new Behaviour[0];return true;
        }
        void Forward(){endingState=new S13EndingState{exitReached=true,whiteCovered=target.WhiteHeld&&target.screen.overlay.color.a>=.999f};onEndingRequested.Invoke();}
        public void Cancel()=>target.Cancel();
        public void ReleaseWhiteAfterHostReady()=>target.ReleaseWhite();
    }
}
