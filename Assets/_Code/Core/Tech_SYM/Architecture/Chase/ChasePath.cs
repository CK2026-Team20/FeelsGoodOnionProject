using System;
using System.Collections.Generic;
using UnityEngine;
namespace Cooked.Chase
{
    /// <summary>Horizontal distance along a polyline. Jump height does not increase escape progress.</summary>
    public sealed class ChasePath
    {
        private readonly Vector3[] points;
        private readonly float[] distances;
        public float Length => distances[distances.Length - 1];
        public ChasePath(IReadOnlyList<Vector3> waypoints)
        {
            if (waypoints == null || waypoints.Count < 2) throw new ArgumentException("At least two route points required.");
            points = new Vector3[waypoints.Count]; distances = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                if (!IsFinite(waypoints[i])) throw new ArgumentException("Route coordinates must be finite.");
                points[i] = waypoints[i];
                if (i == 0) continue;
                float length = Horizontal(points[i] - points[i - 1]).magnitude;
                if (length < .001f) throw new ArgumentException("Route segments require horizontal separation.");
                distances[i] = distances[i - 1] + length;
            }
            if (!ChaseSettings.IsFinite(Length) || Length <= ChaseSettings.InitialGap)
                throw new ArgumentException("Route is too short for the initial gap.");
        }
        public float Project(Vector3 position)
        {
            if (!IsFinite(position)) throw new ArgumentException("Actor position must be finite.");
            float nearest = float.PositiveInfinity, progress = 0;
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 segment = Horizontal(points[i] - points[i - 1]);
                float t = Mathf.Clamp01(Vector3.Dot(Horizontal(position - points[i - 1]), segment) / segment.sqrMagnitude);
                float error = Horizontal(position - (points[i - 1] + segment * t)).sqrMagnitude;
                if (error < nearest) { nearest = error; progress = distances[i - 1] + (distances[i] - distances[i - 1]) * t; }
            }
            return progress;
        }
        public float DistanceFromRoute(Vector3 position) => Horizontal(position - Evaluate(Project(position))).magnitude;
        public Vector3 Evaluate(float progress)
        {
            if (!ChaseSettings.IsFinite(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
            progress = Mathf.Clamp(progress, 0, Length);
            for (int i = 1; i < points.Length; i++)
                if (progress <= distances[i])
                    return Vector3.LerpUnclamped(points[i - 1], points[i], (progress - distances[i - 1]) / (distances[i] - distances[i - 1]));
            return points[points.Length - 1];
        }
        private static Vector3 Horizontal(Vector3 value) => new Vector3(value.x, 0, value.z);
        internal static bool IsFinite(Vector3 p) => ChaseSettings.IsFinite(p.x) && ChaseSettings.IsFinite(p.y) && ChaseSettings.IsFinite(p.z);
    }
}