using UnityEngine;

/// <summary>스킬의 공통 설정 데이터</summary>
/// <remarks>
/// 여러 스킬 인스턴스가 공유할 수 있으며,<br/>
/// 현재 실행 단계나 남은 쿨다운 등 실행 중 상태는 저장하지 않습니다.
/// </remarks>
public abstract class SkillDefinition : ScriptableObject
{
    [SerializeField, Min(0f)]
    [Tooltip("스킬이 실제로 발동한 뒤 적용할 쿨다운입니다.\n(단위: 초)")]
    private float cooldown;

    [SerializeField, Min(-1f)]
    [Tooltip("장착 직후에만 적용할 쿨다운입니다. 음수이면 기본 쿨다운을 동일하게 사용합니다.\n(단위: 초)")]
    private float initCooldown = -1f;

    /// <summary>해당 설정이 나타내는 스킬의 종류<br/>
    /// 세부 파생클래스에서 직접 지정</summary>
    public abstract PlayerSkillId Id { get; }

    /// <summary>스킬이 실제로 발동한 뒤 적용할 쿨다운<br/>
    /// (단위: s)</summary>
    public float Cooldown => cooldown;

    /// <summary>스킬을 장착한 직후 적용할 쿨다운<br/>
    /// (단위: s)</summary>
    /// <remarks>
    /// 설정값이 음수이면 <see cref="Cooldown"/>을 반환합니다.
    /// </remarks>
    public float InitCooldown => initCooldown < 0f ? Cooldown : initCooldown;
}
