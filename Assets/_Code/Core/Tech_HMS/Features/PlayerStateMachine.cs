using System;

/// <summary>
/// 플레이어의 기본 행동 상태
/// </summary>
/// <remarks>
/// 형태와 이동 모드는 각 컨트롤러에서 관리합니다.<br/>
/// 착지와 즉발 스킬은 조작을 제한하지 않으므로 별도 상태로 구분하지 않습니다.
/// </remarks>
public enum PlayerState
{
    /// <summary>접지 중이며 자체 수평 속도가 이동 판정 기준 이하인 상태</summary>
    Idle = 0,
    /// <summary>접지 중이며 자체 수평 속도가 이동 판정 기준을 초과한 상태. 감속 중에도 유지될 수 있습니다.</summary>
    Move = 1,
    /// <summary>
    /// 공중에서 상승 중인 상태<br/>
    /// 점프 입력 수락 여부를 뜻하지 않으며, 넉백 제한이 해제된 뒤에도 상승 중이면 진입합니다.
    /// </summary>
    Jump = 2,
    /// <summary>공중에서 수직 속도가 0 이하인 상태. 점프 최고점과 발판에서 떨어지는 경우를 포함합니다.</summary>
    Fall = 3,
    /// <summary>
    /// 넉백 적용이 예약되어 있거나 이동 담당의 제어 제한이 유지되는 상태<br/>
    /// 이동·점프·스킬을 제한하며, 제한이 끝나면 공중에서도 다른 행동 상태로 전환합니다.
    /// </summary>
    Knockback = 4,
    /// <summary>사망 상태. 일반 회복으로 해제되지 않으며 명시적인 부활이 필요합니다.</summary>
    Dead = 5
}

/// <summary>
/// 플레이어의 기본 행동 상태와 전환을 관리하는 FSM
/// </summary>
/// <remarks>
/// PlayerFacade가 소유하며, 물리 계산과 제어 제한 시간은 CharacterMovement에서 관리합니다.<br/>
/// 사망 → 넉백 제한 → 공중 상승·낙하 → 지상 이동·정지 순서로 조건을 검사합니다.
/// </remarks>
public sealed class PlayerStateMachine
{
    /// <summary>지상에서 Move로 판단할 최소 자체 수평 속도 기준. (단위: m/s)</summary>
    private const float MovingSpeedThreshold = 0.01f;

    /// <summary>현재 행동 상태. 초기값은 Idle이며 첫 갱신에서 실제 상태를 반영합니다.</summary>
    public PlayerState CurrentState { get; private set; } = PlayerState.Idle;
    /// <summary>
    /// 현재 행동 상태에서 조작을 허용하는지 확인합니다.
    /// </summary>
    /// <remarks>외부 입력 차단이나 컴포넌트 활성 여부는 포함하지 않습니다. 최종 판단은 PlayerFacade.CanReceiveInput을 사용합니다.</remarks>
    public bool AllowsControl => CurrentState != PlayerState.Dead && CurrentState != PlayerState.Knockback;

    /// <summary>
    /// 현재 상태를 변경한 뒤 이전 상태와 새 상태를 순서대로 전달합니다.<br/>
    /// 같은 상태를 유지하는 갱신에서는 발생하지 않습니다.
    /// </summary>
    public event Action<PlayerState, PlayerState> StateChanged;

    /// <summary>
    /// 최신 물리 결과 또는 생명 상태 변경을 반영합니다.
    /// </summary>
    /// <param name="isDead">플레이어의 사망 여부. true이면 다른 조건보다 우선합니다.</param>
    /// <param name="controlLocked">넉백 적용 예약 또는 이동 담당의 제어 제한 여부</param>
    /// <param name="isGrounded">이동 담당에서 판정한 접지 여부</param>
    /// <param name="selfHorizontalSpeed">플랫폼 운반·외력을 제외한 자체 수평 속력. 0 이상. (단위: m/s)</param>
    /// <param name="verticalSpeed">월드 기준 수직 속도. 공중에서 양수이면 Jump, 0 이하이면 Fall로 판단합니다. (단위: m/s)</param>
    internal void Refresh(bool isDead, bool controlLocked, bool isGrounded,
        float selfHorizontalSpeed, float verticalSpeed)
    {
        PlayerState nextState;
        if (isDead)
        {
            nextState = PlayerState.Dead;
        }
        else if (controlLocked)
        {
            nextState = PlayerState.Knockback;
        }
        else if (!isGrounded)
        {
            nextState = verticalSpeed > 0f ? PlayerState.Jump : PlayerState.Fall;
        }
        else
        {
            nextState = selfHorizontalSpeed > MovingSpeedThreshold ? PlayerState.Move : PlayerState.Idle;
        }

        if (nextState == CurrentState) return;

        PlayerState previousState = CurrentState;
        CurrentState = nextState;
        StateChanged?.Invoke(previousState, nextState);
    }
}
