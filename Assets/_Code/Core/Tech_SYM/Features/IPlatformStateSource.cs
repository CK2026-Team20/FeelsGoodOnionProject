using System.Collections.Generic;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>플랫폼이 구체적인 캐릭터 상태 구현을 몰라도 조회할 수 있는 경계.</summary>
    public interface IPlatformStateSource
    {
        bool IsHeavy(Collider actorCollider);
        void CollectHeavyColliders(List<Collider> results);
    }
}
