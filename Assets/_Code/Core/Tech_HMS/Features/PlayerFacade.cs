using System;
using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterMovement))]
public sealed class PlayerFacade : MonoBehaviour, IDamageable, IKnockbackable
{
    [Header("Initial Health")]
    [SerializeField, Min(1)] private int maxHP = 3;
    [SerializeField, Min(0)] private int currentHP = 3;
    [SerializeField] private bool invincible;

    [Header("Future Feature Settings")]
    [SerializeField, Min(0)] private int currentSkillFragment;
    [Tooltip("스킬 1회에 필요한 조각 수. 동시에, 최대 보유량을 의미합니다.")]
    [SerializeField, Min(1)] private int skillChargePerSkillFragment = 3;
    [Tooltip("남은 시간이 아닌 스킬 쿨다운 설정값(초)입니다.")]
    [SerializeField, Min(0f)] private float skillCooldown = 1f;
    [Tooltip("남은 시간이 아닌 형태 전환 쿨다운 설정값(초)입니다.")]
    [SerializeField, Min(0f)] private float formChangeCooldown = 1f;

    private PlayerModel model;
    private CharacterMovement movement;
    
    public bool IsDead => Model.IsDead;

    /// <summary>
    /// 외부 요청으로 인한 PC의 조작(X, Z축 이동 및 점프)이 차된된 상태를 의미합니다.
    /// </summary>
    /// <remarks>
    /// 사망이나 비활성 상태 등을 모두 포함한 최종 입력 가능 여부를 의미하지 않으며, 이는 CanReceiveInput를 통해 확인해야 합니다.<para>
    /// 새 PC 인스턴스에서는 기본 false로 시작합니다.</para>
    /// </remarks>
    public bool IsInputBlocked { get; private set; }
    /// <summary>
    /// 외부 입력 차단 여부, 활성 상태, 생존 여부와 이동 컴포넌트의 활성 상태 등을 종합적으로 확인하고 이동 입력 수신 여부를 결정합니다.
    /// </summary>
    public bool CanReceiveInput => isActiveAndEnabled && !IsInputBlocked && !IsDead && movement.isActiveAndEnabled;
    
    public IReadOnlyPlayerModel Model
    {
        get
        {
            EnsureInitialized();
            return model;
        }
    }
    
    public bool AllowDepthMovement
    {
        get
        {
            EnsureInitialized();
            return movement.AllowDepthMovement;
        }
    }
    public Vector3 CurrentVelocity
    {
        get
        {
            EnsureInitialized();
            return movement.Velocity;
        }
    }
    
    private void Awake()
    {
        EnsureInitialized();
    }

    /// <summary>UI 등 외부 접근자가 만약 초기화 시점 이전에 접근할 경우를 대비</summary>
    private void EnsureInitialized()
    {
        if (model != null) return;
        
        OnValidate();
        movement = GetComponent<CharacterMovement>();
        model = new PlayerModel(currentHP, maxHP, currentSkillFragment, skillChargePerSkillFragment, skillCooldown, formChangeCooldown);
    }

    /// <summary>활성화 및 생존 상태를 검사하고 Model에 피해를 적용합니다. 피격 무적 시간은 자동 생성하지 않습니다.</summary>
    /// <param name="damage">요청 피해량. 0 이하는 무시합니다.</param>
    /// <returns>실제 감소한 HP.</returns>
    public int Damage(int damage)
    {
        EnsureInitialized();
        if (!isActiveAndEnabled || model.IsDead || IsInvincible())
        {
            return 0;
        }
        int applied = model.ApplyDamage(damage);
        if (model.IsDead)
        {
            ClearInput();
        }
        return applied;
    }

    /// <summary>명시적으로 설정한 무적 여부를 반환</summary>
    public bool IsInvincible() => invincible;

    /// <summary>외부 시스템이 무적을 설정</summary>
    /// <param name="value">무적 활성 여부</param>
    public void SetInvincible(bool value) => invincible = value;

    /// <summary>현재 운동을 교체하는 넉백을 이동 담당에 전달함.</summary>
    /// <param name="knockbackVelocity">월드 기준 초기 속도(m/s). 현재 속도를 빼지 않은 값.</param>
    /// <param name="controlLockDuration">공용 IKnockbackable 계약의 밀리초(ms). 이동 담당에 전달할 때 초로 변환됩니다.</param>
    public void ApplyKnockback(Vector3 knockbackVelocity, float controlLockDuration)
    {
        EnsureInitialized();

        if (!isActiveAndEnabled || model.IsDead || !movement.isActiveAndEnabled)
        {
            return;
        }

        movement.ApplyKnockback(knockbackVelocity, controlLockDuration / 1000f);
    }

    /// <summary>입력 가능할 때 월드 이동 입력을 전달하고, 불가능하면 남은 이동 입력을 해제합니다.</summary>
    /// <param name="input">월드 XZ 방향. 사이드 이동 모드의 Z 성분은 제거합니다.</param>
    public void SetMoveInput(Vector3 input)
    {
        EnsureInitialized();
        if (!movement.AllowDepthMovement)
        {
            input.z = 0f;
        }
        movement.SetMoveInput(CanReceiveInput ? input : Vector3.zero);
    }

    /// <summary>입력 가능하면 점프를 요청합니다. 실제 접지·넉백 잠금 검사는 이동 담당이 수행합니다.</summary>
    public void RequestJump()
    {
        EnsureInitialized();
        if (CanReceiveInput)
        {
            movement.RequestJump();
        }
    }

    /// <summary>남은 이동 입력과 미실행 점프 요청을 지움. (외력과 중력은 유지)</summary>
    public void ClearInput()
    {
        if (movement == null) return;
        movement.SetMoveInput(Vector3.zero);
        movement.CancelJumpRequest();
    }

    /// <summary>HP를 회복하고 실제 회복량을 반환합니다. HP 0에서도 회복 가능하지만 위치 이동·리스폰은 수행하지 않습니다.</summary>
    /// <param name="amount">회복 요청량.</param>
    public int Heal(int amount)
    {
        EnsureInitialized();
        return model.Heal(amount);
    }

    /// <summary>최대 HP를 변경합니다. 감소 시 현재 HP도 유효 범위로 제한합니다.</summary>
    /// <param name="value">1 이상의 최대 HP.</param>
    public void SetMaxHP(int value)
    {
        EnsureInitialized();
        model.SetMaxHP(value);
    }

    /// <summary>스킬 조각을 지정된 개수만큼 추가합니다.</summary>
    /// <remarks>
    /// 반환값은 실제로 추가된 개수를 의미하기 때문에, 이미 최대 상한만큼 보유한 경우 반환값은 항상 0입니다.
    /// </remarks>
    /// <param name="amount">추가할 조각 수. 0 이하는 무시합니다.</param>
    /// <returns>실제로 추가한 조각 수.</returns>
    public int AddSkillFragments(int amount)
    {
        EnsureInitialized();
        return model.AddSkillFragments(amount);
    }

    /// <summary>향후 스킬 사용 담당이 사용할 조각 소비 창구입니다. 데이터 차감만 수행합니다.</summary>
    /// <param name="amount">소비할 양수 수량.</param>
    public bool TryConsumeSkillFragments(int amount)
    {
        EnsureInitialized();
        return model.TryConsumeSkillFragments(amount);
    }

    /// <summary>스킬 필요 조각 수와 쿨다운 설정값을 변경합니다.<br/>
    /// 필요 조각 수가 감소했을 때 보유 조각 수가 그를 초과한다면 보유 조각 수 또한 변경된 상한으로 제한됩니다.</summary>
    /// <param name="fragmentsPerCharge">스킬 1회에 필요한 조각 수.</param>
    /// <param name="cooldownSeconds">스킬 쿨다운 설정값(단위: s).</param>
    public void ConfigureSkill(int fragmentsPerCharge, float cooldownSeconds)
    {
        EnsureInitialized();
        model.ConfigureSkill(fragmentsPerCharge, cooldownSeconds);
    }

    /// <summary>향후 형태 전환 담당이 읽을 쿨다운 설정값을 변경합니다.</summary>
    /// <param name="seconds">설정값(단위: s).</param>
    public void SetFormChangeCooldown(float seconds)
    {
        EnsureInitialized();
        model.SetFormChangeCooldown(seconds);
    }

    /// <summary>
    /// 플레이어의 이동·점프 조작 입력을 차단하거나 해제한다.
    /// 중력·외력과 UI 닫기 입력은 중단하지 않는다.
    /// </summary>
    /// <param name="blocked"><c>true</c>: 입력 차단<br/><c>false</c>: 입력 차단 해제.</param>
    public void SetInputBlocked(bool blocked)
    {
        EnsureInitialized();

        IsInputBlocked = blocked;

        if (blocked)
        {
            ClearInput();
        }
    }
    
    #region Obsolete: 요청자 구분 이동 제한 함수
    // TODO: 여러 요청자의 독립적인 입력 차단 구분이 필요해지면 사용 검토
    // (현재 요구 사항은 단일 흐름제어만 지원해도 충분)
    // 복원 시, IsInputBlocked를 요청자 집합의 Count > 0 로 변경해야 함.
    
    // [System.Obsolete("추후 입력 제한 요청에 요청자 구분이 필요해질 경우 사용", true)]
    // private readonly HashSet<MovementInputBlockToken> movementInputBlockRequesters = new HashSet<MovementInputBlockToken>();
    // /// <summary>
    // /// 요청자 식별용 전용 토큰을 받고, 이동 및 점프 입력 차단 여부를 설정합니다.<br/>
    // /// 하나 이상의 요청자가 차단을 유지하는 동안 PC의 X, Z축 이동 입력과 점프 입력을 허용하지 않습니다.<br/>
    // /// (외부에서 가해지는 물리적 힘을 제한하지는 않습니다.)
    // /// </summary>
    // /// <param name="requester">
    // /// 요청자를 구분하는 전용 토큰입니다. 차단과 해제 시 동일한 인스턴스를 전달해야 합니다.<br/>
    // /// (매 호출마다 new를 하지 않도록 주의해야 합니다.)
    // /// </param>
    // /// <param name="blocked">
    // /// true이면 해당 요청자의 차단을 등록하고, false이면 해당 요청자의 차단만 해제합니다.<br/>
    // /// 동일한 요청자가 기존 상태와 동일한 요청을 반복 전달할 경우 아무런 처리도 하지 않습니다.
    // /// </param>
    // /// <remarks>
    // /// 같은 요청자의 true 반복 호출은 중복 등록되지 않습니다.<br/>
    // /// 등록되지 않은 요청자의 false 호출은 다른 요청자의 차단에 영향을 주지 않습니다.<br/>
    // /// true 호출 시 남은 이동 입력과 미실행 점프 요청을 즉시 해제합니다.<br/>
    // /// 기존 속도, 중력, 외력, 발판 운반 및 피해·회복 처리는 중단하지 않습니다.<br/>
    // /// 요청자가 종료될 때 자신의 차단을 해제해야 하며 자동 해제되지는 않습니다.
    // /// </remarks>
    // /// <exception cref="ArgumentNullException">requester가 null인 경우.</exception>
    // [System.Obsolete("추후 입력 제한 요청에 요청자 구분이 필요해질 경우 사용", true)]
    // public void SetInputBlocked(MovementInputBlockToken requester, bool blocked)
    // {
    //     if (requester is null) throw new ArgumentNullException(nameof(requester));
    //     EnsureInitialized();
    //     
    //     if (blocked)
    //     {
    //         if (!movementInputBlockRequesters.Add(requester)) return; // 동일한 요청자의 true 요청이 이미 있었을 경우 차단 상태도 동일하며, 이동 입력 정리 X
    //         
    //         // 새로운 차단 요청일 경우 이동 입력을 정리
    //         ClearInput();
    //     }
    //     else
    //     {
    //         movementInputBlockRequesters.Remove(requester); // 동일한 요청자의 false 요청이 계속 들어올 경우의 처리가 별도로 필요할 경우 반환값을 활용할 것
    //     }
    // }
    #endregion
    
    /// <summary>Inspector 초기값을 유효 범위로 보정</summary>
    private void OnValidate()
    {
        maxHP = Mathf.Max(1, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        skillChargePerSkillFragment = Mathf.Max(1, skillChargePerSkillFragment);
        currentSkillFragment = Mathf.Clamp(currentSkillFragment, 0, skillChargePerSkillFragment);
        skillCooldown = float.IsFinite(skillCooldown) ? Mathf.Max(0f, skillCooldown) : 0f;
        formChangeCooldown = float.IsFinite(formChangeCooldown) ? Mathf.Max(0f, formChangeCooldown) : 0f;
    }
    
    /// <summary>
    /// Facade 비활성화 시 남은 이동 입력과 미실행 점프 요청을 해제합니다.<br/>
    /// 기존 속도와 외력은 직접 제거하지 않습니다.
    /// </summary>
    /// <remarks>
    /// Facade 자체를 비활성화하는 직접적인 상황은 없도록 할 예정이며, 혹시 모를 상황을 대비하기 위한 용도입니다.
    /// </remarks>
    private void OnDisable()
    {
        ClearInput();
    }
}
