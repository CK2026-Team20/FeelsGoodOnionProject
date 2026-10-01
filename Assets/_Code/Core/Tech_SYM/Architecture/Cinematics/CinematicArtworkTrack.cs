using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Cooked.Cinematics
{
    [TrackColor(0.93f, 0.64f, 0.30f)]
    [TrackClipType(typeof(CinematicArtworkClip))]
    [TrackBindingType(typeof(CinematicFrameBridge))]
    public sealed class CinematicArtworkTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<CinematicArtworkMixer>.Create(graph, inputCount);
        }
    }

    public sealed class CinematicArtworkMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var bridge = playerData as CinematicFrameBridge;
            if (bridge == null) return;
            int active = -1;
            for (int i = 0; i < playable.GetInputCount(); i++)
                if (playable.GetInputWeight(i) > 0) active = i;
            if (active < 0) return; // Retain the last sample at the held endpoint.
            CinematicPanel Sample(int index)
            {
                if (index > active) return default;
                var input = (ScriptPlayable<CinematicArtworkBehaviour>)playable.GetInput(index);
                var data = input.GetBehaviour();
                float progress = index < active || input.GetDuration() <= 0 ? 1 :
                    Mathf.Clamp01((float)(input.GetTime() / input.GetDuration()));
                float scale = data.SampleScale(progress, index == 0);
                return new CinematicPanel(data.Artwork, scale, data.Opacity);
            }
            bridge.Publish(new CinematicFrame(Sample(0), Sample(1), Sample(2), Sample(3)));
        }
    }
}
