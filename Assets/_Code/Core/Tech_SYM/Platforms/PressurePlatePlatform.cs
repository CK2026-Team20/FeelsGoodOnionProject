using System.Collections.Generic;
using DG.Tweening;
using FeelsGoodOnion.TechSYM.Features;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>활성 HeavyState가 밟으면 내려가는 압력판.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-450)]
    [RequireComponent(typeof(KinematicPlatformMotion), typeof(PlatformContacts))]
    public sealed class PressurePlatePlatform : MonoBehaviour
    {
        [Header("Pressure")]
        [Tooltip("버튼이 내려가는 거리(m).")]
        [SerializeField, Min(0.01f)] private float pressDepth = 0.2f;
        [Tooltip("끝까지 누르거나 복귀하는 시간(초). 중간 해제 시 남은 거리만큼 적용합니다.")]
        [SerializeField, Min(0.02f)] private float travelSeconds = 3f;
        [Header("Connection")]
        [Tooltip("함께 움직이는 발판. 같은 버튼에 연결된 발판이 필요합니다.")]
        [SerializeField] private PressureLinkedPlatform linkedPlatform;

        private readonly List<Collider> occupants = new List<Collider>();
        private PlatformContacts contacts;
        private KinematicPlatformMotion motion;
        private Vector3 origin;
        private Tween transition;
        public float Progress { get; private set; }
        public bool IsPressed { get; private set; }

        private void Awake()
        {
            OnValidate();
            contacts = GetComponent<PlatformContacts>();
            motion = GetComponent<KinematicPlatformMotion>();
            origin = transform.position;
        }

        private void Start()
        {
            if (linkedPlatform == null || linkedPlatform.Plate != this)
            {
                Debug.LogError("압력판에 연동 발판을 연결하세요.", this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (contacts == null || motion == null) return;
            if (linkedPlatform == null)
            {
                Debug.LogError("압력판의 연동 발판이 제거되었습니다.", this);
                enabled = false;
                return;
            }
            contacts.CopyOccupants(occupants);
            bool pressed = false;
            foreach (Collider other in occupants)
            {
                GameObject actor = PlatformContacts.Actor(other);
                if (actor != null && actor.TryGetComponent<HeavyState>(out var heavy) && heavy.isActiveAndEnabled)
                { pressed = true; break; }
            }
            if (pressed != IsPressed)
            {
                IsPressed = pressed;
                StartTransition(pressed ? 1f : 0f);
            }
            if (transition != null && transition.IsActive()) transition.ManualUpdate(Time.fixedDeltaTime, Time.fixedDeltaTime);
            motion.MoveTo(origin + Vector3.down * (pressDepth * Progress));
        }

        /// <summary>현재 위치에서 누르기 또는 복귀 시작.</summary>
        private void StartTransition(float target)
        {
            StopTransition();
            float duration = Mathf.Abs(target - Progress) * travelSeconds;
            if (duration <= 0.00001f) { Progress = target; return; }
            transition = DOTween.To(() => Progress, value => Progress = value, target, duration)
                .SetEase(Ease.Linear).SetUpdate(UpdateType.Manual).SetTarget(this)
                .SetAutoKill(true).SetRecyclable(false)
                .OnKill(() => transition = null);
            transition.Play();
        }

        private void StopTransition()
        {
            Tween owned = transition;
            transition = null;
            if (owned != null && owned.IsActive()) owned.Kill(false);
        }

        private void OnDisable()
        {
            StopTransition();
            IsPressed = false;
            Progress = 0f;
            if (motion != null) motion.MoveTo(origin);
            occupants.Clear();
        }
        private void OnDestroy() => StopTransition();
        private void OnValidate()
        {
            if (!float.IsFinite(pressDepth) || pressDepth < 0.01f) pressDepth = 0.01f;
            if (!float.IsFinite(travelSeconds) || travelSeconds < 0.02f) travelSeconds = 0.02f;
        }
    }
}
