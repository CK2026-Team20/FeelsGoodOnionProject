using Cooked.Authoring;
using UnityEngine;

namespace Cooked.Chase
{
    [CreateAssetMenu(menuName = "Cooked/Chase Definition")]
    public sealed class ChaseDefinition : ScriptableObject
    {
        [Tooltip("추격 무리가 경로를 따라 이동하는 속도입니다(월드 단위/초, 0보다 커야 함). 높이면 플레이어에게 더 빨리 접근합니다.")]
        [SerializeField, InspectorLabel("추격 속도"), Min(.01f)] private float speed = 4f;
        [Tooltip("경로를 따라 잰 플레이어와 무리의 포획 거리입니다(미터, 기본 0.8, 0 이상 8 미만). 이 거리 이하이면 기존 사망 표현과 복귀를 한 번 요청합니다. 점프 높이는 거리에 포함하지 않으며 0은 무리가 플레이어 위치에 도달했을 때입니다.")]
        [SerializeField, InspectorLabel("포획 간격"), Min(0)] private float captureGap = .8f;
        public ChaseSettings CreateSettings() => new ChaseSettings(speed, captureGap);
    }
}
