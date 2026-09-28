using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    /// <summary>축 정렬 Box 몸체의 기준 위치와 부유 위치를 합성한다. 플랫폼 운동을 전달받지 않는다.</summary>
    public sealed class EnemyMotor
    {
        private const float Skin = .002f;
        private readonly BoxCollider body;
        private readonly Transform root;
        private readonly LayerMask terrain;
        private readonly Vector3 half;
        private readonly Vector3 centerOffset;
        private readonly bool bounded;
        private readonly EnemyActor owner;
        private Vector3 physical;
        private bool reportedBlocked;
        public Vector3 Origin { get; }
        public Vector3 End { get; }
        public Vector3 BasePosition { get; private set; }
        public Vector3 Facing { get; private set; }
        public float MinimumY { get; }
        public float MaximumY { get; }
        public bool FoundFloor { get; }
        public bool FoundCeiling { get; }
        public bool IsTrapped { get; private set; }

        public EnemyMotor(EnemyActor owner, BoxCollider body, LayerMask terrain, Vector3 direction,
            float distance, float searchDistance, float clearance, float lower, float upper)
        {
            this.owner = owner; this.body = body; root = owner.transform; this.terrain = terrain;
            bounded = owner.Definition.Kind == EnemyKind.Patrol;
            Origin = BasePosition = root.position;
            End = Origin + direction * distance;
            Facing = direction;
            physical = body.transform.position;
            half = Vector3.Scale(body.size, body.transform.lossyScale) * .5f;
            centerOffset = body.transform.TransformPoint(body.center) - root.position;
            MinimumY = bounded ? Origin.y - lower : float.NegativeInfinity;
            MaximumY = bounded ? Origin.y + upper : float.PositiveInfinity;
            if (bounded)
            {
                Vector3 center = Origin + centerOffset;
                if (Cast(center, new Vector3(half.x, .001f, half.z), Vector3.down, searchDistance, out var floor))
                {
                    MinimumY = floor.point.y + clearance + half.y - centerOffset.y;
                    FoundFloor = true;
                }
                if (Cast(center, new Vector3(half.x, .001f, half.z), Vector3.up, searchDistance, out var ceiling))
                {
                    MaximumY = ceiling.point.y - clearance - half.y - centerOffset.y;
                    FoundCeiling = true;
                }
                if (MinimumY > MaximumY || Origin.y < MinimumY || Origin.y > MaximumY)
                    throw new System.InvalidOperationException("적 시작 위치 또는 상하 높이 구간이 유효하지 않습니다.");
            }
            if (Overlaps(physical)) throw new System.InvalidOperationException("적 몸체가 시작부터 지형에 묻혀 있습니다.");
        }

        private Vector3 Center(Vector3 position) => position + body.transform.TransformVector(body.center);
        private bool Overlaps(Vector3 position)
        {
            foreach (var c in Physics.OverlapBox(Center(position), half, Quaternion.identity, terrain, QueryTriggerInteraction.Ignore))
                if (owner.IsTerrainCollider(c) && Physics.ComputePenetration(body, position, Quaternion.identity, c, c.transform.position,
                    c.transform.rotation, out _, out float depth) && depth > Skin) return true;
            return false;
        }
        private Vector3 Sweep(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance < .000001f) return start;
            if (Cast(Center(start), half, delta / distance, distance + Skin, out var hit))
                return start + delta / distance * Mathf.Max(0, hit.distance - Skin);
            return end;
        }
        private bool Cast(Vector3 center, Vector3 extents, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = default;
            float best = float.PositiveInfinity;
            foreach (var hit in Physics.BoxCastAll(center, extents, direction, Quaternion.identity, distance,
                terrain, QueryTriggerInteraction.Ignore))
                if (owner.IsTerrainCollider(hit.collider) && hit.distance < best)
                { best = hit.distance; nearest = hit; }
            return best < float.PositiveInfinity;
        }
        public bool MoveBase(Vector3 target, float distance)
        {
            if (IsTrapped) return true;
            target.z = Origin.z;
            Vector3 requested = Vector3.MoveTowards(BasePosition, target, distance);
            Vector3 limited = requested;
            limited.y = Mathf.Clamp(limited.y, MinimumY, MaximumY);
            Vector3 delta = limited - BasePosition;
            if (delta.sqrMagnitude > .000001f) Facing = delta.normalized;
            Vector3 nextPhysical = Sweep(physical, physical + delta);
            Vector3 applied = nextPhysical - physical;
            BasePosition += applied;
            physical = nextPhysical;
            return (requested - BasePosition).sqrMagnitude > .000001f;
        }
        public void ApplyFloat(float offset)
        {
            // Moving terrain can enter a static Trigger. Resolve only overlap, never platform velocity.
            Vector3 corrected = physical;
            for (int iteration = 0; iteration < 6; iteration++)
            {
                bool overlap = false;
                foreach (var c in Physics.OverlapBox(Center(corrected), half, Quaternion.identity, terrain, QueryTriggerInteraction.Ignore))
                {
                    if (!owner.IsTerrainCollider(c)) continue;
                    if (!Physics.ComputePenetration(body, corrected, Quaternion.identity, c, c.transform.position,
                        c.transform.rotation, out var direction, out float depth) || depth <= Skin) continue;
                    overlap = true;
                    // Preserve the side-scrolling plane even when PhysX's shortest escape points into depth.
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
                Vector3 desired = BasePosition + Vector3.up * offset;
                desired.y = Mathf.Clamp(desired.y, MinimumY, MaximumY);
                physical = Sweep(physical, desired);
            }
            root.position = BasePosition;
            body.transform.position = physical;
        }
    }
}
