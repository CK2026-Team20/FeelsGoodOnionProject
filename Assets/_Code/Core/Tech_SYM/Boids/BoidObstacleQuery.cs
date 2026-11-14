using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Boids
{
    // One nearby snapshot per group tick. No scene-wide object enumeration or obstacle registration.
    public sealed class BoidObstacleQuery
    {
        sealed class Snapshot
        {
            public Collider collider;
            public BoidMovingObstacle motion;
            public Rigidbody rigidbody;
            public Vector3 position, velocity, displacement, half, center, inverseScale;
            public Matrix4x4 inverse;
            public Bounds bounds;
            public Vector3 minimum, maximum;
            public float stopTime;
            public int seen;
            public bool box, analytic;
        }

        readonly int mask;
        readonly float approachSpeed;
        readonly Transform agentRoot;
        readonly Dictionary<Collider, Snapshot> history = new Dictionary<Collider, Snapshot>();
        readonly List<Snapshot> nearby = new List<Snapshot>(64);
        readonly List<Collider> stale = new List<Collider>();
        Collider[] overlaps = new Collider[64];
        RaycastHit[] hits = new RaycastHit[64];
        int generation;
        public int Casts { get; private set; }

        public int NearbyCount => nearby.Count;
        public BoidObstacleQuery(int layerMask, float maximumApproachSpeed, Transform ignoredRoot = null)
        {
            mask = layerMask;
            approachSpeed = maximumApproachSpeed;
            agentRoot = ignoredRoot;
        }

        public void ResetHistory()
        {
            history.Clear();
            nearby.Clear();
            stale.Clear();
            generation = 0;
        }

        bool Included(Collider c) => c
            && c.enabled
            && c.gameObject.activeInHierarchy
            && !c.isTrigger
            && (!agentRoot
            || !c.transform.IsChildOf(agentRoot));
        public void Refresh(Vector3[] positions, int count, float bodyRadius, float speed, float horizon, float dt)
        {
            using var profile = BoidProfiler.Discovery.Auto();
            if (count == 0)
            {
                ResetHistory();
                return;
            }

            var bounds = new Bounds(positions[0], Vector3.zero);
            for (int i = 1; i < count; i++)
                bounds.Encapsulate(positions[i]);
            // The speed budget also covers obstacles approaching from outside the boid's own reach.
            bounds.Expand(2 * (bodyRadius + (speed + approachSpeed) * horizon));
            int found;
            while (true)
            {
                found = Physics.OverlapBoxNonAlloc(bounds.center,
                     bounds.extents,
                     overlaps,
                     Quaternion.identity,
                     mask,
                     QueryTriggerInteraction.Ignore);
                if (found < overlaps.Length)
                    break;
                Grow(ref overlaps);
            }

            generation++;
            nearby.Clear();
            for (int i = 0; i < found; i++)
            {
                var c = overlaps[i];
                if (!Included(c))
                    continue;
                if (!history.TryGetValue(c, out var state))
                {
                    state = new Snapshot{collider = c, seen = -1};
                    history.Add(c, state);
                }

                Vector3 position = c.transform.position;
                // Re-check the provider once per tick, allowing runtime component additions/removals.
                var motion = c.GetComponentInParent<BoidMovingObstacle>();
                state.motion = motion && motion.Body == c ? motion : null;
                state.rigidbody = c.attachedRigidbody;
                bool continuous = state.seen == generation - 1;
                state.displacement = continuous ? position - state.position : Vector3.zero;
                // Observed collider motion includes kinematic and child motion; Rigidbody is a first-observation fallback.
                state.velocity = continuous
                    && dt > 0 ? state.displacement / dt : (state.rigidbody ? state.rigidbody.linearVelocity : Vector3.zero);
                state.stopTime = float.PositiveInfinity;
                if (state.motion)
                {
                    state.velocity = state.motion.Velocity;
                    state.displacement = state.motion.LastDisplacement;
                    state.stopTime = state.motion.StopTime;
                }

                state.position = position;
                state.seen = generation;
                state.bounds = c.bounds;
                state.minimum = state.bounds.min;
                state.maximum = state.bounds.max;
                var box = c as BoxCollider;
                state.box = box;
                state.analytic = state.motion || state.velocity.sqrMagnitude > 1e-8f;
                if (box)
                {
                    state.inverse = c.transform.worldToLocalMatrix;
                    state.center = box.center;
                    state.half = box.size * .5f;
                    var scale = c.transform.lossyScale;
                    state.inverseScale = new Vector3(1 / Mathf.Max(Mathf.Abs(scale.x),
                         1e-6f),
                         1 / Mathf.Max(Mathf.Abs(scale.y),
                         1e-6f),
                         1 / Mathf.Max(Mathf.Abs(scale.z),
                         1e-6f));
                }
                else
                {
                    state.inverse = Matrix4x4.identity;
                    state.center = state.bounds.center;
                    state.half = state.bounds.extents;
                    state.inverseScale = Vector3.one;
                }

                nearby.Add(state);
            }

            stale.Clear();
            foreach (var pair in history)
                if (pair.Value.seen != generation)
                    stale.Add(pair.Key);
            foreach (var c in stale)
                history.Remove(c);
        }

        static void Grow<T>(ref T[] buffer)
        {
            if (buffer.Length >= 4096)
                throw new InvalidOperationException("보이드 충돌 검색이 4096개 한도를 초과했습니다. 장애물 레이어와 군집 범위를 줄이세요.");
            Array.Resize(ref buffer, buffer.Length * 2);
        }

        static Vector3 Offset(Snapshot s, float time) => s.velocity * Mathf.Min(Mathf.Max(0, time), s.stopTime);
        static bool Slab(Vector3 a, Vector3 b, Vector3 half)
        {
            float lo = 0, hi = 1;
            Vector3 delta = b - a;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(delta[axis]) < 1e-7f)
                {
                    if (Mathf.Abs(a[axis]) > half[axis])
                        return false;
                }
                else
                {
                    float x = (-half[axis] - a[axis]) / delta[axis], y = (half[axis] - a[axis]) / delta[axis];
                    if (x > y)
                        (x, y) = (y, x);
                    lo = Mathf.Max(lo, x);
                    hi = Mathf.Min(hi, y);
                    if (lo > hi)
                        return false;
                }
            }

            return true;
        }

        static bool BoundsHit(Snapshot s, Vector3 a, Vector3 b, float radius)
        {
            return !(Mathf.Min(a.x,
                 b.x) > s.maximum.x + radius
                || Mathf.Max(a.x,
                 b.x) < s.minimum.x - radius
                || Mathf.Min(a.y,
                 b.y) > s.maximum.y + radius
                || Mathf.Max(a.y,
                 b.y) < s.minimum.y - radius
                || Mathf.Min(a.z,
                 b.z) > s.maximum.z + radius
                || Mathf.Max(a.z,
                 b.z) < s.minimum.z - radius);
        }

        static bool BoxHit(Snapshot s, Vector3 a, Vector3 b, float radius)
        {
#if BOIDS_DETAILED_PROFILING
            using var profile = BoidProfiler.MovingBox.Auto();
#endif
            if (!BoundsHit(s, a, b, radius))
                return false;
            return Slab(s.inverse.MultiplyPoint3x4(a) - s.center,
                 s.inverse.MultiplyPoint3x4(b) - s.center,
                 s.half + s.inverseScale * radius);
        }

        bool StaticSafe(Vector3 a, Vector3 b, float radius)
        {
#if BOIDS_DETAILED_PROFILING
            using var profile = BoidProfiler.StaticCast.Auto();
#endif
            bool possible = false;
            foreach (var s in nearby)
                if (!s.analytic && BoundsHit(s, a, b, radius))
                {
                    possible = true;
                    break;
                }

            if (!possible)
                return true;
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < 1e-7f)
                return true;
            int found;
            while (true)
            {
                Casts++;
                found = Physics.SphereCastNonAlloc(a, radius, delta / length, hits, length, mask, QueryTriggerInteraction.Ignore);
                if (found < hits.Length)
                    break;
                Grow(ref hits);
            }

            for (int i = 0; i < found; i++)
                if (history.TryGetValue(hits[i].collider, out var s) && s.seen == generation && !s.analytic)
                    return false;
            return true;
        }

        public bool SegmentSafe(Vector3 a, Vector3 b, float radius, float t0, float t1)
        {
            if (!StaticSafe(a, b, radius))
                return false;
            foreach (var s in nearby)
            {
                if (!s.analytic)
                    continue;
                if (t0 < s.stopTime && s.stopTime < t1)
                {
                    Vector3 atStop = Vector3.Lerp(a, b, (s.stopTime - t0) / (t1 - t0));
                    if (BoxHit(s,
                         a - Offset(s,
                         t0),
                         atStop - Offset(s,
                         s.stopTime),
                         radius)
                        || BoxHit(s,
                         atStop - Offset(s,
                         s.stopTime),
                         b - Offset(s,
                         t1),
                         radius))
                        return false;
                }
                else if (BoxHit(s, a - Offset(s, t0), b - Offset(s, t1), radius))
                    return false;
            }

            return true;
        }

        // Validation queries intentionally use actual colliders independently of the prediction cache.
        public bool ActualSegmentSafe(Vector3 before, Vector3 after, float radius)
        {
            Vector3 delta = after - before;
            float length = delta.magnitude;
            if (length > 1e-7f)
            {
                int found;
                while (true)
                {
                    found = Physics.SphereCastNonAlloc(before, radius, delta / length, hits, length, mask, QueryTriggerInteraction.Ignore);
                    if (found < hits.Length)
                        break;
                    Grow(ref hits);
                }

                for (int i = 0; i < found; i++)
                    if (Included(hits[i].collider))
                        return false;
            }

            foreach (var s in nearby)
                if (s.displacement.sqrMagnitude > 1e-10f && BoxHit(s, before + s.displacement, after, radius))
                    return false;
            return true;
        }

        public bool Overlapping(Vector3 p, float radius)
        {
            int found;
            while (true)
            {
                found = Physics.OverlapSphereNonAlloc(p, radius, overlaps, mask, QueryTriggerInteraction.Ignore);
                if (found < overlaps.Length)
                    break;
                Grow(ref overlaps);
            }

            for (int i = 0; i < found; i++)
                if (Included(overlaps[i]))
                    return true;
            return false;
        }

        public float TimeToThreat(Vector3 p, Vector3 velocity, float radius, float horizon)
        {
            const int steps = 8;
            for (int i = 0; i < steps; i++)
            {
                float a = horizon * i / steps, b = horizon * (i + 1) / steps;
                if (!SegmentSafe(p + velocity * a, p + velocity * b, radius, a, b))
                    return Mathf.Max(.04f, b);
            }

            return horizon;
        }
    }
}
