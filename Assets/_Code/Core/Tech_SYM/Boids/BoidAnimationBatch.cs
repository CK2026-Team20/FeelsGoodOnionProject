using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Cooked.Boids
{
    /// <summary>풀에 속한 Idle 출력들을 하나의 그래프로 평가한다. 원본 클립과 개별 시작 위상은 유지한다.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class BoidAnimationBatch : MonoBehaviour
    {
        sealed class Entry
        {
            public AnimationClipPlayable playable;
            public AnimationPlayableOutput output;
            public float elapsed;
            public float length;
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly Dictionary<AnimationClipPlayable, Entry> leases = new Dictionary<AnimationClipPlayable, Entry>();
        PlayableGraph graph;
        public AnimationClipPlayable Acquire(Animator animator, AnimationClip clip, float phase)
        {
            if (!graph.IsValid())
            {
                graph = PlayableGraph.Create("BoidPoolIdle");
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                graph.Play();
            }

            var entry = new Entry{playable = AnimationClipPlayable.Create(graph,
                 clip),
                 output = AnimationPlayableOutput.Create(graph,
                 "Idle",
                 animator),
                 elapsed = phase * clip.length,
                 length = clip.length};
            entry.playable.SetApplyFootIK(false);
            entry.playable.SetTime(entry.elapsed);
            entry.output.SetSourcePlayable(entry.playable);
            entries.Add(entry);
            leases.Add(entry.playable, entry);
            return entry.playable;
        }

        public void Release(AnimationClipPlayable playable)
        {
            if (!leases.TryGetValue(playable, out var entry))
                return;
            leases.Remove(playable);
            entries.Remove(entry);
            if (graph.IsValid())
            {
                graph.DestroyOutput(entry.output);
                graph.DestroyPlayable(entry.playable);
            }
        }

        void LateUpdate()
        {
            if (entries.Count == 0 || !graph.IsValid())
                return;
            using var profile = BoidProfiler.Animation.Auto();
            float dt = Time.deltaTime;
            foreach (var entry in entries)
            {
                entry.elapsed = Mathf.Repeat(entry.elapsed + dt, entry.length);
                entry.playable.SetTime(entry.elapsed);
            }

            // 개체마다 Evaluate를 호출하지 않고, 독립 Animator 출력들을 한 번에 평가한다.
            graph.Evaluate(0);
        }

        void OnDestroy() => Shutdown();
        public void Shutdown()
        {
            if (graph.IsValid())
                graph.Destroy();
            entries.Clear();
            leases.Clear();
        }
    }
}
