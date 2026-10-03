/// <summary>애니메이션 담당이 선택하는 표현의 종류. 실제 클립 이름과는 별개입니다.</summary>
public enum PlayerAnimationId
{
    /// <summary>대기</summary>
    Idle,
    /// <summary>걷기</summary>
    Walk,
    /// <summary>달리기</summary>
    Run,
    /// <summary>공중 상승</summary>
    Jump,
    /// <summary>공중 낙하</summary>
    Fall,
    /// <summary>넉백 제한 중 표현</summary>
    Knockback,
    /// <summary>사망</summary>
    Dead,
    /// <summary>일회성 착지 표현</summary>
    Land,
    /// <summary>일회성 눈물 표현</summary>
    Tear,
    /// <summary>일회성 축소 표현</summary>
    Shrink,
    /// <summary>일회성 복귀 표현</summary>
    RestoreForm
}

/// <summary>재생 종류와 해당 요청의 식별값. 재생 완료 보고 시 같은 식별값을 전달합니다.</summary>
public readonly struct PlayerAnimationRequest
{
    /// <summary>재생 요청 식별값. 같은 종류를 다시 재생해도 새 값으로 변경됩니다.</summary>
    public long RequestId { get; }
    /// <summary>선택된 표현의 종류</summary>
    public PlayerAnimationId Animation { get; }
    /// <summary>완료 보고가 필요한 일회성 표현인지 여부</summary>
    public bool IsOneShot { get; }

    /// <summary>재생 요청 정보를 생성합니다.</summary>
    /// <param name="requestId">재생 요청 식별값</param>
    /// <param name="animation">선택된 표현</param>
    /// <param name="isOneShot">일회성 표현 여부</param>
    internal PlayerAnimationRequest(long requestId, PlayerAnimationId animation, bool isOneShot)
    {
        RequestId = requestId;
        Animation = animation;
        IsOneShot = isOneShot;
    }
}
