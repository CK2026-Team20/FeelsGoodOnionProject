using UnityEngine;

/// <summary>
/// 작은 형태로 전환하는 스킬의 설정 데이터입니다.
/// </summary>
[CreateAssetMenu(
    fileName = "ShrinkSkillDefinition",
    menuName = "Skills/Shrink")]
public class ShrinkSkillDefinition : SkillDefinition
{
    [SerializeField, Min(0f)]
    [Tooltip("사용 요청 수락 후 실제 형태 전환까지 걸리는 시간입니다.\n(단위: 초)")]
    private float preparationTime;

    public override PlayerSkillId Id => PlayerSkillId.Shrink;

    /// <summary>
    /// 실제 형태 전환까지 걸리는 준비 시간.<br/>
    /// (단위: s)
    /// </summary>
    public float PreparationTime => preparationTime;
}