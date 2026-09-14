using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Features
{
    [DisallowMultipleComponent]
    public sealed class HeavyState : MonoBehaviour, IState
    {
        public bool IsEntered { get; private set; }

        private void OnEnable() => Enter();
        private void OnDisable() => Exit();

        public void Enter() => IsEntered = true;

        // 더미 상태에는 이동/공격 등의 캐릭터 동작을 넣지 않는다.
        public void Update() { }

        public void Exit() => IsEntered = false;
    }
}
