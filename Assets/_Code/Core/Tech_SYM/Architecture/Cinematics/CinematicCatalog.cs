using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Cinematics
{
    [Serializable]
    public sealed class CinematicCut
    {
        public const float DefaultDurationSeconds = 4f;
        [Tooltip("이 컷에 표시할 기존 원화 Texture2D 자산 한 장을 연결하세요. 비어 있으면 연출 준비가 실패합니다.")]
        [SerializeField] private Texture2D artwork;
        [Tooltip("현재 원화 아래에 표시할 문구입니다. 비어 있거나 공백만 입력하면 연출 준비가 실패합니다.")]
        [SerializeField] private string caption;
        [Tooltip("이 컷의 표시 시간(초)입니다. 기본은 4초이며 0 이하, 무한대, 숫자가 아닌 값은 사용할 수 없습니다. 옵션을 열면 남은 시간이 유지됩니다.")]
        [SerializeField] private float durationSeconds = DefaultDurationSeconds;
        public Texture2D Artwork => artwork;
        public string Caption => caption;
        public float DurationSeconds => durationSeconds;

        public void Configure(Texture2D artwork, string caption, float durationSeconds = DefaultDurationSeconds)
        {
            Validate(artwork, caption, durationSeconds, "Cinematic cut");
            this.artwork = artwork;
            this.caption = caption;
            this.durationSeconds = durationSeconds;
        }
        internal CinematicCut Snapshot(string source)
        {
            Validate(artwork, caption, durationSeconds, source);
            return new CinematicCut { artwork = artwork, caption = caption, durationSeconds = durationSeconds };
        }
        private static void Validate(Texture2D artwork, string caption, float duration, string source)
        {
            if (artwork == null) throw new InvalidOperationException(source + ": missing artwork Texture2D.");
            if (string.IsNullOrWhiteSpace(caption)) throw new InvalidOperationException(source + ": missing bottom caption.");
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration <= 0)
                throw new InvalidOperationException(source + ": durationSeconds must be finite and positive.");
        }
    }

    [CreateAssetMenu(menuName = "Cooked/Cinematics/Catalog")]
    public sealed class CinematicCatalog : ScriptableObject
    {
        [Tooltip("오프닝 컷을 표시 순서대로 넣으세요. 각 컷에 기존 원화 한 장, 하단 문구, 표시 시간(초)이 필요하며 컷 수에는 제한이 없습니다.")]
        [SerializeField] private CinematicCut[] openingCuts = Array.Empty<CinematicCut>();
        [Tooltip("엔딩 컷을 표시 순서대로 넣으세요. 각 컷에 기존 원화 한 장, 하단 문구, 표시 시간(초)이 필요하며 빈 목록은 사용할 수 없습니다.")]
        [SerializeField] private CinematicCut[] endingCuts = Array.Empty<CinematicCut>();

        public IReadOnlyList<CinematicCut> Get(CinematicId id) => Array.AsReadOnly(Snapshot(id));
        public void Configure(CinematicId id, params CinematicCut[] cuts)
        {
            var snapshot = Copy(cuts, id);
            switch (id)
            {
                case CinematicId.Opening: openingCuts = snapshot; break;
                case CinematicId.Ending: endingCuts = snapshot; break;
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }
        internal CinematicCut[] Snapshot(CinematicId id)
        {
            switch (id)
            {
                case CinematicId.Opening: return Copy(openingCuts, id);
                case CinematicId.Ending: return Copy(endingCuts, id);
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }
        private static CinematicCut[] Copy(CinematicCut[] cuts, CinematicId id)
        {
            if (cuts == null || cuts.Length == 0) throw new InvalidOperationException("Cinematic " + id + ": missing ordered cuts.");
            var result = new CinematicCut[cuts.Length];
            for (int i = 0; i < cuts.Length; i++)
            {
                string source = "Cinematic " + id + " cut " + (i + 1);
                if (cuts[i] == null) throw new InvalidOperationException(source + ": missing cut data.");
                result[i] = cuts[i].Snapshot(source);
            }
            return result;
        }
    }
}
