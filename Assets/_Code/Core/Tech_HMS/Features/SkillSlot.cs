using System;

/// <summary>
/// 장착된 스킬과 해당 슬롯의 남은 쿨다운을 관리합니다.
/// </summary>
/// <remarks>
/// 하나의 PlayerSkill 인스턴스는 하나의 슬롯에만 장착해야 합니다.
/// 플레이어 공통 사용 제한은 PlayerSkillController에서 검사합니다.
/// </remarks>
public class SkillSlot
{
    private PlayerSkill equippedSkill;
    private float remainingCooldown;
    /// <summary>쿨다운을 적용한 뒤 실제 발동이 확정된 스킬 인스턴스를 전달합니다.</summary>
    /// <remarks>스킬의 Activated 이벤트를 전달하므로 같은 호출 안에서 종료되는 즉발 스킬도 수신할 수 있습니다.</remarks>
    public event Action<PlayerSkill> Activated;
    /// <summary>현재 장착된 스킬입니다.<br/>
    /// 빈 슬롯이라면 null입니다.</summary>
    public PlayerSkill EquippedSkill => equippedSkill;
    /// <summary>스킬이 장착되어 있는지 확인합니다.</summary>
    public bool HasSkill => equippedSkill != null;
    /// <summary>남은 쿨다운입니다.<br/>
    /// (단위: s)</summary>
    public float RemainingCooldown => remainingCooldown;
    /// <summary>슬롯이 쿨다운 중인지 확인합니다.</summary>
    public bool IsOnCooldown => remainingCooldown > 0f;
    
    private float totalCooldown;
    /// <summary>
    /// 현재 쿨다운이 시작될 때 적용한 전체 시간<br/>
    /// 초기 쿨다운과 사용 후 쿨다운을 모두 포함합니다.
    /// </summary>
    public float TotalCooldown => totalCooldown;

    /// <summary>
    /// 스킬을 장착하고 초기 쿨다운을 적용합니다.
    /// </summary>
    /// <remarks>
    /// 기존 스킬이 있다면 먼저 해제한 후 장착합니다.<br/>
    /// 이미 장착된 인스턴스를 다시 전달하면 아무 작업도 하지 않습니다.
    /// </remarks>
    /// <param name="skill">장착할 스킬 인스턴스</param>
    public void Equip(PlayerSkill skill)
    {
        if (skill == null)
        {
            throw new ArgumentNullException(nameof(skill));
        }

        if (ReferenceEquals(equippedSkill, skill))
        {
            return;
        }

        if (skill.IsInUse)
        {
            throw new InvalidOperationException(
                "준비 또는 실행 중인 스킬은 장착할 수 없습니다.");
        }

        Unequip();

        equippedSkill = skill;
        equippedSkill.Activated += HandleSkillActivated;
        totalCooldown = skill.InitCooldown;
        remainingCooldown = totalCooldown;
    }

    /// <summary>
    /// 장착된 스킬을 해제하고 쿨다운을 초기화합니다.
    /// </summary>
    /// <remarks>
    /// 해제하는 스킬의 준비 또는 실행도 취소합니다.<br/>
    /// 빈 슬롯이라면 아무 작업도 하지 않습니다.
    /// </remarks>
    public void Unequip()
    {
        if (equippedSkill == null) return;

        PlayerSkill previousSkill = equippedSkill;

        previousSkill.Activated -= HandleSkillActivated;
        equippedSkill = null;
        remainingCooldown = 0f;
        totalCooldown = 0f;

        previousSkill.Cancel();
    }

    /// <summary>
    /// 슬롯과 개별 스킬의 사용 조건을 검사합니다.
    /// </summary>
    /// <remarks>
    /// 플레이어의 사망이나 제어 제한 등 공통 조건은 검사하지 않습니다.
    /// </remarks>
    /// <returns>조건을 만족하면 None, 아니라면 사용 불가 사유</returns>
    public PlayerSkillBlockReason GetBlockReason()
    {
        if (equippedSkill == null) return PlayerSkillBlockReason.EmptySlot;
        if (equippedSkill.IsInUse) return PlayerSkillBlockReason.AlreadyInUse;
        if (IsOnCooldown) return PlayerSkillBlockReason.Cooldown;

        return equippedSkill.GetBlockReason();
    }

    /// <summary>
    /// 사용 조건을 검사하고 장착된 스킬에 사용을 요청합니다.
    /// </summary>
    /// <param name="blockReason">
    /// 수락했다면 None, 거절했다면 사용 불가 사유
    /// </param>
    /// <returns>사용 요청을 수락했는지 여부</returns>
    public bool TryUse(out PlayerSkillBlockReason blockReason)
    {
        blockReason = GetBlockReason();

        if (blockReason != PlayerSkillBlockReason.None)
        {
            return false;
        }

        return equippedSkill.TryUse(out blockReason);
    }

    /// <summary>
    /// 남은 쿨다운과 장착된 스킬의 실행 상태를 갱신합니다.
    /// </summary>
    /// <param name="deltaTime">경과 시간<br/>
    /// (단위: s)</param>
    public void Tick(float deltaTime)
    {
        if (float.IsNaN(deltaTime) ||
            float.IsInfinity(deltaTime) ||
            deltaTime < 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTime));
        }

        remainingCooldown = Math.Max(0f, remainingCooldown - deltaTime);
        equippedSkill?.Tick(deltaTime);
    }

    /// <summary>
    /// 장착 상태와 남은 쿨다운을 유지하며 스킬 실행만 취소합니다.
    /// </summary>
    public void Cancel() => equippedSkill?.Cancel();

    /// <summary>현재 장착된 스킬의 발동이면 쿨다운을 시작하고 외부에 전달합니다.</summary>
    /// <param name="skill">발동 이벤트를 발생시킨 스킬 인스턴스</param>
    private void HandleSkillActivated(PlayerSkill skill)
    {
        if (!ReferenceEquals(equippedSkill, skill)) return;

        totalCooldown = skill.Cooldown;
        remainingCooldown = totalCooldown;
        Activated?.Invoke(skill);
    }
}
