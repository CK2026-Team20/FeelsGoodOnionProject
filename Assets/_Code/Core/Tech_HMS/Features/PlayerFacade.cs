using UnityEngine;

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
    [Tooltip("스킬 1회에 필요한 조각 수. 현재는 설정값만 저장합니다.")]
    [SerializeField, Min(1)] private int skillChargePerSkillFragment = 3;
    [Tooltip("남은 시간이 아닌 스킬 쿨다운 설정값(초)입니다.")]
    [SerializeField, Min(0f)] private float skillCooldown = 1f;
    [Tooltip("남은 시간이 아닌 형태 전환 쿨다운 설정값(초)입니다.")]
    [SerializeField, Min(0f)] private float formChangeCooldown = 1f;

    private PlayerModel model;
    private CharacterMovement movement;
    
    public IReadOnlyPlayerModel Model
    {
        get
        {
            EnsureInitialized();
            return model;
        }
    }

    public bool IsDead => Model.IsDead;
    public bool CanReceiveInput => isActiveAndEnabled && !IsDead && movement.isActiveAndEnabled;
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
        if (!CanReceiveInput)
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

    /// <summary>스킬 조각 데이터를 추가합니다. 스킬 충전·발동은 하지 않습니다.</summary>
    /// <param name="amount">추가량.</param>
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

    /// <summary>스킬 필요 조각 수와 쿨다운 설정값을 변경합니다.</summary>
    /// <param name="fragmentsPerCharge">스킬 1회에 필요한 조각 수.</param>
    /// <param name="cooldownSeconds">스킬 쿨다운 설정값(초).</param>
    public void ConfigureSkill(int fragmentsPerCharge, float cooldownSeconds)
    {
        EnsureInitialized();
        model.ConfigureSkill(fragmentsPerCharge, cooldownSeconds);
    }

    /// <summary>향후 형태 전환 담당이 읽을 쿨다운 설정값을 변경합니다.</summary>
    /// <param name="seconds">설정값(초).</param>
    public void SetFormChangeCooldown(float seconds)
    {
        EnsureInitialized();
        model.SetFormChangeCooldown(seconds);
    }

    /// <summary>Inspector 초기값을 유효 범위로 보정</summary>
    private void OnValidate()
    {
        maxHP = Mathf.Max(1, maxHP);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        currentSkillFragment = Mathf.Max(0, currentSkillFragment);
        skillChargePerSkillFragment = Mathf.Max(1, skillChargePerSkillFragment);
        skillCooldown = float.IsFinite(skillCooldown) ? Mathf.Max(0f, skillCooldown) : 0f;
        formChangeCooldown = float.IsFinite(formChangeCooldown) ? Mathf.Max(0f, formChangeCooldown) : 0f;
    }
}
