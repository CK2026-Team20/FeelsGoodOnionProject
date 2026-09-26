using System;

public interface IReadOnlyPlayerModel
{
    int CurrentHP { get; }
    int MaxHP { get; }
    int CurrentSkillFragment { get; }
    /// <summary>스킬 1회 사용에 필요한 조각 수<br/>
    /// 실제 사용 처리는 추후 스킬 담당이 수행해야 함.</summary>
    int SkillChargePerSkillFragment { get; }
    /// <summary>스킬 사용 시 적용될 쿨다운<br/>
    /// 현재 쿨다운은 스킬 사용을 제어하는 곳에서 관리해야 함.<br/>
    /// (단위: s)</summary>
    float SkillCooldown { get; }
    /// <summary>형태 전환 시 적용될 쿨다운<br/>
    /// 현재 쿨다운은 형태 전환을 제어하는 곳에서 관리해야 함.<br/>
    /// (단위: s)</summary>
    float FormChangeCooldown { get; }
    bool IsDead { get; }

    event Action<int, int> HealthChanged;
    event Action<int> SkillFragmentChanged;
    event Action<int, float> SkillSettingsChanged;
    event Action<float> FormChangeCooldownChanged;
}

/// <summary>
/// 플레이어의 체력, 스킬 조각 및 향후 기능의 설정값을 소유하는 클래스.
/// 실제 스킬, 형태 전환, 남은 쿨다운 및 Unity 오브젝트는 관리하지 않음.
/// </summary>
public sealed class PlayerModel : IReadOnlyPlayerModel
{
    public int CurrentHP { get; private set; }
    public int MaxHP { get; private set; }
    public int CurrentSkillFragment { get; private set; }
    public int SkillChargePerSkillFragment { get; private set; }
    public float SkillCooldown { get; private set; }
    public float FormChangeCooldown { get; private set; }

    public bool IsDead => CurrentHP == 0;

    public event Action<int, int> HealthChanged;
    public event Action<int> SkillFragmentChanged;
    public event Action<int, float> SkillSettingsChanged;
    public event Action<float> FormChangeCooldownChanged;

    /// <summary>초기값을 검증해 저장. 생성 시 변경 이벤트는 발생하지 않습니다.</summary>
    /// <param name="currentHP">0 이상 최대 HP 이하인 초기 체력.</param>
    /// <param name="maxHP">1 이상의 최대 체력.</param>
    /// <param name="currentSkillFragment">0 이상의 초기 조각 수.</param>
    /// <param name="skillChargePerSkillFragment">스킬 1회에 필요한 조각 수. 1 이상.</param>
    /// <param name="skillCooldown">스킬 쿨다운 설정값(초). 유한한 0 이상 값.</param>
    /// <param name="formChangeCooldown">형태 전환 쿨다운 설정값(초). 유한한 0 이상 값.</param>
    public PlayerModel(int currentHP, int maxHP, int currentSkillFragment, int skillChargePerSkillFragment, float skillCooldown, float formChangeCooldown)
    {
        if (maxHP < 1 || currentHP < 0 || currentHP > maxHP)
        {
            throw new ArgumentOutOfRangeException(nameof(currentHP), "최대 HP는 1 이상이며 현재 HP는 0부터 최대 HP 사이여야 합니다.");
        }
        if (currentSkillFragment < 0 || skillChargePerSkillFragment < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentSkillFragment), "조각 수는 0 이상, 필요 조각 수는 1 이상이어야 합니다.");
        }
        ValidateCooldown(skillCooldown, nameof(skillCooldown));
        ValidateCooldown(formChangeCooldown, nameof(formChangeCooldown));
        CurrentHP = currentHP;
        MaxHP = maxHP;
        CurrentSkillFragment = currentSkillFragment;
        SkillChargePerSkillFragment = skillChargePerSkillFragment;
        SkillCooldown = skillCooldown;
        FormChangeCooldown = formChangeCooldown;
    }

    public int ApplyDamage(int amount)
    {
        int applied = Math.Min(Math.Max(0, amount), CurrentHP);
        if (applied == 0)
        {
            return 0;
        }
        CurrentHP -= applied;
        HealthChanged?.Invoke(CurrentHP, MaxHP);
        return applied;
    }

    public int Heal(int amount)
    {
        int applied = Math.Min(Math.Max(0, amount), MaxHP - CurrentHP);
        if (applied == 0)
        {
            return 0;
        }
        CurrentHP += applied;
        HealthChanged?.Invoke(CurrentHP, MaxHP);
        return applied;
    }

    public void SetMaxHP(int value)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        if (MaxHP == value)
        {
            return;
        }
        MaxHP = value;
        CurrentHP = Math.Min(CurrentHP, MaxHP);
        HealthChanged?.Invoke(CurrentHP, MaxHP);
    }

    public int AddSkillFragments(int amount)
    {
        int added = Math.Min(Math.Max(0, amount), int.MaxValue - CurrentSkillFragment);
        if (added == 0)
        {
            return 0;
        }
        CurrentSkillFragment += added;
        SkillFragmentChanged?.Invoke(CurrentSkillFragment);
        return added;
    }

    public bool TryConsumeSkillFragments(int amount)
    {
        if (amount <= 0 || amount > CurrentSkillFragment)
        {
            return false;
        }
        CurrentSkillFragment -= amount;
        SkillFragmentChanged?.Invoke(CurrentSkillFragment);
        return true;
    }

    public void ConfigureSkill(int fragmentsPerCharge, float cooldownSeconds)
    {
        if (fragmentsPerCharge < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(fragmentsPerCharge));
        }
        ValidateCooldown(cooldownSeconds, nameof(cooldownSeconds));
        if (SkillChargePerSkillFragment == fragmentsPerCharge && SkillCooldown.Equals(cooldownSeconds))
        {
            return;
        }
        SkillChargePerSkillFragment = fragmentsPerCharge;
        SkillCooldown = cooldownSeconds;
        SkillSettingsChanged?.Invoke(SkillChargePerSkillFragment, SkillCooldown);
    }

    public void SetFormChangeCooldown(float seconds)
    {
        ValidateCooldown(seconds, nameof(seconds));
        if (FormChangeCooldown.Equals(seconds))
        {
            return;
        }
        FormChangeCooldown = seconds;
        FormChangeCooldownChanged?.Invoke(seconds);
    }

    private static void ValidateCooldown(float value, string parameterName)
    {
        if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
