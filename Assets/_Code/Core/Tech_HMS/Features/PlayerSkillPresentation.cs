/// <summary>
/// 스킬 애니메이션 재생 요청의 우선순위를 관리하는 클래스
/// </summary>
/// <remarks>
/// PlayerFacade의 LateUpdate에서 요청을 꺼내기 전까지 수신한 스킬 중 눈물을 우선합니다.<br/>
/// 실제 스킬 효과와 쿨다운은 변경하지 않으며, Animator 재생은 이벤트 수신 측에서 처리합니다.
/// </remarks>
internal sealed class PlayerSkillPresentation
{
    /// <summary>재생 대기 중인 스킬. null이면 대기 요청이 없습니다.</summary>
    private PlayerSkillId? pendingSkill;

    /// <summary>
    /// 스킬 애니메이션 재생을 요청합니다.<br/>
    /// 눈물이 대기 중이면 유지하고, 그 외에는 마지막 요청으로 교체합니다.
    /// </summary>
    /// <param name="skill">실제 발동한 스킬의 종류</param>
    public void Request(PlayerSkillId skill)
    {
        if (pendingSkill != PlayerSkillId.Tear) pendingSkill = skill;
    }

    /// <summary>대기 중인 재생 요청을 꺼낸 뒤 대기 상태를 비웁니다.</summary>
    /// <param name="skill">재생할 스킬. 반환값이 false인 경우 이 값은 사용하지 않습니다.</param>
    /// <returns><c>true</c>: 재생 요청을 전달함<br/><c>false</c>: 대기 중인 요청이 없음</returns>
    public bool TryTake(out PlayerSkillId skill)
    {
        skill = pendingSkill.GetValueOrDefault();
        bool hasPendingSkill = pendingSkill.HasValue;
        pendingSkill = null;
        return hasPendingSkill;
    }

    /// <summary>대기 중인 재생 요청을 제거합니다. 이미 발동한 스킬 효과는 취소하지 않습니다.</summary>
    public void Clear() => pendingSkill = null;
}
