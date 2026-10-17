using System;
using System.Collections.Generic;
using FeelsGoodOnion.TechSYM.Features;
using UnityEngine;
using Cooked.Authoring;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>활성 HeavyState가 밟으면 내려가는 압력판.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-450)]
    [RequireComponent(typeof(KinematicPlatformMotion))]
    public sealed class PressurePlatePlatform : MonoBehaviour
    {
        [Header("누름")]
        [Tooltip("이동 몸체 부모의 로컬 -Y축 눌림 거리입니다(m, 배율 1 기준). 부모의 회전과 배율을 적용하므로 비균일 배율에서도 캡과 바디의 상대 깊이를 유지합니다.")]
        [SerializeField, InspectorLabel("눌림 깊이"), Min(0.01f)] private float pressDepth = 0.08f;
        [Tooltip("끝까지 누르거나 복귀하는 시간(초). 중간 해제 시 남은 거리만큼 적용합니다.")]
        [SerializeField, InspectorLabel("누름 시간"), Min(0.02f)] private float travelSeconds = 3f;

        private readonly List<Collider> occupants = new List<Collider>();
        private PlatformContacts contacts;
        private KinematicPlatformMotion motion;
        public float Progress { get; private set; }
        public bool Pressed { get; private set; }
        /// <summary>버튼의 전체 이동 시간(초).</summary>
        public float TravelSeconds => float.IsFinite(travelSeconds) ? Mathf.Max(0.02f, travelSeconds) : 0.02f;

        private void Awake()
        {
            OnValidate();
            motion = GetComponent<KinematicPlatformMotion>();
            contacts = motion.Contacts;
        }

        /// <summary>현재 진행에서 누름/해제를 반전하는 실제 실행의 시간 규칙이다.</summary>
        public static float AdvanceProgress(float progress, bool pressed, float seconds, float deltaSeconds)
        {
            if (!float.IsFinite(progress) || progress < 0 || progress > 1 || !float.IsFinite(seconds) || seconds < .02f
                || !float.IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            return (float)Math.Clamp((double)progress + (pressed ? 1 : -1) * (double)deltaSeconds / seconds, 0, 1);
        }

        private void FixedUpdate()
        {
            if (Time.timeScale <= 0f) return;
            if (contacts == null || motion == null || !motion.isActiveAndEnabled)
            { Pressed = false; occupants.Clear(); return; }
            contacts.CopyOccupants(occupants);
            bool pressed = false;
            foreach (Collider other in occupants)
            {
                GameObject actor = PlatformContacts.Actor(other);
                if (actor != null && actor.TryGetComponent<HeavyState>(out var heavy) && heavy.isActiveAndEnabled
                    && (actor.TryGetComponent<PlayerDebris>(out _) ||
                        (actor.TryGetComponent<PlayerFacade>(out var player) && player.isActiveAndEnabled
                        && (!actor.TryGetComponent<PlayerFormController>(out var form) || form.CurrentForm == PlayerForm.Normal))))
                { pressed = true; break; }
            }
            Pressed = pressed;
            Progress = AdvanceProgress(Progress, pressed, TravelSeconds, Time.fixedDeltaTime);
            // 고정 부모의 좌표계에서 깊이를 변환한다. 움직이는 캡 위치를 누적 기준으로 사용하지 않는다.
            Transform frame = motion.PhysicsBody != null ? motion.PhysicsBody.transform.parent : null;
            Vector3 offset = Vector3.down * (pressDepth * Progress);
            motion.MoveTo(motion.RestPosition + (frame != null ? frame.TransformVector(offset) : offset));
        }

        public void ResetForRetry()
        {
            Pressed = false; Progress = 0f;
            motion?.ResetForRetry(); contacts?.ResetForRetry(); occupants.Clear();
        }
        private void OnDisable()
        {
            Pressed = false;
            motion?.CancelMotion();
            occupants.Clear();
        }
        private void OnValidate()
        {
            if (!float.IsFinite(pressDepth) || pressDepth < 0.01f) pressDepth = 0.01f;
            if (!float.IsFinite(travelSeconds) || travelSeconds < 0.02f) travelSeconds = 0.02f;
        }
    }
}
