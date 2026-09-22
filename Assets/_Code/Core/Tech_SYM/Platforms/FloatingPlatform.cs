using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>플레이어 착지 시 눌림과 복귀를 순서대로 실행한다. 부유와 독립적으로 작동한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(KinematicPlatformMotion))]
    public sealed class FloatingPlatform : MonoBehaviour
    {
        [Header("Action")]
        [Tooltip("착지할 때 아래로 눌리는 거리(m).")]
        [SerializeField, Min(0f)] private float landingDepth = 0.12f;
        [Tooltip("아래로 눌리는 시간(초).")]
        [FormerlySerializedAs("sinkSeconds")]
        [SerializeField, Min(0.02f)] private float actionSeconds = 0.25f;
        [Tooltip("아래로 눌리는 곡선.")]
        [SerializeField] private Ease actionEase = Ease.OutQuad;
        [Header("Reaction")]
        [Tooltip("부유 기준 높이보다 위로 올라갈 목표 거리(m). Back/Elastic 곡선은 이 높이를 더 넘을 수 있습니다.")]
        [SerializeField, Min(0f)] private float overshootHeight = 0.12f;
        [Tooltip("눌린 위치에서 위쪽 목표 높이까지 올라가는 시간(초).")]
        [FormerlySerializedAs("bounceSeconds")]
        [SerializeField, Min(0.02f)] private float reactionSeconds = 1.2f;
        [Tooltip("위쪽 목표 높이로 올라가는 곡선.")]
        [FormerlySerializedAs("ease")]
        [SerializeField] private Ease reactionEase = Ease.OutBack;
        [Header("Settle")]
        [Tooltip("위쪽 목표 높이에서 부유 기준 높이로 정착하는 시간(초).")]
        [SerializeField, Min(0.02f)] private float settleSeconds = 0.35f;
        [Tooltip("반동 이후 기준 높이로 정착하는 곡선.")]
        [SerializeField] private Ease settleEase = Ease.OutBounce;
        [Header("Landing")]
        [Tooltip("다시 착지할 수 있도록 판정을 풀어주는 윗면과의 간격(m).")]
        [SerializeField, Min(0.01f)] private float rearmGap = 0.2f;

        private KinematicPlatformMotion motion;
        private BoxCollider surface;
        private float landingOffset;
        private Sequence landingTween;
        private readonly Dictionary<Rigidbody, Collider> landed = new Dictionary<Rigidbody, Collider>();
        private readonly List<Rigidbody> departed = new List<Rigidbody>();
        public bool IsReacting => landingTween != null && landingTween.IsActive();

        private void Awake()
        {
            OnValidate();
            motion = GetComponent<KinematicPlatformMotion>();
            surface = GetComponent<BoxCollider>();
        }

        private void OnEnable()
        {
            if (motion == null || !motion.RegisterEffect(this)) { enabled = false; return; }
            landingOffset = 0f;
        }

        private void FixedUpdate()
        {
            if (motion == null || !motion.isActiveAndEnabled
                || surface == null || !surface.enabled) return;
            ReleaseDepartedPlayers();
            if (landingTween != null && landingTween.IsActive())
                landingTween.ManualUpdate(Time.fixedDeltaTime, Time.fixedDeltaTime);
            motion.SetEffectOffset(this, Vector3.up * landingOffset);
        }

        /// <summary>플레이어가 윗면에 새로 착지했을 때만 실행한다.</summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (!isActiveAndEnabled || motion == null || !motion.isActiveAndEnabled
                || surface == null || !surface.enabled || collision == null) return;
            Rigidbody actor = collision.rigidbody;
            Collider other = collision.collider;
            if (actor == null || other == null || other.isTrigger
                || !actor.TryGetComponent<PlayerController>(out var player) || player == null
                || landed.ContainsKey(actor)) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint point = collision.GetContact(i);
                if (point.thisCollider != surface || Vector3.Dot(-point.normal, Vector3.up) < 0.5f) continue;
                landed.Add(actor, other);
                if (!IsReacting && landingDepth > 0f) BeginLanding();
                return;
            }
        }

        private void BeginLanding()
        {
            landingTween = DOTween.Sequence().SetTarget(this).SetUpdate(UpdateType.Manual)
                .SetAutoKill(true).SetRecyclable(false);
            landingTween.Append(CreateActionTween());
            landingTween.Append(CreateReactionTween());
            landingTween.Append(CreateSettleTween());
            landingTween.OnComplete(() => landingOffset = 0f);
            landingTween.OnKill(() => landingTween = null);
            landingTween.Play();
        }

        /// <summary>작용: 착지 지점에서 아래로 눌린다.</summary>
        private Tween CreateActionTween() => DOTween.To(() => landingOffset, value => landingOffset = value,
            -landingDepth, actionSeconds).SetEase(actionEase);

        /// <summary>반작용: 지정한 위쪽 높이까지 올라간다.</summary>
        private Tween CreateReactionTween() => DOTween.To(() => landingOffset, value => landingOffset = value,
            overshootHeight, reactionSeconds).SetEase(reactionEase);

        /// <summary>반동을 마치고 부유 기준 높이로 정착한다.</summary>
        private Tween CreateSettleTween() => DOTween.To(() => landingOffset, value => landingOffset = value,
            0f, settleSeconds).SetEase(settleEase);

        /// <summary>반동의 짧은 접촉 해제를 제외하고, 충분히 떨어졌을 때 다시 허용한다.</summary>
        private void ReleaseDepartedPlayers()
        {
            departed.Clear();
            Bounds top = surface.bounds;
            foreach (var entry in landed)
            {
                Collider other = entry.Value;
                if (entry.Key == null || other == null || !other.enabled || !other.gameObject.activeInHierarchy)
                { departed.Add(entry.Key); continue; }
                Bounds actor = other.bounds;
                if (actor.min.y > top.max.y + rearmGap || actor.max.y < top.min.y
                    || actor.max.x < top.min.x || actor.min.x > top.max.x
                    || actor.max.z < top.min.z || actor.min.z > top.max.z)
                    departed.Add(entry.Key);
            }
            foreach (Rigidbody actor in departed) landed.Remove(actor);
        }

        private void OnDisable()
        {
            Sequence owned = landingTween;
            landingTween = null;
            if (owned != null && owned.IsActive()) owned.Kill(false);
            landingOffset = 0f;
            landed.Clear();
            departed.Clear();
            if (motion != null) motion.ReleaseEffect(this);
        }

        private void OnValidate()
        {
            landingDepth = Valid(landingDepth, 0f, 0.12f);
            actionSeconds = Valid(actionSeconds, 0.02f, 0.25f);
            reactionSeconds = Valid(reactionSeconds, 0.02f, 1.2f);
            overshootHeight = Valid(overshootHeight, 0f, 0.12f);
            settleSeconds = Valid(settleSeconds, 0.02f, 0.35f);
            rearmGap = Valid(rearmGap, 0.01f, 0.2f);
        }

        private static float Valid(float value, float minimum, float fallback) =>
            float.IsFinite(value) ? Mathf.Max(minimum, value) : fallback;
    }
}