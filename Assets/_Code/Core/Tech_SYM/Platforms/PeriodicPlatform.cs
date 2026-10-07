using System;
using System.Collections.Generic;
using DG.Tweening;
using Cooked.Session;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>개별 위상의 5/2/3초 주기와 실제 몸체 생성 침범을 소유한다.</summary>
    [DisallowMultipleComponent]
    public sealed class PeriodicPlatform : MonoBehaviour
    {
        public enum CyclePhase { Visible, Hidden, Warning }
        [Tooltip("물리 자식 또는 기존 같은 객체의 비 Trigger 지지 BoxCollider입니다.")]
        [SerializeField] private BoxCollider supportCollider;
        [Tooltip("밟을 수 있는 Solid 시간(초)입니다. 이번 명세는 5초로 고정합니다.")]
        [SerializeField] private float lifetime = 5f;
        [Tooltip("외형과 충돌이 없는 Absent 시간(초)입니다. 이번 명세는 2초로 고정합니다.")]
        [SerializeField] private float hiddenSeconds = 2f;
        [Tooltip("형성 중 비충돌 Forming 시간(초)입니다. 이번 명세는 3초로 고정합니다.")]
        [SerializeField] private float warningSeconds = 3f;
        [Tooltip("이 개체의 최초 주기 위치(초)입니다. 0은 Solid 시작이며 10초 주기로 환산됩니다. 재시도에도 같은 개별 위상을 적용합니다.")]
        [SerializeField, Min(0f)] private float initialPhaseSeconds;
        [Tooltip("Forming 3초 동안 깜빡이는 횟수입니다. 1 이상입니다.")]
        [SerializeField, Min(1)] private int blinkCount = 8;
        [Tooltip("깜빡임 가속도입니다. 1.01~4 범위로 후반에 빠르게 깜빡입니다.")]
        [SerializeField, Range(1.01f, 4f)] private float blinkAcceleration = 2f;
        private BoxCollider support;
        private Renderer[] renderers;
        private bool[] defaults;
        private bool colliderDefault;
        private double elapsed;
        private readonly HashSet<Rigidbody> examined = new HashSet<Rigidbody>();
        private readonly List<Tween> expressions = new List<Tween>();
        private PlayerBridgeService bridge;
        private Func<Transform, Transform> resolveVisual;
        public CyclePhase Phase { get; private set; }
        public void BindMaterialization(PlayerBridgeService player, Func<Transform, Transform> visualResolver)
        { CancelExpressions(); bridge = player; resolveVisual = visualResolver; }
        private void Awake()
        {
            OnValidate();
            support = supportCollider != null ? supportCollider : GetComponent<BoxCollider>();
            if (support == null || support.isTrigger)
            { Debug.LogError("주기 발판의 비 Trigger 지지 Collider를 연결하세요.", this); enabled = false; return; }
            colliderDefault = support.enabled;
            renderers = GetComponentsInChildren<Renderer>(true);
            defaults = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) defaults[i] = renderers[i].enabled;
            elapsed = initialPhaseSeconds % 10f;
        }
        private void OnEnable() { if (support != null) ApplyPhase(true); }
        private void FixedUpdate()
        {
            if (support == null || Time.timeScale <= 0f) return;
            elapsed = (elapsed + Time.fixedDeltaTime) % 10.0;
            // Float step accumulation is rounded only at the declared phase boundaries.
            if (Math.Abs(elapsed - 5.0) < 0.000001) elapsed = 5.0;
            if (Math.Abs(elapsed - 7.0) < 0.000001) elapsed = 7.0;
            if (10.0 - elapsed < 0.000001) elapsed = 0.0;
            ApplyPhase(false);
        }
        private void ApplyPhase(bool enabling)
        {
            var previous = Phase;
            Phase = elapsed < 5.0 ? CyclePhase.Visible : elapsed < 7.0 ? CyclePhase.Hidden : CyclePhase.Warning;
            if (Phase == CyclePhase.Visible && (enabling || previous != CyclePhase.Visible) && colliderDefault)
                ResolveMaterialization();
            support.enabled = Phase == CyclePhase.Visible && colliderDefault;
            bool visible = Phase == CyclePhase.Visible || (Phase == CyclePhase.Warning
                && Mathf.FloorToInt(Mathf.Pow((float)(elapsed - 7.0) / 3f, blinkAcceleration) * blinkCount * 2f) % 2 == 1);
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = visible && defaults[i];
        }
        private void ResolveMaterialization()
        {
            examined.Clear();
            Vector3 scale = support.transform.lossyScale;
            Vector3 half = Vector3.Scale(support.size * 0.5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            foreach (var other in Physics.OverlapBox(support.transform.TransformPoint(support.center), half,
                support.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                var body = other.attachedRigidbody;
                if (body == null || other == support || !other.enabled || !other.gameObject.activeInHierarchy) continue;
                var player = body.GetComponent<PlayerFacade>();
                var shell = body.GetComponent<PlayerDebris>();
                if (player == null && shell == null) continue;
                // OverlapBox is candidate discovery; the actual two Collider shapes must overlap.
                if (!Physics.ComputePenetration(support, support.transform.position, support.transform.rotation,
                    other, other.transform.position, other.transform.rotation, out var separationDirection, out float intrusion)) continue;
                float height = other.bounds.size.y;
                // ComputePenetration moves support; the body's separation is the opposite direction.
                // An upward MTD alone is not a landing: deep side/under overlaps may also resolve upward.
                bool normalTop = Vector3.Dot(-separationDirection, support.transform.up) > 0.5f
                    && intrusion <= 0.001f;
                if (normalTop || height <= 0f || intrusion < height * 0.25f) continue; // shallow intrusion lands through normal physics
                if (!examined.Add(body)) continue;
                PlayerFacade victim = player != null ? player : shell.Owner != null ? shell.Owner.GetComponent<PlayerFacade>() : null;
                if (victim == null || bridge == null || bridge.Actor != victim)
                { Debug.LogError("주기 생성 사망에는 활성 Actor의 PlayerBridge 연결이 필요합니다.", this); continue; }
                Transform visual = resolveVisual?.Invoke(body.transform);
                if (visual != null && visual != body.transform && visual.IsChildOf(body.transform)
                    && visual.GetComponentsInChildren<Collider>(true).Length == 0)
                {
                    float seconds = PlayerBridgeService.GetKnockbackControlLockMilliseconds(victim) / 1000f;
                    // Missing clip keeps the existing explicit 0ms fallback; do not invent expression timing.
                    expressions.Add(visual.DOMove(visual.position + Vector3.up * intrusion, seconds)
                        .SetEase(Ease.OutCubic).SetUpdate(UpdateType.Fixed, false));
                }
                else Debug.LogWarning("주기 발판의 Collider 없는 Actor/껍질 Visual 표현 연결 대기입니다.", this);
                bridge.ForceDeath(Vector3.zero);
            }
        }
        private void CancelExpressions()
        { foreach (var tween in expressions) tween?.Kill(false); expressions.Clear(); examined.Clear(); }
        public void ResetForRetry()
        {
            CancelExpressions(); elapsed = initialPhaseSeconds % 10f;
            if (support != null) ApplyPhase(false);
        }
        private void OnDisable()
        {
            CancelExpressions(); bridge = null; resolveVisual = null;
            if (support != null) support.enabled = false;
        }
        private void OnValidate()
        {
            lifetime = 5f; hiddenSeconds = 2f; warningSeconds = 3f;
            initialPhaseSeconds = float.IsFinite(initialPhaseSeconds) ? Mathf.Max(0f, initialPhaseSeconds) % 10f : 0f;
            blinkCount = Mathf.Clamp(blinkCount, 1, 100);
            blinkAcceleration = float.IsFinite(blinkAcceleration) ? Mathf.Clamp(blinkAcceleration, 1.01f, 4f) : 2f;
        }
    }
}
