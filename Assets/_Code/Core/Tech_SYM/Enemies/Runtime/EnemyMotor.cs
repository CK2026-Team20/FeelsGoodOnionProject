using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    /// <summary>고정 Transform 사이의 X/Y 이동과 지형 제한을 계산하고 적 Root 전체의 위치를 작성합니다.</summary>
    public sealed class EnemyMotor
    {
        private const float Skin = .002f;
        private readonly BoxCollider body;
        private readonly Transform root, patrolStart, patrolEnd;
        private readonly LayerMask terrain;
        private readonly Vector3 half, centerOffset, initialPosition;
        private readonly bool bounded;
        private readonly EnemyActor owner;
        private readonly PhysicsScene physics;
        private RaycastHit[] hits = new RaycastHit[16];
        private Collider[] overlaps = new Collider[16];
        private Vector3 physical;
        private bool reportedBlocked;
        public Vector3 Origin => bounded ? RequirePoint(patrolStart).position : initialPosition;
        public Vector3 End => bounded ? RequirePoint(patrolEnd).position : initialPosition;
        public Vector3 BasePosition { get; private set; }
        public Vector3 Facing { get; private set; }
        public float MinimumY { get; }
        public float MaximumY { get; }
        public bool FoundFloor { get; }
        public bool FoundCeiling { get; }
        public bool IsTrapped { get; private set; }

        public EnemyMotor(EnemyActor owner, BoxCollider body, LayerMask terrain, Transform patrolStart,
            Transform patrolEnd, float searchDistance, float clearance)
        {
            this.owner = owner; this.body = body; root = owner.transform; this.terrain = terrain;
            this.patrolStart = patrolStart; this.patrolEnd = patrolEnd;
            bounded = owner.Definition.Kind == EnemyKind.Patrol;
            initialPosition = BasePosition = physical = root.position;
            physics = owner.gameObject.scene.GetPhysicsScene();
            if (!physics.IsValid()) throw new InvalidOperationException("적이 속한 실제 물리 씬이 필요합니다.");
            Facing = bounded ? (owner.TowardEnd ? End - Origin : Origin - End).normalized : Vector3.zero;
            half = Vector3.Scale(body.size, body.transform.lossyScale) * .5f;
            centerOffset = body.transform.TransformPoint(body.center) - root.position;
            MinimumY = bounded ? Math.Min(Origin.y, End.y) : float.NegativeInfinity;
            MaximumY = bounded ? Math.Max(Origin.y, End.y) : float.PositiveInfinity;
            if (bounded)
            {
                Vector3 center = initialPosition + centerOffset;
                if (Cast(center, new Vector3(half.x, .001f, half.z), Vector3.down, searchDistance, out var floor))
                { MinimumY = floor.point.y + clearance + half.y - centerOffset.y; FoundFloor = true; }
                if (Cast(center, new Vector3(half.x, .001f, half.z), Vector3.up, searchDistance, out var ceiling))
                { MaximumY = ceiling.point.y - clearance - half.y - centerOffset.y; FoundCeiling = true; }
                if (MinimumY > MaximumY || initialPosition.y < MinimumY || initialPosition.y > MaximumY ||
                    Origin.y < MinimumY || Origin.y > MaximumY || End.y < MinimumY || End.y > MaximumY)
                    throw new InvalidOperationException("적의 초기 위치와 실제 순찰 끝점이 지형의 안전 높이 구간 안에 있어야 합니다.");
            }
            if (Overlaps(physical)) throw new InvalidOperationException("적 몸체가 시작부터 지형에 묻혀 있습니다.");
        }
        private static Transform RequirePoint(Transform point) => point != null ? point :
            throw new InvalidOperationException("순찰 Transform이 해제됐습니다. Stage 연결을 확인하세요.");
        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        /// <summary>실제 점의 X/Y 축·초기 위치와 첫 방향을 검증하고 첫 목표 끝점을 반환합니다.</summary>
        public static bool ValidatePatrol(Vector3 start, Vector3 end, Vector3 origin, PatrolDirection direction)
        {
            if (!Finite(start) || !Finite(end) || !Finite(origin) || !EnemyDefinition.IsAxisDirection(direction))
                throw new ArgumentException("유한한 X/Y 순찰 지점과 축 방향이 필요합니다.");
            Vector3 delta = end - start;
            if (!Finite(delta)) throw new ArgumentException("순찰 구간의 길이가 유한해야 합니다.");
            bool x = Math.Abs(delta.x) > .0001f && Math.Abs(delta.y) <= .0001f && Math.Abs(delta.z) <= .0001f;
            bool y = Math.Abs(delta.y) > .0001f && Math.Abs(delta.x) <= .0001f && Math.Abs(delta.z) <= .0001f;
            if ((!x && !y) || (x && direction != PatrolDirection.Left && direction != PatrolDirection.Right) ||
                (y && direction != PatrolDirection.Up && direction != PatrolDirection.Down))
                throw new ArgumentException("서로 다른 두 순찰 지점과 첫 방향이 같은 X 또는 Y 축에 있어야 합니다.");
            float position = x ? origin.x : origin.y, a = x ? start.x : start.y, b = x ? end.x : end.y;
            if (position < Math.Min(a, b) - .0001f || position > Math.Max(a, b) + .0001f ||
                Math.Abs(origin.z - start.z) > .0001f || Math.Abs((x ? origin.y - start.y : origin.x - start.x)) > .0001f)
                throw new ArgumentException("적의 초기 위치가 실제 순찰 선분에 있어야 합니다.");
            Vector3 facing = EnemyDefinition.Direction(direction);
            return (x ? delta.x * facing.x : delta.y * facing.y) > 0;
        }
        /// <summary>종점에 도착한 같은 단계에 방향을 반전합니다. 지형 통과 여부는 실제 Sweep이 결정합니다.</summary>
        public static Vector3 StepPatrol(Vector3 position, Vector3 start, Vector3 end, float distance, ref bool towardEnd)
        {
            if (!Finite(position) || !Finite(start) || !Finite(end) || !float.IsFinite(distance) || distance < 0)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (distance == 0) return position;
            Vector3 target = towardEnd ? end : start;
            Vector3 next = Vector3.MoveTowards(position, target, distance);
            if ((next - target).sqrMagnitude <= .00000001f) towardEnd = !towardEnd;
            return next;
        }
        private Vector3 Center(Vector3 position) => position + centerOffset;
        private int CollectOverlaps(Vector3 position)
        {
            int count;
            while ((count = physics.OverlapBox(Center(position), half, overlaps, Quaternion.identity, terrain,
                QueryTriggerInteraction.Ignore)) == overlaps.Length) Array.Resize(ref overlaps, checked(overlaps.Length * 2));
            return count;
        }
        private bool Overlaps(Vector3 position)
        {
            int count = CollectOverlaps(position);
            for (int i = 0; i < count; i++)
            {
                var c = overlaps[i];
                if (owner.IsTerrainCollider(c) && Physics.ComputePenetration(body, position, Quaternion.identity, c,
                    c.transform.position, c.transform.rotation, out _, out float depth) && depth > Skin) return true;
            }
            return false;
        }
        private Vector3 Sweep(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance < .000001f) return start;
            if (Cast(Center(start), half, delta / distance, distance + Skin, out var hit))
                return start + delta / distance * Math.Max(0, hit.distance - Skin);
            return end;
        }
        private bool Cast(Vector3 center, Vector3 extents, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            int count;
            while ((count = physics.BoxCast(center, extents, direction, hits, Quaternion.identity, distance, terrain,
                QueryTriggerInteraction.Ignore)) == hits.Length) Array.Resize(ref hits, checked(hits.Length * 2));
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
                if (owner.IsTerrainCollider(hits[i].collider) && hits[i].distance < best)
                { best = hits[i].distance; nearest = hits[i]; }
            return best < float.PositiveInfinity;
        }
        public void Move(float distance, ref bool towardEnd)
        {
            if (IsTrapped || !bounded) return;
            bool previousDirection = towardEnd;
            Vector3 requested = StepPatrol(BasePosition, Origin, End, distance, ref towardEnd);
            Vector3 limited = requested;
            limited.y = Mathf.Clamp(limited.y, MinimumY, MaximumY);
            Vector3 delta = limited - BasePosition;
            if (delta.sqrMagnitude > .000001f) Facing = delta.normalized;
            Vector3 nextPhysical = Sweep(physical, physical + delta);
            BasePosition += nextPhysical - physical;
            physical = nextPhysical;
            if ((requested - BasePosition).sqrMagnitude > .000001f) towardEnd = !previousDirection;
        }
        public void ApplyPosition()
        {
            Vector3 corrected = physical;
            for (int iteration = 0; iteration < 6; iteration++)
            {
                bool overlap = false;
                int count = CollectOverlaps(corrected);
                for (int i = 0; i < count; i++)
                {
                    var c = overlaps[i];
                    if (!owner.IsTerrainCollider(c) || !Physics.ComputePenetration(body, corrected, Quaternion.identity,
                        c, c.transform.position, c.transform.rotation, out var direction, out float depth) || depth <= Skin) continue;
                    overlap = true;
                    direction.z = 0;
                    if (direction.sqrMagnitude < .0001f) break;
                    corrected += direction.normalized * (depth + Skin);
                    corrected.y = Mathf.Clamp(corrected.y, MinimumY, MaximumY);
                }
                if (!overlap) break;
            }
            IsTrapped = Overlaps(corrected);
            if (IsTrapped)
            {
                if (!reportedBlocked) Debug.LogWarning("적이 지형 사이에 갇혀 이동을 중단했습니다. 배치를 확인하세요.", owner);
                reportedBlocked = true;
            }
            else
            {
                reportedBlocked = false;
                if (bounded) BasePosition += corrected - physical;
                physical = corrected;
                Vector3 desired = BasePosition;
                desired.y = Mathf.Clamp(desired.y, MinimumY, MaximumY);
                physical = Sweep(physical, desired);
            }
            BasePosition = physical;
            root.position = physical;
        }
    }
}
