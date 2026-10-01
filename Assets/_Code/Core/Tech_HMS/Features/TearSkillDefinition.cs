using UnityEngine;

/// <summary>눈물 스킬의 설정 데이터입니다.</summary>
[CreateAssetMenu(fileName = "TearSkillDefinition", menuName = "Skills/Tear")]
public class TearSkillDefinition : SkillDefinition
{
    [SerializeField, Min(0)]
    [Tooltip("스킬을 한 번 발동할 때 소비하는 조각 수입니다.")]
    private int requiredFragments = 1;

    [SerializeField, Min(0f)]
    [Tooltip("사용 요청 수락 후 실제 발동까지 걸리는 시간입니다.\n(단위: 초)")]
    private float preparationTime = 0f;

    [SerializeField, Min(0f)]
    [Tooltip("효과 중심을 기준으로 스턴 대상을 검사할 반경입니다.\n(단위: Unity 월드 단위)")]
    private float effectRadius = 3f;

    [SerializeField, Min(0f)]
    [Tooltip("대상에게 적용할 스턴 지속 시간입니다.\n(단위: 초)")]
    private float stunDuration = 2f;
    
    public override PlayerSkillId Id => PlayerSkillId.Tear;

    /// <summary>한 번 발동할 때 소비하는 조각 수입니다.</summary>
    public int RequiredFragments => requiredFragments;

    /// <summary>사용 요청 수락 후 실제 발동까지 걸리는 시간입니다.<br/>
    /// (단위: s)</summary>
    /// <remarks>
    /// 0이면 별도의 준비 시간 없이 발동합니다.
    /// </remarks>
    public float PreparationTime => preparationTime;

    /// <summary>효과 중심을 기준으로 대상을 검사할 반경입니다.</summary>
    public float EffectRadius => effectRadius;

    /// <summary>대상에게 적용할 스턴 지속 시간입니다.<br/>
    /// (단위: s)</summary>
    public float StunDuration => stunDuration;
}