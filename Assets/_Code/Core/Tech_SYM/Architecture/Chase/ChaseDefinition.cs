using UnityEngine;

namespace Cooked.Chase
{
    [CreateAssetMenu(menuName = "Cooked/Chase Definition")]
    public sealed class ChaseDefinition : ScriptableObject
    {
        [Tooltip("추격 무리가 경로를 따라 이동하는 속도입니다(월드 단위/초, 0보다 커야 함). 높이면 플레이어에게 더 빨리 접근합니다.")]
        [SerializeField, Min(.01f)] private float speed = 4f;
        [Tooltip("추격 시작을 허용할 때 경로 끝에 남겨 두는 거리입니다(월드 단위, 0 이상 8 미만). 높이면 경로 끝에 더 많은 여유가 필요합니다. 현재 포획은 앞쪽 트리거의 실제 접촉으로 판정하며 이 값만으로 포획하지 않습니다.")]
        [SerializeField, Min(0)] private float captureGap = .8f;
        public ChaseSettings CreateSettings() => new ChaseSettings(speed, captureGap);
    }
}
