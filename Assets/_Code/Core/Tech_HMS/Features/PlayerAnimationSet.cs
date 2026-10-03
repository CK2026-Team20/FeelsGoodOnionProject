using System;
using UnityEngine;

/// <summary>공통 Animator 틀에 적용할 플레이어 클립과 재생 설정입니다.</summary>
/// <remarks>큰 형태와 작은 형태에서 같은 설정을 공유합니다. 클립 이름은 자유롭게 지정할 수 있습니다.</remarks>
[CreateAssetMenu(fileName = "PlayerAnimationSet", menuName = "Player/Animation Set")]
public sealed class PlayerAnimationSet : ScriptableObject
{
    [Tooltip("제공된 PlayerAnimationTemplate을 연결합니다.")]
    [SerializeField] private RuntimeAnimatorController controllerTemplate;
    [SerializeField, Min(0f)] private float blendDuration = 0.1f;
    [SerializeField, Min(0.01f)] private float walkReferenceSpeed = 3f;
    [SerializeField, Min(0.01f)] private float runReferenceSpeed = 5f;

    [Header("Base Clips")]
    [SerializeField] private AnimationClip idle;
    [SerializeField] private AnimationClip walk;
    [SerializeField] private AnimationClip run;
    [SerializeField] private AnimationClip jump;
    [SerializeField] private AnimationClip fall;
    [SerializeField] private AnimationClip knockback;
    [SerializeField] private AnimationClip dead;
    [Header("One Shot Clips")]
    [SerializeField] private AnimationClip land;
    [SerializeField] private AnimationClip tear;
    [SerializeField] private AnimationClip shrink;
    [SerializeField] private AnimationClip restoreForm;

    /// <summary>코드가 상태 이름으로 접근할 공통 Animator Controller</summary>
    public RuntimeAnimatorController ControllerTemplate => controllerTemplate;
    /// <summary>Inspector에서 설정이 바뀌면 재생 연결을 갱신하기 위한 버전값</summary>
    public int Revision { get; private set; }

    /// <summary>클립 또는 재생 설정 변경을 실행 중인 수신 측에 알립니다.</summary>
    private void OnValidate() => Revision++;
    /// <summary>표현 전환 블렌딩 시간. (단위: s)</summary>
    public float BlendDuration => float.IsFinite(blendDuration) ? Mathf.Max(0f, blendDuration) : 0.1f;

    /// <summary>요청한 클립을 조회하고 기본 표현에 한해 누락 대체를 적용합니다.</summary>
    /// <param name="requested">요청한 표현</param>
    /// <param name="resolved">실제로 선택된 표현. 클립이 null이면 사용하지 않습니다.</param>
    /// <returns>사용 가능한 클립. 없거나 Legacy 클립이면 null</returns>
    /// <remarks>일회성은 대체하지 않습니다. 이동은 다른 이동 클립, 공중은 다른 공중 클립, 마지막으로 Idle을 시도합니다.</remarks>
    public AnimationClip Resolve(PlayerAnimationId requested, out PlayerAnimationId resolved)
    {
        resolved = requested;
        AnimationClip clip = GetClip(requested);
        if (IsUsable(clip)) return clip;
        switch (requested)
        {
            case PlayerAnimationId.Walk: resolved = PlayerAnimationId.Run; break;
            case PlayerAnimationId.Run: resolved = PlayerAnimationId.Walk; break;
            case PlayerAnimationId.Jump: resolved = PlayerAnimationId.Fall; break;
            case PlayerAnimationId.Fall: resolved = PlayerAnimationId.Jump; break;
            case PlayerAnimationId.Land:
            case PlayerAnimationId.Tear:
            case PlayerAnimationId.Shrink:
            case PlayerAnimationId.RestoreForm: return null;
            default: resolved = PlayerAnimationId.Idle; break;
        }
        clip = GetClip(resolved);
        if (IsUsable(clip)) return clip;
        resolved = PlayerAnimationId.Idle;
        return IsUsable(idle) ? idle : null;
    }

    /// <summary>자체 이동 속력에 맞춰 이동 클립의 재생 배율을 계산합니다.</summary>
    /// <param name="animation">실제 선택된 클립 종류</param>
    /// <param name="speed">자체 수평 속력. (단위: m/s)</param>
    /// <returns>이동 표현은 0.1~3 배, 다른 표현은 1 배</returns>
    public float GetPlaybackSpeed(PlayerAnimationId animation, float speed)
    {
        if (animation != PlayerAnimationId.Walk && animation != PlayerAnimationId.Run) return 1f;
        float reference = animation == PlayerAnimationId.Walk ? walkReferenceSpeed : runReferenceSpeed;
        if (!float.IsFinite(reference) || reference <= 0f || !float.IsFinite(speed)) return 1f;
        return Mathf.Clamp(speed / reference, 0.1f, 3f);
    }

    /// <summary>Animator에서 재생할 수 있는 양의 길이의 클립인지 검사합니다.</summary>
    /// <param name="clip">검사할 클립</param>
    /// <returns>클립이 존재하고 Legacy가 아니며 길이가 양수이면 true</returns>
    private static bool IsUsable(AnimationClip clip) => clip != null && !clip.legacy && clip.length > 0f;

    /// <summary>설정된 원본 클립을 반환합니다.</summary>
    /// <param name="animation">조회할 표현 종류</param>
    /// <returns>할당된 클립 또는 null</returns>
    private AnimationClip GetClip(PlayerAnimationId animation)
    {
        switch (animation)
        {
            case PlayerAnimationId.Idle: return idle;
            case PlayerAnimationId.Walk: return walk;
            case PlayerAnimationId.Run: return run;
            case PlayerAnimationId.Jump: return jump;
            case PlayerAnimationId.Fall: return fall;
            case PlayerAnimationId.Knockback: return knockback;
            case PlayerAnimationId.Dead: return dead;
            case PlayerAnimationId.Land: return land;
            case PlayerAnimationId.Tear: return tear;
            case PlayerAnimationId.Shrink: return shrink;
            case PlayerAnimationId.RestoreForm: return restoreForm;
            default: throw new ArgumentOutOfRangeException(nameof(animation));
        }
    }
}
