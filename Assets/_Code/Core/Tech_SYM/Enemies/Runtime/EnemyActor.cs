using System;
using Cooked.Authoring;
using Cooked.Integration;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed class EnemyActor : MonoBehaviour, IStunnable, IStunState
    {
        [SerializeField, InspectorLabel("설정 자산"), Tooltip("적 종류와 이동 속도, 첫 대기 시간을 정할 자산을 연결하세요.")]
        private EnemyDefinition definition;
        [SerializeField, InspectorLabel("몸체"), Tooltip("고정 기능 루트 안에서 움직일 몸체 Trigger를 연결하세요. 같은 객체에 접촉 센서, 그 아래에 외형과 효과를 둡니다.")]
        private BoxCollider body;
        [SerializeField, InspectorLabel("순찰 시작점"), Tooltip("기능 루트 안, 몸체 밖의 고정 시작점을 연결하세요. 하늘색으로 표시하며 고정 적은 비워 둡니다.")]
        private Transform patrolStart;
        [SerializeField, InspectorLabel("순찰 끝점"), Tooltip("기능 루트 안, 몸체 밖의 고정 끝점을 연결하세요. XYZ 방향을 자유롭게 배치하며 하늘색으로 표시합니다. 고정 적은 비워 둡니다.")]
        private Transform patrolEnd;
        private EnemyActivityState currentState = EnemyActivityState.IDLE, resumeState = EnemyActivityState.IDLE;
        private float idleRemaining;
        private double stunMilliseconds;
        private bool towardEnd = true, initialPlacementCaptured, setupFailed, prepared, publishedStunState;
        private Vector3 initialLocalPosition;
        private EnemyContactSensor contactSensor;
        public EnemyDefinition Definition => definition;
        private EnemyMotor Motor { get; set; }
        public bool IsStunned => currentState == EnemyActivityState.STUNNED;
        public bool IsPrepared => prepared;
        public BoxCollider HurtCollider => body;
        /// <summary>위치 작성 대상입니다. 고정 Root와 그 아래 Anchors는 이동하지 않습니다.</summary>
        public Transform MotionBody => body != null ? body.transform : null;
        public event Action<bool> StunStateChanged;

        private void OnEnable()
        {
            if (!prepared) ResetForRetry();
        }
        private void FailSetup(Exception exception)
        {
            prepared = false;
            Motor = null;
            contactSensor?.UnbindPlayer();
            if (!setupFailed)
                Debug.LogError($"적 '{name}'의 배치 준비에 실패했습니다: {exception.Message}", this);
            setupFailed = true;
            enabled = false;
        }
        private void ValidateSetup()
        {
            if (definition == null || !definition.IsValid || body == null)
                throw new InvalidOperationException("설정 자산과 실제 몸체, 유효한 설정이 필요합니다.");
            if (body.transform == transform || !body.transform.IsChildOf(transform) || !body.isTrigger)
                throw new InvalidOperationException("고정 기능 루트 안의 별도 몸체 Trigger가 필요합니다.");
            contactSensor = body.GetComponent<EnemyContactSensor>();
            if (contactSensor == null || contactSensor.Owner != this)
                throw new InvalidOperationException("몸체 Trigger와 같은 객체의 접촉 센서가 이 적을 소유자로 연결해야 합니다.");
            if (GetComponentInParent<Rigidbody>() != null || GetComponentInChildren<Rigidbody>(true) != null)
                throw new InvalidOperationException("적 및 조상/자식에 Rigidbody를 둘 수 없습니다.");
            if (!Finite(body.size) || !Finite(body.center) || !Finite(transform.position) || !Finite(body.transform.position) ||
                body.size.x <= 0 || body.size.y <= 0 || body.size.z <= 0 ||
                !PositiveScale(transform.lossyScale) || !PositiveScale(body.transform.lossyScale))
                throw new InvalidOperationException("몸체 크기와 실제 배율은 양수·유한값, 몸체 위치는 유한값이어야 합니다.");
            if (definition.Kind == EnemyKind.Patrol)
            {
                RequirePatrolPoint(patrolStart, "순찰 시작점");
                RequirePatrolPoint(patrolEnd, "순찰 끝점");
                EnemyMotor.ValidatePatrol(patrolStart.position, patrolEnd.position, body.transform.position);
            }
            towardEnd = true;
        }
        internal void RequirePatrolPoint(Transform point, string label)
        {
            if (!IsPatrolPoint(point))
                throw new InvalidOperationException(label + ": 기능 루트 안, 몸체 밖의 유효한 고정 지점을 연결하세요.");
        }
        private bool IsPatrolPoint(Transform point) => point != null && body != null && point != transform
            && point.IsChildOf(transform) && !point.IsChildOf(body.transform)
            && SceneComponentLookup.IsFixedPoint(this, point);
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        private static bool PositiveScale(Vector3 value) => Finite(value) && value.x > 0 && value.y > 0 && value.z > 0;
        private void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>적의 상태·이동을 진행합니다. 접촉 반복 피해의 갱신자는 몸체 Sensor입니다.</summary>
        public void Tick(float deltaSeconds)
        {
            if (!isActiveAndEnabled || !prepared || !float.IsFinite(deltaSeconds) || deltaSeconds <= 0 ||
                (contactSensor != null && contactSensor.PhysicsPaused)) return;
            try
            {
                if (definition == null || !definition.IsValid)
                    throw new InvalidOperationException("실행할 적 설정 자산과 유효한 속도·대기 시간이 필요합니다.");
                float patrolSeconds = AdvanceState(definition.Kind, deltaSeconds, ref currentState, resumeState,
                    ref idleRemaining, ref stunMilliseconds);
                if (patrolSeconds > 0) Motor.Move(definition.Speed * patrolSeconds, ref towardEnd);
                if (!IsStunned) Motor.ApplyPosition();
            }
            catch (InvalidOperationException exception) { FailSetup(exception); }
            catch (ArgumentException exception) { FailSetup(exception); }
            PublishStunStateChanged();
        }
        private void OnDisable()
        {
            prepared = false;
            contactSensor?.UnbindPlayer();
            Motor = null;
            currentState = resumeState = EnemyActivityState.IDLE;
            stunMilliseconds = 0; idleRemaining = 0;
            PublishStunStateChanged();
        }
        /// <summary>같은 Stage의 최초 위치·방향·기절·접촉을 재준비합니다. 저장 payload가 아닙니다.</summary>
        public void ResetForRetry()
        {
            try { PrepareForRetry(); }
            catch (InvalidOperationException exception) { FailSetup(exception); }
            catch (ArgumentException exception) { FailSetup(exception); }
        }
        private void PrepareForRetry()
        {
            prepared = false;
            contactSensor?.ClearContacts();
            Motor = null;
            ResetState(EnemyKind.Stationary, 0, out currentState, out resumeState, out idleRemaining, out stunMilliseconds);
            if (!initialPlacementCaptured)
            {
                ValidateSetup();
                initialLocalPosition = body.transform.localPosition;
                initialPlacementCaptured = true;
            }
            if (body == null) throw new InvalidOperationException("복원할 적 몸체가 없습니다.");
            body.transform.localPosition = initialLocalPosition;
            ValidateSetup();
            // 복원된 Body Collider를 반영한다. 자연 물리 콜백을 강제로 만들지 않는다.
            if (gameObject.activeInHierarchy) Physics.SyncTransforms();
            Motor = new EnemyMotor(this, body, patrolStart, patrolEnd);
            ResetState(definition.Kind, definition.InitialIdle, out currentState, out resumeState, out idleRemaining, out stunMilliseconds);
            prepared = true;
            bool recoverFailedSetup = setupFailed;
            setupFailed = false;
            if (recoverFailedSetup) enabled = true;
            PublishStunStateChanged();
        }
        public int TryStun(int stunDur)
        {
            if (!isActiveAndEnabled || !prepared) return 0;
            int applied = AddStun(stunDur, ref currentState, ref resumeState, ref stunMilliseconds);
            PublishStunStateChanged();
            return applied;
        }
        /// <summary>Actor가 소유한 실행 상태를 초기화하는 순수 규칙입니다. 공유 설정 자산을 변경하지 않습니다.</summary>
        public static void ResetState(EnemyKind kind, float initialIdle, out EnemyActivityState state, out EnemyActivityState resume,
            out float idle, out double stun)
        {
            if ((kind != EnemyKind.Stationary && kind != EnemyKind.Patrol) || !float.IsFinite(initialIdle) || initialIdle < 0)
                throw new ArgumentOutOfRangeException(nameof(initialIdle));
            state = kind == EnemyKind.Patrol && initialIdle == 0 ? EnemyActivityState.PATROL : EnemyActivityState.IDLE;
            resume = state; idle = initialIdle; stun = 0;
        }
        public static int AddStun(int milliseconds, ref EnemyActivityState state, ref EnemyActivityState resume, ref double remainingMilliseconds)
        {
            if (milliseconds <= 0) return 0;
            if (!double.IsFinite(remainingMilliseconds) || remainingMilliseconds < 0)
                throw new ArgumentOutOfRangeException(nameof(remainingMilliseconds));
            if (state != EnemyActivityState.STUNNED) resume = state;
            remainingMilliseconds += milliseconds;
            state = EnemyActivityState.STUNNED;
            return milliseconds;
        }
        /// <summary>이 단계에서 순찰에 쓸 시간(초)을 반환합니다. 기절 동안 대기 시계도 멈춥니다.</summary>
        public static float AdvanceState(EnemyKind kind, float delta, ref EnemyActivityState state, EnemyActivityState resume,
            ref float idle, ref double stun)
        {
            if (!float.IsFinite(delta) || delta <= 0) return 0;
            if (state == EnemyActivityState.STUNNED)
            {
                stun = Math.Max(0, stun - (double)delta * 1000);
                if (stun <= .001) { stun = 0; state = resume; }
                return 0;
            }
            if (kind == EnemyKind.Stationary) return 0;
            if (state == EnemyActivityState.IDLE)
            {
                float used = Math.Min(idle, delta);
                idle -= used;
                if (idle > 0) return 0;
                state = EnemyActivityState.PATROL;
                return delta - used;
            }
            return state == EnemyActivityState.PATROL ? delta : 0;
        }
        private void PublishStunStateChanged()
        {
            bool value = IsStunned;
            if (publishedStunState == value) return;
            publishedStunState = value;
            StunStateChanged?.Invoke(value);
        }
        private void OnDrawGizmosSelected()
        {
            if (definition == null || body == null) return;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            try
            {
                Gizmos.color = Color.cyan;
                Gizmos.matrix = body.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(body.center, body.size);
                if (definition.Kind != EnemyKind.Patrol || patrolStart == null || patrolEnd == null) return;
                bool invalid = !IsPatrolPoint(patrolStart) || !IsPatrolPoint(patrolEnd);
                try { EnemyMotor.ValidatePatrol(patrolStart.position, patrolEnd.position, body.transform.position); }
                catch (ArgumentException) { invalid = true; }
                Gizmos.color = invalid ? Color.red : Color.cyan;
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.DrawLine(patrolStart.position, patrolEnd.position);
                // 월드 평행이동만 더해 Body의 실제 회전·비균등 배율·Collider 중심을 그대로 표시한다.
                Gizmos.matrix = Matrix4x4.Translate(patrolStart.position - body.transform.position) * body.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(body.center, body.size);
                Gizmos.matrix = Matrix4x4.Translate(patrolEnd.position - body.transform.position) * body.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(body.center, body.size);
            }
            finally
            {
                Gizmos.matrix = previousMatrix;
                Gizmos.color = previousColor;
            }
        }
    }
}
