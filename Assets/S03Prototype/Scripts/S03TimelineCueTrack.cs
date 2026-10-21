using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace S03Prototype
{
    [TrackColor(0.20f, 0.55f, 0.75f)]
    [TrackClipType(typeof(S03TimelineCueClip))]
    [TrackBindingType(typeof(S03TimelineController))]
    public sealed class S03TimelineCueTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer = ScriptPlayable<S03TimelineCueMixer>.Create(graph, inputCount);
            mixer.GetBehaviour().cues = GetClips().OrderBy(c => c.start).Select(c =>
                new S03TimelineCueMixer.Cue { action = ((S03TimelineCueClip)c.asset).cue,
                    start = c.start, duration = c.duration }).ToArray();
            return mixer;
        }
    }

    public sealed class S03TimelineCueMixer : PlayableBehaviour
    {
        public struct Cue { public S03TimelineCue action; public double start; public double duration; }
        public Cue[] cues;

        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            // Edit-mode scrubbing never teleports the authoring player or alters the scene.
            var controller = playerData as S03TimelineController;
            if (!Application.isPlaying || controller == null || !controller.IsRunning || cues == null) return;
            var director = playable.GetGraph().GetResolver() as PlayableDirector;
            if (director == null) return;
            double time = director.time;
            foreach (var cue in cues)
            {
                if (time + 0.000001 < cue.start) continue;
                float progress = cue.duration > 0 ? Mathf.Clamp01((float)((time-cue.start)/cue.duration)) : 1f;
                // Completed cues still receive their exact final value, including after skipped frames.
                controller.ApplyCue(cue.action, progress);
            }
        }
    }
}
