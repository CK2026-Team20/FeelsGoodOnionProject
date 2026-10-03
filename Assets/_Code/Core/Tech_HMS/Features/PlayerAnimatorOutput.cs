using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>선택된 애니메이션 요청을 현재 형태의 Animator에서 재생합니다.</summary>
/// <remarks>
/// 플레이어 루트에 추가하고 Animation Set을 할당합니다. Animator Controller는 실행 시 자동 연결합니다.<br/>
/// 다른 스크립트나 Timeline에서 같은 Animator를 동시에 제어하지 않아야 합니다.
/// </remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAnimationController))]
[DefaultExecutionOrder(200)]
public sealed class PlayerAnimatorOutput : MonoBehaviour
{
    [SerializeField] private PlayerAnimationSet animationSet;
    [Tooltip("비워두면 PlayerFormController의 현재 형태 Animator를 사용합니다. 외형 하나만 쓰는 구성에서는 직접 지정할 수 있습니다.")]
    [SerializeField] private Animator animatorOverride;

    private PlayerAnimationController requests;
    private PlayerFormController forms;
    private Animator animator;
    private RuntimeAnimatorController originalController;
    private bool originalRootMotion;
    private AnimatorCullingMode originalCulling;
    private AnimatorUpdateMode originalUpdateMode;
    private float originalSpeed;
    private AnimatorOverrideController runtimeController;
    private PlayerAnimationSet loadedSet;
    private int loadedRevision;
    private long activeRequestId;
    private int activeStateHash;
    private PlayerAnimationId resolvedAnimation;
    private float savedNormalizedTime;
    private bool hasClip;
    private bool warnedUnavailable;
    private int lastStartFrame;
    private readonly HashSet<PlayerAnimationId> warnedMissing = new HashSet<PlayerAnimationId>();

    private static readonly int PlaybackSpeed = Animator.StringToHash("PlaybackSpeed");

    /// <summary>요청 담당과 형태 담당을 캐싱합니다.</summary>
    private void Awake()
    {
        requests = GetComponent<PlayerAnimationController>();
        forms = GetComponent<PlayerFormController>();
    }

    /// <summary>재생 대상 변경·요청 변경·완료를 순서대로 처리합니다.</summary>
    /// <remarks>CurrentRequest를 조회하므로 비활성화 중 발생한 이벤트도 재활성화 시 최신 상태로 동기화합니다.</remarks>
    private void LateUpdate()
    {
        if (!requests.isActiveAndEnabled)
        {
            ReleaseAnimator();
            activeRequestId = 0;
            return;
        }

        PlayerAnimationRequest request = requests.CurrentRequest;
        Animator target = animatorOverride != null ? animatorOverride : forms.CurrentAnimator;
        bool targetChanged = animator != target || loadedSet != animationSet ||
            (animationSet != null && loadedRevision != animationSet.Revision);
        float resumeTime = activeRequestId == request.RequestId ? savedNormalizedTime : 0f;
        if (targetChanged)
        {
            ReleaseAnimator();
            BindAnimator(target);
        }

        bool available = animator != null && animator.isActiveAndEnabled && runtimeController != null;
        if (!available)
        {
            if (!warnedUnavailable)
            {
                Debug.LogWarning("[Player Animation] 활성 Animator 또는 유효한 Animation Set/Controller가 없습니다. 일회성 표현은 건너뜁니다.", this);
                warnedUnavailable = true;
            }
            if (request.IsOneShot) requests.TryComplete(request.RequestId);
            activeRequestId = 0;
            return;
        }
        warnedUnavailable = false;

        if (targetChanged || activeRequestId != request.RequestId)
        {
            StartRequest(request, targetChanged, resumeTime);
        }
        if (!hasClip) return;

        animator.SetFloat(PlaybackSpeed, animationSet.GetPlaybackSpeed(resolvedAnimation, requests.SelfHorizontalSpeed));
        // Play/CrossFade 직후에는 이전 상태 정보가 남아 있을 수 있어 다음 갱신부터 검사합니다.
        if (Time.frameCount == lastStartFrame) return;
        AnimatorStateInfo state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if (state.fullPathHash != activeStateHash) return;
        savedNormalizedTime = state.normalizedTime;

        if (request.IsOneShot && state.normalizedTime >= 1f)
        {
            requests.TryComplete(activeRequestId);
        }
        else if (!request.IsOneShot && state.normalizedTime >= 1f)
        {
            bool repeat = resolvedAnimation == PlayerAnimationId.Idle || resolvedAnimation == PlayerAnimationId.Walk || resolvedAnimation == PlayerAnimationId.Run;
            if (repeat && !state.loop)
            {
                animator.Play(activeStateHash, 0, state.normalizedTime % 1f);
            }
            else if (!repeat)
            {
                // 공중·넉백·사망은 상태가 끝날 때까지 마지막 포즈를 유지합니다.
                animator.Play(activeStateHash, 0, 0.99999f);
                animator.SetFloat(PlaybackSpeed, 0f);
            }
        }
    }

    /// <summary>새 Animator에 공통 Controller와 클립 교체표를 적용합니다.</summary>
    /// <param name="target">현재 형태에서 사용할 Animator. 없으면 null</param>
    private void BindAnimator(Animator target)
    {
        animator = target;
        loadedSet = animationSet;
        loadedRevision = animationSet != null ? animationSet.Revision : 0;
        if (animator == null || animationSet == null || animationSet.ControllerTemplate == null) return;
        originalController = animator.runtimeAnimatorController;
        originalRootMotion = animator.applyRootMotion;
        originalCulling = animator.cullingMode;
        originalUpdateMode = animator.updateMode;
        originalSpeed = animator.speed;
        runtimeController = new AnimatorOverrideController(animationSet.ControllerTemplate);
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        runtimeController.GetOverrides(overrides);
        for (int index = 0; index < overrides.Count; index++)
        {
            AnimationClip placeholder = overrides[index].Key;
            if (Enum.TryParse(placeholder.name.Replace("__PC_", ""), out PlayerAnimationId animation))
            {
                AnimationClip clip = animationSet.Resolve(animation, out _);
                overrides[index] = new KeyValuePair<AnimationClip, AnimationClip>(placeholder, clip != null ? clip : placeholder);
            }
        }
        runtimeController.ApplyOverrides(overrides);
        animator.runtimeAnimatorController = runtimeController;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.speed = 1f;
    }

    /// <summary>새 요청을 재생하거나 누락된 일회성 요청을 완료 처리합니다.</summary>
    /// <param name="request">최신 재생 요청</param>
    /// <param name="targetChanged">형태 또는 설정 변경으로 Animator를 다시 연결했는지 여부</param>
    /// <param name="resumeTime">같은 요청을 다른 형태에서 이어갈 정규화 시간</param>
    private void StartRequest(PlayerAnimationRequest request, bool targetChanged, float resumeTime)
    {
        activeRequestId = request.RequestId;
        savedNormalizedTime = resumeTime;
        AnimationClip clip = animationSet.Resolve(request.Animation, out resolvedAnimation);
        hasClip = clip != null;
        if (!hasClip && warnedMissing.Add(request.Animation))
        {
            Debug.LogWarning($"[Player Animation] {request.Animation} 클립이 없습니다. 일회성은 건너뛰고 기본 표현은 빈 상태를 사용합니다.", this);
        }
        if (!hasClip && request.IsOneShot)
        {
            requests.TryComplete(request.RequestId);
            return;
        }
        activeStateHash = Animator.StringToHash("Base Layer." + request.Animation);
        if (!animator.HasState(0, activeStateHash))
        {
            if (warnedMissing.Add(request.Animation)) Debug.LogWarning($"[Player Animation] Controller에 {request.Animation} 상태가 없습니다.", this);
            hasClip = false;
            if (request.IsOneShot) requests.TryComplete(request.RequestId);
            return;
        }
        animator.SetFloat(PlaybackSpeed, 1f);
        if (targetChanged || !hasClip || animationSet.BlendDuration <= 0f)
        {
            animator.Play(activeStateHash, 0, resumeTime);
        }
        else
        {
            float blend = request.IsOneShot ? Mathf.Min(animationSet.BlendDuration, clip.length * 0.25f) : animationSet.BlendDuration;
            animator.CrossFadeInFixedTime(activeStateHash, blend, 0, 0f);
        }
        lastStartFrame = Time.frameCount;
    }

    /// <summary>생성한 Controller를 정리하고 기존 Animator 설정을 복원합니다.</summary>
    private void ReleaseAnimator()
    {
        if (animator != null && runtimeController != null)
        {
            animator.runtimeAnimatorController = originalController;
            animator.applyRootMotion = originalRootMotion;
            animator.cullingMode = originalCulling;
            animator.updateMode = originalUpdateMode;
            animator.speed = originalSpeed;
        }
        if (runtimeController != null) Destroy(runtimeController);
        runtimeController = null;
        animator = null;
        loadedSet = null;
        hasClip = false;
    }

    /// <summary>재생 연결을 해제합니다. 재활성화 시 최신 요청을 처음부터 동기화합니다.</summary>
    private void OnDisable()
    {
        ReleaseAnimator();
        activeRequestId = 0;
        savedNormalizedTime = 0f;
    }
}
