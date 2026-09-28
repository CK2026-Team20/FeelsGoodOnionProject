using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    public enum EnemyKind { Stationary, Patrol }
    public enum PatrolDirection { Right, Left, Up, Down, UpRight, UpLeft, DownRight, DownLeft }

    [CreateAssetMenu(menuName = "Tech SYM/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Enemy")]
        [Tooltip("고정 부유형 또는 XY 왕복 순찰형입니다.")]
        [SerializeField] private EnemyKind kind = EnemyKind.Patrol;
        [Tooltip("활성화마다 초기화할 체력입니다.")]
        [SerializeField, Min(1)] private int health = 3;
        [Tooltip("순찰 및 추격 속도(m/s)입니다.")]
        [SerializeField, Min(0.01f)] private float speed = 2f;
        [Tooltip("플레이어에게 접촉당 한 번 가하는 피해입니다.")]
        [SerializeField, Min(0)] private int contactDamage = 1;
        [Tooltip("최초 IDLE 유지 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float initialIdle = .5f;
        [Header("Knockback")]
        [Tooltip("접촉 피해가 적용된 플레이어를 적의 바깥쪽으로 밀어낼 속력(m/s). 0이면 넉백하지 않습니다.")]
        [SerializeField, Min(0f)] private float knockbackSpeed = 3f;
        [Tooltip("넉백 시 이동·점프 제어 제한 시간(ms). 0이면 새 제한을 요청하지 않습니다.")]
        [SerializeField, Min(0f)] private float controlLockMilliseconds;
        [Header("Float")]
        [Tooltip("중심에서 위아래로 움직이는 최대 거리(m)입니다. 0이면 부유하지 않습니다.")]
        [SerializeField, Min(0f)] private float floatAmplitude = .15f;
        [Tooltip("위아래 왕복 한 주기의 시간(초)입니다.")]
        [SerializeField, Min(.02f)] private float floatPeriod = 2f;

        public EnemyKind Kind => kind;
        public int Health => health;
        public float Speed => speed;
        public int ContactDamage => contactDamage;
        public float InitialIdle => initialIdle;
        public float KnockbackSpeed => knockbackSpeed;
        public float ControlLockMilliseconds => controlLockMilliseconds;
        public float FloatAmplitude => floatAmplitude;
        public float FloatPeriod => floatPeriod;

        public bool IsValid => health > 0 && contactDamage >= 0 && Positive(speed) &&
            Positive(floatPeriod) && float.IsFinite(initialIdle) && initialIdle >= 0 &&
            float.IsFinite(floatAmplitude) && floatAmplitude >= 0 &&
            float.IsFinite(knockbackSpeed) && knockbackSpeed >= 0 &&
            float.IsFinite(controlLockMilliseconds) && controlLockMilliseconds >= 0;
        private static bool Positive(float value) => float.IsFinite(value) && value > 0;

        public static Vector3 Direction(PatrolDirection value)
        {
            switch (value)
            {
                case PatrolDirection.Left: return Vector3.left;
                case PatrolDirection.Up: return Vector3.up;
                case PatrolDirection.Down: return Vector3.down;
                case PatrolDirection.UpRight: return new Vector3(1, 1, 0).normalized;
                case PatrolDirection.UpLeft: return new Vector3(-1, 1, 0).normalized;
                case PatrolDirection.DownRight: return new Vector3(1, -1, 0).normalized;
                case PatrolDirection.DownLeft: return new Vector3(-1, -1, 0).normalized;
                default: return Vector3.right;
            }
        }
    }
}
