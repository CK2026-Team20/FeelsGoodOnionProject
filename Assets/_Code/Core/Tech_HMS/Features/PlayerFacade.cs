using System;
using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterMovement))]
[RequireComponent(typeof(PlayerSkillController))]
[RequireComponent(typeof(PlayerFormController))]
public sealed class PlayerFacade : MonoBehaviour, IDamageable, IKnockbackable
{
    [Header("Initial Health")]
    [SerializeField, Min(1)] private int maxHP = 3;
    [SerializeField, Min(0)] private int initialHP = 3;
    [SerializeField] private bool invincible;

    [Header("Future Feature Settings")]
    [SerializeField, Min(0)] private int initialSkillFragment;
    
    [Header("Skill Settings"), Tooltip("사전 생성된 ScriptableObject를 할당")]
    [SerializeField] private TearSkillDefinition tearSkillDefinition;
    [SerializeField] private ShrinkSkillDefinition shrinkSkillDefinition;
    [SerializeField] private RestoreFormSkillDefinition restoreFormSkillDefinition;

    private ShrinkSkill shrinkSkill;
    private RestoreFormSkill restoreFormSkill;
    
    private PlayerSkillController skillController;

    private const int SkillSlotCount = 2;
    private const int FormSlotIndex = 0;
    private const int TearSlotIndex = 1;
    private bool formSkillRefreshPending;

    private PlayerModel model;
    private CharacterMovement movement;
    private PlayerFormController formController;
    
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
        formController = GetComponent<PlayerFormController>();
        int maxSkillFragment = tearSkillDefinition != null ? tearSkillDefinition.RequiredFragments : 0;
        model = new PlayerModel(initialHP, maxHP, initialSkillFragment, maxSkillFragment);
        InitializeSkills();
    }
    
    /// <summary>스킬 슬롯을 초기화하고 시작 스킬을 생성 및 장착</summary>
    private void InitializeSkills()
    {
        skillController = GetComponent<PlayerSkillController>();

        skillController.Initialize(SkillSlotCount, CheckCommonSkillCondition);

        if (tearSkillDefinition != null)
        {
            TearSkill tearSkill = new TearSkill(tearSkillDefinition, model, transform);
            skillController.Equip(TearSlotIndex, tearSkill);
        }
        else
        {
            Debug.LogWarning("눈물 스킬 설정이 없어 해당 슬롯을 비워둡니다.", this);
        }

        if (shrinkSkillDefinition == null || restoreFormSkillDefinition == null)
        {
            Debug.LogWarning("축소 또는 복귀 스킬 설정이 없어 형태 전환 슬롯을 비워둡니다.", this);
            return;
        }

        shrinkSkill = new ShrinkSkill(shrinkSkillDefinition, formController);
        restoreFormSkill = new RestoreFormSkill(restoreFormSkillDefinition, formController);
        formController.FormChanged += HandleFormChanged;

        formSkillRefreshPending = true;
        RefreshFormSkill();
    }
    
    /// <summary>플레이어 상태에 따른 공통 스킬 사용 조건을 검사하는 함수<br/>
    /// SkillController 초기화 시점에 주입해주어야 함</summary>
    private PlayerSkillBlockReason CheckCommonSkillCondition()
    {
        if (!isActiveAndEnabled || model == null || movement == null || !movement.isActiveAndEnabled) return PlayerSkillBlockReason.Unavailable;
        if (model.IsDead) return PlayerSkillBlockReason.Dead;
        if (movement.IsMovementLocked) return PlayerSkillBlockReason.ControlLocked;

        return PlayerSkillBlockReason.None;
    }
    
    /// <summary>
    /// 형태 변경 시 슬롯 교체를 예약<br/>
    /// 실행 중인 스킬을 이벤트 안에서 즉시 해제하지 않습니다.
    /// </summary>
    private void HandleFormChanged(PlayerForm form)
    {
        formSkillRefreshPending = true;
    }

    /// <summary>
    /// 기존 스킬의 실행이 끝났다면 현재 형태에 맞는 스킬을 장착합니다.
    /// </summary>
    private void RefreshFormSkill()
    {
        if (!formSkillRefreshPending ||
            skillController == null ||
            !skillController.IsInitialized ||
            shrinkSkill == null ||
            restoreFormSkill == null)
        {
            return;
        }

        PlayerSkill currentSkill = skillController.GetEquippedSkill(FormSlotIndex);

        if (currentSkill != null && currentSkill.IsInUse) return;

        PlayerSkill nextSkill = formController.IsSmall ? (PlayerSkill)restoreFormSkill : shrinkSkill;

        if (!ReferenceEquals(currentSkill, nextSkill))
        {
            skillController.Equip(FormSlotIndex, nextSkill);
        }

        formSkillRefreshPending = false;
    }

    private void LateUpdate()
    {
        RefreshFormSkill();
    }

    /// <summary>활성화 및 생존 상태를 검사하고 Model에 피해를 적용합니다. 피격 무적 시간은 자동 생성하지 않습니다.</summary>
    /// <param name="damage">요청 피해량. 0 이하는 무시합니다.</param>
    /// <returns>실제 감소한 HP.</returns>
    public int Damage(int damage)
    {
        EnsureInitialized();
        if (!isActiveAndEnabled || model.IsDead || IsInvincible()) return 0;
        
        int applied = model.ApplyDamage(damage);
        if (model.IsDead)
        { 
            ClearInput();
            skillController.CancelAll();
        }
        
        return applied;
    }
    
    /// <summary>플레이어 공통 조건과 거리 조건을 확인하고 껍질을 회수</summary>
    public bool TryRecoverDebris()
    {
        EnsureInitialized();

        if (CheckCommonSkillCondition() != PlayerSkillBlockReason.None)
        {
            return false;
        }

        return formController.TryRecoverNearbyDebris();
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
    
    /// <summary>현재 형태에 대응하는 축소 또는 복귀 스킬 사용을 요청합니다.</summary>
    public bool TryUseFormChangeSkill(out PlayerSkillBlockReason blockReason)
    {
        EnsureInitialized();
        // 이전 전환의 교체 예약이 남아 있다면 먼저 반영
        RefreshFormSkill();
        return skillController.TryUse(FormSlotIndex, out blockReason);
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
    
    /// <summary>눈물 스킬 사용을 요청합니다.</summary>
    /// <returns>사용 요청을 수락했는지 여부</returns>
    public bool TryUseTearSkill(out PlayerSkillBlockReason blockReason)
    {
        EnsureInitialized();

        return skillController.TryUse(TearSlotIndex, out blockReason);
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
        initialHP = Mathf.Clamp(initialHP, 0, maxHP);
        
        int maxSkillFragment = tearSkillDefinition != null ? Mathf.Max(0, tearSkillDefinition.RequiredFragments) : 0;
        initialSkillFragment = Mathf.Clamp(initialSkillFragment, 0, maxSkillFragment);
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
        skillController?.CancelAll();
    }

    /// <summary>PC 선택 시 눈물 스킬의 효과 범위를 표시</summary>
    private void OnDrawGizmos()
    {
        if (tearSkillDefinition == null) return;

        Color previousColor = Gizmos.color;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, tearSkillDefinition.EffectRadius);
        Gizmos.color = previousColor;
    }
    
    private void OnDestroy()
    {
        if (formController != null)
        {
            formController.FormChanged -= HandleFormChanged;
        }
    }
}
