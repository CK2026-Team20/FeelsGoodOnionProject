using System;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>피해 Trigger가 있는 자식의 물리 콜백만 전달합니다. 피해 규칙과 구독 수명은 피해원이 소유합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class DamageTriggerRelay : MonoBehaviour
    {
        public event Action<Collider> ContactReported;
        private void OnTriggerEnter(Collider other) => Report(other);
        private void OnTriggerStay(Collider other) => Report(other);
        private void Report(Collider other)
        {
            if (isActiveAndEnabled) ContactReported?.Invoke(other);
        }
    }
}
