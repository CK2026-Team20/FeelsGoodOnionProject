using System;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed class EnemyActor : MonoBehaviour, IDamageable, IStunnable, IStunState
    {
        [Header("Definition")]
        [Tooltip("개체별 실행 상태는 공유하지 않는 설정 SO입니다.")]
        [SerializeField] private EnemyDefinition definition;
        [Tooltip("외형과 머리 센서를 함께 포함하는 실제 몸체 Trigger입니다.")]
        [SerializeField] private BoxCollider body;
        [Tooltip("실제 머리 윗면 안쪽의 얇은 별도 Trigger입니다.")]
        [SerializeField] private BoxCollider head;
        [Tooltip("감지 및 전투 대상. 씬에서 명시적으로 연결합니다.")]
        [SerializeField] private PlayerFacade target;
        [Tooltip("활성화 시 적용할 무적 여부입니다.")]
        [SerializeField] private bool initiallyInvincible;
        [Header("Patrol")]
        [Tooltip("최초 XY 이동 방향입니다.")]
        [SerializeField] private PatrolDirection initialDirection;
        [Tooltip("최초 위치에서 순찰 끝점까지의 거리(m)입니다.")]
        [SerializeField, Min(.01f)] private float patrolDistance = 3f;
        [Tooltip("최초 위치 중심의 추격 허용 반경(m). 순찰 거리 이상이어야 합니다.")]
        [SerializeField, Min(.01f)] private float patrolRadius = 5f;
        [Header("Sight")]
        [Tooltip("플레이어 감지 거리(m)입니다.")]
        [SerializeField, Min(.01f)] private float sightDistance = 5f;
        [Tooltip("이동 방향 중심의 전체 시야각입니다.")]
        [SerializeField, Range(1, 360)] private float sightAngle = 120f;
        [Header("Height")]
        [Tooltip("플랫폼과 벽을 검사할 레이어입니다. Trigger는 제외합니다.")]
        [SerializeField] private LayerMask terrainMask = 1;
        [Tooltip("시작 시 위아래 플랫폼 탐색 거리(m)입니다.")]
        [SerializeField, Min(.01f)] private float heightSearchDistance = 8f;
        [Tooltip("플랫폼과 몸체 표면 사이 최소 간격(m)입니다.")]
        [SerializeField, Min(0)] private float surfaceClearance = .15f;
        [Tooltip("아래 플랫폼이 없을 때 시작점 아래로 허용할 거리(m)입니다.")]
        [SerializeField, Min(0)] private float manualLowerDistance = 2f;
        [Tooltip("위 플랫폼이 없을 때 시작점 위로 허용할 거리(m)입니다.")]
        [SerializeField, Min(0)] private float manualUpperDistance = 3f;

        private EnemyModel model;
        private EnemyStateMachine states;
        private EnemyFloatMotion floating;
        private EnemyCombat combat;
        private Collider targetShape;
        private bool deathRaised;
        private bool publishedStunState;
        public EnemyDefinition Definition => definition;
        public EnemyMotor Motor { get; private set; }
        public EnemyState CurrentState => states?.Current ?? EnemyState.IDLE;
        public int CurrentHealth => model?.Health ?? (definition != null ? definition.Health : 0);
        public bool IsStunned => model != null && !model.IsDead && model.IsStunned;
        public double StunRemainingSeconds => (model?.StunMilliseconds ?? 0) / 1000;
        public bool IsDead => model != null && model.IsDead;
        public bool IsReturning => states != null && states.IsReturning;
        public Vector3 SavedPatrolPosition => states?.ResumePosition ?? transform.position;
        public bool TowardEnd => states == null || states.TowardEnd;
        public Vector3 BodyPosition => body != null ? body.transform.position : transform.position;
        // Aim within the player's occupied volume and our travel band, allowing a grounded player
        // whose pivot lies a few centimetres below the enemy's minimum floating height.
        public Vector3 TargetPosition
        {
            get
            {
                if (target == null) return BodyPosition;
                Vector3 point = targetShape != null ? targetShape.bounds.center : target.transform.position;
                if (Motor != null) point.y = Mathf.Clamp(point.y, Motor.MinimumY, Motor.MaximumY);
                return point;
            }
        }
        public float FloatOffset => floating?.Offset ?? 0;
        public event Action<EnemyActor> Died;
        /// <summary>스턴 상태가 변경되면 알립니다. true는 적용, false는 해제입니다.</summary>
        public event Action<bool> StunStateChanged;

        private void OnEnable()
        {
            try
            {
                ValidateSetup();
                targetShape = target != null ? target.GetComponent<Collider>() : null;
                body.transform.localPosition = Vector3.zero;
                model = new EnemyModel(definition.Health, initiallyInvincible);
                deathRaised = false;
                Motor = new EnemyMotor(this, body, terrainMask, EnemyDefinition.Direction(initialDirection),
                    patrolDistance, heightSearchDistance, surfaceClearance, manualLowerDistance, manualUpperDistance);
                floating = new EnemyFloatMotion(definition.FloatAmplitude, definition.FloatPeriod);
                states = definition.Kind == EnemyKind.Patrol ? new EnemyStateMachine(this) : null;
                combat = new EnemyCombat(this, body, head, target);
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
            if (definition == null || !definition.IsValid || body == null || head == null)
                throw new InvalidOperationException("Definition, 몸체, 머리 및 유효한 설정이 필요합니다.");
            if (body.transform.parent != transform || !head.transform.IsChildOf(body.transform) ||
                body == head || !body.isTrigger || !head.isTrigger)
                throw new InvalidOperationException("루트 아래 몸체 Trigger와 몸체 아래 머리 Trigger 구조가 필요합니다.");
            if (GetComponentInParent<Rigidbody>() != null || GetComponentInChildren<Rigidbody>(true) != null)
                throw new InvalidOperationException("적 및 조상/자식에 Rigidbody를 둘 수 없습니다.");
            if (Quaternion.Angle(body.transform.rotation, Quaternion.identity) > .01f ||
                Quaternion.Angle(head.transform.rotation, Quaternion.identity) > .01f ||
                (transform.lossyScale - Vector3.one).sqrMagnitude > .0001f ||
                (body.transform.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                throw new InvalidOperationException("물리 루트/몸체는 단위 스케일, Collider는 월드 축 정렬을 사용하세요. 외형 자식만 회전/확대하세요.");
            if (body.size.x <= 0 || body.size.y <= 0 || body.size.z <= 0 || head.size.x <= 0 || head.size.y <= 0 || head.size.z <= 0)
                throw new InvalidOperationException("몸체와 머리 Collider 크기는 양수여야 합니다.");
            if (!FinitePositive(patrolDistance) || !FinitePositive(patrolRadius) || patrolRadius < patrolDistance ||
                !FinitePositive(sightDistance) || !FinitePositive(heightSearchDistance) ||
                !float.IsFinite(sightAngle) || sightAngle < 1 || sightAngle > 360 ||
                !NonNegative(surfaceClearance) || !NonNegative(manualLowerDistance) || !NonNegative(manualUpperDistance))
                throw new InvalidOperationException("순찰/감지/높이 설정 범위를 확인하세요.");
        }
        private static bool FinitePositive(float n) => float.IsFinite(n) && n > 0;
        private static bool NonNegative(float n) => float.IsFinite(n) && n >= 0;
        private void FixedUpdate()
        {
            // Resolve the preceding physics step before the next movement; head always wins over body.
            combat.Resolve();
            if (!isActiveAndEnabled || IsDead) return;
            model.Tick(Time.fixedDeltaTime);
            states?.Tick(Time.fixedDeltaTime);
            floating.Tick(Time.fixedDeltaTime);
            Motor.ApplyFloat(floating.Offset);
            PublishStunStateChanged();
        }
        private void OnDisable()
        {
            floating?.Dispose(); floating = null;
            combat?.Clear(); combat = null;
            states = null;
        }
        public void BindTarget(PlayerFacade player)
        {
            target = player;
            targetShape = target != null ? target.GetComponent<Collider>() : null;
            if (isActiveAndEnabled && model != null) combat = new EnemyCombat(this, body, head, target);
        }
        public bool IsInvincible() => model?.IsInvincible ?? initiallyInvincible;
        public void SetInvincible(bool value) { model?.SetInvincible(value); }
        public int Damage(int damage)
        {
            if (!isActiveAndEnabled || model == null) return 0;
            int applied = model.Damage(damage);
            if (model.IsDead) Die();
            return applied;
        }
        public int TryStun(int stunDur)
        {
            if (!isActiveAndEnabled || model == null) return 0;
            int applied = model.Stun(stunDur);
            if (applied > 0) states?.Stun();
            PublishStunStateChanged();
            return applied;
        }
        public void Stomp()
        {
            if (!isActiveAndEnabled || model == null || model.IsDead) return;
            model.Kill(); Die();
        }
        private void Die()
        {
            if (deathRaised) return;
            deathRaised = true;
            try
            {
                PublishStunStateChanged();
                Died?.Invoke(this);
            }
            finally { gameObject.SetActive(false); }
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
        public bool InPatrolRange(Vector3 point)
        {
            Vector3 delta = point - Motor.Origin; delta.z = 0;
            return delta.sqrMagnitude <= patrolRadius * patrolRadius && point.y >= Motor.MinimumY && point.y <= Motor.MaximumY;
        }
        public bool CanChase()
        {
            if (target == null || !target.isActiveAndEnabled || target.IsDead || !InPatrolRange(TargetPosition)) return false;
            if (targetShape != null && (!targetShape.enabled || targetShape.bounds.max.y < Motor.MinimumY || targetShape.bounds.min.y > Motor.MaximumY)) return false;
            if (targetShape == null && (target.transform.position.y < Motor.MinimumY || target.transform.position.y > Motor.MaximumY)) return false;
            Vector3 delta = TargetPosition - BodyPosition;
            // Ignore targets on another side-scrolling lane.
            if (Mathf.Abs(delta.z) > body.size.z * .5f + .5f) return false;
            delta.z = 0;
            if (delta.sqrMagnitude > sightDistance * sightDistance || Vector3.Angle(Motor.Facing, delta) > sightAngle * .5f) return false;
            Vector3 ray = TargetPosition - BodyPosition;
            foreach (var hit in Physics.RaycastAll(BodyPosition, ray.normalized, ray.magnitude, terrainMask, QueryTriggerInteraction.Ignore))
                if (IsTerrainCollider(hit.collider)) return false;
            return true;
        }
        public bool IsTerrainCollider(Collider collider) => collider != null && !collider.isTrigger &&
            !collider.transform.IsChildOf(transform) &&
            !(collider.attachedRigidbody != null && collider.attachedRigidbody.TryGetComponent<PlayerFacade>(out _));
        public void RecordContact(Collider other, bool entered, bool isHead) => combat?.Record(other, entered, isHead);

        private void OnDrawGizmosSelected()
        {
            if (definition == null || body == null) return;
            Vector3 start = Application.isPlaying && Motor != null ? Motor.Origin : transform.position;
            Vector3 direction = EnemyDefinition.Direction(initialDirection);
            Vector3 size = Vector3.Scale(body.size, body.transform.lossyScale);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(start, size + Vector3.up * definition.FloatAmplitude * 2);
            if (head != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(head.bounds.center, head.bounds.size);
            }
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
            Gizmos.DrawLine(new Vector3(start.x - patrolRadius, min, start.z), new Vector3(start.x + patrolRadius, min, start.z));
            Gizmos.DrawLine(new Vector3(start.x - patrolRadius, max, start.z), new Vector3(start.x + patrolRadius, max, start.z));
            Gizmos.color = Color.cyan;
            DrawArc(start, Vector3.right, 360, patrolRadius);
            Vector3 facing = Application.isPlaying && Motor != null ? Motor.Facing : direction;
            Vector3 first = Quaternion.AngleAxis(-sightAngle / 2, Vector3.forward) * facing;
            Gizmos.color = Color.green;
            DrawArc(BodyPosition, first, sightAngle, sightDistance);
            Gizmos.DrawLine(BodyPosition, BodyPosition + first * sightDistance);
            Gizmos.DrawLine(BodyPosition, BodyPosition + Quaternion.AngleAxis(sightAngle, Vector3.forward) * first * sightDistance);
        }
        private static void DrawArc(Vector3 center, Vector3 first, float angle, float radius)
        {
            const int segments = 48;
            Vector3 previous = center + first * radius;
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = center + Quaternion.AngleAxis(angle * i / segments, Vector3.forward) * first * radius;
                Gizmos.DrawLine(previous, next); previous = next;
            }
        }
    }
}
