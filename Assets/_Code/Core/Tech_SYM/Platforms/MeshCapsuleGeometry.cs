using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>
    /// 삼각형 표면과 캡슐의 교차를 double로 계산한다. Bounds는 탐색 상한/안쪽 방향에만 사용한다.
    /// 닫힌 메시의 내부는 winding number로 검사하므로 용기의 빈 내부를 채우지 않는다.
    /// 열린 표면은 삼각형 자체의 교차만 의미한다. Convex cooking 결과를 원본 삼각형으로 대체하지 않는다.
    /// </summary>
    public static class MeshCapsuleGeometry
    {
        private const int Iterations = 64;

        [Serializable]
        public sealed class Snapshot
        {
            [SerializeField, HideInInspector] private Mesh source;
            [SerializeField, HideInInspector] private Vector3[] vertices;
            [SerializeField, HideInInspector] private int[] triangles;
            [SerializeField, HideInInspector] private string sourceRevision;
            public bool Matches(Mesh mesh) => source == mesh && vertices != null && vertices.Length > 0 &&
                triangles != null && triangles.Length >= 3 && vertices.Length == mesh.vertexCount && RevisionMatches(mesh);

            private bool RevisionMatches(Mesh mesh)
            {
#if UNITY_EDITOR
                // 같은 GUID의 재임포트도 감지한다. 메모리에서 직접 deform한 Mesh는 BakeCollisionGeometry로 명시 갱신한다.
                string path = UnityEditor.AssetDatabase.GetAssetPath(mesh);
                if (!string.IsNullOrEmpty(path)) return sourceRevision == UnityEditor.AssetDatabase.GetAssetDependencyHash(path).ToString();
#endif
                return true;
            }

            /// <summary>Editor 자동 준비에서 같은 자산의 정점/인덱스 직접 수정까지 비교한다. 런타임 프레임에서는 호출하지 않는다.</summary>
            public bool ContentEquals(Snapshot other)
            {
                if (other == null || source != other.source || sourceRevision != other.sourceRevision ||
                    vertices == null || other.vertices == null || vertices.Length != other.vertices.Length ||
                    triangles == null || other.triangles == null || triangles.Length != other.triangles.Length) return false;
                for (int i = 0; i < vertices.Length; i++) if (!vertices[i].Equals(other.vertices[i])) return false;
                for (int i = 0; i < triangles.Length; i++) if (triangles[i] != other.triangles[i]) return false;
                return true;
            }

            public static Snapshot Capture(Mesh mesh)
            {
                if (mesh == null) throw new ArgumentNullException(nameof(mesh));
                // Editor에서만 CPU 사본을 준비한다. 런타임 AcquireReadOnlyMeshData는 isReadable 제한을 우회하지 못한다.
#if UNITY_EDITOR
                using (var data = UnityEditor.MeshUtility.AcquireReadOnlyMeshData(mesh))
#else
                using (var data = Mesh.AcquireReadOnlyMeshData(mesh))
#endif
                {
                    var item = data[0];
                    var result = new Snapshot { source = mesh, vertices = new Vector3[item.vertexCount] };
                    using (var positions = new Unity.Collections.NativeArray<Vector3>(item.vertexCount, Unity.Collections.Allocator.Temp))
                    {
                        item.GetVertices(positions);
                        positions.CopyTo(result.vertices);
                    }
                    var indices = new List<int>();
                    for (int sub = 0; sub < item.subMeshCount; sub++)
                    {
                        var descriptor = item.GetSubMesh(sub);
                        if (descriptor.topology != MeshTopology.Triangles)
                            throw new InvalidOperationException("주기 충돌 메시에는 삼각형 서브메시만 사용할 수 있습니다.");
                        using (var values = new Unity.Collections.NativeArray<int>(descriptor.indexCount, Unity.Collections.Allocator.Temp))
                        {
                            item.GetIndices(values, sub, true);
                            for (int i = 0; i < values.Length; i++) indices.Add(values[i]);
                        }
                    }
                    result.triangles = indices.ToArray();
#if UNITY_EDITOR
                    string path = UnityEditor.AssetDatabase.GetAssetPath(mesh);
                    result.sourceRevision = string.IsNullOrEmpty(path) ? string.Empty : UnityEditor.AssetDatabase.GetAssetDependencyHash(path).ToString();
#endif
                    return result;
                }
            }

            public WorldMesh Transform(Matrix4x4 matrix)
            {
                if (vertices == null || triangles == null) throw new InvalidOperationException("충돌 메시 CPU 사본이 준비되지 않았습니다.");
                var world = new Vector3[vertices.Length];
                for (int i = 0; i < world.Length; i++) world[i] = matrix.MultiplyPoint3x4(vertices[i]);
                return new WorldMesh(world, triangles);
            }
        }

        /// <summary>네이티브 오브젝트 없이 테스트 가능한 불변 삼각형 입력. 호출 뒤 입력 배열을 변경하지 않는다.</summary>
        public sealed class WorldMesh
        {
            private readonly Vector3[] vertices;
            private readonly int[] triangles;
            private readonly Point minimum, maximum;

            public WorldMesh(Vector3[] worldVertices, int[] triangleIndices)
            {
                if (worldVertices == null || worldVertices.Length == 0 || triangleIndices == null ||
                    triangleIndices.Length == 0 || triangleIndices.Length % 3 != 0)
                    throw new ArgumentException("유효한 월드 정점과 삼각형 인덱스가 필요합니다.");
                vertices = worldVertices; triangles = triangleIndices;
                var min = new Point(vertices[0]); var max = min;
                foreach (var vertex in vertices)
                {
                    var p = new Point(vertex);
                    if (!p.Finite) throw new ArgumentException("메시 정점은 유한해야 합니다.");
                    min = new Point(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                    max = new Point(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
                }
                foreach (int index in triangles)
                    if (index < 0 || index >= vertices.Length) throw new ArgumentOutOfRangeException(nameof(triangleIndices));
                minimum = min; maximum = max;
            }

            public PeriodicPlatform.OverlapResult Inspect(Vector3 capsuleStart, Vector3 capsuleEnd, float radius,
                float maximumGroundAngle, bool normalTop, out double bottom, out double top, out Vector3 correction,
                bool horizontalAllowed = true, bool depthAllowed = true)
            {
                var result = Measure(capsuleStart, capsuleEnd, radius, maximumGroundAngle, normalTop,
                    out bottom, out top, out bool supported);
                correction = Vector3.zero;
                if (result.Kind == PeriodicPlatform.OverlapKind.None || result.Kind == PeriodicPlatform.OverlapKind.SupportedTop) return result;
                var start = new Point(capsuleStart); var end = new Point(capsuleEnd);
                Point travel;
                if (result.Kind == PeriodicPlatform.OverlapKind.Fatal || supported) travel = new Point(0, 1, 0);
                else
                {
                    var toward = (minimum + maximum) * .5 - (start + end) * .5;
                    travel = new Point(horizontalAllowed ? toward.X : 0, 0, depthAllowed ? toward.Z : 0);
                    if (travel.LengthSquared == 0)
                        throw new InvalidOperationException("현재 이동축에서 메시 Bounds 중심으로 향하는 수평 방향이 없습니다. 배치를 확인하세요.");
                    travel *= 1 / Math.Sqrt(travel.LengthSquared);
                }
                double exit = FirstExit(start, end, radius, travel);
                correction = (travel * (exit + Math.Max(.00001, radius * .002))).Vector;
                return result;
            }

            /// <summary>실제 삼각형 교차의 월드 Y 구간과 비율만 반환한다. 분리 방향/Physics/Transform에 의존하지 않는다.</summary>
            public PeriodicPlatform.OverlapResult Measure(Vector3 capsuleStart, Vector3 capsuleEnd, float radius,
                float maximumGroundAngle, bool normalTop, out double bottom, out double top, out bool supported)
            {
                var start = new Point(capsuleStart); var end = new Point(capsuleEnd);
                if (!start.Finite || !end.Finite || !float.IsFinite(radius) || radius <= 0 ||
                    !float.IsFinite(maximumGroundAngle) || maximumGroundAngle < 0 || maximumGroundAngle > 85)
                    throw new ArgumentOutOfRangeException(nameof(radius));
                bottom = double.PositiveInfinity; top = double.NegativeInfinity;
                double bodyBottom = Math.Min(start.Y, end.Y) - radius, bodyTop = Math.Max(start.Y, end.Y) + radius;
                var lowCap = start.Y <= end.Y ? start : end;
                var highCap = start.Y <= end.Y ? end : start;
                var axis = highCap - lowCap;
                if (axis.LengthSquared == 0) axis = new Point(0, 1, 0);
                supported = false;
                double cosine = Math.Cos(maximumGroundAngle * Math.PI / 180);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Triangle(i, out var a, out var b, out var c);
                    // 실제 교차 검사 전에 캡슐 AABB와 삼각형 AABB만 비교한다. 비율에는 사용하지 않는다.
                    if (!Candidate(a, b, c, start, end, radius)) continue;
                    if (TriangleInterval(a, b, c, start, end, radius, out double lo, out double hi))
                    {
                        bottom = Math.Min(bottom, lo); top = Math.Max(top, hi);
                        var normal = Point.Cross(b - a, c - a);
                        if (normal.LengthSquared > 0 && normal.Y / Math.Sqrt(normal.LengthSquared) >= cosine &&
                            LowerHemisphereIntersects(a, b, c, lowCap, axis, radius)) supported = true;
                    }
                }
                // 메시 표면이 캡슐 안을 지나지 않는 완전 포함도 실제 메시 내부 검사로 판정한다.
                if (Inside(lowCap + new Point(0, -radius, 0))) bottom = bodyBottom;
                if (Inside(highCap + new Point(0, radius, 0))) top = bodyTop;
                if (!double.IsFinite(bottom) || !double.IsFinite(top) || top < bottom)
                    return new PeriodicPlatform.OverlapResult(PeriodicPlatform.OverlapKind.None, 0);
                return top > bottom ? PeriodicPlatform.ClassifyOverlap(true, normalTop, bodyBottom, bodyTop, bottom, top) :
                    new PeriodicPlatform.OverlapResult(normalTop ? PeriodicPlatform.OverlapKind.SupportedTop : PeriodicPlatform.OverlapKind.Shallow, 0);
            }

            private void Triangle(int index, out Point a, out Point b, out Point c)
            {
                a = new Point(vertices[triangles[index]]);
                b = new Point(vertices[triangles[index + 1]]);
                c = new Point(vertices[triangles[index + 2]]);
            }

            private bool Inside(Point point)
            {
                if (point.X < minimum.X || point.X > maximum.X || point.Y < minimum.Y || point.Y > maximum.Y ||
                    point.Z < minimum.Z || point.Z > maximum.Z) return false;
                // Signed solid angle: 반대 방향의 내부 벽은 외벽의 winding을 상쇄한다.
                // 열린/비정상 메시를 임의의 Bounds 부피로 채우지 않는다.
                double angle = 0;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Triangle(i, out var a, out var b, out var c);
                    a -= point; b -= point; c -= point;
                    double al = Math.Sqrt(a.LengthSquared), bl = Math.Sqrt(b.LengthSquared), cl = Math.Sqrt(c.LengthSquared);
                    angle += 2 * Math.Atan2(Point.Dot(a, Point.Cross(b, c)),
                        al * bl * cl + Point.Dot(a, b) * cl + Point.Dot(b, c) * al + Point.Dot(c, a) * bl);
                }
                return Math.Abs(angle) >= 4 * Math.PI - 1e-7;
            }

            private double FirstExit(Point start, Point end, double radius, Point direction)
            {
                // 각 triangle ⊕ capsule의 이동축 교차는 볼록한 한 구간이다. 그 합집합의 첫 출구를 찾는다.
                // concave 메시 전체를 단일 단조 함수로 이분 탐색하면 반대쪽 벽을 건너뛰므로 사용하지 않는다.
                double limit = Math.Sqrt((maximum - minimum).LengthSquared) +
                    Math.Sqrt(((minimum + maximum) * .5 - (start + end) * .5).LengthSquared) +
                    Math.Sqrt((end - start).LengthSquared) + radius * 2 + 1;
                var intervals = new List<Interval>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Triangle(i, out var a, out var b, out var c);
                    double first = Math.Min(Point.Dot(a, direction), Math.Min(Point.Dot(b, direction), Point.Dot(c, direction))) -
                        Math.Max(Point.Dot(start, direction), Point.Dot(end, direction)) - radius;
                    double last = Math.Max(Point.Dot(a, direction), Math.Max(Point.Dot(b, direction), Point.Dot(c, direction))) -
                        Math.Min(Point.Dot(start, direction), Point.Dot(end, direction)) + radius;
                    double low = Math.Max(0, first), high = Math.Min(limit, last);
                    if (high < low) continue;
                    if (!SweptCandidate(a, b, c, start, end, radius, direction * low, direction * high)) continue;
                    double Distance(double t) => SegmentTriangleDistance(start + direction * t, end + direction * t, a, b, c);
                    if (ConvexInterval(Distance, low, high, radius * radius, out double lo, out double hi))
                        intervals.Add(new Interval(lo, hi));
                }
                intervals.Sort((a, b) => a.Low.CompareTo(b.Low));
                double exit = 0;
                foreach (var interval in intervals)
                {
                    if (interval.Low > exit + 1e-10 && !Inside((start + end) * .5 + direction * ((exit + interval.Low) * .5)))
                        return exit;
                    exit = Math.Max(exit, interval.High);
                }
                if (exit <= 0) throw new InvalidOperationException("실제 메시 교차의 분리 출구를 계산하지 못했습니다.");
                return exit;
            }
        }

        private readonly struct Interval
        {
            public readonly double Low, High;
            public Interval(double low, double high) { Low = low; High = high; }
        }

        private static bool Candidate(Point a, Point b, Point c, Point start, Point end, double radius) =>
            Math.Max(a.X, Math.Max(b.X, c.X)) >= Math.Min(start.X, end.X) - radius &&
            Math.Min(a.X, Math.Min(b.X, c.X)) <= Math.Max(start.X, end.X) + radius &&
            Math.Max(a.Y, Math.Max(b.Y, c.Y)) >= Math.Min(start.Y, end.Y) - radius &&
            Math.Min(a.Y, Math.Min(b.Y, c.Y)) <= Math.Max(start.Y, end.Y) + radius &&
            Math.Max(a.Z, Math.Max(b.Z, c.Z)) >= Math.Min(start.Z, end.Z) - radius &&
            Math.Min(a.Z, Math.Min(b.Z, c.Z)) <= Math.Max(start.Z, end.Z) + radius;

        private static bool SweptCandidate(Point a, Point b, Point c, Point start, Point end, double radius, Point first, Point last)
        {
            var low = new Point(Math.Min(start.X, end.X) + Math.Min(first.X, last.X),
                Math.Min(start.Y, end.Y) + Math.Min(first.Y, last.Y), Math.Min(start.Z, end.Z) + Math.Min(first.Z, last.Z));
            var high = new Point(Math.Max(start.X, end.X) + Math.Max(first.X, last.X),
                Math.Max(start.Y, end.Y) + Math.Max(first.Y, last.Y), Math.Max(start.Z, end.Z) + Math.Max(first.Z, last.Z));
            return Candidate(a, b, c, low, high, radius);
        }

        private static bool LowerHemisphereIntersects(Point a, Point b, Point c, Point center, Point axis, double radius)
        {
            Span<Point> input = stackalloc Point[3] { a, b, c };
            Span<Point> clipped = stackalloc Point[4];
            int count = 0;
            for (int i = 0; i < 3; i++)
            {
                var p = input[i]; var q = input[(i + 1) % 3];
                double pd = Point.Dot(p - center, axis), qd = Point.Dot(q - center, axis);
                if (pd <= 0) clipped[count++] = p;
                if ((pd < 0 && qd > 0) || (pd > 0 && qd < 0)) clipped[count++] = p + (q - p) * (pd / (pd - qd));
            }
            for (int i = 1; i + 1 < count; i++)
                if (PointTriangleDistance(center, clipped[0], clipped[i], clipped[i + 1]) <= radius * radius) return true;
            return false;
        }

        private static bool TriangleInterval(Point a, Point b, Point c, Point start, Point end, double radius,
            out double bottom, out double top)
        {
            double low = Math.Max(Math.Min(a.Y, Math.Min(b.Y, c.Y)), Math.Min(start.Y, end.Y) - radius);
            double high = Math.Min(Math.Max(a.Y, Math.Max(b.Y, c.Y)), Math.Max(start.Y, end.Y) + radius);
            bottom = top = 0;
            if (high < low) return false;
            if (SegmentTriangleDistance(start, end, a, b, c) > radius * radius) return false;
            if (a.Y == b.Y && b.Y == c.Y)
            {
                if (SegmentTriangleDistance(start, end, a, b, c) > radius * radius) return false;
                bottom = top = a.Y; return true;
            }
            double Distance(double height) => SliceDistance(a, b, c, height, start, end);
            return ConvexInterval(Distance, low, high, radius * radius, out bottom, out top);
        }

        private static bool ConvexInterval(Func<double, double> distance, double low, double high, double radiusSquared,
            out double bottom, out double top)
        {
            // 거리 제곱은 볼록하다. 끝점을 포함해 최솟값을 찾고 교차 구간의 양끝을 바깥 bracket으로 반환한다.
            // 1/4 비교에 epsilon을 더하지 않으며, 정확한 경계를 아래쪽으로 반올림하지 않는다.
            double left = low, right = high;
            for (int i = 0; i < Iterations; i++)
            {
                double one = (2 * left + right) / 3, two = (left + 2 * right) / 3;
                if (distance(one) <= distance(two)) right = two; else left = one;
            }
            double seed = (left + right) * .5, best = distance(seed);
            double atLow = distance(low), atHigh = distance(high);
            if (atLow < best) { seed = low; best = atLow; }
            if (atHigh < best) { seed = high; best = atHigh; }
            bottom = top = 0;
            if (best > radiusSquared) return false;
            bottom = low; top = high;
            if (atLow > radiusSquared)
            {
                double outside = low, inside = seed;
                for (int i = 0; i < Iterations; i++)
                {
                    double mid = (outside + inside) * .5;
                    if (distance(mid) <= radiusSquared) inside = mid; else outside = mid;
                }
                bottom = outside;
            }
            if (atHigh > radiusSquared)
            {
                double inside = seed, outside = high;
                for (int i = 0; i < Iterations; i++)
                {
                    double mid = (inside + outside) * .5;
                    if (distance(mid) <= radiusSquared) inside = mid; else outside = mid;
                }
                top = outside;
            }
            return true;
        }

        private static double SliceDistance(Point a, Point b, Point c, double height, Point start, Point end)
        {
            Span<Point> points = stackalloc Point[3] { a, b, c };
            Span<Point> section = stackalloc Point[6]; int count = 0;
            for (int i = 0; i < 3; i++)
            {
                var p = points[i]; var q = points[(i + 1) % 3];
                if (height < Math.Min(p.Y, q.Y) || height > Math.Max(p.Y, q.Y)) continue;
                if (p.Y == q.Y) { section[count++] = p; section[count++] = q; }
                else section[count++] = p + (q - p) * ((height - p.Y) / (q.Y - p.Y));
            }
            if (count == 0) return double.PositiveInfinity;
            double best = PointSegmentDistance(section[0], start, end);
            for (int i = 0; i < count; i++) for (int j = i + 1; j < count; j++)
                best = Math.Min(best, SegmentDistance(start, end, section[i], section[j]));
            return best;
        }

        private static double SegmentTriangleDistance(Point start, Point end, Point a, Point b, Point c)
        {
            var normal = Point.Cross(b - a, c - a);
            double denominator = Point.Dot(normal, end - start);
            if (denominator != 0)
            {
                double t = Point.Dot(normal, a - start) / denominator;
                if (t >= 0 && t <= 1 && InTriangle(start + (end - start) * t, a, b, c, normal)) return 0;
            }
            return Math.Min(Math.Min(PointTriangleDistance(start, a, b, c), PointTriangleDistance(end, a, b, c)),
                Math.Min(SegmentDistance(start, end, a, b), Math.Min(SegmentDistance(start, end, b, c), SegmentDistance(start, end, c, a))));
        }
        private static bool InTriangle(Point point, Point a, Point b, Point c, Point normal) =>
            Point.Dot(Point.Cross(b - a, point - a), normal) >= 0 &&
            Point.Dot(Point.Cross(c - b, point - b), normal) >= 0 &&
            Point.Dot(Point.Cross(a - c, point - c), normal) >= 0;

        private static double PointTriangleDistance(Point point, Point a, Point b, Point c)
        {
            var normal = Point.Cross(b - a, c - a);
            if (normal.LengthSquared > 0)
            {
                double signed = Point.Dot(point - a, normal);
                var projected = point - normal * (signed / normal.LengthSquared);
                if (InTriangle(projected, a, b, c, normal)) return signed * signed / normal.LengthSquared;
            }
            return Math.Min(PointSegmentDistance(point, a, b), Math.Min(PointSegmentDistance(point, b, c), PointSegmentDistance(point, c, a)));
        }
        private static double PointSegmentDistance(Point p, Point a, Point b)
        {
            var delta = b - a;
            double t = delta.LengthSquared > 0 ? Clamp(Point.Dot(p - a, delta) / delta.LengthSquared, 0, 1) : 0;
            return (p - a - delta * t).LengthSquared;
        }
        private static double SegmentDistance(Point p, Point q, Point a, Point b)
        {
            var u = q - p; var v = b - a; var w = p - a;
            double uu = u.LengthSquared, vv = v.LengthSquared, uw = Point.Dot(u, w), vw = Point.Dot(v, w);
            if (uu == 0) return PointSegmentDistance(p, a, b);
            if (vv == 0) return PointSegmentDistance(a, p, q);
            double uv = Point.Dot(u, v), denominator = uu * vv - uv * uv;
            double s = denominator > 0 ? Clamp((uv * vw - uw * vv) / denominator, 0, 1) : 0;
            double t = (uv * s + vw) / vv;
            if (t < 0) { t = 0; s = Clamp(-uw / uu, 0, 1); }
            else if (t > 1) { t = 1; s = Clamp((uv - uw) / uu, 0, 1); }
            return (w + u * s - v * t).LengthSquared;
        }
        private static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));
        private readonly struct Point
        {
            public readonly double X, Y, Z;
            public Point(double x, double y, double z) { X = x; Y = y; Z = z; }
            public Point(Vector3 value) : this(value.x, value.y, value.z) { }
            public bool Finite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
            public double LengthSquared => X * X + Y * Y + Z * Z;
            public Vector3 Vector => new Vector3((float)X, (float)Y, (float)Z);
            public static Point operator +(Point a, Point b) => new Point(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static Point operator -(Point a, Point b) => new Point(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static Point operator *(Point a, double b) => new Point(a.X * b, a.Y * b, a.Z * b);
            public static double Dot(Point a, Point b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
            public static Point Cross(Point a, Point b) => new Point(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }
    }
}
