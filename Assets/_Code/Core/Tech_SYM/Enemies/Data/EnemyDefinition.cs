using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    public enum EnemyKind { Stationary = 0, Patrol = 1 }
    // Preserve native values. Diagonals require explicit T8/T9 axis migration, never reinterpret them.
    public enum PatrolDirection { Right = 0, Left = 1, Up = 2, Down = 3, UpRight = 4, UpLeft = 5, DownRight = 6, DownLeft = 7 }

    [CreateAssetMenu(menuName = "Tech SYM/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Enemy")]
        [Tooltip("고정형(0) 또는 순찰형(1) 설정 자산입니다. 순찰의 X/Y 축과 외형은 개체 배치에서 정합니다.")]
        [SerializeField] private EnemyKind kind = EnemyKind.Patrol;
        [Tooltip("순찰 속도(m/s)입니다.")]
        [SerializeField, Min(0.01f)] private float speed = 2f;
        [Tooltip("최초 IDLE 유지 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float initialIdle = .5f;
        [Header("Knockback")]
        [Tooltip("수락된 몸통 피해에서 플레이어를 바깥쪽으로 밀어낼 속력(m/s)입니다. 양수가 필요하며 기존 자산의 값을 유지합니다.")]
        [SerializeField, Min(0f)] private float knockbackSpeed = 3f;

        public EnemyKind Kind => kind;
        public float Speed => speed;
        public int ContactDamage => 1;
        public float InitialIdle => initialIdle;
        public float KnockbackSpeed => knockbackSpeed;

        public bool IsValid => (kind == EnemyKind.Stationary || kind == EnemyKind.Patrol) && Positive(speed) &&
            float.IsFinite(initialIdle) && initialIdle >= 0 && Positive(knockbackSpeed);
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
