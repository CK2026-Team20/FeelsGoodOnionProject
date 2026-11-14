using UnityEngine;

namespace Cooked.Boids
{
    public static class BoidSteering
    {
        public static Vector3 Flock(int index, Vector3[] positions, Vector3[] velocities, int count, BoidSettings s)
        {
            using var profile = BoidProfiler.Flock.Auto();
            Vector3 separation = Vector3.zero, center = Vector3.zero, average = Vector3.zero;
            int neighbors = 0, separated = 0;
            Vector3 p = positions[index], v = velocities[index];
            for (int j = 0; j < count; j++)
            {
                if (j == index)
                    continue;
                Vector3 delta = p - positions[j];
                float sq = delta.sqrMagnitude;
                if (sq > s.NeighborRadius * s.NeighborRadius)
                    continue;
                neighbors++;
                center += positions[j];
                average += velocities[j];
                if (sq < s.SeparationRadius * s.SeparationRadius)
                {
                    if (sq < .000001f)
                    {
                        delta = index < j ? Vector3.right : Vector3.left;
                        sq = 1;
                    }

                    separation += delta / Mathf.Max(sq, .01f);
                    separated++;
                }
            }

            if (neighbors == 0)
                return Vector3.zero;
            Vector3 alignment = (average / neighbors - v) / s.ResponseTime;
            Vector3 offset = center / neighbors - p;
            Vector3 cohesion = Vector3.ClampMagnitude(offset, s.NeighborRadius) / s.NeighborRadius * s.CruiseSpeed;
            return (separated > 0 ? separation / separated * s.SeparationWeight * s.CruiseSpeed : Vector3.zero) + alignment * s.AlignmentWeight + cohesion * s.CohesionWeight;
        }

        public static Vector3[] Directions(int count)
        {
            Vector3[] result = new Vector3[count];
            float golden = Mathf.PI * (3 - Mathf.Sqrt(5));
            for (int i = 0; i < count; i++)
            {
                float z = 1 - 2 * (i + .5f) / count, r = Mathf.Sqrt(1 - z * z), angle = i * golden;
                result[i] = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, z);
            }

            return result;
        }
    }

    public struct BoidAvoidanceMemory
    {
        public Vector3 direction;
        public float rate;
        public float remaining;
        public float duration;
    }

    public sealed class BoidMotionPlanner
    {
        struct Candidate : System.IComparable<Candidate>
        {
            public Vector3 direction;
            public float angle, rate, baseScore, upperScore;
            public int order;
            public int CompareTo(Candidate other)
            {
                int score = other.upperScore.CompareTo(upperScore);
                return score != 0 ? score : order.CompareTo(other.order);
            }
        }

        readonly Candidate[] candidates;
        readonly BoidSettings settings;
        readonly BoidObstacleQuery query;
        readonly BoidActivityVolume volume;
        readonly Vector3[] directions;
        readonly BoidBezierPath path;
        int pathCursor;
        float integrationStep;
        float boundaryHorizon;
        Matrix4x4 volumeInverse;
        Vector3 volumeCenter, volumeHalf;
        bool VolumeContains(Vector3 p)
        {
            Vector3 local = volumeInverse.MultiplyPoint3x4(p) - volumeCenter;
            return Mathf.Abs(local.x) <= volumeHalf.x && Mathf.Abs(local.y) <= volumeHalf.y && Mathf.Abs(local.z) <= volumeHalf.z;
        }

        public int AvoidanceSteps { get; private set; }

        public int FailedPlans { get; private set; }

        public float PeakAcceleration { get; private set; }

        public BoidMotionPlanner(BoidSettings s, BoidObstacleQuery q, BoidActivityVolume v, BoidBezierPath route)
        {
            settings = s;
            query = q;
            volume = v;
            path = route;
            directions = BoidSteering.Directions(s.DirectionCount);
            candidates = new Candidate[directions.Length + 3];
        }

        const int PreviewBlockSize = 6;
        readonly Vector3[] previewVertices = new Vector3[PreviewBlockSize];
        bool ArcSafe(Vector3 p, Vector3 velocity, Vector3 target, float rate, float radius, float horizon)
        {
#if BOIDS_DETAILED_PROFILING
            using var profile = BoidProfiler.Arc.Auto();
#endif
            int cursor = pathCursor;
            float step = integrationStep;
            int count = Mathf.CeilToInt(horizon / step);
            float speed = velocity.magnitude;
            Vector3 direction = velocity.normalized;
            // 실제 다음 tick은 기존 조향식 그대로 검사한다.
            Vector3 firstDirection = Vector3.RotateTowards(direction, target, rate * step, 0);
            Vector3 first = p + firstDirection * speed * step;
            if (volume && !VolumeContains(first))
                return false;
            if (path && !path.Contains(first, radius, ref cursor))
                return false;
            if (!query.SegmentSafe(p, first, radius, 0, step))
                return false;
            p = first;
            direction = firstDirection;
            float remainingAngle = Vector3.Angle(direction, target) * Mathf.Deg2Rad;
            Vector3 axis = Vector3.Cross(direction, target).normalized;
            Quaternion rotation = Quaternion.AngleAxis(rate * step * Mathf.Rad2Deg, axis);
            int i = 1;
            while (i < count)
            {
                if ((!path || path.IsStraight || i * step >= boundaryHorizon) && (direction - target).sqrMagnitude < 1e-10f)
                {
                    if (path && i * step < boundaryHorizon)
                    {
                        int boundarySteps = Mathf.Min(count - i, Mathf.CeilToInt(boundaryHorizon / step) - i);
                        if (!path.Contains(p,
                             radius,
                             ref cursor)
                            || !path.Contains(p + direction * speed * (boundarySteps * step),
                             radius,
                             ref cursor))
                            return false;
                    }

                    Vector3 end = p + direction * speed * ((count - i) * step);
                    if (volume && !VolumeContains(end))
                        return false;
                    return query.SegmentSafe(p, end, radius, i * step, count * step);
                }

                int block = Mathf.Min(PreviewBlockSize, count - i);
                if (path && i * step < boundaryHorizon)
                    block = Mathf.Min(block, Mathf.CeilToInt(boundaryHorizon / step) - i);
                Vector3 start = p;
                for (int j = 0; j < block; j++)
                {
                    if (remainingAngle <= rate * step)
                    {
                        direction = target;
                        remainingAngle = 0;
                    }
                    else
                    {
                        direction = rotation * direction;
                        remainingAngle -= rate * step;
                    }

                    p += direction * speed * step;
                    previewVertices[j] = p;
                    // 원래의 모든 이산 위치 경계 검사는 유지한다.
                    if (volume && !VolumeContains(p))
                        return false;
                    if (path && (i + j) * step < boundaryHorizon && !path.Contains(p, radius, ref cursor))
                        return false;
                }

                // 여러 선분을 하나의 swept capsule로 감싼다.
                // 꼭짓점의 현 이탈량만큼 반경을 늘려, 중간 궤적을 누락하지 않는다.
                Vector3 chord = p - start;
                float deviationSquared = 0;
                for (int j = 0; j < block - 1; j++)
                {
                    // 이동 장애물의 상대 궤적도 감싸기 위해 같은 시각의 현 위치와 비교한다.
                    float t = (float)(j + 1) / block;
                    deviationSquared = Mathf.Max(deviationSquared, (previewVertices[j] - start - chord * t).sqrMagnitude);
                }

                float inflated = radius + Mathf.Sqrt(deviationSquared) + .0001f;
                if (!query.SegmentSafe(start, p, inflated, i * step, (i + block) * step))
                    return false;
                i += block;
            }

            return true;
        }

        bool Inside(Vector3 p, float radius)
        {
            return (!volume || volume.Contains(p, radius)) && (!path || path.Contains(p, radius, pathCursor));
        }

        public Vector3 Choose(Vector3 p,
             Vector3 current,
             Vector3 preferred,
             float bodyRadius,
             float dt,
             int cursor,
             ref BoidAvoidanceMemory memory)
        {
            using var profile = BoidProfiler.Choose.Auto();
            pathCursor = cursor;
            integrationStep = dt;
            float speed = Mathf.Clamp(preferred.magnitude, settings.MinimumSpeed, settings.MaximumSpeed);
            if (current.sqrMagnitude < .00001f)
                current = Vector3.forward * speed;
            Vector3 forward = current.normalized, target = preferred.sqrMagnitude > .00001f ? preferred.normalized : forward;
            float radius = bodyRadius + settings.Clearance;
            if (volume)
            {
                var box = volume.Box;
                volumeInverse = box.transform.worldToLocalMatrix;
                volumeCenter = box.center;
                var scale = box.transform.lossyScale;
                volumeHalf = box.size * .5f - new Vector3(radius / Mathf.Abs(scale.x),
                     radius / Mathf.Abs(scale.y),
                     radius / Mathf.Abs(scale.z));
            }

            float normalRate = 1 / settings.ResponseTime;
            boundaryHorizon = path ? Mathf.Min(settings.PredictionTime,
                 Mathf.Max(dt * 4,
                 (path.Radius - radius) * .7f / speed)) : settings.PredictionTime;
            // 최근 회피 해를 짧게 유지하되, 매 tick 전체 미래 구간을 다시 검증한다.
            // 장애물이 새로 접근하거나 통로를 벗어나게 되면 즉시 새 후보를 탐색한다.
            if (memory.remaining > 0 && ArcSafe(p, forward * speed, memory.direction, memory.rate, radius, settings.PredictionTime))
            {
                memory.remaining -= dt;
                Vector3 reused = Vector3.RotateTowards(forward, memory.direction, memory.rate * dt, 0) * speed;
                PeakAcceleration = Mathf.Max(PeakAcceleration, (reused - current).magnitude / dt);
                return reused;
            }

            memory.remaining = 0;
            if (ArcSafe(p, forward * speed, target, normalRate, radius, settings.PredictionTime))
                return Vector3.RotateTowards(forward, target, normalRate * dt, 0) * speed;
            AvoidanceSteps++;
            float threat = query.TimeToThreat(p, current, radius, settings.PredictionTime);
            if (volume)
            {
                for (float t = dt; t < threat; t += .05f)
                    if (!volume.Contains(p + current * t, radius))
                    {
                        threat = t;
                        break;
                    }
            }

            float best = -float.MaxValue, bestRate = 0;
            Vector3 selected = forward;
            bool found = false;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, forward);
            int routeCursor = pathCursor;
            Vector3 routeForward = path ? path.Tangent(path.Project(p, ref routeCursor)) : target;
            // Fixed bounded search. Required turn rate comes from remaining time, never a designer-entered emergency acceleration.
            int candidateCount = 0;
            for (int i = -3; i < directions.Length; i++)
            {
                Vector3 direction = i == -3 ? routeForward : i == -2 ? target : i == -1 ? (volume ? volume.Inward(p) : -forward) : rotation * directions[i];
                if (direction.sqrMagnitude < .00001f)
                    continue;
                float angle = Vector3.Angle(forward, direction) * Mathf.Deg2Rad;
                float rate = Mathf.Max(normalRate, angle / Mathf.Max(dt, threat * .55f));
                float baseScore = Vector3.Dot(direction,
                     target) + (path ? Vector3.Dot(direction,
                     routeForward) * 3 : Vector3.Dot(direction,
                     target)) + Vector3.Dot(direction,
                     forward) * .5f;
                candidates[candidateCount++] = new Candidate{direction = direction,
                     angle = angle,
                     rate = rate,
                     baseScore = baseScore,
                     upperScore = baseScore - rate * .002f,
                     order = i};
            }

            // 먼저 높은 점수 상한을 검사한다. 남은 후보의 상한보다 좋은 안전 해를 찾으면 종료한다.
            // 빠른 회전 재검사는 감점만 늘리므로 더 좋은 안전 후보를 건너뛰지 않는다.
            System.Array.Sort(candidates, 0, candidateCount);
            int selectedOrder = int.MaxValue;
            for (int i = 0; i < candidateCount; i++)
            {
                Candidate candidate = candidates[i];
                if (candidate.upperScore < best)
                    break;
                float rate = candidate.rate;
                bool safe = ArcSafe(p, forward * speed, candidate.direction, rate, radius, settings.PredictionTime);
                if (!safe)
                {
                    rate = Mathf.Max(rate, candidate.angle / dt);
                    safe = ArcSafe(p, forward * speed, candidate.direction, rate, radius, settings.PredictionTime);
                }

                if (!safe)
                    continue;
                float score = candidate.baseScore - rate * .002f;
                if (score > best || (score == best && candidate.order < selectedOrder))
                {
                    best = score;
                    selected = candidate.direction;
                    bestRate = rate;
                    found = true;
                    selectedOrder = candidate.order;
                }
            }

            if (!found)
            {
                // Explicit degraded state: retain continuous movement only if one physical step is clear.
                FailedPlans++;
                for (int i = 0; i < directions.Length; i++)
                {
                    Vector3 candidate = rotation * directions[i];
                    Vector3 next = p + candidate * speed * dt;
                    if (Inside(next, radius) && query.SegmentSafe(p, next, radius, 0, dt))
                    {
                        selected = candidate;
                        bestRate = Mathf.PI / dt;
                        found = true;
                        break;
                    }
                }
            }

            if (found)
            {
                memory.direction = selected;
                memory.rate = bestRate;
                memory.remaining = Mathf.Max(.08f, memory.duration);
            }

            Vector3 result = found ? Vector3.RotateTowards(forward, selected, bestRate * dt, 0) * speed : current;
            PeakAcceleration = Mathf.Max(PeakAcceleration, (result - current).magnitude / dt);
            return result;
        }
    }
}
