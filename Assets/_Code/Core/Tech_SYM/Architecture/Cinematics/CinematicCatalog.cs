using System;
using Cooked.Contracts;
using UnityEngine;
using UnityEngine.Timeline;

namespace Cooked.Cinematics
{
    [CreateAssetMenu(menuName = "Cooked/Cinematics/Catalog")]
    public sealed class CinematicCatalog : ScriptableObject
    {
        [SerializeField] private TimelineAsset opening;
        [SerializeField] private TimelineAsset ending;
        public TimelineAsset Opening => opening;
        public TimelineAsset Ending => ending;
        public TimelineAsset Get(CinematicId id)
        {
            switch (id)
            {
                case CinematicId.Opening: return opening;
                case CinematicId.Ending: return ending;
                default: throw new ArgumentOutOfRangeException(nameof(id));
            }
        }
    }
}
