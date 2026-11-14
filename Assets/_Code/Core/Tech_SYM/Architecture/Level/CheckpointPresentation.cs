using Cooked.Authoring;
using UnityEngine;
namespace Cooked.Level
{
    /// <summary>StageService의 활성화 상태만 표시하며 저장 상태를 따로 소유하지 않는다.</summary>
    [DisallowMultipleComponent]
    public sealed class CheckpointPresentation : MonoBehaviour
    {
        [SerializeField, InspectorLabel("체크포인트"), Tooltip("표시할 같은 기능 객체의 체크포인트 트리거를 연결하세요.")] private StageTrigger point;
        [SerializeField, InspectorLabel("깃발 메시"), Tooltip("숨기거나 색상을 변경할 깃발 MeshRenderer를 연결하세요. Collider는 숨기지 않습니다.")] private MeshRenderer flag;
        [SerializeField, InspectorLabel("흰 깃발"), Tooltip("미활성 깃발에 사용할 독립 머테리얼입니다.")] private Material white;
        [SerializeField, InspectorLabel("빨간 깃발"), Tooltip("활성화된 깃발의 원본 머테리얼을 참조합니다.")] private Material red;
        [SerializeField, InspectorLabel("활성 효과"), Tooltip("이 객체가 소유하는 CheckPoint 파티클 인스턴스를 연결하세요.")] private ParticleSystem effect;
        private StageService owner;
        public int ActivationEffectCount { get; private set; }
        private void OnEnable() { StopEffect(); RefreshOwner(); }
        private void LateUpdate() { RefreshOwner(); }
        private void RefreshOwner()
        {
            var next = point != null ? point.Owner : null;
            if (next == owner) return;
            Unsubscribe(); owner = next;
            if (owner != null) { owner.CheckpointStateChanged += Refresh; owner.CheckpointCaptured += Captured; }
            Refresh();
        }
        private void Refresh()
        {
            if (flag == null || owner == null || point == null) return;
            flag.enabled = owner.IsStartCheckpoint(point);
            flag.sharedMaterial = owner.IsActivated(point) ? red : white;
        }
        private void Captured(StageTrigger captured)
        {
            Refresh();
            if (captured != point || !owner.IsStartCheckpoint(point) || effect == null) return;
            effect.Play(true); ActivationEffectCount++;
        }
        private void StopEffect() { if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        private void Unsubscribe()
        {
            if (owner != null) { owner.CheckpointStateChanged -= Refresh; owner.CheckpointCaptured -= Captured; }
            owner = null;
        }
        private void OnDisable() { Unsubscribe(); StopEffect(); }
        private void OnDestroy() { Unsubscribe(); }
    }
}
