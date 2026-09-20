using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>
    /// 플레이어의 무거운 속성. Rigidbody와 같은 오브젝트에 추가한다.
    /// 비활성화해도 적용되며, 제거하면 해제된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeavyState : MonoBehaviour { }
}
