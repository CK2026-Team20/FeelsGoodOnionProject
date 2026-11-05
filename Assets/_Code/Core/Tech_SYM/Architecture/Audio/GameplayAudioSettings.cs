using System;
using Cooked.Authoring;
using UnityEngine;

namespace Cooked.Audio
{
    [Serializable]
    public sealed class GameplayAudioSettings
    {
        [SerializeField, InspectorLabel("발소리 간격"), Tooltip("이동·기름 효과음을 반복하는 간격입니다(초)."), Min(.05f)] private float footstepSeconds = .5f;
        [SerializeField, InspectorLabel("적 기본음 최소 간격"), Tooltip("적 기본음의 무작위 대기 최소 시간입니다(초)."), Min(.1f)] private float idleMinimum = 10;
        [SerializeField, InspectorLabel("적 기본음 최대 간격"), Tooltip("적 기본음의 무작위 대기 최대 시간입니다(초)."), Min(.1f)] private float idleMaximum = 15;
        [SerializeField, InspectorLabel("최소 청취 거리"), Tooltip("PC와 음원 사이에서 최대 음량을 유지할 거리입니다(m)."), Min(0)] private float nearDistance = 1;
        [SerializeField, InspectorLabel("최대 청취 거리"), Tooltip("PC와 음원 사이에서 음량이 0이 되는 거리입니다(m)."), Min(.1f)] private float farDistance = 20;
        [SerializeField, InspectorLabel("거리 감쇠 지수"), Tooltip("값이 클수록 거리에 따른 음량이 빠르게 줄어듭니다."), Min(.1f)] private float falloff = 1;
        public float FootstepSeconds => footstepSeconds;
        public float IdleMinimum => idleMinimum;
        public float IdleMaximum => idleMaximum;
        public float FarDistance => farDistance;
        public float Attenuation(float distance) => Mathf.Pow(1 - Mathf.InverseLerp(nearDistance, farDistance, distance), falloff);
        public void Validate()
        {
            if (!float.IsFinite(footstepSeconds) || footstepSeconds < .05f || !float.IsFinite(idleMinimum) || idleMinimum <= 0 ||
                !float.IsFinite(idleMaximum) || idleMaximum < idleMinimum || !float.IsFinite(nearDistance) || nearDistance < 0 ||
                !float.IsFinite(farDistance) || farDistance <= nearDistance || !float.IsFinite(falloff) || falloff <= 0)
                throw new InvalidOperationException("게임 효과음 간격과 청취 거리 설정을 확인하세요.");
        }
    }
}
