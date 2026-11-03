using System;
using Cooked.Authoring;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    /// <summary>Owns only the visual Idle phase, independent of patrol speed and gameplay state.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyActor)), DefaultExecutionOrder(100)]
    public sealed class EnemyIdleAnimation : MonoBehaviour
    {
        [SerializeField, InspectorLabel("외형 애니메이터"), Tooltip("Idle 클립의 본 계층을 가진 외형 루트 Animator를 연결하세요. 이동 몸체와 고정 앵커는 변경하지 않습니다.")]
        private Animator visualAnimator;
        [SerializeField, InspectorLabel("시작 위치 직접 지정"), Tooltip("끄면 개체마다 0~1 균등 난수를 한 번 뽑습니다. 켜면 아래 시작 위치를 사용합니다. 재시도에는 같은 값을 유지합니다.")]
        private bool useManualPhase;
        [SerializeField, Range(0f,1f), InspectorLabel("Idle 시작 위치"), Tooltip("클립의 정규화 시작 위치입니다. 0은 처음, 0.5는 중간이며 반복 클립의 1은 0과 같습니다. 직접 지정 Flag를 켜면 적용됩니다.")]
        private float manualPhase;
        private EnemyActor owner;
        private bool sampled, pending, reported;
        private float randomPhase;
        public float StartPhase
        {
            get
            {
                if (useManualPhase)
                {
                    if (!float.IsFinite(manualPhase) || manualPhase < 0 || manualPhase > 1)
                        throw new InvalidOperationException("Idle 시작 위치는 0~1의 유한값이어야 합니다.");
                    return Mathf.Repeat(manualPhase,1f);
                }
                if (!sampled) { randomPhase = Mathf.Repeat(UnityEngine.Random.value,1f); sampled = true; }
                return randomPhase;
            }
        }
        private void OnEnable()
        {
            owner = GetComponent<EnemyActor>();
            owner.Prepared += OnPrepared;
            if (owner.IsPrepared) OnPrepared();
        }
        private void OnDisable()
        {
            if (owner != null) owner.Prepared -= OnPrepared;
            pending = false;
        }
        private void OnPrepared() { pending = true; ApplyPending(); }
        private void LateUpdate() { if (pending) ApplyPending(); }
        private void OnValidate() { if (Application.isPlaying) pending = true; }
        private void ApplyPending()
        {
            if (!pending || owner == null || !owner.IsPrepared || !isActiveAndEnabled) return;
            pending = false;
            try { ApplyStartPhase(); reported = false; }
            catch (InvalidOperationException error)
            { if (!reported) Debug.LogError($"적 '{name}' Idle 시작 위치 적용 실패: {error.Message}", this); reported = true; }
        }
        public void ApplyStartPhase()
        {
            if (visualAnimator == null || !visualAnimator.transform.IsChildOf(transform) || visualAnimator.transform == transform ||
                !visualAnimator.isActiveAndEnabled || visualAnimator.runtimeAnimatorController == null)
                throw new InvalidOperationException("활성화된 적 외형 자식 Animator와 Controller가 필요합니다.");
            float phase = StartPhase;
            var culling = visualAnimator.cullingMode;
            try
            {
                visualAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                visualAnimator.Rebind();
                visualAnimator.Update(0f);
                if (visualAnimator.layerCount == 0) throw new InvalidOperationException("Idle 기본 레이어가 없습니다.");
                var state = visualAnimator.GetCurrentAnimatorStateInfo(0);
                if (!state.loop) throw new InvalidOperationException("기본 Idle 상태에는 반복 클립을 연결하세요.");
                visualAnimator.Play(state.fullPathHash,0,phase);
                visualAnimator.Update(0f);
            }
            finally { visualAnimator.cullingMode = culling; }
        }
    }
}
