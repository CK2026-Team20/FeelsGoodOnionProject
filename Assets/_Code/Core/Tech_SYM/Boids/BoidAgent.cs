using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Cooked.Authoring;

namespace Cooked.Boids
{
    [DisallowMultipleComponent]
    public sealed class BoidAgent : MonoBehaviour
    {
        [SerializeField, InspectorLabel("외형 Animator"), Tooltip("원본 배율을 보존한 Monster Rig의 Animator를 연결하세요.")]
        Animator animator;
        [SerializeField, InspectorLabel("Idle 클립"), Tooltip("보이드가 반복 재생할 원본 Monster Idle 클립입니다. 원본 임포트 설정을 변경하지 않습니다.")]
        AnimationClip idle;
        [SerializeField, InspectorLabel("몸 반경"), Tooltip("회피에 사용하는 원본 메시 외접 구의 반경입니다(m)."), Min(.01f)]
        float radius = .4f;
        internal BoidAvoidanceMemory SteeringMemory;
        PlayableGraph graph;
        BoidAnimationBatch animationBatch;
        AnimationClipPlayable playable;
        TrailRenderer[] trails;
        float elapsed;
        public bool AnimationReady => playable.IsValid() && animator && idle;
        public double AnimationTime => playable.IsValid() ? playable.GetTime() : -1;
        public void Validate()
        {
            if (!animator || !idle || idle.length <= 0 || !float.IsFinite(radius) || radius <= 0)
                throw new System.InvalidOperationException("보이드 Animator·Idle·몸 반경이 유효해야 합니다.");
        }

        public Vector3 Position => transform.position;
        public Vector3 Velocity { get; private set; }

        public float Radius => radius;
        public float StartPhase { get; private set; }

        public float Progress { get; internal set; }

        public int Cursor { get; internal set; }

        public bool Leased { get; private set; }

        public void Configure(Animator a, AnimationClip clip, float bodyRadius)
        {
            animator = a;
            idle = clip;
            radius = bodyRadius;
        }

        public void Acquire(Vector3 position, Vector3 velocity, float phase, BoidAnimationBatch batch = null)
        {
            Validate();
            animationBatch = batch;
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(velocity.normalized));
            Velocity = velocity;
            StartPhase = phase;
            SteeringMemory = new BoidAvoidanceMemory{duration = .08f + phase * .08f};
            Progress = 0;
            Cursor = 0;
            Leased = true;
            gameObject.SetActive(true);
            if (trails == null)
                trails = GetComponentsInChildren<TrailRenderer>(true);
            foreach (var t in trails)
            {
                t.emitting = false;
                t.Clear();
            }

            if (animator && idle)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animationBatch)
                    playable = animationBatch.Acquire(animator, idle, phase);
                else
                {
                    graph = PlayableGraph.Create("BoidIdle");
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    playable = AnimationClipPlayable.Create(graph, idle);
                    playable.SetApplyFootIK(false);
                    var output = AnimationPlayableOutput.Create(graph, "Idle", animator);
                    output.SetSourcePlayable(playable);
                    elapsed = phase * idle.length;
                    playable.SetTime(elapsed);
                    graph.Play();
                    graph.Evaluate(0);
                }
            }

            foreach (var t in trails)
                t.emitting = true;
        }

        public void Move(Vector3 velocity, float dt)
        {
            Velocity = velocity;
            transform.position += velocity * dt;
            if (velocity.sqrMagnitude > .000001f)
                transform.rotation = Quaternion.LookRotation(velocity.normalized);
        }

        void LateUpdate()
        {
            if (animationBatch)
                return;
            using var profile = BoidProfiler.Animation.Auto();
            if (!Leased || !graph.IsValid())
                return;
            elapsed = Mathf.Repeat(elapsed + Time.deltaTime, idle.length);
            playable.SetTime(elapsed);
            graph.Evaluate(0);
        }

        public void Release()
        {
            if (animationBatch)
                animationBatch.Release(playable);
            animationBatch = null;
            if (graph.IsValid())
                graph.Destroy();
            if (trails != null)
                foreach (var t in trails)
                {
                    t.emitting = false;
                    t.Clear();
                }

            Velocity = Vector3.zero;
            Leased = false;
            SteeringMemory = default;
            Progress = 0;
            Cursor = 0;
            gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (animationBatch)
                animationBatch.Release(playable);
            animationBatch = null;
            if (graph.IsValid())
                graph.Destroy();
        }
    }
}
