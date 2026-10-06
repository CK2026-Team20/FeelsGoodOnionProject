using DG.Tweening;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>붕괴 발판의 경고, Animator 재생과 표시를 관리한다.</summary>
    public sealed class PlatformPresentation
    {
        public const int AnimatorLayerIndex = 0;
        public const string AnimatorLayerName = "Base Layer";
        public const string ReadyStateName = "Ready";
        public const string CollapseStateName = "Collapse";
        public const string CompletedStateName = "Completed";
        public const string CollapseTriggerName = "Collapse";
        public const string ReadyStatePath = AnimatorLayerName + "." + ReadyStateName;
        public const string CollapseStatePath = AnimatorLayerName + "." + CollapseStateName;
        public const string CompletedStatePath = AnimatorLayerName + "." + CompletedStateName;

        public enum AnimationProgress { Waiting, Playing, Completed, ClipNotLinked, Failed }

        private const float EntryTimeoutSeconds = 2f;
        private const float StalledPlaybackSeconds = 2f;
        private static readonly int ReadyStateHash = Animator.StringToHash(ReadyStatePath);
        private static readonly int CollapseStateHash = Animator.StringToHash(CollapseStatePath);
        private static readonly int CompletedStateHash = Animator.StringToHash(CompletedStatePath);
        private static readonly int CollapseTriggerHash = Animator.StringToHash(CollapseTriggerName);

        private readonly Transform visual;
        private readonly Animator animator;
        private readonly bool animatorWasEnabled;
        private readonly Transform[] transforms;
        private readonly Vector3[] restPositions;
        private readonly Quaternion[] restRotations;
        private readonly Vector3[] restScales;
        private readonly Renderer[] renderers;
        private readonly bool[] rendererDefaults;
        private Tween ownedTween;
        private bool animatorSuspended;
        private RuntimeAnimatorController requestedController;
        private bool observedCollapse;
        private float entryElapsed;
        private float stalledElapsed;
        private float lastNormalizedTime;

        public bool IsValid => visual != null && visual.gameObject.activeInHierarchy;
        public bool TweenCompleted => ownedTween != null && ownedTween.IsActive() && ownedTween.IsComplete();
        public bool TweenExists => ownedTween != null && ownedTween.IsActive();
        public string AnimationIssue { get; private set; }

        public PlatformPresentation(Transform visual, Animator animator)
        {
            this.visual = visual;
            this.animator = animator;
            animatorWasEnabled = animator != null && animator.enabled;
            transforms = visual.GetComponentsInChildren<Transform>(true);
            restPositions = new Vector3[transforms.Length];
            restRotations = new Quaternion[transforms.Length];
            restScales = new Vector3[transforms.Length];
            for (int index = 0; index < transforms.Length; index++)
            {
                restPositions[index] = transforms[index].localPosition;
                restRotations[index] = transforms[index].localRotation;
                restScales[index] = transforms[index].localScale;
            }
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            rendererDefaults = new bool[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
                rendererDefaults[index] = renderers[index] != null && renderers[index].enabled;
        }

        public void Shake(float seconds, float degrees, float frequency)
        {
            CancelTween();
            ownedTween = visual.DOShakeRotation(seconds, new Vector3(degrees, 0f, degrees),
                Mathf.Clamp(Mathf.RoundToInt(seconds * frequency), 1, 1000), 90f, true)
                .SetEase(Ease.Linear).SetUpdate(UpdateType.Fixed, false)
                .SetAutoKill(false).SetRecyclable(false).SetLink(visual.gameObject, LinkBehaviour.KillOnDisable);
        }

        public void Blink(float seconds, float interval)
        {
            CancelTween();
            float elapsed = 0f;
            ownedTween = DOTween.To(() => elapsed, value =>
            {
                elapsed = value;
                SetVisible(Mathf.FloorToInt(value / interval) % 2 == 0);
            }, seconds, seconds).SetEase(Ease.Linear).SetUpdate(UpdateType.Fixed, false)
                .SetAutoKill(false).SetRecyclable(false).SetLink(visual.gameObject, LinkBehaviour.KillOnDisable);
        }

        public void CancelTween()
        {
            Tween owned = ownedTween;
            ownedTween = null;
            if (owned != null && owned.IsActive()) owned.Kill(false);
        }

        /// <summary>필수 구성 오류와 단순 클립 미연결을 구분할 수 있도록 상태 진입을 요청한다.</summary>
        public bool TryBeginCollapseAnimation()
        {
            AnimationIssue = null;
            observedCollapse = false;
            entryElapsed = stalledElapsed = lastNormalizedTime = 0f;
            if (!ValidateAnimator()) return false;
            requestedController = animator.runtimeAnimatorController;
            // 화면 밖에서도 완료를 관찰해야 하므로 이 표현 객체의 Animator만 계속 갱신한다.
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.ResetTrigger(CollapseTriggerHash);
            animator.SetTrigger(CollapseTriggerHash);
            // 시간은 진행시키지 않고 요청을 평가하여 짧은 실제 클립의 진입도 관찰한다.
            animator.Update(0f);
            return true;
        }

        /// <summary>실제 상태와 클립의 진행만 완료 근거로 사용한다. 시간 제한은 실패 진단용이다.</summary>
        public AnimationProgress ObserveCollapseAnimation(float scaledDeltaSeconds)
        {
            if (!ValidateAnimator()) return AnimationProgress.Failed;
            if (animator.runtimeAnimatorController != requestedController)
                return Fail("재생 중 Animator Controller가 바뀌었습니다.");

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(AnimatorLayerIndex);
            bool transitioning = animator.IsInTransition(AnimatorLayerIndex);
            if (state.fullPathHash == CollapseStateHash)
            {
                AnimationProgress playback = ObserveCollapseState(state,
                    animator.GetCurrentAnimatorClipInfo(AnimatorLayerIndex), scaledDeltaSeconds, true);
                if (playback == AnimationProgress.ClipNotLinked || playback == AnimationProgress.Failed) return playback;
                if (transitioning)
                {
                    AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(AnimatorLayerIndex);
                    if (next.fullPathHash != CompletedStateHash)
                        return Fail("Collapse에서 Completed 이외의 상태로 전환하고 있습니다.");
                    if (state.normalizedTime < 1f)
                        return Fail("실제 클립이 끝나기 전에 Completed 전환이 시작되었습니다. Exit Time을 1로 설정하세요.");
                }
                return playback;
            }
            if (state.fullPathHash == CompletedStateHash)
            {
                if (animator.GetCurrentAnimatorClipInfo(AnimatorLayerIndex).Length != 0)
                    return Fail("Completed 상태의 Motion은 비워 두어야 합니다. 실제 클립은 Collapse에만 연결하세요.");
                if (observedCollapse) return AnimationProgress.Completed;
                return Fail("실제 붕괴 클립의 진입을 관찰하지 못한 채 Completed 상태로 전환되었습니다.");
            }
            if (state.fullPathHash != ReadyStateHash)
                return Fail("요청된 Collapse 상태가 아닌 상태에서 Animator가 실행 중입니다.");
            if (animator.GetCurrentAnimatorClipInfo(AnimatorLayerIndex).Length != 0)
                return Fail("Ready 상태의 Motion은 비워 두어야 합니다. 실제 클립은 Collapse에만 연결하세요.");
            entryElapsed += scaledDeltaSeconds;
            if (entryElapsed >= EntryTimeoutSeconds)
                return Fail("Collapse 요청 이후 붕괴 상태에 진입하지 않았습니다. Trigger와 Ready 전환을 확인하세요.");
            if (transitioning)
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(AnimatorLayerIndex);
                if (next.fullPathHash != CollapseStateHash)
                    return Fail("Ready 상태에서 Collapse 이외의 상태로 전환하고 있습니다.");
                // 짧은 클립이 다음 프레임에 Completed에 도달해도 실제 진입 증거를 잃지 않는다.
                return ObserveCollapseState(next, animator.GetNextAnimatorClipInfo(AnimatorLayerIndex), scaledDeltaSeconds, false);
            }
            if (observedCollapse) return Fail("붕괴 재생이 완료 상태에 도달하지 않고 Ready로 되돌아갔습니다.");
            return AnimationProgress.Waiting;
        }

        private AnimationProgress ObserveCollapseState(AnimatorStateInfo state, AnimatorClipInfo[] clips,
            float scaledDeltaSeconds, bool canComplete)
        {
            if (clips.Length == 0)
            {
                AnimationIssue = "Collapse 상태의 Motion에 실제 붕괴 클립이 연결되지 않았습니다.";
                return AnimationProgress.ClipNotLinked;
            }
            if (state.loop) return Fail("Collapse 상태의 실제 클립이 반복 재생으로 설정되어 있습니다. 1회 재생 클립을 연결하세요.");
            if (!float.IsFinite(state.normalizedTime) || !float.IsFinite(state.length) || state.length <= 0f
                || !float.IsFinite(state.speed) || state.speed <= 0f
                || !float.IsFinite(state.speedMultiplier) || state.speedMultiplier <= 0f)
                return Fail("Collapse 상태의 길이 또는 재생 속도가 올바르지 않아 완료를 확인할 수 없습니다.");
            foreach (AnimatorClipInfo clip in clips)
                if (clip.clip == null || !float.IsFinite(clip.clip.length) || clip.clip.length <= 0f)
                    return Fail("Collapse 상태에 길이가 없는 클립 또는 잘못된 클립 참조가 연결되었습니다.");
            if (!observedCollapse)
            {
                observedCollapse = true;
                lastNormalizedTime = state.normalizedTime;
            }
            if (canComplete && state.normalizedTime >= 1f) return AnimationProgress.Completed;
            if (state.normalizedTime > lastNormalizedTime)
                stalledElapsed = 0f;
            else
                stalledElapsed += scaledDeltaSeconds;
            lastNormalizedTime = state.normalizedTime;
            if (stalledElapsed >= StalledPlaybackSeconds)
                return Fail("월드가 실행 중인데 실제 붕괴 클립의 재생이 진행되지 않습니다.");
            return AnimationProgress.Playing;
        }

        private bool ValidateAnimator()
        {
            if (animator == null) return Invalid("Visual에 연결할 Animator 참조가 없습니다.");
            if (animator.transform != visual) return Invalid("Animator는 Collider가 없는 Visual 자신에 연결해야 합니다.");
            if (animator.runtimeAnimatorController == null) return Invalid("Animator Controller가 연결되지 않았습니다.");
            if (!animator.isActiveAndEnabled || !animator.isInitialized)
                return Invalid("연결된 Animator가 활성화·초기화되어 있지 않습니다.");
            if (animator.layerCount == 0 || animator.GetLayerName(AnimatorLayerIndex) != AnimatorLayerName)
                return Invalid("Animator의 첫 번째 레이어 이름은 Base Layer여야 합니다.");
            if (!animator.HasState(AnimatorLayerIndex, ReadyStateHash)
                || !animator.HasState(AnimatorLayerIndex, CollapseStateHash)
                || !animator.HasState(AnimatorLayerIndex, CompletedStateHash))
                return Invalid("Base Layer에 Ready, Collapse, Completed 상태가 모두 필요합니다.");
            if (!HasCollapseTrigger()) return Invalid("Collapse라는 이름의 Trigger 파라미터가 필요합니다.");
            if (animator.updateMode == AnimatorUpdateMode.UnscaledTime)
                return Invalid("Animator는 월드 정지에 맞춰 멈추는 Normal 또는 Fixed 갱신을 사용해야 합니다.");
            if (animator.applyRootMotion) return Invalid("Animator의 Apply Root Motion을 꺼서 물리 루트 이동을 막아야 합니다.");
            if (!float.IsFinite(animator.speed) || animator.speed <= 0f)
                return Invalid("Animator의 재생 속도는 0보다 큰 유한한 값이어야 합니다.");
            return true;
        }

        private bool HasCollapseTrigger()
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                if (parameter.nameHash == CollapseTriggerHash && parameter.type == AnimatorControllerParameterType.Trigger)
                    return true;
            return false;
        }

        private bool Invalid(string message)
        {
            AnimationIssue = message;
            return false;
        }

        private AnimationProgress Fail(string message)
        {
            AnimationIssue = message;
            return AnimationProgress.Failed;
        }

        /// <summary>후처리 동안 Animator가 렌더러나 배치를 덮어쓰지 않게 자기 표시 Animator만 멈춘다.</summary>
        public void EndCollapseAnimation()
        {
            if (animator == null || animator.transform != visual) return;
            animatorSuspended = true;
            animator.enabled = false;
        }

        public void SetVisible(bool visible)
        {
            for (int index = 0; index < renderers.Length; index++)
                if (renderers[index] != null) renderers[index].enabled = visible && rendererDefaults[index];
        }

        /// <summary>자기 트윈과 재생 요청을 해제하고 애니메이션으로 바뀐 자식의 원래 배치를 복원한다.</summary>
        public void Restore()
        {
            CancelTween();
            requestedController = null;
            observedCollapse = false;
            if (animatorSuspended && animator != null && animator.transform == visual)
            {
                animatorSuspended = false;
                animator.enabled = animatorWasEnabled;
            }
            if (animator != null && animator.transform == visual && animator.runtimeAnimatorController != null
                && animator.isActiveAndEnabled && animator.layerCount > 0
                && animator.HasState(AnimatorLayerIndex, ReadyStateHash))
            {
                if (HasCollapseTrigger()) animator.ResetTrigger(CollapseTriggerHash);
                animator.Rebind();
                animator.Play(ReadyStateHash, AnimatorLayerIndex, 0f);
                animator.Update(0f);
            }
            for (int index = 0; index < transforms.Length; index++)
            {
                if (transforms[index] == null) continue;
                transforms[index].localPosition = restPositions[index];
                transforms[index].localRotation = restRotations[index];
                transforms[index].localScale = restScales[index];
            }
            SetVisible(true);
        }
    }
}
