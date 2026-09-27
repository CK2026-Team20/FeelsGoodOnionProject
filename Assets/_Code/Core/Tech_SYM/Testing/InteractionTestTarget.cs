using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Testing
{
    /// <summary>Play 검증에서만 생성하는 메모 이외의 공통 계약 테스트 대상.</summary>
    public sealed class InteractionTestTarget : MonoBehaviour, IInteractable
    {
        public int InvocationCount { get; private set; }
        public bool Interact() { InvocationCount++; return true; }
    }
}
