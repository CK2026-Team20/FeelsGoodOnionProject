using System;
using UnityEngine;
using Cooked.Authoring;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>고정된 실물 시작/끝 지점 사이를 월드 속도로 왕복한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(KinematicPlatformMotion))]
    public sealed class LinearShuttlePlatform : MonoBehaviour
    {
        private const float MinimumMoveSpeed = 0.01f;

        [Header("이동")]
        [Tooltip("시작 지점을 연결합니다. 기능 루트 안, 몸체 밖에 두며 청록색 경로의 시작입니다.")]
        [SerializeField, InspectorLabel("시작 지점")] private Transform startPoint;
        [Tooltip("끝 지점을 연결합니다. 기능 루트 안, 몸체 밖에 두며 청록색 경로의 끝입니다. 시작과 같으면 정지합니다.")]
        [SerializeField, InspectorLabel("끝 지점")] private Transform endPoint;
        [Tooltip("경로를 따라 이동하는 속도를 조절합니다(m/s, 최소 0.01). 양 끝에서 0.5초 기다립니다.")]
        [SerializeField, InspectorLabel("이동 속도"), Min(0.01f)] private float moveSpeed = 2f;

        private KinematicPlatformMotion motion;
        private ShuttleStep step;
        private bool initialized;
        private bool invalidPathReported;

        public Vector3 StartPosition => startPoint != null ? startPoint.position : throw new InvalidOperationException("선형 발판의 시작 지점이 없습니다.");
        public Vector3 EndPosition => endPoint != null ? endPoint.position : throw new InvalidOperationException("선형 발판의 끝 지점이 없습니다.");
        public const double EndpointWaitSeconds = .5;

        public readonly struct ShuttleStep
        {
            public double Progress { get; }
            public double WaitSeconds { get; }
            public bool Returning { get; }
            public ShuttleStep(double progress, double waitSeconds, bool returning)
            { Progress = progress; WaitSeconds = waitSeconds; Returning = returning; }
        }

        /// <summary>진행·종점 대기·반전을 계산한다. 물리 접지 결과가 아닌 실제 실행의 시간 규칙이다.</summary>
        public static ShuttleStep Advance(ShuttleStep current, double length, double speed, double deltaSeconds)
        {
            if (!double.IsFinite(length) || length < 0 || !double.IsFinite(speed) || speed <= 0
                || !double.IsFinite(deltaSeconds) || deltaSeconds < 0 || !double.IsFinite(current.Progress)
                || current.Progress < 0 || current.Progress > 1 || !double.IsFinite(current.WaitSeconds)
                || current.WaitSeconds < 0 || current.WaitSeconds > EndpointWaitSeconds)
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), "왕복 이동의 경로·속도·시간·진행이 유효하지 않습니다.");
            if (length == 0) return new ShuttleStep(0, 0, false);
            double travel = length / speed;
            double cycle = 2 * (travel + EndpointWaitSeconds);
            if (!double.IsFinite(cycle) || travel <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            double phase = current.WaitSeconds > 0
                ? (current.Returning ? travel + EndpointWaitSeconds : cycle) - current.WaitSeconds
                : current.Returning ? travel + EndpointWaitSeconds + (1 - current.Progress) * travel : current.Progress * travel;
            phase = (phase + deltaSeconds % cycle) % cycle;
            if (phase < travel) return new ShuttleStep(phase / travel, 0, false);
            if (phase < travel + EndpointWaitSeconds) return new ShuttleStep(1, travel + EndpointWaitSeconds - phase, true);
            if (phase < 2 * travel + EndpointWaitSeconds) return new ShuttleStep(1 - (phase - travel - EndpointWaitSeconds) / travel, 0, true);
            return new ShuttleStep(0, cycle - phase, false);
        }

        private void Awake()
        {
            OnValidate();
            motion = GetComponent<KinematicPlatformMotion>();
            step = default;
            initialized = TryGetEndpoints(out _, out _);
            if (!initialized) { ReportInvalidPath("실물 시작/끝 지점을 같은 씬의 움직이는 몸체 밖에 연결하세요."); enabled = false; }
        }

        private void FixedUpdate()
        {
            if (!initialized || motion == null || !motion.isActiveAndEnabled || Time.timeScale <= 0f) return;
            float deltaTime = Time.fixedDeltaTime;
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f) return;
            if (!TryGetEndpoints(out var start, out var end))
            {
                ReportInvalidPath("시작/끝 지점이 제거되었거나 유효한 고정 지점이 아닙니다.");
                enabled = false;
                return;
            }

            // 부모 배율이 바뀌어도 현재 월드 경로 길이에 맞는 속도로 진행한다.
            double length = PathLength(start, end);
            ShuttleStep nextStep = Advance(step, length, moveSpeed, deltaTime);
            Vector3 target = PointOnPath(start, end, nextStep.Progress);

            // MoveTo의 좌표뿐 아니라 운반 속도를 구하는 float 연산의 overflow도 막는다.
            Vector3 velocity = (target - motion.Position) / deltaTime;
            if (!Finite(target) || !Finite(velocity))
            {
                ReportInvalidPath("이번 물리 스텝의 목표 위치 또는 탑승 속도가 유한한 범위를 벗어나 이동을 중단합니다.");
                return;
            }
            step = nextStep;
            motion.MoveTo(target);
        }

        /// <summary>Stage의 초기 경로 시작점으로 복구한다. 저장 데이터가 아니다.</summary>
        public void ResetForRetry()
        {
            step = default;
            invalidPathReported = false;
            if (motion != null && TryGetEndpoints(out var start, out _))
                motion.ResetForRetry(new Pose(start, startPoint.rotation));
            else motion?.ResetForRetry();
        }

        private bool TryGetEndpoints(out Vector3 start, out Vector3 end)
        {
            start = end = Vector3.zero;
            var writer = motion != null ? motion : GetComponent<KinematicPlatformMotion>();
            if (writer == null || !writer.IsFixedPoint(startPoint) || !writer.IsFixedPoint(endPoint)) return false;
            // Transform.position이 작성 부모→world의 유일한 변환 경계다.
            start = startPoint.position;
            end = endPoint.position;
            return Finite(start) && Finite(end);
        }

        public static double PathLength(Vector3 start, Vector3 end)
        {
            double x = (double)end.x - start.x;
            double y = (double)end.y - start.y;
            double z = (double)end.z - start.z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        public static Vector3 PointOnPath(Vector3 start, Vector3 end, double fraction) => new Vector3(
            (float)((double)start.x + ((double)end.x - start.x) * fraction),
            (float)((double)start.y + ((double)end.y - start.y) * fraction),
            (float)((double)start.z + ((double)end.z - start.z) * fraction));

        private static bool Finite(Vector3 value) => float.IsFinite(value.x)
            && float.IsFinite(value.y) && float.IsFinite(value.z);

        private void OnDisable() => motion?.CancelMotion();

        private void ReportInvalidPath(string reason)
        {
            if (invalidPathReported) return;
            invalidPathReported = true;
            Debug.LogWarning($"선형 발판 '{name}': {reason}", this);
        }

        private void OnValidate()
        {
            if (!float.IsFinite(moveSpeed) || moveSpeed < MinimumMoveSpeed)
            {
                moveSpeed = MinimumMoveSpeed;
                Debug.LogWarning($"선형 발판 '{name}': 잘못된 이동 속도를 최소값 0.01m/s로 변경했습니다.", this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryGetEndpoints(out var start, out var end)) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(start, end);
            var writer = motion != null ? motion : GetComponent<KinematicPlatformMotion>();
            if (writer == null || writer.PhysicsBody == null) return;
            PlatformColliderGizmos.Draw(writer.Contacts, Matrix4x4.identity);
            PlatformColliderGizmos.DrawAt(writer, start, writer.PhysicsBody.rotation);
            PlatformColliderGizmos.DrawAt(writer, end, writer.PhysicsBody.rotation);
        }
    }
}
