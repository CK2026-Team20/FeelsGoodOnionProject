using Cooked.Authoring;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    public enum EnemyActivityState { IDLE = 0, PATROL = 1, STUNNED = 2 }
    public enum EnemyKind
    {
        [InspectorName("고정")] Stationary = 0,
        [InspectorName("순찰")] Patrol = 1
    }
    [CreateAssetMenu(menuName = "Tech SYM/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField, InspectorLabel("종류"), Tooltip("고정 또는 순찰 종류를 정합니다. 순찰 시작점과 끝점은 씬의 적에서 연결합니다.")]
        private EnemyKind kind = EnemyKind.Patrol;
        [SerializeField, Min(.01f), InspectorLabel("이동 속도"), Tooltip("Body가 실제 공간에서 초당 이동할 거리를 정합니다(m/s). 루트 배율과 무관하며 양수가 필요합니다.")]
        private float speed = 2f;
        [SerializeField, Min(0), InspectorLabel("첫 대기 시간"), Tooltip("순찰을 시작하기 전 대기 시간을 정합니다(초). 0이면 바로 이동합니다.")]
        private float initialIdle = .5f;

        public EnemyKind Kind => kind;
        public float Speed => speed;
        public float InitialIdle => initialIdle;

        public bool IsValid => (kind == EnemyKind.Stationary || kind == EnemyKind.Patrol) && Positive(speed) &&
            float.IsFinite(initialIdle) && initialIdle >= 0;
        private static bool Positive(float value) => float.IsFinite(value) && value > 0;
    }
}
