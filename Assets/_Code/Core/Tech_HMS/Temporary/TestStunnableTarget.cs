using UnityEngine;

/// <summary>
/// 스턴 호출 여부와 전달된 시간을 확인하는 임시 테스트 컴포넌트입니다.
/// 실제 행동 제한은 구현하지 않습니다.
/// </summary>
public class TestStunnableTarget : MonoBehaviour, IStunnable
{
    private int receivedCount;

    public int TryStun(int stunDur)
    {
        receivedCount++;
        Debug.Log($"[스턴 수신] {name} / " + $"지속 시간: {stunDur}ms / " + $"누적 호출: {receivedCount}회",this);

        return stunDur;
    }
}