using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Cinematics
{
    // Timeline binds this presentation adapter, never the View or a global locator.
    public sealed class CinematicFrameBridge : MonoBehaviour
    {
        private CinematicPresentationState state;
        private readonly HashSet<CinematicArtworkBehaviour> clips = new HashSet<CinematicArtworkBehaviour>();
        internal void Register(CinematicArtworkBehaviour clip) { clips.Add(clip); }
        internal void Unregister(CinematicArtworkBehaviour clip) { clips.Remove(clip); }
        public DG.Tweening.Tween[] CaptureOwnedTweens()
        {
            var result = new List<DG.Tweening.Tween>();
            foreach (var clip in clips) { if (clip.OwnedTween != null) result.Add(clip.OwnedTween); if (clip.RevealTween != null) result.Add(clip.RevealTween); }
            return result.ToArray();
        }
        internal void ReleaseClipTweens()
        {
            // Playable graph destruction can be deferred. Hide must release its own handles now.
            foreach (var clip in new List<CinematicArtworkBehaviour>(clips)) clip.ReleaseTween();
            clips.Clear();
        }
        private void OnDestroy() { ReleaseClipTweens(); }
        internal void Bind(CinematicPresentationState value) { state = value; }
        internal void Publish(CinematicFrame frame) { state?.SetFrame(frame); }
    }
}
