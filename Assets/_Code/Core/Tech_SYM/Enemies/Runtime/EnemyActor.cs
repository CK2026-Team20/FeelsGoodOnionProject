using System;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed class EnemyActor : MonoBehaviour, IStunnable, IStunState
    {
        [Header("Definition")]
        [Tooltip("개체별 실행 상태는 공유하지 않는 설정 SO입니다.")]
        [SerializeField] private EnemyDefinition definition;
        [Tooltip("몸통 접촉 피해를 판정하는 실제 몸체 Trigger입니다.")]
        [SerializeField] private BoxCollider body;
        [Tooltip("몸통 접촉 전투 대상. 씬에서 명시적으로 연결합니다.")]
        [SerializeField] private PlayerFacade target;
        [Header("Patrol")]
        [Tooltip("월드 X/Y 순찰의 최초 방향입니다. 오른쪽0·왼쪽1·위2·아래3만 사용합니다. 하늘색은 경로, 빨강은 막힌 배치입니다. 대각선4~7은 명시 이관이 필요합니다.")]
        [SerializeField] private PatrolDirection initialDirection;
        [Tooltip("최초 월드 위치에서 순찰 끝점까지의 양수 거리(m)입니다. 하늘색 경로와 끝점으로 표시합니다.")]
        [SerializeField, Min(.01f)] private float patrolDistance = 3f;
        [Header("Height")]
        [Tooltip("플랫폼과 벽을 검사할 레이어입니다. Trigger는 제외합니다.")]
        [SerializeField] private LayerMask terrainMask = 1;
        [Tooltip("시작 시 위아래 플랫폼 탐색 거리(m)입니다.")]
        [SerializeField, Min(.01f)] private float heightSearchDistance = 8f;
        [Tooltip("플랫폼과 몸체 표면 사이 최소 간격(m)입니다.")]
        [SerializeField, Min(0)] private float surfaceClearance = .15f;
        [Tooltip("아래 플랫폼이 없을 때 최초 월드 위치 아래로 허용할 거리(m)입니다. 0이면 아래 이동 불가이며 노란 선으로 표시합니다.")]
        [SerializeField, Min(0)] private float manualLowerDistance = 2f;
        [Tooltip("위 플랫폼이 없을 때 최초 월드 위치 위로 허용할 거리(m)입니다. 0이면 위 이동 불가이며 노란 선으로 표시합니다.")]
        [SerializeField, Min(0)] private float manualUpperDistance = 3f;

        private EnemyStateMachine states;
        private EnemyCombat combat;
        private bool initialPlacementCaptured;
        private Vector3 initialPosition;
        private bool publishedStunState;
        public EnemyDefinition Definition => definition;
        public EnemyMotor Motor { get; private set; }
        public EnemyState CurrentState => states?.Current ?? EnemyState.IDLE;
        public bool IsStunned => states != null && states.IsStunned;
        public double StunRemainingSeconds => states?.StunRemainingSeconds ?? 0;
        public bool TowardEnd => states == null || states.TowardEnd;
        public Vector3 BodyPosition => body != null ? body.transform.position : transform.position;
        public BoxCollider HurtCollider => body;
        // T9 removes EnemyOilOnDeath and this inert compile contract together; no kill/oil execution.
        [Obsolete("T9: remove with EnemyOilOnDeath native/source transfer. General enemies cannot die.")]
        public event Action<EnemyActor> Died { add { } remove { } }
        /// <summary>스턴 상태가 변경되면 알립니다. true는 적용, false는 해제입니다.</summary>
        public event Action<bool> StunStateChanged;

        private void OnEnable()
        {
            try
            {
                ResetForRetry();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Enemy setup failed ({name}): {exception.Message}", this);
                enabled = false;
            }
            if (isActiveAndEnabled) PublishStunStateChanged();
        }
        private void ValidateSetup()
        {
            if (definition == null || !definition.IsValid || body == null)
                throw new InvalidOperationException("Definition, 몸체 및 유효한 설정이 필요합니다.");
            if (body.transform.parent != transform || !body.isTrigger)
                throw new InvalidOperationException("루트 바로 아래 몸체 Trigger가 필요합니다.");
            if (GetComponentInParent<Rigidbody>() != null || GetComponentInChildren<Rigidbody>(true) != null)
                throw new InvalidOperationException("적 및 조상/자식에 Rigidbody를 둘 수 없습니다.");
            if (!AxisAligned(transform.rotation) || !AxisAligned(body.transform.rotation) ||
                !UnitScale(transform.lossyScale) || !UnitScale(body.transform.lossyScale) ||
                !UnitScale(transform.localScale) || !UnitScale(body.transform.localScale))
                throw new InvalidOperationException("물리 루트/몸체는 단위 스케일, Collider는 월드 축 정렬을 사용하세요. 외형 자식만 회전/확대하세요.");
            if (body.size.x <= 0 || body.size.y <= 0 || body.size.z <= 0)
                throw new InvalidOperationException("몸체 Collider 크기는 양수여야 합니다.");
            if (!float.IsFinite(body.size.x) || !float.IsFinite(body.size.y) || !float.IsFinite(body.size.z) ||
                !float.IsFinite(body.center.x) || !float.IsFinite(body.center.y) || !float.IsFinite(body.center.z) ||
                !float.IsFinite(transform.position.x) || !float.IsFinite(transform.position.y) || !float.IsFinite(transform.position.z) ||
                (body.transform.localPosition - Vector3.zero).sqrMagnitude > .0001f || terrainMask.value == 0 ||
                !EnemyDefinition.IsAxisDirection(initialDirection))
                throw new InvalidOperationException("몸체/위치는 유한값, 몸체 로컬 위치는 0, 지형 레이어는 비어 있지 않아야 하며 순찰 방향은 X/Y 축만 허용합니다.");
            if (!FinitePositive(patrolDistance) || !FinitePositive(heightSearchDistance) ||
                !NonNegative(surfaceClearance) || !NonNegative(manualLowerDistance) || !NonNegative(manualUpperDistance))
                throw new InvalidOperationException("순찰/높이 설정 범위를 확인하세요.");
        }
        private static bool FinitePositive(float n) => float.IsFinite(n) && n > 0;
        private static bool NonNegative(float n) => float.IsFinite(n) && n >= 0;
        private static bool UnitScale(Vector3 value) => FinitePositive(value.x) && FinitePositive(value.y) &&
            FinitePositive(value.z) && (value - Vector3.one).sqrMagnitude <= .0001f;
        private static bool AxisAligned(Quaternion value)
        {
            float angle = Quaternion.Angle(value, Quaternion.identity);
            return float.IsFinite(angle) && angle <= .01f;
        }
        private void FixedUpdate()
        {
            if (!isActiveAndEnabled || Time.fixedDeltaTime <= 0f) return;
            // Contacts from the preceding step use the current stun state before expiry/movement.
            combat?.Resolve();
            if (!isActiveAndEnabled) return;
            states?.Tick(Time.fixedDeltaTime);
            // Stun pauses movement, including terrain correction; no physical body bob/tween.
            if (!IsStunned) Motor?.ApplyPosition();
            PublishStunStateChanged();
        }
        private void OnDisable()
        {
            combat?.Clear(); combat = null;
            states = null;
            Motor = null;
            target = null;
            PublishStunStateChanged();
        }
        public void BindTarget(PlayerFacade player)
        {
            combat?.Clear();
            target = player;
            combat = isActiveAndEnabled && states != null ? new EnemyCombat(this, body, target) : null;
        }
        /// <summary>같은 Stage 인스턴스의 초기 위치·방향·기절·접촉을 재준비합니다. 저장 payload가 아닙니다.</summary>
        public void ResetForRetry()
        {
            combat?.Clear(); combat = null;
            states = null; Motor = null;
            if (!initialPlacementCaptured)
            {
                ValidateSetup();
                initialPosition = transform.position;
                initialPlacementCaptured = true;
            }
            transform.position = initialPosition;
            ValidateSetup();
            var motor = new EnemyMotor(this, body, terrainMask, EnemyDefinition.Direction(initialDirection),
                patrolDistance, heightSearchDistance, surfaceClearance, manualLowerDistance, manualUpperDistance);
            Motor = motor;
            states = new EnemyStateMachine(this);
            if (isActiveAndEnabled) combat = new EnemyCombat(this, body, target);
            PublishStunStateChanged();
        }
        public int TryStun(int stunDur)
        {
            if (!isActiveAndEnabled || states == null) return 0;
            int applied = states.TryStun(stunDur);
            PublishStunStateChanged();
            return applied;
        }
        /// <summary>직전 알림과 상태가 다를 때만 이벤트를 호출합니다.</summary>
        /// <remarks>스턴 시간 연장 시에는 중복 호출하지 않으며, 사망 및 재활성화로 해제된 상태도 반영합니다.</remarks>
        private void PublishStunStateChanged()
        {
            bool isStunned = IsStunned;
            if (publishedStunState == isStunned) return;
            publishedStunState = isStunned;
            StunStateChanged?.Invoke(isStunned);
        }
        public bool IsTerrainCollider(Collider collider) => collider != null && !collider.isTrigger &&
            !collider.transform.IsChildOf(transform) &&
            !(collider.attachedRigidbody != null && collider.attachedRigidbody.TryGetComponent<PlayerFacade>(out _));
        public void RecordContact(Collider other, bool entered, BoxCollider source)
        {
            // Ignore legacy sensors that are not the configured body.
            if (source == body) combat?.Record(other, entered);
        }

        private void OnDrawGizmosSelected()
        {
            if (definition == null || body == null) return;
            Vector3 start = Application.isPlaying && Motor != null ? Motor.Origin : transform.position;
            if (!EnemyDefinition.IsAxisDirection(initialDirection)) return;
            Vector3 direction = EnemyDefinition.Direction(initialDirection);
            Vector3 size = Vector3.Scale(body.size, body.transform.lossyScale);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(start, size);
            if (definition.Kind != EnemyKind.Patrol) return;
            Vector3 end = start + direction * patrolDistance;
            float min = start.y - manualLowerDistance, max = start.y + manualUpperDistance;
            if (Application.isPlaying && Motor != null)
            { min = Motor.MinimumY; max = Motor.MaximumY; }
            else
            {
                Vector3 center = body.transform.TransformPoint(body.center);
                float down = float.PositiveInfinity, up = float.PositiveInfinity;
                Vector3 extents = new Vector3(size.x / 2, .001f, size.z / 2);
                foreach (var hit in Physics.BoxCastAll(center, extents, Vector3.down, Quaternion.identity,
                    heightSearchDistance, terrainMask, QueryTriggerInteraction.Ignore))
                    if (IsTerrainCollider(hit.collider) && hit.distance < down)
                    { down = hit.distance; min = hit.point.y + surfaceClearance + size.y / 2 - (center.y - start.y); }
                foreach (var hit in Physics.BoxCastAll(center, extents, Vector3.up, Quaternion.identity,
                    heightSearchDistance, terrainMask, QueryTriggerInteraction.Ignore))
                    if (IsTerrainCollider(hit.collider) && hit.distance < up)
                    { up = hit.distance; max = hit.point.y - surfaceClearance - size.y / 2 - (center.y - start.y); }
            }
            bool blocked = min > max || start.y < min || start.y > max || end.y < min || end.y > max;
            foreach (var hit in Physics.BoxCastAll(body.transform.TransformPoint(body.center), size / 2, direction,
                Quaternion.identity, patrolDistance, terrainMask, QueryTriggerInteraction.Ignore))
                if (IsTerrainCollider(hit.collider)) { blocked = true; break; }
            Gizmos.color = blocked ? Color.red : Color.cyan;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireCube(end, size);
            // 화살표 길이는 1초 이동 거리(경로 길이 상한)이다.
            Vector3 arrow = start + direction * Mathf.Min(patrolDistance, definition.Speed);
            Vector3 side = Vector3.Cross(direction, Vector3.forward) * .12f;
            Gizmos.DrawLine(start, arrow);
            Gizmos.DrawLine(arrow, arrow - direction * .2f + side);
            Gizmos.DrawLine(arrow, arrow - direction * .2f - side);
            Gizmos.color = min > max ? Color.red : Color.yellow;
            Gizmos.DrawLine(new Vector3(start.x - patrolDistance, min, start.z), new Vector3(start.x + patrolDistance, min, start.z));
            Gizmos.DrawLine(new Vector3(start.x - patrolDistance, max, start.z), new Vector3(start.x + patrolDistance, max, start.z));
        }
    }
}
