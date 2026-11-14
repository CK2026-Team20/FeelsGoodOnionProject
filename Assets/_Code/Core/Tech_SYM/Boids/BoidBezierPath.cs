using System;
using System.Collections.Generic;
using UnityEngine;
using Cooked.Authoring;

namespace Cooked.Boids
{
    [Serializable]
    public sealed class BoidBezierWaypoint
    {
        [InspectorLabel("경유점"), Tooltip("곡선이 통과하는 Transform을 연결하세요.")]
        public Transform anchor;
        [InspectorLabel("진입 핸들"), Tooltip("진입 곡선을 조절하는 Transform을 연결하세요.")]
        public Transform incoming;
        [InspectorLabel("출발 핸들"), Tooltip("출발 곡선을 조절하는 Transform을 연결하세요.")]
        public Transform outgoing;
    }

    [DisallowMultipleComponent]
    public sealed class BoidBezierPath : MonoBehaviour
    {
        [SerializeField, InspectorLabel("웨이포인트"), Tooltip("베지어 경유점과 핸들을 순서대로 연결하세요. 노란색은 중심선입니다.")]
        BoidBezierWaypoint[] waypoints;
        [SerializeField, InspectorLabel("순환"), Tooltip("마지막 곡선을 첫 경유점에 연결합니다. 끄면 끝을 통과한 개체를 풀로 반환합니다.")]
        bool loop;
        [SerializeField, InspectorLabel("전체 경로 폭"), Tooltip("중심선 주변 허용 통로의 전체 지름입니다(m). 군집을 이 너비로 강제로 넓히지 않습니다."), Min(.1f)]
        float width = 5;
        [SerializeField, InspectorLabel("앞을 보는 거리"), Tooltip("자신의 진행 위치보다 앞선 곡선 방향을 읽는 거리입니다(m)."), Min(.1f)]
        float lookAhead = 2;
        float approximationError;
        Vector3[] points = Array.Empty<Vector3>(), segmentDelta = Array.Empty<Vector3>();
        float[] distances = Array.Empty<float>(), inverseLengthSquared = Array.Empty<float>(), segmentLength = Array.Empty<float>();
        public float Length => distances.Length > 0 ? distances[distances.Length - 1] : 0;
        public float Radius => width * .5f;
        public bool Loop => loop;
        public bool IsStraight => !loop && points.Length == 2;
        public void Configure(BoidBezierWaypoint[] nodes, bool closed, float fullWidth)
        {
            waypoints = nodes;
            loop = closed;
            width = fullWidth;
            Rebuild();
        }

        public void Rebuild()
        {
            if (waypoints == null || waypoints.Length < 2)
                throw new InvalidOperationException("베지어 경유점이 2개 이상 필요합니다.");
            var list = new List<Vector3>();
            int segments = loop ? waypoints.Length : waypoints.Length - 1;
            for (int s = 0; s < segments; s++)
            {
                var a = waypoints[s];
                var b = waypoints[(s + 1) % waypoints.Length];
                if (!a.anchor || !a.outgoing || !b.anchor || !b.incoming)
                    throw new InvalidOperationException("베지어 핸들 연결이 누락되었습니다.");
                float estimate = Vector3.Distance(a.anchor.position,
                     a.outgoing.position) + Vector3.Distance(a.outgoing.position,
                     b.incoming.position) + Vector3.Distance(b.incoming.position,
                     b.anchor.position);
                int samples = Mathf.Clamp(Mathf.CeilToInt(estimate / .2f), 16, 512);
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / samples, u = 1 - t;
                    list.Add(u * u * u * a.anchor.position + 3 * u * u * t * a.outgoing.position + 3 * u * t * t * b.incoming.position + t * t * t * b.anchor.position);
                }
            }

            list.Add(waypoints[loop ? 0 : waypoints.Length - 1].anchor.position);
            // 직선 베지어의 촘촘한 샘플을 한 선분으로 축약한다.
            // 최대 0.1mm 근사 오차를 허용하고, Contains에서 그 오차만큼 통로를 줄인다.
            approximationError = 0;
            Vector3 lineStart = list[0], lineDelta = list[list.Count - 1] - lineStart;
            float lineSq = lineDelta.sqrMagnitude, previousAlong = -1, maximumErrorSq = 0;
            bool straight = lineSq > .0001f;
            for (int i = 0; i < list.Count && straight; i++)
            {
                float along = Vector3.Dot(list[i] - lineStart, lineDelta) / lineSq;
                float errorSq = (list[i] - (lineStart + lineDelta * along)).sqrMagnitude;
                maximumErrorSq = Mathf.Max(maximumErrorSq, errorSq);
                if (errorSq > .00000001f || along < previousAlong - .000001f)
                    straight = false;
                previousAlong = along;
            }

            if (straight && !loop)
            {
                Vector3 endPoint = list[list.Count - 1];
                approximationError = Mathf.Sqrt(maximumErrorSq);
                list.Clear();
                list.Add(lineStart);
                list.Add(endPoint);
            }

            points = list.ToArray();
            distances = new float[points.Length];
            segmentDelta = new Vector3[points.Length - 1];
            inverseLengthSquared = new float[points.Length - 1];
            segmentLength = new float[points.Length - 1];
            for (int i = 0; i < points.Length - 1; i++)
            {
                var delta = points[i + 1] - points[i];
                segmentDelta[i] = delta;
                inverseLengthSquared[i] = 1 / Mathf.Max(delta.sqrMagnitude, .000001f);
                segmentLength[i] = delta.magnitude;
                distances[i + 1] = distances[i] + segmentLength[i];
            }

            if (Length < .01f)
                throw new InvalidOperationException("베지어 경로 길이가 0입니다.");
        }

        public Vector3 Point(float distance)
        {
            float d = loop ? Mathf.Repeat(distance, Length) : Mathf.Clamp(distance, 0, Length);
            int i = Array.BinarySearch(distances, d);
            if (i < 0)
                i = ~i;
            i = Mathf.Clamp(i, 1, points.Length - 1);
            float t = Mathf.InverseLerp(distances[i - 1], distances[i], d);
            return Vector3.Lerp(points[i - 1], points[i], t);
        }

        public Vector3 Tangent(float d)
        {
            return (Point(Mathf.Min(d + .1f, loop ? float.MaxValue : Length)) - Point(Mathf.Max(0, d - .1f))).normalized;
        }

        public float Project(Vector3 p, ref int cursor, bool global = false)
        {
            return Project(p, ref cursor, global, out _);
        }

        float Project(Vector3 p, ref int cursor, bool global, out Vector3 nearest)
        {
            int start = global ? 0 : Mathf.Max(0,
                 cursor - 12),
                 end = global ? points.Length - 2 : Mathf.Min(points.Length - 2,
                 cursor + 20);
            float best = float.PositiveInfinity, result = 0;
            int index = cursor;
            nearest = points[Mathf.Clamp(cursor, 0, points.Length - 1)];
            for (int i = start; i <= end; i++)
            {
                Vector3 a = points[i], d = segmentDelta[i];
                float x = p.x - a.x, y = p.y - a.y, z = p.z - a.z;
                float t = Mathf.Clamp01((x * d.x + y * d.y + z * d.z) * inverseLengthSquared[i]);
                x -= d.x * t;
                y -= d.y * t;
                z -= d.z * t;
                float sq = x * x + y * y + z * z;
                if (sq < best)
                {
                    best = sq;
                    result = distances[i] + segmentLength[i] * t;
                    index = i;
                    nearest = new Vector3(p.x - x, p.y - y, p.z - z);
                }
            }

            cursor = index;
            if (loop && cursor >= points.Length - 3 && (p - points[0]).sqrMagnitude < lookAhead * lookAhead)
            {
                cursor = 0;
                nearest = points[0];
                return 0;
            }

            return result;
        }

        public bool Contains(Vector3 p, float bodyRadius, int cursor)
        {
            return Contains(p, bodyRadius, ref cursor);
        }

        public bool Contains(Vector3 p, float bodyRadius, ref int cursor)
        {
#if BOIDS_DETAILED_PROFILING
            using var profile = BoidProfiler.PathContains.Auto();
#endif
            float progress = Project(p, ref cursor, false, out Vector3 center);
            if (!loop && progress >= Length - .01f)
            {
                Vector3 tangent = Tangent(Length);
                float along = Vector3.Dot(p - center, tangent);
                if (along > 0)
                    center += along * tangent;
            }

            float allowed = Radius - bodyRadius - approximationError;
            return allowed >= 0 && (p - center).sqrMagnitude <= allowed * allowed;
        }

        public Vector3 Desired(Vector3 p, float progress, float bodyRadius)
        {
            Vector3 center = Point(progress), offset = p - center;
            float allowed = Mathf.Max(.05f, Radius - bodyRadius);
            Vector3 tangent = (Point(progress + lookAhead) - center).normalized;
            if (tangent.sqrMagnitude < .001f)
                tangent = Tangent(progress);
            float strength = Mathf.Clamp01((offset.magnitude - allowed * .65f) / (allowed * .35f));
            return (tangent - offset.normalized * strength * 1.8f).normalized;
        }

        public bool Finished(Vector3 p, float progress)
        {
            return !loop && progress >= Length - .2f && Vector3.Dot(p - Point(Length), Tangent(Length)) >= 0;
        }

        void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Length < 2)
                return;
            try
            {
                if (!Application.isPlaying)
                    Rebuild();
                Gizmos.color = Color.yellow;
                for (int i = 1; i < points.Length; i++)
                    Gizmos.DrawLine(points[i - 1], points[i]);
                for (int i = 0; i < points.Length; i += Mathf.Max(1, points.Length / 12))
                {
                    Gizmos.color = new Color(1, 1, 0, .25f);
                    Gizmos.DrawWireSphere(points[i], Radius);
                }
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
