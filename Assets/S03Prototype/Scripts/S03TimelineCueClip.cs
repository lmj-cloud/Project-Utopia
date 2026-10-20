using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace S03Prototype
{
    public enum S03TimelineCue
    {
        DoorOpen, CameraMotion, FadeToWhite, HoldWhite, FadeFromWhite,
        TravelToArena, DoorSound, CrowdFadeIn, RestoreControls
    }

    public sealed class S03TimelineCueClip : PlayableAsset, ITimelineClipAsset
    {
        public S03TimelineCue cue;
        public ClipCaps clipCaps => ClipCaps.None;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
            => Playable.Create(graph);
    }
}
