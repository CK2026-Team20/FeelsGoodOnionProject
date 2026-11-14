using System;
using System.Collections.Generic;
using UnityEngine;
using Cooked.Authoring;

namespace Cooked.Boids
{
    public enum BoidMovementMode
    {
        Volume,
        Path
    }

    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class BoidGroup : MonoBehaviour
    {
        [SerializeField, InspectorLabel("이동 방식"), Tooltip("박스 내부 활동 또는 개체별 베지어 경로 추종을 선택하세요.")]
        BoidMovementMode mode;
        [SerializeField, InspectorLabel("군집 설정"), Tooltip("분리·정렬·응집·회피 설정을 연결하세요.")]
        BoidSettings settings;
        [SerializeField, InspectorLabel("보이드 프리팹"), Tooltip("원본 Monster Rig 배율과 Idle·Trail을 가진 보이드 전용 프리팹입니다.")]
        BoidAgent prefab;
        [SerializeField, InspectorLabel("활동 영역"), Tooltip("영역 방식에서만 사용하는 BoxCollider 활동 영역입니다.")]
        BoidActivityVolume volume;
        [SerializeField, InspectorLabel("베지어 경로"), Tooltip("경로 방식에서만 사용하는 연속 베지어 경로입니다.")]
        BoidBezierPath path;
        [SerializeField, InspectorLabel("스폰 영역"), Tooltip("최초 배치할 BoxCollider Trigger입니다. 앞 방향으로 출발합니다.")]
        BoxCollider spawn;
        [SerializeField, InspectorLabel("개체 수"), Tooltip("최초 대여할 개체 수입니다."), Min(1)]
        int count = 12;
        [SerializeField, InspectorLabel("난수 시드"), Tooltip("스폰 위치와 Idle 분포를 재현하는 난수 시드입니다.")]
        int seed = 137;
        [SerializeField, InspectorLabel("장애물 레이어"), Tooltip("주변에서 자동 검색할 충돌체 레이어입니다. Trigger와 자신의 풀 개체는 제외합니다.")]
        LayerMask obstacleMask = ~0;
        [SerializeField, InspectorLabel("접근 속력 예산"), Tooltip("검색 범위를 확장할 장애물 예상 최대 접근 속력입니다(m/s). 실제 이동 예측은 관측 속도를 사용합니다."), Min(0)]
        float maximumObstacleSpeed = 10;
        readonly System.Diagnostics.Stopwatch stepClock = new System.Diagnostics.Stopwatch();
        readonly List<BoidAgent> active = new List<BoidAgent>();
        BoidPool pool;
        BoidObstacleQuery query;
        BoidMotionPlanner planner;
        Transform runtimeRoot;
        Vector3[] positions, velocities, next;
        public IReadOnlyList<BoidAgent> Agents => active;
        public BoidMovementMode Mode => mode;
        public BoidBezierPath Path => path;
        public BoidActivityVolume Volume => volume;
        public int ExpectedCount => count;
        public int SweptContacts { get; private set; }

        public int PathExits { get; private set; }

        public float MinimumPairDistance { get; private set; } = float.PositiveInfinity;
        public int TickCount { get; private set; }

        public int Contacts { get; private set; }

        public int BoundaryExits { get; private set; }

        public int Returned { get; private set; }

        public float MinimumObservedSpeed { get; private set; } = float.PositiveInfinity;
        public float MaximumStepMilliseconds { get; private set; }

        public double TotalStepMilliseconds { get; private set; }

        public int PoolCreated => pool?.Created ?? 0;
        public int AvoidanceSteps => planner?.AvoidanceSteps ?? 0;
        public int FailedPlans => planner?.FailedPlans ?? 0;
        public float PeakAcceleration => planner?.PeakAcceleration ?? 0;
        public void Configure(BoidMovementMode m,
             BoidSettings s,
             BoidAgent a,
             BoxCollider region,
             BoidActivityVolume v,
             BoidBezierPath p,
             int n = 12)
        {
            mode = m;
            settings = s;
            prefab = a;
            spawn = region;
            volume = v;
            path = p;
            count = n;
        }

        void OnEnable()
        {
            if (Application.isPlaying)
                StartGroup();
        }

        void OnDisable()
        {
            if (Application.isPlaying)
                Clear();
        }

        public void StartGroup()
        {
            if (pool != null)
                return;
            try
            {
                if (!float.IsFinite(maximumObstacleSpeed) || maximumObstacleSpeed < 0)
                    throw new InvalidOperationException("접근 속력 예산은 0 이상의 유한값이어야 합니다.");
                if (count < 1)
                    throw new InvalidOperationException("개체 수는 1 이상이어야 합니다.");
                if (!settings || !prefab || !spawn)
                    throw new InvalidOperationException("군집 설정·전용 프리팹·스폰 박스를 연결하세요.");
                settings.Validate();
                prefab.Validate();
                if (mode == BoidMovementMode.Volume)
                {
                    if (!volume)
                        throw new InvalidOperationException("활동 볼륨이 없습니다.");
                    volume.Validate(prefab.Radius + settings.Clearance);
                }
                else
                {
                    if (!path)
                        throw new InvalidOperationException("베지어 경로가 없습니다.");
                    path.Rebuild();
                    if (path.Radius <= prefab.Radius)
                        throw new InvalidOperationException("경로 폭이 몸 크기보다 작습니다.");
                }

                runtimeRoot = new GameObject(name + "_PooledAgents").transform;
                runtimeRoot.SetParent(transform, false);
                query = new BoidObstacleQuery(obstacleMask, maximumObstacleSpeed, runtimeRoot);
                planner = new BoidMotionPlanner(settings,
                     query,
                     mode == BoidMovementMode.Volume ? volume : null,
                     mode == BoidMovementMode.Path ? path : null);
                pool = new BoidPool(prefab, runtimeRoot, count);
                positions = new Vector3[count];
                velocities = new Vector3[count];
                next = new Vector3[count];
                Respawn();
            }
            catch (Exception e)
            {
                Debug.LogError("보이드 군집 초기화 실패: " + e.Message, this);
                Clear();
                enabled = false;
            }
        }

        public void Respawn()
        {
            if (pool == null)
                return;
            query.ResetHistory();
            foreach (var a in active)
                pool.Return(a);
            active.Clear();
            TickCount = Contacts = BoundaryExits = Returned = SweptContacts = PathExits = 0;
            MinimumPairDistance = float.PositiveInfinity;
            MinimumObservedSpeed = float.PositiveInfinity;
            MaximumStepMilliseconds = 0;
            TotalStepMilliseconds = 0;
            var random = new System.Random(seed);
            var phases = BoidPool.StratifiedPhases(count, random);
            try
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 p = Vector3.zero;
                    bool valid = false;
                    for (int attempt = 0; attempt < 2000; attempt++)
                    {
                        Vector3 local = spawn.center + Vector3.Scale(spawn.size,
                             new Vector3((float)random.NextDouble() - .5f,
                             (float)random.NextDouble() - .5f,
                             (float)random.NextDouble() - .5f));
                        p = spawn.transform.TransformPoint(local);
                        valid = !query.Overlapping(p,
                             prefab.Radius + settings.Clearance)
                            && (mode != BoidMovementMode.Volume
                            || volume.Contains(p,
                             prefab.Radius + settings.Clearance));
                        if (valid && mode == BoidMovementMode.Path)
                        {
                            int candidateCursor = 0;
                            path.Project(p, ref candidateCursor, true);
                            valid = path.Contains(p, prefab.Radius + settings.Clearance, candidateCursor);
                        }

                        if (valid)
                            foreach (var other in active)
                                if ((other.Position - p).sqrMagnitude < Mathf.Pow(prefab.Radius * 2 + settings.Clearance, 2))
                                {
                                    valid = false;
                                    break;
                                }

                        if (valid)
                            break;
                    }

                    if (!valid)
                        throw new InvalidOperationException("스폰 공간 부족: 겹치지 않는 위치를 찾지 못했습니다.");
                    var a = pool.Rent(p, spawn.transform.forward * settings.CruiseSpeed, phases[i]);
                    if (mode == BoidMovementMode.Path)
                    {
                        int c = 0;
                        a.Progress = path.Project(p, ref c, true);
                        a.Cursor = c;
                    }

                    active.Add(a);
                }
            }
            catch
            {
                foreach (var a in active)
                    pool.Return(a);
                active.Clear();
                throw;
            }
        }

        void FixedUpdate()
        {
            if (pool == null || active.Count == 0)
                return;
            using var profile = BoidProfiler.Tick.Auto();
            var clock = stepClock;
            clock.Restart();
            Physics.SyncTransforms();
            float dt = Time.fixedDeltaTime;
            int n = active.Count;
            for (int i = 0; i < n; i++)
            {
                positions[i] = active[i].Position;
                velocities[i] = active[i].Velocity;
            }

            query.Refresh(positions, n, prefab.Radius + settings.Clearance, settings.MaximumSpeed, settings.PredictionTime + dt, dt);
            for (int i = 0; i < n; i++)
            {
                var a = active[i];
                Vector3 acceleration = BoidSteering.Flock(i, positions, velocities, n, settings);
                Vector3 preferred = velocities[i] + acceleration * dt;
                if (mode == BoidMovementMode.Path)
                {
                    int cursor = a.Cursor;
                    a.Progress = path.Project(positions[i], ref cursor);
                    a.Cursor = cursor;
                    Vector3 direction = path.Desired(positions[i], a.Progress, a.Radius);
                    preferred = Vector3.Lerp(preferred,
                         direction * Mathf.Clamp(preferred.magnitude,
                         settings.MinimumSpeed,
                         settings.MaximumSpeed),
                         Mathf.Clamp01(dt / settings.ResponseTime));
                }

                if (preferred.sqrMagnitude < .0001f)
                    preferred = velocities[i];
                preferred = preferred.normalized * Mathf.Clamp(preferred.magnitude, settings.MinimumSpeed, settings.MaximumSpeed);
                next[i] = planner.Choose(positions[i], velocities[i], preferred, a.Radius, dt, a.Cursor, ref a.SteeringMemory);
            }

            for (int i = n - 1; i >= 0; i--)
            {
                var a = active[i];
                a.Move(next[i], dt);
                MinimumObservedSpeed = Mathf.Min(MinimumObservedSpeed, next[i].magnitude);
                if (query.Overlapping(a.Position, a.Radius))
                    Contacts++;
                if (!query.ActualSegmentSafe(positions[i], a.Position, a.Radius))
                    SweptContacts++;
                if (mode == BoidMovementMode.Path && !path.Contains(a.Position, a.Radius, a.Cursor))
                    PathExits++;
                if (mode == BoidMovementMode.Volume && !volume.Contains(a.Position, a.Radius))
                    BoundaryExits++;
                if (mode == BoidMovementMode.Path && path.Finished(a.Position, a.Progress))
                {
                    pool.Return(a);
                    active.RemoveAt(i);
                    Returned++;
                }
            }

            using (BoidProfiler.PairDistances.Auto())
            {
                // Transform은 개체당 한 번만 읽는다. 쌍별 비교는 값 배열과 제곱 거리로 처리한다.
                for (int i = 0; i < active.Count; i++)
                    positions[i] = active[i].Position;
                float minimumSquared = MinimumPairDistance * MinimumPairDistance;
                for (int i = 0; i < active.Count; i++)
                    for (int j = i + 1; j < active.Count; j++)
                        minimumSquared = Mathf.Min(minimumSquared, (positions[i] - positions[j]).sqrMagnitude);
                MinimumPairDistance = Mathf.Sqrt(minimumSquared);
            }

            TickCount++;
            clock.Stop();
            float ms = (float)clock.Elapsed.TotalMilliseconds;
            MaximumStepMilliseconds = Mathf.Max(MaximumStepMilliseconds, ms);
            TotalStepMilliseconds += ms;
        }

        public void Clear()
        {
            active.Clear();
            pool?.Dispose();
            pool = null;
            if (runtimeRoot)
                Destroy(runtimeRoot.gameObject);
            runtimeRoot = null;
        }
    }
}
