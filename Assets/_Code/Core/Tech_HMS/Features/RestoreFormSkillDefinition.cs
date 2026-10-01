using UnityEngine;

/// <summary>기본 형태로 복귀하는 스킬의 설정 데이터입니다.</summary>
[CreateAssetMenu(fileName = "RestoreFormSkillDefinition", menuName = "Skills/Restore Form")]
public class RestoreFormSkillDefinition : SkillDefinition
{
    [SerializeField, Min(0f)]
    [Tooltip("사용 요청 수락 후 실제 복귀까지 걸리는 시간입니다.\n(단위: 초)")]
    private float preparationTime;

    public override PlayerSkillId Id => PlayerSkillId.RestoreForm;

    /// <summary>
    /// 실제 복귀까지 걸리는 준비 시간입니다.<br/>
    /// (단위: s)
    /// </summary>
    public float PreparationTime => preparationTime;
}