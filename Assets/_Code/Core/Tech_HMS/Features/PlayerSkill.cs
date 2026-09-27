using System;

/// <summary>
/// 개별 스킬의 실행 상태와 동작을 관리하는 공통 기반 클래스입니다.
/// </summary>
/// <remarks>
/// 캐릭터별로 별도의 인스턴스를 생성해 사용합니다.
/// 남은 쿨다운은 이 클래스가 아닌 SkillSlot에서 관리합니다.
/// </remarks>
public abstract class PlayerSkill
{
    /// <summary>이 스킬이 사용하는 설정 데이터</summary>
    protected SkillDefinition Definition { get; }

    /// <summary>스킬의 종류</summary>
    public PlayerSkillId Id => Definition.Id;

    /// <summary>실제 발동 후 적용할 쿨다운<br/>
    /// (단위: s)</summary>
    public float Cooldown => Definition.Cooldown;

    /// <summary>장착 직후 적용할 쿨다운<br/>
    /// (단위: s)</summary>
    public float InitCooldown => Definition.InitCooldown;

    /// <summary>현재 스킬의 실행 단계</summary>
    public PlayerSkillPhase Phase { get; protected set; } = PlayerSkillPhase.Idle;

    /// <summary>스킬이 준비 또는 실행 중인지 여부</summary>
    public bool IsInUse => Phase != PlayerSkillPhase.Idle;

    /// <summary>스킬이 실제로 발동했을 때 발생합니다.</summary>
    /// <remarks>
    /// SkillSlot은 이 이벤트를 받아 쿨다운을 시작하며,<br/>
    /// 사용 요청 수락만으로는 발생하지 않습니다.
    /// </remarks>
    public event Action<PlayerSkill> Activated;

    protected PlayerSkill(SkillDefinition definition)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        Definition = definition;
    }

    /// <summary>
    /// 개별 스킬의 사용 조건을 검사합니다.
    /// </summary>
    /// <remarks>
    /// 실행 중 여부, 현재 형태, 조각 수 등 스킬 자체의 조건을 검사합니다.<br/>
    /// 슬롯의 쿨다운과 플레이어 공통 제한은 호출 측에서 검사합니다.<br/>
    /// 검사 과정에서 상태나 자원을 변경하지 않습니다.
    /// </remarks>
    /// <returns>사용할 수 있다면 None, 아니라면 사용 불가 사유</returns>
    public abstract PlayerSkillBlockReason GetBlockReason();

    /// <summary>
    /// 사용 조건을 검사하고 스킬 사용을 시도합니다.
    /// </summary>
    /// <param name="blockReason">
    /// 수락했다면 None, 거절했다면 사용 불가 사유
    /// </param>
    /// <returns>사용 요청을 수락했는지 여부</returns>
    /// <remarks>
    /// true는 실제 효과가 발동했다는 의미가 아닙니다.<br/>
    /// 준비 단계가 있다면 Preparing 상태로 진입할 수 있습니다.
    /// </remarks>
    public abstract bool TryUse(out PlayerSkillBlockReason blockReason);

    /// <summary>
    /// 준비 시간이나 지속 효과 등 실행 중 상태를 갱신합니다.
    /// </summary>
    /// <param name="deltaTime">지난 갱신 이후 경과 시간입니다.<br/>
    /// (단위: s)</param>
    public abstract void Tick(float deltaTime);

    /// <summary>
    /// 진행 중인 준비 또는 실행을 취소하고 Idle 상태로 돌아갑니다.
    /// </summary>
    /// <remarks>
    /// 이미 Idle 상태라면 아무 작업도 하지 않습니다.<br/>
    /// 이미 적용한 효과나 소비한 자원을 자동으로 되돌리는 의미는 아닙니다.<br/>
    /// 필요한 정리 작업은 개별 스킬에서 구현해야 합니다.
    /// </remarks>
    public abstract void Cancel();

    /// <summary>
    /// 스킬이 실제로 발동했음을 알립니다.
    /// </summary>
    /// <remarks>
    /// 파생 클래스에서 발동이 확정된 시점에 사용 요청당 한 번 호출하며,<br/>
    /// 지속 효과의 매 프레임 갱신에서는 호출하지 않습니다.
    /// </remarks>
    protected void NotifyActivated()
    {
        Activated?.Invoke(this);
    }
}