using System.Collections;
using UnityEngine;
namespace Capstone.Direction.Workspace.S10
{
    // A transition overlay only, not an S11 scene. Survives unloading S10 during async loading.
    public sealed class S10BlackoutRelay : MonoBehaviour
    {
        static S10BlackoutRelay current;
        public static S10BlackoutRelay Current=>current;
        public UnityEngine.UI.Image Overlay {get;private set;}
        public float timeoutSeconds=30;
        public S10CrashSequence Owner {get;private set;}
        public bool TimedOut {get;private set;}
        float began;
        bool transferring;
        public static void Acquire(S10CrashSequence source,float timeout)
        {
            if(current)throw new System.InvalidOperationException("Another S10 blackout already owns the handoff.");
            var go=new GameObject("S10_PersistentBlackoutRelay");DontDestroyOnLoad(go);current=go.AddComponent<S10BlackoutRelay>();current.Owner=source;current.timeoutSeconds=timeout;current.began=Time.realtimeSinceStartup;
            var canvas=go.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;
            var image=new GameObject("OpaqueTransition",typeof(RectTransform),typeof(UnityEngine.UI.Image));image.transform.SetParent(go.transform,false);var rect=(RectTransform)image.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            current.Overlay=image.GetComponent<UnityEngine.UI.Image>();current.Overlay.color=Color.black;current.Overlay.raycastTarget=false;Canvas.ForceUpdateCanvases();
        }
        public static bool TransferToReadyDestination(UnityEngine.UI.Image destination)
        {
            if(!current||current.transferring||!Opaque(destination))return false;
            if(destination.transform.IsChildOf(current.transform)||(current.Owner&&destination.transform.IsChildOf(current.Owner.transform.root)))return false;
            current.transferring=true;current.StartCoroutine(current.Transfer(destination));return true;
        }
        static bool Opaque(UnityEngine.UI.Image image)
        {
            if(!image||!image.isActiveAndEnabled||image.color.a<.999f||image.color.r>.01f||image.color.g>.01f||image.color.b>.01f)return false;
            var canvas=image.canvas;if(!canvas||!canvas.isActiveAndEnabled||canvas.rootCanvas.renderMode!=RenderMode.ScreenSpaceOverlay||canvas.rootCanvas.targetDisplay!=0)return false;
            if(image.transform.parent!=canvas.rootCanvas.transform||image.sprite||image.material!=image.defaultMaterial)return false;
            var r=image.rectTransform;if(r.anchorMin!=Vector2.zero||r.anchorMax!=Vector2.one||r.offsetMin.sqrMagnitude>.001f||r.offsetMax.sqrMagnitude>.001f||r.localScale!=Vector3.one||Quaternion.Angle(r.localRotation,Quaternion.identity)>.01f)return false;
            foreach(var group in image.GetComponentsInParent<CanvasGroup>())if(group.alpha<.999f)return false;
            return true;
        }
        IEnumerator Transfer(UnityEngine.UI.Image destination)
        {
            Canvas.ForceUpdateCanvases();yield return null;yield return null;
            if(!Opaque(destination)){transferring=false;yield break;}
            if(Owner)Owner.HandoffDestinationReady();current=null;Destroy(gameObject);
        }
        public static void CancelPending()
        {
            if(!current)return;var old=current;current=null;if(old.Owner)old.Owner.HandoffAborted();Destroy(old.gameObject);
        }
        void Update()
        {
            if(!TimedOut&&Time.realtimeSinceStartup-began>timeoutSeconds)
            {
                TimedOut=true;Debug.LogWarning("S10 handoff timed out; black remains until a valid destination or explicit abort.");
                if(Owner)Owner.HandoffWaitingTimedOut();
            }
        }
        void OnDestroy(){if(current==this)current=null;}
    }
}
