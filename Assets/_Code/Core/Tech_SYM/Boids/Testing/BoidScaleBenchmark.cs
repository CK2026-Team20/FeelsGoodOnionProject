#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Unity.Profiling;

namespace Cooked.Boids
{
    [DefaultExecutionOrder(500)]
    public sealed class BoidScaleBenchmark : MonoBehaviour
    {
        [Serializable]
        public sealed class Result
        {
            public string scenario, mode, failure;
            public int count,
                 initialCount,
                 contacts,
                 sweeps,
                 pathExits,
                 volumeExits,
                 failedPlans,
                 returned,
                 largestMinimum,
                 detachedEver,
                 rejoinedEver;
            public double averageFPS, low1FPS, minimumFrameFPS, approachFPS, approachLow1FPS;
            public int samples;
            public bool timedOut;
        }

        [Serializable]
        public sealed class Report
        {
            public string label, cpu, gpu, utc;
            public int ramMB, width, height;
            public Result[] results;
        }

        struct Frame
        {
            public int test, frame, active;
            public double dt, sim;
            public long tick, choose, path, animation, pairs, discovery, tickCalls;
        }

        sealed class Backup
        {
            public BoidGroup group;
            public int count;
            public BoxCollider spawn;
            public Vector3 size;
            public Vector3 position;
            public bool enabled;
        }

        List<Backup> backups = new List<Backup>();
        List<Frame> frames = new List<Frame>(40000);
        List<Result> results = new List<Result>();
        List<double> times = new List<double>(8000), approach = new List<double>(8000);
        BoidTestSession session;
        bool sessionEnabled;
        BoidMovingObstacle[] movers;
        bool[] moverEnabled;
        Camera camera;
        Camera[] otherCameras;
        bool[] otherCameraEnabled;
        readonly Dictionary<Transform, Vector3> nodePositions = new Dictionary<Transform, Vector3>();
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        BoidGroup[] groups;
        int[] counts;
        int index = -1;
        BoidGroup current;
        BoidMovingObstacle mover;
        Result result;
        string label;
        double caseWall, lastWall;
        float caseTime, nextObserve;
        int oldTarget;
        bool oldProfiler;
        ProfilerRecorder[] recorders;
        Vector3[] observed = new Vector3[256];
        int[] parents = new int[256], sizes = new int[256];
        Dictionary<BoidAgent, int> identities = new Dictionary<BoidAgent, int>();
        bool[] detached = new bool[256], rejoined = new bool[256];
        public bool Completed { get; private set; }

        public string Status => Completed ? "완료" : current ? current.name + " / " + result.count : "준비";
        public static BoidScaleBenchmark Begin(string name, int[] population, bool allScenarios = false)
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Play required");
            var go = new GameObject("Temporary_BoidScaleBenchmark");
            var b = go.AddComponent<BoidScaleBenchmark>();
            b.Initialize(name, population, allScenarios);
            return b;
        }

        void Initialize(string name, int[] population, bool allScenarios)
        {
            label = name;
            counts = population;
            session = UnityEngine.Object.FindAnyObjectByType<BoidTestSession>();
            sessionEnabled = session.enabled;
            session.enabled = false;
            var all = UnityEngine.Object.FindObjectsByType<BoidGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var g in all)
            {
                var so = new SerializedObject(g);
                var spawn = (BoxCollider)so.FindProperty("spawn").objectReferenceValue;
                backups.Add(new Backup{group = g,
                     count = g.ExpectedCount,
                     spawn = spawn,
                     size = spawn.size,
                     position = spawn.transform.position,
                     enabled = false});
                g.enabled = false;
            }

            groups = allScenarios ? all.OrderBy(g => g.name.Contains("ApproachingBox") ? 0 : 1).ThenBy(g => g.name).ToArray() : new[]{all.Single(x => x.name == "Path_ApproachingBox"),
                 all.Single(x => x.name == "Volume_ApproachingBox")};
            foreach (var group in groups)
                if (group.Path)
                    foreach (var node in group.Path.GetComponentsInChildren<Transform>(true))
                        nodePositions[node] = node.position;
            movers = UnityEngine.Object.FindObjectsByType<BoidMovingObstacle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            moverEnabled = movers.Select(x => x.enabled).ToArray();
            foreach (var m in movers)
                m.enabled = false;
            camera = GameObject.Find("BoidsTestCamera").GetComponent<Camera>();
            otherCameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,
                 FindObjectsSortMode.None).Where(c => c != camera
                && c.gameObject.scene == camera.gameObject.scene
                && c.cameraType == CameraType.Game).ToArray();
            otherCameraEnabled = otherCameras.Select(c => c.enabled).ToArray();
            foreach (var other in otherCameras)
                other.enabled = false;
            cameraPosition = camera.transform.position;
            cameraRotation = camera.transform.rotation;
            oldTarget = Application.targetFrameRate;
            Application.targetFrameRate = -1;
            oldProfiler = UnityEngine.Profiling.Profiler.enabled;
            UnityEngine.Profiling.Profiler.enabled = true;
            string[] names = {"Boids.Tick",
                 "Boids.Choose",
                 "Boids.PathContains",
                 "Boids.Animation",
                 "Boids.PairDistances",
                 "Boids.Discovery"};
            recorders = new ProfilerRecorder[names.Length];
            for (int i = 0; i < names.Length; i++)
                recorders[i] = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, names[i], 1);
            Next();
        }

        void Next()
        {
            if (current)
            {
                FinishCase();
                current.enabled = false;
                if (mover)
                    mover.enabled = false;
            }

            index++;
            if (index >= counts.Length * groups.Length)
            {
                Complete();
                return;
            }

            current = groups[index % groups.Length];
            int n = counts[index / groups.Length];
            var so = new SerializedObject(current);
            so.FindProperty("count").intValue = n;
            so.ApplyModifiedPropertiesWithoutUndo();
            var sp = (BoxCollider)so.FindProperty("spawn").objectReferenceValue;
            sp.size = new Vector3(10, 10, 10);
            if (current.Path && !current.name.Contains("ApproachingBox"))
            {
                Transform anchor = current.Path.transform.Find("A");
                anchor.position = nodePositions[anchor] + Vector3.back * 30;
                current.Path.Rebuild();
                sp.transform.position = current.transform.position + Vector3.back * 23;
                sp.size = new Vector3(10, 10, 30);
            }

            mover = current.GetComponent<BoidMovingObstacle>();
            if (mover)
            {
                mover.ResetMotion();
                mover.enabled = true;
            }

            Physics.SyncTransforms();
            current.enabled = true;
            result = new Result{scenario = current.name,
                 mode = current.Mode.ToString(),
                 count = n,
                 initialCount = current.Agents.Count,
                 largestMinimum = n};
            if (result.initialCount != n)
                result.failure = "Spawn count mismatch";
            times.Clear();
            approach.Clear();
            identities.Clear();
            Array.Clear(detached, 0, detached.Length);
            Array.Clear(rejoined, 0, rejoined.Length);
            for (int i = 0; i < current.Agents.Count; i++)
                identities[current.Agents[i]] = i;
            camera.transform.position = current.transform.position + new Vector3(18, 14, -23);
            camera.transform.LookAt(current.transform.position);
            caseTime = Time.time;
            caseWall = lastWall = Time.realtimeSinceStartupAsDouble;
            nextObserve = 0;
        }

        void LateUpdate()
        {
            if (Completed || !current)
                return;
            double now = Time.realtimeSinceStartupAsDouble, dt = now - lastWall;
            lastWall = now;
            float elapsed = Time.time - caseTime;
            // Full population only; spawn frames and returned-agent frames cannot inflate FPS.
            if (elapsed > .5f && elapsed <= 6 && current.Agents.Count == result.count && dt > 0)
            {
                times.Add(dt);
                if (elapsed >= 1.5f && elapsed <= 5.5f)
                    approach.Add(dt);
                frames.Add(new Frame{test = index,
                     frame = Time.frameCount,
                     active = current.Agents.Count,
                     dt = dt,
                     sim = elapsed,
                     tick = recorders[0].LastValue,
                     choose = recorders[1].LastValue,
                     path = recorders[2].LastValue,
                     animation = recorders[3].LastValue,
                     pairs = recorders[4].LastValue,
                     discovery = recorders[5].LastValue,
                     tickCalls = recorders[0].Count > 0 ? recorders[0].GetSample(recorders[0].Count - 1).Count : 0});
            }

            if (elapsed >= nextObserve)
            {
                Observe();
                nextObserve = elapsed + .2f;
            }

            if (elapsed >= 18 || now - caseWall > 120 || !string.IsNullOrEmpty(result.failure))
            {
                result.timedOut = elapsed < 18 && now - caseWall > 120;
                Next();
            }
        }

        int Find(int i)
        {
            while (parents[i] != i)
            {
                parents[i] = parents[parents[i]];
                i = parents[i];
            }

            return i;
        }

        void Observe()
        {
            var a = current.Agents;
            int n = a.Count;
            if (n == 0)
                return;
            for (int i = 0; i < n; i++)
            {
                parents[i] = i;
                sizes[i] = 1;
                observed[i] = a[i].Position;
            }

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                    if ((observed[i] - observed[j]).sqrMagnitude <= 9)
                    {
                        int x = Find(i), y = Find(j);
                        if (x != y)
                        {
                            parents[y] = x;
                            sizes[x] += sizes[y];
                        }
                    }

            int largest = 0;
            for (int i = 1; i < n; i++)
                if (sizes[i] > sizes[largest])
                    largest = i;
            result.largestMinimum = Math.Min(result.largestMinimum, sizes[largest]);
            for (int i = 0; i < n; i++)
            {
                int id = identities[a[i]];
                if (Find(i) != largest)
                    detached[id] = true;
                else if (detached[id])
                    rejoined[id] = true;
            }
        }

        static double FPS(List<double> list) => list.Count > 0 ? list.Count / list.Sum() : 0;
        static double Low(List<double> list)
        {
            if (list.Count == 0)
                return 0;
            var sorted = list.OrderByDescending(x => x).Take(Math.Max(1, (int)Math.Ceiling(list.Count * .01))).ToArray();
            return sorted.Length / sorted.Sum();
        }

        void FinishCase()
        {
            result.averageFPS = FPS(times);
            result.low1FPS = Low(times);
            result.minimumFrameFPS = times.Count > 0 ? 1 / times.Max() : 0;
            result.approachFPS = FPS(approach);
            result.approachLow1FPS = Low(approach);
            result.samples = times.Count;
            result.contacts = current.Contacts;
            result.sweeps = current.SweptContacts;
            result.pathExits = current.PathExits;
            result.volumeExits = current.BoundaryExits;
            result.failedPlans = current.FailedPlans;
            result.returned = current.Returned;
            result.detachedEver = detached.Count(x => x);
            result.rejoinedEver = rejoined.Count(x => x);
            results.Add(result);
            Write();
        }

        void Write()
        {
            string dir = "output/Tech_SYM/Boids/2026-11-13/ScaleFPS/" + label;
            Directory.CreateDirectory(dir);
            File.WriteAllText(dir + "/results.json",
                 JsonUtility.ToJson(new Report{label = label,
                 cpu = SystemInfo.processorType,
                 gpu = SystemInfo.graphicsDeviceName,
                 ramMB = SystemInfo.systemMemorySize,
                 width = Screen.width,
                 height = Screen.height,
                 utc = DateTime.UtcNow.ToString("O"),
                 results = results.ToArray()},
                 true));
            var b = new System.Text.StringBuilder("case,frame,active,deltaSeconds,simulationTime,tickNs,chooseNs,pathNs,animationNs,pairsNs,discoveryNs,tickCalls\n");
            foreach (var f in frames)
                b.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                     "{0},{1},{2},{3:R},{4:R},{5},{6},{7},{8},{9},{10},{11}\n",
                     f.test,
                     f.frame,
                     f.active,
                     f.dt,
                     f.sim,
                     f.tick,
                     f.choose,
                     f.path,
                     f.animation,
                     f.pairs,
                     f.discovery,
                     f.tickCalls);
            File.WriteAllText(dir + "/frames.csv", b.ToString());
        }

        void Complete()
        {
            Completed = true;
            Write();
            Restore();
            Debug.Log("Boid FPS suite completed: " + label);
        }

        void Restore()
        {
            if (recorders == null)
                return;
            foreach (var r in recorders)
                r.Dispose();
            recorders = null;
            UnityEngine.Profiling.Profiler.enabled = oldProfiler;
            Application.targetFrameRate = oldTarget;
            foreach (var b in backups)
            {
                b.group.enabled = false;
                var so = new SerializedObject(b.group);
                so.FindProperty("count").intValue = b.count;
                so.ApplyModifiedPropertiesWithoutUndo();
                b.spawn.size = b.size;
                b.spawn.transform.position = b.position;
            }

            foreach (var node in nodePositions)
                if (node.Key)
                    node.Key.position = node.Value;
            if (otherCameras != null)
                for (int i = 0; i < otherCameras.Length; i++)
                    if (otherCameras[i])
                        otherCameras[i].enabled = otherCameraEnabled[i];
            for (int i = 0; i < movers.Length; i++)
                movers[i].enabled = moverEnabled[i];
            if (session)
                session.enabled = sessionEnabled;
            if (camera)
                camera.transform.SetPositionAndRotation(cameraPosition, cameraRotation);
        }

        void OnDestroy()
        {
            Restore();
        }
    }
}
#endif
