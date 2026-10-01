/// <summary>
/// 슬롯에 장착할 수 있는 스킬의 종류
/// </summary>
/// <remarks>
/// 빈 슬롯은 슬롯의 장착 스킬 참조가 없는 것으로 표현합니다.<br/>
/// (별도의 None 스킬 생성 X)
/// </remarks>
public enum PlayerSkillId
{
    /// <summary>작은 형태로 전환하고 껍질을 생성하는 스킬입니다.</summary>
    Shrink = 0,
    /// <summary>기본 형태로 복귀하는 스킬입니다.</summary>
    RestoreForm = 1,
    /// <summary>지정된 범위의 대상에게 스턴을 적용하는 눈물 스킬입니다.</summary>
    Tear = 2
}

/// <summary>
/// 개별 스킬의 실행 단계<br/>
/// 슬롯의 장착 여부 및 쿨다운과는 별개입니다.
/// </summary>
public enum PlayerSkillPhase
{
    /// <summary>
    /// 진행 중인 사용 요청이 없는 상태<br/>
    /// 슬롯이 쿨다운 중이라면 이 상태여도 사용할 수 없습니다.
    /// </summary>
    Idle = 0,
    /// <summary>
    /// 사용 요청을 수락하고 실제 발동을 준비하는 상태<br/>
    /// 준비 중에는 같은 스킬의 새로운 사용 요청을 받지 않습니다.
    /// </summary>
    Preparing = 1,
    /// <summary>
    /// 실제 효과를 적용하거나 지속 효과를 실행하는 상태<br/>
    /// 즉시 적용되는 효과라면 같은 호출 흐름에서 종료될 수 있습니다.
    /// </summary>
    Executing = 2
}

/// <summary>
/// 스킬을 사용할 수 없는 대표 사유<br/>
/// 스킬 공통 제한과 개별 스킬의 제한을 하나의 형식으로 전달합니다.
/// </summary>
/// <remarks>
/// 모든 스킬이 모든 사유를 사용하는 것은 아닙니다.
/// 여러 조건에 걸리면 정해진 검사 순서에 따라 하나를 반환합니다.
/// UI 표시 문구와 해당 enum은 별개입니다. (ViewModel 등에서 변환해 사용)
/// </remarks>
public enum PlayerSkillBlockReason
{
    /// <summary>공용: 검사한 사용 조건을 모두 만족</summary>
    None = 0,
    /// <summary>공용: 요청한 슬롯을 찾을 수 없음</summary>
    InvalidSlot = 1,
    /// <summary>공용: 슬롯에 장착된 스킬이 없음</summary>
    EmptySlot = 2,
    /// <summary>공용: 플레이어나 스킬이 비활성 상태이거나 사용할 준비가 되지 않음</summary>
    Unavailable = 3,
    /// <summary>공용: 플레이어가 사망하여 사용할 수 없음</summary>
    Dead = 4,
    /// <summary>공용: 넉백 등에 의한 제어 제한으로 스킬 사용이 금지되어 있음</summary>
    ControlLocked = 5,
    /// <summary>공용: 해당 슬롯의 스킬이 이미 준비 또는 실행 중임</summary>
    AlreadyInUse = 6,
    /// <summary>공용: 슬롯의 쿨다운이 아직 끝나지 않았음</summary>
    Cooldown = 7,
    /// <summary>Tear: 스킬 사용에 필요한 조각 수가 부족함</summary>
    InsufficientFragments = 8,
    /// <summary>Shrink, RestoreForm: 현재 캐릭터 형태에서는 해당 스킬을 사용할 수 없음</summary>
    /// <remarks>테스트 목적 이외, 정상적인 게임 플레이에서 해당 사유가 나오면 안됨</remarks>
    InvalidForm = 9,
    /// <summary>RestoreForm: 껍질을 먼저 회수해야 하는 조건을 만족하지 않았음</summary>
    DebrisNotRecovered = 10,
    /// <summary>RestoreForm: 기본 형태로 전환할 공간이 부족함</summary>
    NotEnoughSpace = 11
}