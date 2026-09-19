public interface IStunnable
{
    /// <summary>
    /// 해당 개체에게 지정된 시간만큼 Stun 효과 적용을 시도합니다.
    /// </summary>
    /// <param name="stunDur">적용을 시도할 Stun의 지속 시간<br/>(단위: ms)</param>
    /// <returns>실제로 적용된 Stun 지속 시간<br/>(단위: ms)</returns>
    int TryStun(int stunDur);
}
