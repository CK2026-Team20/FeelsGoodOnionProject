using UnityEngine;
using UnityEngine.Serialization;

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

    [FormerlySerializedAs("effectRadius")]
    [SerializeField, Min(0f)]
    [InspectorName("기본 형태 반경")]
    [Tooltip("기본 형태에서 스턴 대상을 검사할 반경입니다. Unity 월드 단위이며 0이면 대상을 검사하지 않습니다. 청록색(Cyan) 기즈모로 표시됩니다.")]
    private float normalFormRadius = 0.5f;

    [SerializeField, Min(0f), InspectorName("작은 형태 반경")]
    [Tooltip("작은 형태에서 스턴 대상을 검사할 반경입니다. Unity 월드 단위이며 0이면 대상을 검사하지 않습니다. 기본 형태 반경 및 Visual Scale과 독립적이며 초록빛 청록색 기즈모로 표시됩니다.")]
    private float smallFormRadius = 0.3f;

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

    /// <summary>기본 형태에서 효과 중심을 기준으로 대상을 검사할 월드 반경입니다.</summary>
    public float NormalFormRadius => SanitizeRadius(normalFormRadius);

    /// <summary>작은 형태에서 효과 중심을 기준으로 대상을 검사할 월드 반경입니다.</summary>
    public float SmallFormRadius => SanitizeRadius(smallFormRadius);

    /// <summary>현재 형태에 따른 월드 반경입니다. 공유 설정 자산의 값을 변경하지 않습니다.</summary>
    public float GetEffectRadius(PlayerForm form)
        => form == PlayerForm.Small ? SmallFormRadius : NormalFormRadius;

    private static float SanitizeRadius(float radius)
    {
        return float.IsNaN(radius) || float.IsInfinity(radius) ? 0f : Mathf.Max(0f, radius);
    }

    /// <summary>대상에게 적용할 스턴 지속 시간입니다.<br/>
    /// (단위: s)</summary>
    public float StunDuration => stunDuration;
}
