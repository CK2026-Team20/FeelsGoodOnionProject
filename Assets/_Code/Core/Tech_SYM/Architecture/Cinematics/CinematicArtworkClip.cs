using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Cooked.Cinematics
{
    public sealed class CinematicArtworkClip : PlayableAsset, ITimelineClipAsset
    {
        [Tooltip("이 타임라인 구간에 전체 화면으로 표시할 원화 텍스처입니다. 원화는 지정 순서대로 한 장씩 표시됩니다.")]
        [SerializeField] private Texture2D artwork;
        [Tooltip("이전 자산 호환용 배율입니다. 현재 컷신은 배율 1로 표시하므로 1을 유지하세요. 다른 값은 연출 준비 검사에서 거부됩니다.")]
        [SerializeField, Range(1, 1.1f)] private float endScale = 1f;
        public Texture2D Artwork => artwork;
        public float EndScale => endScale;
        public ClipCaps clipCaps => ClipCaps.None;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<CinematicArtworkBehaviour>.Create(graph);
            playable.GetBehaviour().Initialize(artwork);
            return playable;
        }
    }

    public sealed class CinematicArtworkBehaviour : PlayableBehaviour
    {
        internal float Opacity => 1;
        public Texture2D Artwork { get; private set; }
        internal void Initialize(Texture2D artwork)
        { Artwork = artwork; }
        internal float SampleScale(float normalizedClipTime, bool firstPanel = false) => 1;
    }
}
