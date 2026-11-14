#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Cooked.Authoring;

namespace Cooked.Boids
{
    [Serializable]
    public sealed class BoidScenario
    {
        [InspectorLabel("군집"), Tooltip("검증할 군집을 연결하세요.")]
        public BoidGroup group;
        [InspectorLabel("통과 기준"), Tooltip("장애물 통과를 측정하는 고정 Transform입니다. 앞 방향이 통과 방향입니다.")]
        public Transform gate;
        [InspectorLabel("구멍 반경"), Tooltip("도넛 구멍 통과를 인정할 반경입니다(m). 0이면 일반 통과만 측정합니다.")]
        public float holeRadius;
        [InspectorLabel("움직이는 구조물"), Tooltip("시나리오 시작시 초기화할 이동 장애물입니다.")]
        public BoidMovingObstacle mover;
    }

    [Serializable]
    public sealed class BoidScenarioResult
    {
        public string name;
        public string failure;
        public bool passed;
        public int sweptContacts, pathExits, animationAdvanced;
        public float minimumPairDistance;
        public int ticks, contacts, boundaryExits, crossed, holePassed, returned, failedPlans, avoidanceSteps, phaseBins, poolCreated;
        public bool poolReused;
        public float minSpeed, meanMs, maxMs, peakAcceleration, maxPathOffset;
        public int initialCount;
    }

    [Serializable]
    public sealed class BoidTestReport
    {
        public string utc;
        public BoidScenarioResult[] cases;
    }

    [DefaultExecutionOrder(200)]
    public sealed class BoidTestSession : MonoBehaviour
    {
        [SerializeField, InspectorLabel("시험 시나리오"), Tooltip("현재 씬의 경로·영역 시나리오를 순서대로 연결하세요.")]
        BoidScenario[] scenarios;
        [SerializeField, InspectorLabel("자동 검증"), Tooltip("Play 시작시 각 시나리오를 실제 물리 업데이트로 순차 실행합니다.")]
        bool runAutomatically = true;
        [SerializeField, InspectorLabel("시나리오 시간"), Tooltip("각 시나리오를 관찰할 실제 게임 시간입니다(s)."), Min(3)]
        float duration = 18;
        [SerializeField, InspectorLabel("시험 카메라"), Tooltip("시나리오를 따라 이동할 전용 시험 카메라입니다.")]
        Camera testCamera;
        int index = -1;
        float elapsed;
        readonly List<BoidScenarioResult> results = new List<BoidScenarioResult>();
        readonly Dictionary<BoidAgent, Vector3> last = new Dictionary<BoidAgent, Vector3>();
        readonly HashSet<BoidAgent> crossed = new HashSet<BoidAgent>(), holes = new HashSet<BoidAgent>();
        readonly Dictionary<BoidAgent, double> initialAnimation = new Dictionary<BoidAgent, double>();
        readonly HashSet<BoidAgent> animated = new HashSet<BoidAgent>();
        BoidScenarioResult current;
        public bool Completed { get; private set; }

        public int CurrentIndex => index;
        public string CurrentName => index >= 0 && index < scenarios.Length ? scenarios[index].group.name : "완료";
        public IReadOnlyList<BoidScenarioResult> Results => results;
        public void Configure(BoidScenario[] list, Camera camera)
        {
            scenarios = list;
            testCamera = camera;
        }

        void Start()
        {
            if (runAutomatically)
                Begin();
        }

        public void Begin()
        {
            foreach (var s in scenarios)
            {
                s.group.enabled = false;
                if (s.mover)
                    s.mover.enabled = false;
            }

            results.Clear();
            index = -1;
            Completed = false;
            Next();
        }

        void Next()
        {
            if (index >= 0)
            {
                var s = scenarios[index];
                var g = s.group;
                current.ticks = g.TickCount;
                current.sweptContacts = g.SweptContacts;
                current.pathExits = g.PathExits;
                current.minimumPairDistance = g.MinimumPairDistance;
                current.animationAdvanced = animated.Count;
                current.contacts = g.Contacts;
                current.boundaryExits = g.BoundaryExits;
                current.crossed = crossed.Count;
                current.holePassed = holes.Count;
                current.returned = g.Returned;
                current.minSpeed = g.MinimumObservedSpeed;
                current.meanMs = (float)(g.TotalStepMilliseconds / Math.Max(1, g.TickCount));
                current.maxMs = g.MaximumStepMilliseconds;
                current.failedPlans = g.FailedPlans;
                current.avoidanceSteps = g.AvoidanceSteps;
                current.peakAcceleration = g.PeakAcceleration;
                int created = g.PoolCreated;
                try
                {
                    if (s.mover)
                        s.mover.ResetMotion();
                    Physics.SyncTransforms();
                    g.Respawn();
                    current.poolReused = current.initialCount == g.ExpectedCount
                        && current.initialCount > 0
                        && g.PoolCreated == created
                        && g.Agents.Count == current.initialCount;
                }
                catch (Exception e)
                {
                    current.failure = e.Message;
                    current.poolReused = false;
                }

                current.poolCreated = g.PoolCreated;
                g.enabled = false;
                if (s.mover)
                    s.mover.enabled = false;
                current.passed = string.IsNullOrEmpty(current.failure)
                    && current.initialCount == g.ExpectedCount
                    && current.ticks > 0
                    && current.contacts == 0
                    && current.sweptContacts == 0
                    && current.boundaryExits == 0
                    && current.pathExits == 0
                    && current.failedPlans == 0
                    && current.poolReused
                    && current.phaseBins == current.initialCount
                    && current.animationAdvanced == current.initialCount
                    && current.minSpeed > 0
                    && current.crossed > 0
                    && (s.holeRadius <= 0
                    || current.holePassed == current.initialCount)
                    && (g.Mode != BoidMovementMode.Path
                    || g.Path.Loop
                    || current.returned == current.initialCount);
                results.Add(current);
            }

            index++;
            initialAnimation.Clear();
            animated.Clear();
            last.Clear();
            crossed.Clear();
            holes.Clear();
            elapsed = 0;
            if (index >= scenarios.Length)
            {
                Completed = true;
                string folder = "output/Tech_SYM/Boids/2026-11-13";
                System.IO.Directory.CreateDirectory(folder);
                var report = new BoidTestReport{utc = DateTime.UtcNow.ToString("O"), cases = results.ToArray()};
                System.IO.File.WriteAllText(folder + "/play-results-" + DateTime.UtcNow.ToString("HHmmss") + ".json",
                     JsonUtility.ToJson(report,
                     true));
                Debug.Log("Boids Test 자동 실행 완료: " + results.Count + "개 시나리오");
                return;
            }

            var scenario = scenarios[index];
            if (scenario.mover)
            {
                scenario.mover.ResetMotion();
                scenario.mover.enabled = true;
            }

            Physics.SyncTransforms();
            scenario.group.enabled = true;
            current = new BoidScenarioResult{name = scenario.group.name, initialCount = scenario.group.Agents.Count};
            if (current.initialCount != scenario.group.ExpectedCount || !scenario.group.enabled)
                current.failure = "군집 초기화 또는 예상 개체 수 불일치";
            var bins = new HashSet<int>();
            foreach (var a in scenario.group.Agents)
            {
                last[a] = a.Position;
                initialAnimation[a] = a.AnimationTime;
                if (!a.AnimationReady)
                    current.failure = "Idle 재생 그래프 미준비";
                bins.Add(Mathf.FloorToInt(a.StartPhase * scenario.group.Agents.Count));
            }

            current.phaseBins = bins.Count;
            if (testCamera)
            {
                testCamera.transform.position = scenario.gate.position + new Vector3(18, 14, -23);
                testCamera.transform.LookAt(scenario.gate.position);
            }
        }

        void FixedUpdate()
        {
            if (index < 0 || Completed)
                return;
            var s = scenarios[index];
            foreach (var a in s.group.Agents)
            {
                Vector3 now = a.Position;
                if (a.AnimationReady && initialAnimation.TryGetValue(a, out double initial) && Math.Abs(a.AnimationTime - initial) > .01)
                    animated.Add(a);
                if (last.TryGetValue(a, out var before))
                {
                    Vector3 p0 = s.gate.InverseTransformPoint(before), p1 = s.gate.InverseTransformPoint(now);
                    if (p0.z < 0 && p1.z >= 0)
                    {
                        crossed.Add(a);
                        float t = -p0.z / (p1.z - p0.z);
                        Vector3 hit = Vector3.Lerp(p0, p1, t);
                        if (s.holeRadius > 0 && new Vector2(hit.x, hit.y).magnitude + a.Radius <= s.holeRadius)
                            holes.Add(a);
                    }
                }

                last[a] = now;
                if (s.group.Mode == BoidMovementMode.Path)
                    current.maxPathOffset = Mathf.Max(current.maxPathOffset, Vector3.Distance(now, s.group.Path.Point(a.Progress)));
            }

            elapsed += Time.fixedDeltaTime;
            if (elapsed >= duration)
                Next();
        }
    }
}
#endif
