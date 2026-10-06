using System;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>부모 기준 시작점과 전체 이동량 사이를 일정 속도로 왕복한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(KinematicPlatformMotion))]
    public sealed class LinearShuttlePlatform : MonoBehaviour
    {
        private const float MinimumMoveSpeed = 0.01f;

        [Header("이동")]
        [Tooltip("시작점에서 도착점까지의 전체 이동량(X/Y/Z, m). 부모가 있으면 부모의 로컬 좌표이며 회전·배율이 반영됩니다. 부모가 없으면 월드 좌표입니다. 0이면 시작점에 머뭅니다. 실행 시작 시 적용하며 청록색 기즈모로 경로를 표시합니다.")]
        [SerializeField] private Vector3 travelOffset = new Vector3(6f, 0f, 0f);
        [Tooltip("현재 월드 경로를 따라 이동하는 속도(m/s). 값이 클수록 빨리 왕복하며 최소값은 0.01입니다. 편도시간은 경로 길이와 속도로 계산하고 끝점에서 기다리지 않습니다. 실행 시작 시 적용합니다.")]
        [SerializeField, Min(0.01f)] private float moveSpeed = 2f;

        private KinematicPlatformMotion motion;
        private Transform pathParent;
        private Vector3 startInParent;
        private Vector3 cachedTravelOffset;
        private float cachedMoveSpeed;
        private double cycleProgress;
        private bool initialized;
        private bool hadParent;
        private bool invalidPathReported;

        public Vector3 StartPosition => TryGetEndpoints(out var start, out _) ? start : SafeCurrentPosition();
        public Vector3 EndPosition => TryGetEndpoints(out _, out var end) ? end : SafeCurrentPosition();

        private void Awake()
        {
            OnValidate();
            motion = GetComponent<KinematicPlatformMotion>();
            pathParent = transform.parent;
            hadParent = pathParent != null;
            startInParent = hadParent ? transform.localPosition : transform.position;
            cachedTravelOffset = travelOffset;
            cachedMoveSpeed = moveSpeed;
            cycleProgress = 0.0;
            initialized = true;
        }

        private void FixedUpdate()
        {
            if (!initialized || motion == null || !motion.isActiveAndEnabled) return;
            float deltaTime = Time.fixedDeltaTime;
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f) return;
            if (!TryGetEndpoints(out var start, out var end))
            {
                ReportInvalidPath("경로 좌표가 유한한 범위를 벗어났거나 실행 시작 시의 부모가 제거되어 이동을 중단합니다.");
                return;
            }

            // 부모 배율이 바뀌어도 현재 월드 경로 길이에 맞는 속도로 진행한다.
            double length = PathLength(start, end);
            double nextProgress = cycleProgress;
            if (length > 0.0)
            {
                double oneWaySeconds = length / cachedMoveSpeed;
                nextProgress = (cycleProgress + (deltaTime / oneWaySeconds) % 2.0) % 2.0;
            }
            double fraction = nextProgress <= 1.0 ? nextProgress : 2.0 - nextProgress;
            Vector3 target = PointOnPath(start, end, fraction);

            // MoveTo의 좌표뿐 아니라 운반 속도를 구하는 float 연산의 overflow도 막는다.
            Vector3 velocity = (target - motion.Position) / deltaTime;
            if (!Finite(target) || !Finite(velocity))
            {
                ReportInvalidPath("이번 물리 스텝의 목표 위치 또는 탑승 속도가 유한한 범위를 벗어나 이동을 중단합니다.");
                return;
            }
            cycleProgress = nextProgress;
            motion.MoveTo(target);
        }

        private bool TryGetEndpoints(out Vector3 start, out Vector3 end)
        {
            bool useCachedPath = Application.isPlaying && initialized;
            Transform parent = useCachedPath ? pathParent : transform.parent;
            Vector3 localStart = useCachedPath ? startInParent
                : parent != null ? transform.localPosition : transform.position;
            Vector3 offset = useCachedPath ? cachedTravelOffset : travelOffset;
            start = end = Vector3.zero;
            if (useCachedPath && hadParent && parent == null) return false;
            if (!Finite(localStart) || !Finite(offset)) return false;

            // float 덧셈/보간의 중간 overflow를 피하고 변환 결과도 경계에서 확인한다.
            Vector3 localEnd = new Vector3(
                (float)((double)localStart.x + offset.x),
                (float)((double)localStart.y + offset.y),
                (float)((double)localStart.z + offset.z));
            if (!Finite(localEnd)) return false;
            start = parent != null ? parent.TransformPoint(localStart) : localStart;
            end = parent != null ? parent.TransformPoint(localEnd) : localEnd;
            return Finite(start) && Finite(end);
        }

        private static double PathLength(Vector3 start, Vector3 end)
        {
            double x = (double)end.x - start.x;
            double y = (double)end.y - start.y;
            double z = (double)end.z - start.z;
            return Math.Sqrt(x * x + y * y + z * z);
        }

        private static Vector3 PointOnPath(Vector3 start, Vector3 end, double fraction) => new Vector3(
            (float)((double)start.x + ((double)end.x - start.x) * fraction),
            (float)((double)start.y + ((double)end.y - start.y) * fraction),
            (float)((double)start.z + ((double)end.z - start.z) * fraction));

        private static bool Finite(Vector3 value) => float.IsFinite(value.x)
            && float.IsFinite(value.y) && float.IsFinite(value.z);

        private Vector3 SafeCurrentPosition() => Finite(transform.position) ? transform.position : Vector3.zero;

        private void ReportInvalidPath(string reason)
        {
            if (invalidPathReported) return;
            invalidPathReported = true;
            Debug.LogWarning($"선형 발판 '{name}': {reason}", this);
        }

        private void OnValidate()
        {
            if (!Finite(travelOffset))
            {
                travelOffset = Vector3.zero;
                Debug.LogWarning($"선형 발판 '{name}': 이동량의 NaN/Infinity를 0으로 변경했습니다.", this);
            }
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
            Vector3 size = transform.lossyScale;
            if (Finite(size)) Gizmos.DrawWireCube(end, size);
        }
    }
}
