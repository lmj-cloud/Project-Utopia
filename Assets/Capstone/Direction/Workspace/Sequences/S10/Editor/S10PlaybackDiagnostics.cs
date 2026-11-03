using UnityEditor;
using UnityEngine;
namespace Capstone.Direction.Workspace.S10.Editor
{
    // Observe the ordinary auto-start path without cancelling, restarting or rebuilding it.
    [InitializeOnLoad]
    public static class S10PlaybackDiagnostics
    {
        static double next;
        static S10PlaybackDiagnostics(){EditorApplication.update+=Observe;}
        [MenuItem("Capstone/Direction/S10 Workspace/Toggle playback diagnostics")]
        public static void Toggle(){SessionState.SetBool("S10.Diagnostics",!SessionState.GetBool("S10.Diagnostics",false));}
        static void Observe()
        {
            if(!SessionState.GetBool("S10.Diagnostics",false))return;
            if(!EditorApplication.isPlaying){next=0;return;}
            if(EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+1;
            var c=Object.FindFirstObjectByType<S10CrashSequence>();
            if(!c||!c.Active)return;
            if(c.Playhead<3)foreach(var camera in Camera.allCameras)Debug.Log($"S10_RENDER_CAMERA {camera.name} depth={camera.depth} pos={camera.transform.position} parent={camera.transform.parent?.name} target={camera.targetTexture}");
            Debug.Log($"S10_PLAYBACK t={c.Playhead:F2} duration={c.sequence.director.duration:F2} state={c.sequence.director.state} phase={c.Phase} camera={c.outputCamera.transform.position} seat={c.driver.seatEye.position} truck={c.truck.position} alpha={c.screen.overlay.color.a:F2}");
        }
    }
}
