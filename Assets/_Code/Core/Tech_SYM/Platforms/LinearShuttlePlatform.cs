using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>월드 X/Y 방향으로 일정 속도 왕복.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(KinematicPlatformMotion))]
    public sealed class LinearShuttlePlatform : MonoBehaviour
    {
        public enum TravelDirection { Left, Right, Up, Down, UpLeft, UpRight, DownLeft, DownRight }
        [Header("Movement")]
        [Tooltip("최초 이동 방향. 실행 중에는 변경되지 않습니다.")]
        [SerializeField] private TravelDirection initialDirection = TravelDirection.Right;
        [Tooltip("이동 속도(m/s).")]
        [SerializeField, Min(0.01f)] private float moveSpeed = 2f;
        [Tooltip("편도 이동 시간(초). 같은 경로로 무한 왕복합니다.")]
        [SerializeField, Min(0.02f)] private float travelSeconds = 3f;

        private KinematicPlatformMotion motion;
        private Vector3 origin;
        private Vector3 offset;
        private double elapsed;
        private float duration;
        public Vector3 StartPosition => origin;
        public Vector3 EndPosition => origin + offset;

        private void Awake()
        {
            OnValidate();
            motion = GetComponent<KinematicPlatformMotion>();
            origin = transform.position;
            duration = travelSeconds;
            offset = DirectionVector(initialDirection) * moveSpeed * duration;
        }

        private void FixedUpdate()
        {
            if (motion == null || !motion.isActiveAndEnabled) return;
            elapsed += Time.fixedDeltaTime;
            double phase = elapsed % (2.0 * duration);
            float fraction = (float)(phase <= duration ? phase / duration : 2.0 - phase / duration);
            motion.MoveTo(origin + offset * fraction);
        }

        public static Vector3 DirectionVector(TravelDirection direction)
        {
            switch (direction)
            {
                case TravelDirection.Left: return Vector3.left;
                case TravelDirection.Up: return Vector3.up;
                case TravelDirection.Down: return Vector3.down;
                case TravelDirection.UpLeft: return new Vector3(-1f, 1f, 0f).normalized;
                case TravelDirection.UpRight: return new Vector3(1f, 1f, 0f).normalized;
                case TravelDirection.DownLeft: return new Vector3(-1f, -1f, 0f).normalized;
                case TravelDirection.DownRight: return new Vector3(1f, -1f, 0f).normalized;
                default: return Vector3.right;
            }
        }

        private void OnValidate()
        {
            if (!float.IsFinite(moveSpeed) || moveSpeed < 0.01f) moveSpeed = 0.01f;
            if (!float.IsFinite(travelSeconds) || travelSeconds < 0.02f) travelSeconds = 0.02f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 start = Application.isPlaying ? origin : transform.position;
            Vector3 end = Application.isPlaying ? origin + offset : start + DirectionVector(initialDirection) * moveSpeed * travelSeconds;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireCube(end, transform.lossyScale);
        }
    }
}
