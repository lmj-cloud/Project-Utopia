using UnityEngine;
using UnityEngine.Events;
namespace Capstone.Direction.Workspace.S10
{
    // Bind S09's boarding/drive-ready UnityEvent to ReceiveS09. No S09/S11 scene is authored here.
    public sealed class S10StoryAdapter : MonoBehaviour
    {
        public S10CrashSequence target;
        public S10DriveState incoming=new S10DriveState();
        public UnityEvent onHospitalRequested=new UnityEvent();
        public S10HospitalState LastHospitalState {get;private set;}
        S10CrashSequence subscribed;
        void OnEnable(){if(target){subscribed=target;subscribed.onS11Ready.AddListener(Forward);}}
        void OnDisable(){if(subscribed)subscribed.onS11Ready.RemoveListener(Forward);subscribed=null;}
        public void ReceiveS09(){if(target)target.BeginFromS09(incoming);}
        void Forward(){if(!target||!target.Completed)return;LastHospitalState=new S10HospitalState{nextSequence=target.hospitalState.nextSequence,accidentOccurred=target.hospitalState.accidentOccurred,causeConfirmed=false,unconscious=true,requiresBlackStart=true};onHospitalRequested.Invoke();}
        public bool ConfirmDestinationBlack(UnityEngine.UI.Image destination)=>S10BlackoutRelay.TransferToReadyDestination(destination);
    }
}
