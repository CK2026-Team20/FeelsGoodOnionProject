using System.Collections.Generic;
using Cooked.Authoring;
using UnityEngine;
namespace Cooked.Session
{
    /// <summary>플레이어 상태와 입력을 기름 Splash에 표시한다. 원본 VFX는 변경하지 않는다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(260)]
    public sealed class OilSplashPresenter : MonoBehaviour
    {
        [SerializeField, InspectorLabel("플레이어"), Tooltip("이 몸체의 PlayerFacade를 연결하세요.")] private PlayerFacade actor;
        [SerializeField, InspectorLabel("방향 입력"), Tooltip("이 몸체의 기존 PlayerInputReader를 연결하세요.")] private PlayerInputReader input;
        [SerializeField, InspectorLabel("미끄러짐 상태"), Tooltip("접촉 이후 남아 있는 미끄러짐 상태를 제공하는 컴포넌트입니다.")] private CharacterSlipperyEffect slippery;
        [SerializeField, InspectorLabel("발 충돌체"), Tooltip("효과의 월드 발 위치를 계산할 CapsuleCollider입니다.")] private CapsuleCollider shape;
        [SerializeField, InspectorLabel("자체 이동"), Tooltip("실제 Rigidbody 속력과 제어 차단 상태를 제공하는 같은 몸체의 CharacterMovement입니다.")] private CharacterMovement movement;
        [SerializeField, InspectorLabel("기름 효과 원본"), Tooltip("VFX_Oil_Splash 원본 프리팹을 참조합니다. 재생 인스턴스만 변경합니다.")] private GameObject effectPrefab;
        [SerializeField, Min(.01f), InspectorLabel("재활성화 간격"), Tooltip("미끄러짐 중 실제 수평 이동 시 효과를 다시 실행할 최소 시간입니다(초). 0은 허용하지 않습니다.")] private float reactivationSeconds = .1f;
        private sealed class Burst { public GameObject root; public ParticleSystem[] particles; }
        private readonly List<Burst> pool = new List<Burst>();
        private double nextTime;
        public int BurstCount { get; private set; }
        public double LastBurstTime { get; private set; }
        public float ReactivationSeconds => reactivationSeconds;
        private void LateUpdate()
        {
            if (actor == null || input == null || slippery == null || shape == null || effectPrefab == null ||
                movement == null || movement.IsControlLocked || new Vector2(movement.Velocity.x, movement.Velocity.z).sqrMagnitude <= .0001f || actor.IsDead || actor.IsInputBlocked || !slippery.IsActive || !input.CanReadInput || Time.timeScale <= 0f) return;
            double now = Time.timeAsDouble;
            if (now < nextTime) return;
            nextTime = now + (float.IsFinite(reactivationSeconds) ? Mathf.Max(.01f, reactivationSeconds) : .1f);
            PlayBurst(); LastBurstTime = now;
        }
        private void PlayBurst()
        {
            Burst chosen = null;
            foreach (var burst in pool)
            {
                bool alive = false;
                foreach (var p in burst.particles) if (p != null && p.IsAlive(false)) { alive = true; break; }
                if (!alive) { chosen = burst; break; }
            }
            if (chosen == null)
            {
                var root = Instantiate(effectPrefab);
                root.name = "OilSplash (owned)";
                var particles = root.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var p in particles) { p.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); var m=p.main; m.loop=false; m.playOnAwake=false; m.stopAction=ParticleSystemStopAction.None; }
                // 원본에 포함된 고정 기름 평면은 발밑 Splash로 복제하지 않는다.
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled=false;
                chosen = new Burst { root=root, particles=particles }; pool.Add(chosen);
            }
            chosen.root.transform.position = shape.transform.TransformPoint(shape.center - Vector3.up * shape.height * .5f);
            chosen.root.transform.rotation = Quaternion.identity;
            var primary = chosen.root.GetComponent<ParticleSystem>();
            if (primary != null) primary.Play(true);
            BurstCount++;
        }
        private void OnDisable()
        {
            foreach (var burst in pool) foreach (var p in burst.particles)
                if (p != null) p.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
        private void OnDestroy() { foreach (var burst in pool) if (burst.root != null) Destroy(burst.root); pool.Clear(); }
        private void OnValidate() => reactivationSeconds = float.IsFinite(reactivationSeconds) ? Mathf.Max(.01f,reactivationSeconds) : .1f;
    }
}
