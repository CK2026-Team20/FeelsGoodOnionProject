using UnityEngine;

/// <summary>
/// '넉백'을 받을 수 있는 개체의 공통 계약 입니다.
/// 호출자는 넉백의 방향과 속력을 전달하며, 실제 속도 적용은 구현 클래스가 담당해야 합니다.
/// </summary>
public interface IKnockbackable
{
    /// <summary>
    /// 지정한 방향과 속력으로 넉백을 요청합니다.
    /// 넉백 시점 넉백 대상자의 기존 속도의 유지, 초기화 또는 합성 방식은 구현 클래스의 규칙에 따릅니다.
    /// 전달한 속도가 대상의 최종 속도와 일치하거나 일정 시간 유지됨을 보장하지 않습니다.
    /// </summary>
    /// <param name="knockbackVelocity">
    /// <b>월드 좌표 기준</b> 넉백 속도(m/s)입니다.
    /// 벡터의 방향은 밀어낼 방향, 크기는 넉백 속력을 나타냅니다.<br/>
    /// 호출자는 대상의 현재 속도를 미리 빼거나 더하지 않고 전달해야 합니다.
    /// 실제 운동은 구현 클래스의 속도 처리, 이동 축 제한 및 충돌에 따라 달라질 수 있습니다.
    /// </param>
    /// <param name="controlLockDuration">
    /// 자체 이동이나 점프 등을 제한하도록 요청하는 시간입니다.<br/>
    /// 0 이하의 값을 제공할 경우, 새로운 제어 제한을 요청하지 않지만 넉백 자체는 요청합니다.
    /// 제어 제한을 지원하지 않는 대상은 이 값을 무시할 수 있습니다.<br/>
    /// (단위: ms)
    /// </param>
    void ApplyKnockback(Vector3 knockbackVelocity, float controlLockDuration);
}