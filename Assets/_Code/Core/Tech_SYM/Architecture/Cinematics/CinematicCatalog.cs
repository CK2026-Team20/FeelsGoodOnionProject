using System;
using Cooked.Contracts;
using UnityEngine;
using UnityEngine.Timeline;

namespace Cooked.Cinematics
{
    [CreateAssetMenu(menuName = "Cooked/Cinematics/Catalog")]
    public sealed class CinematicCatalog : ScriptableObject
    {
        [Tooltip("새 게임 시작 시 재생할 오프닝 Timeline 자산입니다. 원화의 순서와 표시 시간은 이 자산에서 편집하세요.")]
        [SerializeField] private TimelineAsset opening;
        [Tooltip("탈출 완료 후 재생할 엔딩 Timeline 자산입니다. 완료 후 타이틀로 돌아갑니다.")]
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
