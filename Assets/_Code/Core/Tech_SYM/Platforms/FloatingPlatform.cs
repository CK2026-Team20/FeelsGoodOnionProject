using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Cooked.Authoring;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>플레이어 착지 시 눌림과 복귀를 순서대로 실행한다. 부유와 독립적으로 작동한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(KinematicPlatformMotion))]
    public sealed class FloatingPlatform : MonoBehaviour
    {
        [Header("눌림")]
        [Tooltip("착지할 때 아래로 눌리는 거리(m).")]
        [SerializeField, InspectorLabel("눌림 깊이"), Min(0f)] private float landingDepth = 0.12f;
        [Tooltip("아래로 눌리는 시간(초).")]
        [SerializeField, InspectorLabel("눌림 시간"), Min(0.02f)] private float actionSeconds = 0.25f;
        [Tooltip("아래로 눌리는 곡선.")]
        [SerializeField, InspectorLabel("눌림 곡선")] private MotionEase actionEase = MotionEase.OutQuad;
        [Header("반동")]
        [Tooltip("부유 기준 높이보다 위로 올라갈 목표 거리(m). Back/Elastic 곡선은 이 높이를 더 넘을 수 있습니다.")]
        [SerializeField, InspectorLabel("반동 높이"), Min(0f)] private float overshootHeight = 0.12f;
        [Tooltip("눌린 위치에서 위쪽 목표 높이까지 올라가는 시간(초).")]
        [SerializeField, InspectorLabel("반동 시간"), Min(0.02f)] private float reactionSeconds = 1.2f;
        [Tooltip("위쪽 목표 높이로 올라가는 곡선.")]
        [SerializeField, InspectorLabel("반동 곡선")] private MotionEase reactionEase = MotionEase.OutBack;
        [Header("복귀")]
        [Tooltip("위쪽 목표 높이에서 부유 기준 높이로 정착하는 시간(초).")]
        [SerializeField, InspectorLabel("복귀 시간"), Min(0.02f)] private float settleSeconds = 0.35f;
        [Tooltip("반동 이후 기준 높이로 정착하는 곡선.")]
        [SerializeField, InspectorLabel("복귀 곡선")] private MotionEase settleEase = MotionEase.OutBounce;
        [Header("착지")]
        [Tooltip("다시 착지할 수 있도록 판정을 풀어주는 윗면과의 간격(m).")]
        [SerializeField, InspectorLabel("재착지 간격"), Min(0.01f)] private float rearmGap = 0.2f;

        // DOTween의 기존 저장 번호를 그대로 사용하며 이 SYM 작성 필드의 표시만 한국어로 바꾼다.
        public enum MotionEase
        {
            [InspectorName("기본")] Unset = (int)Ease.Unset,
            [InspectorName("일정")] Linear = (int)Ease.Linear,
            [InspectorName("사인 가속")] InSine = (int)Ease.InSine,
            [InspectorName("사인 감속")] OutSine = (int)Ease.OutSine,
            [InspectorName("사인 가감속")] InOutSine = (int)Ease.InOutSine,
            [InspectorName("제곱 가속")] InQuad = (int)Ease.InQuad,
            [InspectorName("제곱 감속")] OutQuad = (int)Ease.OutQuad,
            [InspectorName("제곱 가감속")] InOutQuad = (int)Ease.InOutQuad,
            [InspectorName("세제곱 가속")] InCubic = (int)Ease.InCubic,
            [InspectorName("세제곱 감속")] OutCubic = (int)Ease.OutCubic,
            [InspectorName("세제곱 가감속")] InOutCubic = (int)Ease.InOutCubic,
            [InspectorName("네제곱 가속")] InQuart = (int)Ease.InQuart,
            [InspectorName("네제곱 감속")] OutQuart = (int)Ease.OutQuart,
            [InspectorName("네제곱 가감속")] InOutQuart = (int)Ease.InOutQuart,
            [InspectorName("다섯제곱 가속")] InQuint = (int)Ease.InQuint,
            [InspectorName("다섯제곱 감속")] OutQuint = (int)Ease.OutQuint,
            [InspectorName("다섯제곱 가감속")] InOutQuint = (int)Ease.InOutQuint,
            [InspectorName("지수 가속")] InExpo = (int)Ease.InExpo,
            [InspectorName("지수 감속")] OutExpo = (int)Ease.OutExpo,
            [InspectorName("지수 가감속")] InOutExpo = (int)Ease.InOutExpo,
            [InspectorName("원형 가속")] InCirc = (int)Ease.InCirc,
            [InspectorName("원형 감속")] OutCirc = (int)Ease.OutCirc,
            [InspectorName("원형 가감속")] InOutCirc = (int)Ease.InOutCirc,
            [InspectorName("탄성 가속")] InElastic = (int)Ease.InElastic,
            [InspectorName("탄성 감속")] OutElastic = (int)Ease.OutElastic,
            [InspectorName("탄성 가감속")] InOutElastic = (int)Ease.InOutElastic,
            [InspectorName("반동 가속")] InBack = (int)Ease.InBack,
            [InspectorName("반동 감속")] OutBack = (int)Ease.OutBack,
            [InspectorName("반동 가감속")] InOutBack = (int)Ease.InOutBack,
            [InspectorName("바운스 가속")] InBounce = (int)Ease.InBounce,
            [InspectorName("바운스 감속")] OutBounce = (int)Ease.OutBounce,
            [InspectorName("바운스 가감속")] InOutBounce = (int)Ease.InOutBounce,
            [InspectorName("점멸")] Flash = (int)Ease.Flash,
            [InspectorName("점멸 가속")] InFlash = (int)Ease.InFlash,
            [InspectorName("점멸 감속")] OutFlash = (int)Ease.OutFlash,
            [InspectorName("점멸 가감속")] InOutFlash = (int)Ease.InOutFlash
        }

        private KinematicPlatformMotion motion;
        private BoxCollider surface;
        private PlatformContacts contacts;
        private float landingOffset;
        private Sequence landingTween;
        private readonly Dictionary<Rigidbody, Collider> landed = new Dictionary<Rigidbody, Collider>();
        private readonly List<Rigidbody> departed = new List<Rigidbody>();
        public bool IsReacting => landingTween != null && landingTween.IsActive();

        private void Awake()
        {
            OnValidate();
            motion = GetComponent<KinematicPlatformMotion>();
            contacts = motion.Contacts;
            surface = contacts != null ? contacts.Surface : null;
        }

        private void OnEnable()
        {
            if (contacts != null) { contacts.ContactReported -= ReactToContact; contacts.ContactReported += ReactToContact; }
        }

        private void FixedUpdate()
        {
            if (Time.timeScale <= 0f) return;
            if (motion == null || !motion.isActiveAndEnabled
                || surface == null || !surface.enabled) return;
            if (!motion.RegisterEffect(this)) { enabled = false; return; }
            ReleaseDepartedPlayers();
            if (landingTween != null && landingTween.IsActive())
                landingTween.ManualUpdate(Time.fixedDeltaTime, Time.fixedDeltaTime);
            motion.SetEffectOffset(this, Vector3.up * landingOffset);
        }

        /// <summary>PlayerFacade를 가진 객체가 윗면에 새로 착지했을 때만 실행한다.</summary>
        private void ReactToContact(Collision collision)
        {
            if (!isActiveAndEnabled || motion == null || !motion.isActiveAndEnabled
                || surface == null || !surface.enabled || collision == null) return;
            Rigidbody actor = collision.rigidbody;
            Collider other = collision.collider;
            if (actor == null || other == null || other.isTrigger
                || !actor.TryGetComponent<PlayerFacade>(out var player) || !player.isActiveAndEnabled
                || landed.ContainsKey(actor)) return;
            if (contacts.IsTopContact(other))
            {
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
            -landingDepth, actionSeconds).SetEase((Ease)actionEase);

        /// <summary>반작용: 지정한 위쪽 높이까지 올라간다.</summary>
        private Tween CreateReactionTween() => DOTween.To(() => landingOffset, value => landingOffset = value,
            overshootHeight, reactionSeconds).SetEase((Ease)reactionEase);

        /// <summary>반동을 마치고 부유 기준 높이로 정착한다.</summary>
        private Tween CreateSettleTween() => DOTween.To(() => landingOffset, value => landingOffset = value,
            0f, settleSeconds).SetEase((Ease)settleEase);

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

        public void ResetForRetry()
        {
            CancelLanding(); motion?.ResetForRetry(); contacts?.ResetForRetry();
        }
        private void OnDisable()
        {
            if (contacts != null) contacts.ContactReported -= ReactToContact;
            CancelLanding();
        }
        private void CancelLanding()
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
