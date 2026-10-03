using System;

/// <summary>FSM 기반 표현과 일회성 표현의 유지·중단·복귀 규칙을 관리합니다.</summary>
/// <remarks>
/// Animator나 클립 길이에 의존하지 않습니다.<br/>
/// 사망·넉백 > 눈물 > 형태 전환 > 착지 순서로 표현을 선택합니다.
/// </remarks>
public sealed class PlayerAnimationPlayback
{
    private PlayerState currentState = PlayerState.Idle;
    private PlayerAnimationId baseAnimation = PlayerAnimationId.Idle;
    private PlayerAnimationId? activeOneShot;
    private long requestSequence;

    /// <summary>현재 선택된 재생 요청. 실제 재생 여부는 Animator 연결 담당에서 관리합니다.</summary>
    public PlayerAnimationRequest CurrentRequest { get; private set; }

    /// <summary>초기 대기 표현을 선택합니다.</summary>
    public PlayerAnimationPlayback()
    {
        SelectAnimation(PlayerAnimationId.Idle, false);
    }

    /// <summary>FSM 상태와 이동 표현을 갱신하고, 필요한 경우 일회성 표현을 중단합니다.</summary>
    /// <param name="state">최신 FSM 상태</param>
    /// <param name="useWalk">Move 상태에서 걷기를 사용할지 여부. false이면 달리기를 사용합니다.</param>
    /// <remarks>눈물·형태 전환 재생 중에도 기본 표현을 갱신하여 완료 시 최신 상태로 복귀합니다.</remarks>
    public void Refresh(PlayerState state, bool useWalk)
    {
        currentState = state;
        switch (state)
        {
            case PlayerState.Idle: baseAnimation = PlayerAnimationId.Idle; break;
            case PlayerState.Move: baseAnimation = useWalk ? PlayerAnimationId.Walk : PlayerAnimationId.Run; break;
            case PlayerState.Jump: baseAnimation = PlayerAnimationId.Jump; break;
            case PlayerState.Fall: baseAnimation = PlayerAnimationId.Fall; break;
            case PlayerState.Knockback: baseAnimation = PlayerAnimationId.Knockback; break;
            case PlayerState.Dead: baseAnimation = PlayerAnimationId.Dead; break;
            default: throw new ArgumentOutOfRangeException(nameof(state));
        }

        if (state == PlayerState.Dead || state == PlayerState.Knockback ||
            (activeOneShot == PlayerAnimationId.Land && state != PlayerState.Idle))
        {
            activeOneShot = null;
        }

        if (!activeOneShot.HasValue && CurrentRequest.Animation != baseAnimation)
        {
            SelectAnimation(baseAnimation, false);
        }
    }

    /// <summary>일회성 표현을 요청합니다. 수락된 요청은 완료 또는 중단까지 유지됩니다.</summary>
    /// <param name="animation">Land, Tear, Shrink 또는 RestoreForm</param>
    /// <returns><c>true</c>: 새 재생 요청 수락<br/><c>false</c>: 현재 상태 또는 우선순위에 의해 거절</returns>
    /// <exception cref="ArgumentOutOfRangeException">일회성 표현이 아닌 종류를 전달한 경우</exception>
    /// <remarks>동일 우선순위는 마지막 요청으로 다시 시작합니다. 거절된 요청은 나중에 재생하지 않습니다.</remarks>
    public bool TryRequest(PlayerAnimationId animation)
    {
        int priority = GetPriority(animation);
        if (currentState == PlayerState.Dead || currentState == PlayerState.Knockback) return false;
        if (animation == PlayerAnimationId.Land && currentState != PlayerState.Idle) return false;
        if (activeOneShot.HasValue && GetPriority(activeOneShot.Value) > priority) return false;

        activeOneShot = animation;
        SelectAnimation(animation, true);
        return true;
    }

    /// <summary>현재 일회성 재생을 완료하고 최신 FSM의 기본 표현으로 복귀합니다.</summary>
    /// <param name="requestId">재생 시작 시 받은 요청 식별값</param>
    /// <returns><c>true</c>: 현재 요청을 완료함<br/><c>false</c>: 이미 중단된 요청이거나 일회성 재생이 아님</returns>
    /// <remarks>이전 애니메이션의 늦은 완료 알림이 새 애니메이션을 종료하지 않도록 식별값을 검사합니다.</remarks>
    public bool TryComplete(long requestId)
    {
        if (!activeOneShot.HasValue || CurrentRequest.RequestId != requestId) return false;
        activeOneShot = null;
        SelectAnimation(baseAnimation, false);
        return true;
    }

    /// <summary>비활성화 시 일회성 표현을 비우고 이전 완료 알림을 무효화합니다.</summary>
    public void Reset()
    {
        activeOneShot = null;
        SelectAnimation(baseAnimation, false);
    }

    /// <summary>선택한 표현에 새로운 요청 식별값을 부여합니다.</summary>
    /// <param name="animation">재생할 표현</param>
    /// <param name="isOneShot">일회성 표현 여부</param>
    private void SelectAnimation(PlayerAnimationId animation, bool isOneShot)
    {
        CurrentRequest = new PlayerAnimationRequest(++requestSequence, animation, isOneShot);
    }

    /// <summary>일회성 표현의 우선순위를 반환합니다. 큰 값이 우선합니다.</summary>
    /// <param name="animation">일회성 표현 종류</param>
    /// <returns>착지 1, 형태 전환 2, 눈물 3</returns>
    /// <exception cref="ArgumentOutOfRangeException">일회성 표현이 아닌 경우</exception>
    private static int GetPriority(PlayerAnimationId animation)
    {
        switch (animation)
        {
            case PlayerAnimationId.Land: return 1;
            case PlayerAnimationId.Shrink:
            case PlayerAnimationId.RestoreForm: return 2;
            case PlayerAnimationId.Tear: return 3;
            default: throw new ArgumentOutOfRangeException(nameof(animation));
        }
    }
}
