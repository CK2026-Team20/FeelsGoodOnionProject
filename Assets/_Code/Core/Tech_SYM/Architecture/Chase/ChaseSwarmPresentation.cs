using System;
using Cooked.Contracts;
using DG.Tweening;
using UnityEngine;

namespace Cooked.Chase
{
    public enum ChaseVisualPhase { Hidden, Appearing, Looping, Exiting }
    public enum ChaseVisualOutcome { None, Completed, Cancelled }

    /// <summary>Owns visual tweens only. Root position and capture still come from the distance model.</summary>
    [DisallowMultipleComponent]
    public sealed class ChaseSwarmPresentation : MonoBehaviour
    {
        [Tooltip("등장·퇴장 크기 변화와 상하 흔들림을 적용할 외형 자식입니다. 논리·접촉 트리거 루트 대신 외형만 연결하세요.")]
        [SerializeField] private GameObject visual = null;
        [Tooltip("등장 크기 변화 시간입니다(초, 최소 0.01). 높이면 더 천천히 나타납니다. 월드 정지 중에는 멈춥니다.")]
        [SerializeField, Min(.01f)] private float appearSeconds = .2f;
        [Tooltip("퇴장 크기 변화 시간입니다(초, 최소 0.01). 높이면 더 천천히 사라집니다. 월드 정지와 별개로 진행하지만 옵션 중에는 멈춥니다.")]
        [SerializeField, Min(.01f)] private float exitSeconds = .18f;
        [Tooltip("상하 흔들림의 편도 시간입니다(초, 최소 0.01). 높이면 더 느리게 흔들리며 왕복은 이 시간의 두 배입니다.")]
        [SerializeField, Min(.01f)] private float bobHalfPeriod = .32f;
        [Tooltip("기본 외형 위치에서 위로 흔들리는 거리입니다(로컬 단위, 0 이상). 0이면 상하 흔들림을 만들지 않습니다.")]
        [SerializeField, Min(0)] private float bobHeight = .06f;
        private ChaseService service;
        private IGameplayControlService control;
        private Tween transitionTween, bobTween;
        private Vector3 restPosition, restScale;
        private bool hasRestPose;
        private int generation;
        public ChaseVisualPhase Phase { get; private set; }
        public ChaseVisualOutcome LastTransitionOutcome { get; private set; }

        public void Bind(ChaseService value, IGameplayControlService gameplayControl)
        {
            Unbind();
            if (visual == null || !ValidDuration(appearSeconds) || !ValidDuration(exitSeconds) ||
                !ValidDuration(bobHalfPeriod) || !ChaseSettings.IsFinite(bobHeight) || bobHeight < 0)
                throw new InvalidOperationException("Invalid Chase visual binding or tween settings.");
            service = value ?? throw new ArgumentNullException(nameof(value));
            control = gameplayControl ?? throw new ArgumentNullException(nameof(gameplayControl));
            restPosition = visual.transform.localPosition;
            restScale = visual.transform.localScale;
            hasRestPose = true;
            LastTransitionOutcome = ChaseVisualOutcome.None;
            service.Changed += Render;
            control.Changed += OnControlChanged;
            Render(service.Snapshot);
        }

        /// <summary>Only the owner driver ticks these handles; never calls global DOTween.ManualUpdate.</summary>
        public void Tick(float scaledDelta, float unscaledDelta)
        {
            if (service == null || control == null) return;
            if (!ChaseSettings.IsFinite(scaledDelta) || scaledDelta < 0 ||
                !ChaseSettings.IsFinite(unscaledDelta) || unscaledDelta < 0)
                throw new ArgumentOutOfRangeException(nameof(scaledDelta));
            bool exiting = Phase == ChaseVisualPhase.Exiting;
            bool paused = exiting ? control.State.PresentationPaused : control.State.WorldPaused;
            SetPaused(transitionTween, paused);
            SetPaused(bobTween, control.State.WorldPaused);
            if (paused) return;
            // Exit is a transition visual: it must finish under GameFlow's world pause.
            // Options' presentation lease still suspends it. Gameplay bob/entrance use scaled time.
            float delta = exiting ? unscaledDelta : scaledDelta;
            if (transitionTween != null && transitionTween.IsActive()) transitionTween.ManualUpdate(delta, delta);
            if (!control.State.WorldPaused && bobTween != null && bobTween.IsActive())
                bobTween.ManualUpdate(scaledDelta, scaledDelta);
        }

        public void Unbind()
        {
            generation++;
            if (transitionTween != null || bobTween != null) LastTransitionOutcome = ChaseVisualOutcome.Cancelled;
            KillOwned(ref transitionTween);
            KillOwned(ref bobTween);
            if (service != null) service.Changed -= Render;
            if (control != null) control.Changed -= OnControlChanged;
            service = null;
            control = null;
            Phase = ChaseVisualPhase.Hidden;
            if (visual != null)
            {
                if (hasRestPose)
                {
                    visual.transform.localPosition = restPosition;
                    visual.transform.localScale = restScale;
                }
                visual.SetActive(false);
            }
        }
        private void Render(ChaseSnapshot state)
        {
            if (visual == null) return;
            if (state.State == ChaseState.Running)
            {
                transform.position = state.SwarmPosition;
                if (Phase == ChaseVisualPhase.Hidden) BeginAppearance();
            }
            else if (state.State == ChaseState.Caught || state.State == ChaseState.Stopped || state.State == ChaseState.Faulted)
            {
                if (state.State == ChaseState.Caught) transform.position = state.SwarmPosition;
                if (Phase == ChaseVisualPhase.Appearing || Phase == ChaseVisualPhase.Looping) BeginExit();
            }
            else if (state.State == ChaseState.Disposed) Unbind();
        }

        private void BeginAppearance()
        {
            int version = ++generation;
            Phase = ChaseVisualPhase.Appearing;
            LastTransitionOutcome = ChaseVisualOutcome.None;
            visual.transform.localPosition = restPosition;
            visual.transform.localScale = restScale * .85f;
            visual.SetActive(true);
            transitionTween = visual.transform.DOScale(restScale, appearSeconds)
                .SetEase(Ease.OutQuad).SetUpdate(UpdateType.Manual, false).SetAutoKill(true).SetRecyclable(false)
                .OnComplete(() =>
                {
                    if (version != generation || service == null) return;
                    transitionTween = null;
                    Phase = ChaseVisualPhase.Looping;
                    LastTransitionOutcome = ChaseVisualOutcome.Completed;
                });
            transitionTween.ForceInit();
            if (bobHeight > 0)
                bobTween = visual.transform.DOLocalMoveY(restPosition.y + bobHeight, bobHalfPeriod)
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo)
                    .SetUpdate(UpdateType.Manual, false).SetRecyclable(false);
            bobTween?.ForceInit();
            OnControlChanged(control.State);
        }

        private void BeginExit()
        {
            int version = ++generation;
            KillOwned(ref transitionTween);
            KillOwned(ref bobTween);
            Phase = ChaseVisualPhase.Exiting;
            LastTransitionOutcome = ChaseVisualOutcome.None;
            transitionTween = visual.transform.DOScale(restScale * .85f, exitSeconds)
                .SetEase(Ease.InQuad).SetUpdate(UpdateType.Manual, true).SetAutoKill(true).SetRecyclable(false)
                .OnComplete(() =>
                {
                    if (version != generation || service == null) return;
                    transitionTween = null;
                    Phase = ChaseVisualPhase.Hidden;
                    LastTransitionOutcome = ChaseVisualOutcome.Completed;
                    visual.SetActive(false);
                });
            transitionTween.ForceInit();
            OnControlChanged(control.State);
        }

        private void OnControlChanged(ControlState state)
        {
            SetPaused(transitionTween, Phase == ChaseVisualPhase.Exiting ? state.PresentationPaused : state.WorldPaused);
            SetPaused(bobTween, state.WorldPaused);
        }
        private static bool ValidDuration(float value) => ChaseSettings.IsFinite(value) && value > 0;
        private static void SetPaused(Tween tween, bool paused)
        {
            if (tween == null || !tween.IsActive()) return;
            if (paused) tween.Pause(); else tween.Play();
        }
        private static void KillOwned(ref Tween tween)
        {
            Tween owned = tween;
            tween = null;
            if (owned != null && owned.IsActive()) owned.Kill(false);
        }
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
    }
}
