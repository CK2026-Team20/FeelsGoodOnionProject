using System;
using UnityEngine;

public interface IStunState
{
    /// <summary>
    /// 해당 개체가 현재 Stun 상태인지 여부입니다.
    /// </summary>
    bool IsStunned { get; }

    /// <summary>
    /// 해당 개체의 Stun 상태가 변경되었을 때 호출됩니다.<br/>
    /// true는 Stun 적용, false는 Stun 해제를 의미합니다.
    /// </summary>
    event Action<bool> StunStateChanged;
}