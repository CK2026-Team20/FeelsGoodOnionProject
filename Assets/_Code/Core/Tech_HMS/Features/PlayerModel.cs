using System;

public interface IReadOnlyPlayerModel
{
    int CurrentHP { get; }
    int MaxHP { get; }
    /// <summary>
    /// 현재 보유한 스킬 조각 수
    /// </summary>
    /// <remarks>
    /// PC가 '보유'하는 데이터이기에 현재 PlayerModel에 포함시켰으나, 추후 Skill 관련 구현에 따라 SkillModel로 분리가 필요하다고 판단될 경우 이동될 수 있음.
    /// </remarks>
    int CurrentSkillFragment { get; }
    /// <summary>
    /// 스킬 1회 사용에 필요한 조각 수.<br/>
    /// 동시에 최대 보유량을 의미합니다.
    /// </summary>
    /// <remarks>
    /// 실제 사용 처리는 추후 스킬 담당이 수행해야 함.
    /// </remarks>
    int MaxSkillFragment { get; }
    /// <summary>
    /// 캐릭터의 '현재 체력'이 0인 상태를 의미함.<br/>
    /// 추후 FSM 도입 시 수정되거나 제거될 수 있음.
    /// </summary>
    bool IsDead { get; }
    /// <summary>체력 변경 시 현재 체력과 최대 체력을 순서대로 전달합니다.</summary>
    event Action<int, int> HealthChanged;
    /// <summary>보유 조각 수 변경 시 변경 후 총보유량을 전달합니다.</summary>
    event Action<int> SkillFragmentChanged;
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
    public int MaxSkillFragment { get; private set; }
    public float SkillCooldown { get; private set; }
    public float FormChangeCooldown { get; private set; }

    public bool IsDead => CurrentHP == 0;
    
    /// <summary>체력 변경 시 현재 체력과 최대 체력을 순서대로 전달합니다.</summary>
    public event Action<int, int> HealthChanged;

    /// <summary>보유 조각 수 변경 시 변경 후 총보유량을 전달합니다.</summary>
    public event Action<int> SkillFragmentChanged;

    /// <summary>초기값을 검증해 저장. 생성 시 변경 이벤트는 발생하지 않습니다.</summary>
    /// <param name="currentHP">0 이상 최대 HP 이하인 초기 체력.</param>
    /// <param name="maxHP">1 이상의 최대 체력.</param>
    /// <param name="currentSkillFragment">초기 보유 조각 수. 0 이상이어야 하며 스킬 1회 필요량을 초과할 경우 자동으로 값이 제한됩니다.</param>
    /// <param name="skillChargePerSkillFragment">스킬 1회에 필요한 조각 수. 1 이상.</param>
    /// <param name="skillCooldown">스킬 쿨다운 설정값(단위 :s). 유한한 0 이상 값.</param>
    /// <param name="formChangeCooldown">형태 전환 쿨다운 설정값(단위: s). 유한한 0 이상 값.</param>
    public PlayerModel(int currentHP, int maxHP, int currentSkillFragment, int maxSkillFragment)
    {
        if (maxHP < 1) throw new ArgumentOutOfRangeException(nameof(maxHP), maxHP, "최대 체력은 1 이상이어야 합니다.");
        if (currentHP < 0 || currentHP > maxHP) throw new ArgumentOutOfRangeException(nameof(currentHP), currentHP, "현재 체력은 0부터 최대 체력 사이여야 합니다.");
        if (currentSkillFragment < 0) throw new ArgumentOutOfRangeException(nameof(currentSkillFragment), currentSkillFragment, "보유 조각 수는 0 이상이어야 합니다.");
        if (maxSkillFragment < 0) throw new ArgumentOutOfRangeException(nameof(maxSkillFragment), "최대 보유 조각 수는 0 이상이어야 합니다.");
        
        CurrentHP = currentHP;
        MaxHP = maxHP;
        MaxSkillFragment = maxSkillFragment;
        CurrentSkillFragment = currentSkillFragment < MaxSkillFragment ? currentSkillFragment : MaxSkillFragment;
    }
    /// <summary>
    /// 현재 체력을 요청량만큼 감소시키고, 실제 변경된 경우 HealthChanged 이벤트를 발생시킵니다.
    /// 무적 여부와 사망 처리는 호출하는 측에서 판단합니다.
    /// </summary>
    /// <param name="amount">피해량. 0 이하는 무시하며 현재 체력을 초과한 양은 적용하지 않습니다.</param>
    /// <returns>실제로 감소한 체력.</returns>
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
    /// <summary>
    /// 현재 체력을 제공된 값 만큼 회복하며, 최대 체력을 초과할 수는 없습니다.<br/>
    /// 현재 체력이 실제 변경된 경우 HealthChanged 이벤트를 발생시킵니다.
    /// </summary>
    /// <remarks>
    /// 현재 체력이 0인 경우에도 회복을 제한하는 별도의 처리는 현재 없습니다.
    /// 사망 이후 회복 허용 여부와 부활 처리는 호출하는 측에서 결정해야 합니다.<para>
    /// 추후, 피해와 회복을 공통 처리할 필요가 생기면(또는 구분의 의미가 없다고 판단되면) ApplyDamage와 함께 하나의 체력 변경 함수로 병합을 검토할 수 있습니다.</para>
    /// </remarks>
    /// <param name="amount">회복량. 0 이하는 무시합니다.</param>
    /// <returns>실제로 회복한 체력.</returns>
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
    /// <summary>
    /// 최대 체력을 변경하고 HealthChanged 이벤트를 발생시킵니다.
    /// 최대 체력이 감소하면 현재 체력도 해당 범위로 제한하며, 증가하더라도 자동 회복하지 않습니다.
    /// 기존 최대 체력과 같으면 변경하지 않습니다.
    /// </summary>
    /// <param name="value">새로운 최대 체력. 1 이상이어야 합니다.</param>
    /// <exception cref="ArgumentOutOfRangeException">value가 1 미만인 경우.</exception>
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
    /// <summary>
    /// 스킬 조각을 제공된 값 만큼 추가하며, 1회 사용에 필요한 수량을 초과할 수는 없습니다.<br/>
    /// 현재 보유 조각 개수가 실제로 변경된 경우 SkillFragmentChanged 이벤트를 발생시킵니다.
    /// </summary>
    /// <param name="amount">추가할 조각 수. 0 이하는 무시합니다.</param>
    /// <returns>실제로 추가한 조각 수. (이미 상한에 도달했다면 항상 0)</returns>
    /// <remarks>
    /// 외부에서 스킬 조각 추가 성공 여부를 알아야 할 경우, 해당 함수의 반환값이 0인지 확인해야 합니다.
    /// </remarks>
    public int AddSkillFragments(int amount)
    {
        int added = Math.Min(Math.Max(0, amount), MaxSkillFragment - CurrentSkillFragment);
        if (added == 0)
        {
            return 0;
        }

        CurrentSkillFragment += added;
        SkillFragmentChanged?.Invoke(CurrentSkillFragment);
        return added;
    }
    /// <summary>
    /// 보유한 스킬 조각이 충분하면 요청량을 차감하고 SkillFragmentChanged 이벤트를 발생시킵니다.<br/>
    /// 스킬 발동 조건과 쿨다운은 검사하지 않습니다.
    /// </summary>
    /// <param name="amount">소비할 조각 수. 양수여야 합니다.</param>
    /// <returns>차감에 성공하면 true, 요청량이 유효하지 않거나 보유량이 부족하면 false</returns>
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
}
