using Cooked.Authoring;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    public enum EnemyState { IDLE = 0, PATROL = 1, STUNNED = 2 }
    public enum EnemyKind
    {
        [InspectorName("고정")] Stationary = 0,
        [InspectorName("순찰")] Patrol = 1
    }
    // 기존 번호를 보존한다. 대각선 배치는 축 방향으로 명시적으로 이관한다.
    public enum PatrolDirection
    {
        [InspectorName("오른쪽")] Right = 0,
        [InspectorName("왼쪽")] Left = 1,
        [InspectorName("위")] Up = 2,
        [InspectorName("아래")] Down = 3,
        [InspectorName("오른쪽 위(이관 필요)")] UpRight = 4,
        [InspectorName("왼쪽 위(이관 필요)")] UpLeft = 5,
        [InspectorName("오른쪽 아래(이관 필요)")] DownRight = 6,
        [InspectorName("왼쪽 아래(이관 필요)")] DownLeft = 7
    }

    [CreateAssetMenu(menuName = "Tech SYM/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField, InspectorLabel("종류"), Tooltip("고정 또는 순찰 종류를 정합니다. 순찰 지점과 첫 방향은 씬의 적에서 연결합니다.")]
        private EnemyKind kind = EnemyKind.Patrol;
        [SerializeField, Min(.01f), InspectorLabel("이동 속도"), Tooltip("순찰 이동 속도를 정합니다(m/s). 양수가 필요합니다.")]
        private float speed = 2f;
        [SerializeField, Min(0), InspectorLabel("첫 대기 시간"), Tooltip("순찰을 시작하기 전 대기 시간을 정합니다(초). 0이면 바로 이동합니다.")]
        private float initialIdle = .5f;

        public EnemyKind Kind => kind;
        public float Speed => speed;
        public float InitialIdle => initialIdle;

        public bool IsValid => (kind == EnemyKind.Stationary || kind == EnemyKind.Patrol) && Positive(speed) &&
            float.IsFinite(initialIdle) && initialIdle >= 0;
        private static bool Positive(float value) => float.IsFinite(value) && value > 0;
        public static bool IsAxisDirection(PatrolDirection value) => value >= PatrolDirection.Right && value <= PatrolDirection.Down;

        public static Vector3 Direction(PatrolDirection value)
        {
            switch (value)
            {
                case PatrolDirection.Left: return Vector3.left;
                case PatrolDirection.Up: return Vector3.up;
                case PatrolDirection.Down: return Vector3.down;
                case PatrolDirection.Right: return Vector3.right;
                default: throw new System.ArgumentOutOfRangeException(nameof(value), value, "X/Y 축 방향으로 명시적으로 이관해야 합니다.");
            }
        }
    }
}
