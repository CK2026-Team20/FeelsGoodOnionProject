using DG.Tweening;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Cooked.Cinematics
{
    public sealed class CinematicArtworkClip : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private Texture2D artwork;
        [SerializeField, Range(1, 1.1f)] private float endScale = 1.04f;
        public Texture2D Artwork => artwork;
        public float EndScale => endScale;
        public ClipCaps clipCaps => ClipCaps.Blending;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<CinematicArtworkBehaviour>.Create(graph);
            playable.GetBehaviour().Initialize(artwork, endScale, owner.GetComponent<CinematicFrameBridge>());
            return playable;
        }
    }

    public sealed class CinematicArtworkBehaviour : PlayableBehaviour
    {
        private Tween zoom, reveal;
        private float opacity;
        private float scale = 1;
        private bool released;
        private CinematicFrameBridge owner;
        internal Tween OwnedTween => zoom;
        internal Tween RevealTween => reveal;
        internal float Opacity => opacity;
        public Texture2D Artwork { get; private set; }

        internal void Initialize(Texture2D artwork, float endScale, CinematicFrameBridge bridge)
        {
            Artwork = artwork;
            scale = 1;
            opacity = 0;
            released = false;
            owner = bridge;
            owner?.Register(this);
            // Timeline is the only clock. Seek this owned, paused tween; never start a second clock.
            reveal = DOTween.To(() => opacity, value => opacity = value, 1, .2f)
                .SetEase(Ease.OutSine).SetUpdate(UpdateType.Manual).SetAutoKill(false).SetRecyclable(false).Pause();
            zoom = DOTween.To(() => scale, value => scale = value, endScale, 1)
                .SetEase(Ease.Linear).SetUpdate(UpdateType.Manual)
                .SetAutoKill(false).SetRecyclable(false).Pause();
        }

        internal float SampleScale(float normalizedClipTime, bool firstPanel = false)
        {
            if (!released && zoom != null && zoom.IsActive()) zoom.Goto(Mathf.Clamp01(normalizedClipTime), false);
            if (!released && reveal != null && reveal.IsActive()) reveal.Goto(firstPanel ? .2f : normalizedClipTime * 3, false);
            return scale;
        }

        public override void OnPlayableDestroy(Playable playable)
        { ReleaseTween(); }

        internal void ReleaseTween()
        {
            if (released) return;
            released = true;
            zoom?.Kill(false);
            reveal?.Kill(false);
            zoom = null; reveal = null;
            owner?.Unregister(this);
            owner = null;
        }
    }
}
