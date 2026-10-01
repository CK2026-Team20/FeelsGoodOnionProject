using UnityEngine;

// /// <summary>
// /// 외부(UI 등)에서 PC의 X, Z축 이동 입력 및 점프 입력을 제한하고자 할 때 반드시 사용해야 하는 전용 식별자입니다.<br/>
// /// 각 요청자는 해당 클래스의 인스턴스를 직접 생성하여, PlayerFacade의 입력 제한 함수에 block(bool)여부를 인스턴스와 함께 제공해야 합니다.<br/>
// /// 입력 제한을 해제하고자 할 경우 입력 제한에 사용했던 인스턴스와 동일한 인스턴스를 제공해야 하기 때문에, 요청자는 인스턴스를 직접 보관해야 합니다.
// /// </summary>
// /// <remarks>
// /// 독립적인 차단/해제가 필요한 기능(UI 등)마다 하나의 인스턴스만 생성 및 보관하는 것을 권장합니다.
// /// </remarks>
// [System.Obsolete("추후 입력 제한 요청에 요청자 구분이 필요해질 경우 사용됩니다.", true)]
// public sealed class MovementInputBlockToken { }
