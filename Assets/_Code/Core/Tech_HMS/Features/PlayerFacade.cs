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
    private readonly PlayerStateMachine stateMachine = new PlayerStateMachine();
    private readonly PlayerSkillPresentation skillPresentation = new PlayerSkillPresentation();

    /// <summary>플레이어의 현재 행동 상태</summary>
    /// <remarks>FSM은 Facade가 생성하고 소유합니다. 초기화 전 접근 시 먼저 초기화합니다.</remarks>
    public PlayerState CurrentState
    {
        get
        {
            EnsureInitialized();
            return stateMachine.CurrentState;
        }
    }

    /// <summary>행동 상태가 변경된 뒤 이전 상태와 새 상태를 순서대로 전달합니다.</summary>
    public event Action<PlayerState, PlayerState> StateChanged;
    /// <summary>활성화된 생존 플레이어가 실제 점프를 실행한 물리 스텝의 끝에 발생합니다.</summary>
    public event Action Jumped;
    /// <summary>활성화된 생존 플레이어의 착지를 알립니다. 착지 애니메이션·파티클 연결에 사용합니다.</summary>
    public event Action Landed;
    /// <summary>체력이 실제로 감소한 뒤 적용된 피해량을 전달합니다.</summary>
    /// <remarks>치명적인 피해도 포함합니다. 피격 깜빡임 등에 사용하며 넉백은 별도 요청으로 처리합니다.</remarks>
    public event Action<int> Damaged;
    /// <summary>명시적인 부활에 성공하여 이동 정보·체력·FSM을 갱신한 뒤 발생합니다.</summary>
    public event Action Revived;
    /// <summary>실제 발동이 확정된 스킬의 종류를 전달합니다.</summary>
    /// <remarks>동시에 발동한 스킬도 각각 전달합니다. 애니메이션 우선순위는 SkillAnimationRequested에서 처리합니다.</remarks>
    public event Action<PlayerSkillId> SkillActivated;
    /// <summary>
    /// LateUpdate에서 재생할 스킬 애니메이션의 종류를 전달합니다.
    /// </summary>
    /// <remarks>
    /// 해당 갱신까지 모인 요청 중 눈물이 형태 전환보다 우선하며, 사망·넉백 제한 중에는 요청을 버립니다.<br/>
    /// 수신 측에서 현재 형태의 Animator에 연결해야 합니다.<br/>
    /// 애니메이션 재생 완료를 기다리지 않고 스킬 효과와 FSM 전환을 처리합니다.
    /// </remarks>
    public event Action<PlayerSkillId> SkillAnimationRequested;
    
    public bool IsDead => Model.IsDead;

    /// <summary>
    /// 외부 요청으로 PC의 이동·점프·스킬 조작이 차단된 상태를 의미합니다.
    /// </summary>
    /// <remarks>
    /// 사망이나 비활성 상태 등을 모두 포함한 최종 입력 가능 여부를 의미하지 않으며, 이는 CanReceiveInput를 통해 확인해야 합니다.<para>
    /// 새 PC 인스턴스에서는 기본 false로 시작합니다.</para>
    /// </remarks>
    public bool IsInputBlocked { get; private set; }
    /// <summary>
    /// 외부 입력 차단 여부, 활성 상태, 생존 여부와 이동 컴포넌트의 활성 상태 등을 종합적으로 확인하고 이동 입력 수신 여부를 결정합니다.
    /// </summary>
    public bool CanReceiveInput
    {
        get
        {
            EnsureInitialized();
            return isActiveAndEnabled && !IsInputBlocked && !model.IsDead &&
                movement.isActiveAndEnabled && !movement.IsControlLocked && stateMachine.AllowsControl;
        }
    }
    
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
        stateMachine.StateChanged += HandleStateChanged;
        model.HealthChanged += HandleHealthChanged;
        movement.MovementUpdated += RefreshState;
        movement.Jumped += HandleJumped;
        movement.Landed += HandleLanded;
        InitializeSkills();
        RefreshState();
    }
    
    /// <summary>스킬 슬롯을 초기화하고 시작 스킬을 생성 및 장착</summary>
    private void InitializeSkills()
    {
        skillController = GetComponent<PlayerSkillController>();
        skillController.SkillActivated += HandleSkillActivated;

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
        if (IsInputBlocked || movement.IsControlLocked || !stateMachine.AllowsControl) return PlayerSkillBlockReason.ControlLocked;

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

    /// <summary>형태 전환 슬롯을 갱신한 뒤 우선순위에 따라 스킬 애니메이션 재생 요청을 전달합니다.</summary>
    private void LateUpdate()
    {
        RefreshFormSkill();
        if (skillPresentation.TryTake(out PlayerSkillId skill) &&
            !model.IsDead && !movement.IsControlLocked)
        {
            SkillAnimationRequested?.Invoke(skill);
        }
    }

    /// <summary>현재 생명 상태와 이동 담당의 정보를 FSM에 반영합니다.</summary>
    /// <remarks>물리 스텝 완료·제어 잠금·체력 변경 시 호출합니다. 물리 계산이나 제한 시간을 중복 관리하지 않습니다.</remarks>
    private void RefreshState()
    {
        if (model == null) return;
        stateMachine.Refresh(model.IsDead, movement.IsControlLocked, movement.IsGrounded,
            movement.SelfHorizontalSpeed, movement.Velocity.y);
    }

    /// <summary>사망·넉백 진입 시 남은 입력과 재생 요청을 정리하고 상태 변경을 전달합니다.</summary>
    /// <param name="previousState">변경 전 행동 상태</param>
    /// <param name="currentState">변경 후 행동 상태</param>
    private void HandleStateChanged(PlayerState previousState, PlayerState currentState)
    {
        if (currentState == PlayerState.Dead || currentState == PlayerState.Knockback)
        {
            ClearInput();
            skillPresentation.Clear();
        }
        StateChanged?.Invoke(previousState, currentState);
    }

    /// <summary>체력 변경 시 사망에 필요한 정리를 수행하고 FSM을 갱신합니다.</summary>
    /// <param name="currentHP">변경 후 현재 체력. 실제 사망 여부는 Model에서 조회합니다.</param>
    /// <param name="maximumHP">변경 후 최대 체력. 이벤트 계약에 따라 수신하며 이 처리에서는 사용하지 않습니다.</param>
    private void HandleHealthChanged(int currentHP, int maximumHP)
    {
        if (model.IsDead)
        {
            ClearInput();
            skillController?.CancelAll();
            skillPresentation.Clear();
        }
        RefreshState();
    }

    /// <summary>플레이어가 활성화된 생존 상태이면 이동 담당의 점프 실행 이벤트를 전달합니다.</summary>
    private void HandleJumped()
    {
        if (isActiveAndEnabled && !model.IsDead) Jumped?.Invoke();
    }

    /// <summary>플레이어가 활성화된 생존 상태이면 이동 담당의 착지 이벤트를 전달합니다.</summary>
    private void HandleLanded()
    {
        if (isActiveAndEnabled && !model.IsDead) Landed?.Invoke();
    }

    /// <summary>스킬의 실제 발동을 전달하고, 우선순위를 적용할 애니메이션 요청을 등록합니다.</summary>
    /// <param name="skill">발동이 확정된 스킬의 종류</param>
    private void HandleSkillActivated(PlayerSkillId skill)
    {
        if (!isActiveAndEnabled) return;
        skillPresentation.Request(skill);
        SkillActivated?.Invoke(skill);
    }

    /// <summary>활성화 및 생존 상태를 검사하고 Model에 피해를 적용합니다. 피격 무적 시간은 자동 생성하지 않습니다.</summary>
    /// <param name="damage">요청 피해량. 0 이하는 무시합니다.</param>
    /// <returns>실제 감소한 HP.</returns>
    public int Damage(int damage)
    {
        EnsureInitialized();
        if (!isActiveAndEnabled || model.IsDead || IsInvincible()) return 0;
        
        int applied = model.ApplyDamage(damage);
        if (applied > 0) Damaged?.Invoke(applied);
        
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

    /// <summary>
    /// 입력이 가능하면 월드 이동 입력을 전달하고 불가능하면 남은 이동 입력을 해제.<br/>
    /// 이동 축 제한은 CharacterMovement에서 처리
    /// </summary>
    public void SetMoveInput(Vector3 input)
    {
        EnsureInitialized();
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

    /// <summary>생존 중 HP를 회복합니다. 사망 중에는 0을 반환하며 부활시키지 않습니다.</summary>
    /// <param name="amount">회복 요청량.</param>
    public int Heal(int amount)
    {
        EnsureInitialized();
        return model.Heal(amount);
    }

    /// <summary>
    /// 사망한 플레이어를 현재 위치에서 부활시킵니다.<br/>
    /// 이동 정보와 체력을 정리하고 FSM 갱신 후 Revived 이벤트를 발생시킵니다.
    /// </summary>
    /// <param name="health">부활 시 체력. 양수여야 하며 최대 HP를 초과하면 최대 HP로 제한합니다.</param>
    /// <param name="clearGround"><c>true</c>: 이전 접지·접촉 기록도 초기화<br/><c>false</c>: 현재 접지 정보 유지</param>
    /// <returns><c>true</c>: 부활에 성공함<br/><c>false</c>: 생존 중이거나 health가 0 이하임</returns>
    /// <remarks>
    /// 이전 속도·넉백·입력은 정리하며, 형태·조각·쿨다운·외부 입력 차단 설정은 유지합니다.<br/>
    /// 위치 이동은 리스폰 담당에서 수행합니다. 이동한 뒤 호출한다면 clearGround를 true로 지정해야 합니다.
    /// </remarks>
    public bool TryRevive(int health, bool clearGround = false)
    {
        EnsureInitialized();
        if (!model.IsDead || health <= 0) return false;

        skillController.CancelAll();
        skillPresentation.Clear();
        movement.ResetForRevive(clearGround);
        if (!model.TryRevive(health)) return false;
        RefreshState();
        Revived?.Invoke();
        return true;
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
    /// 플레이어의 이동·점프·스킬 조작 입력을 차단하거나 해제한다.
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
        skillPresentation.Clear();
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
        stateMachine.StateChanged -= HandleStateChanged;
        if (model != null) model.HealthChanged -= HandleHealthChanged;
        if (movement != null)
        {
            movement.MovementUpdated -= RefreshState;
            movement.Jumped -= HandleJumped;
            movement.Landed -= HandleLanded;
        }
        if (skillController != null) skillController.SkillActivated -= HandleSkillActivated;
        if (formController != null)
        {
            formController.FormChanged -= HandleFormChanged;
        }
    }
    
    /// <summary>
    /// 이동 모드에 따른 속도와 월드 X·Z축 이동 허용 여부를 적용.<br/>
    /// 형태별 점프 높이는 유지합니다.
    /// </summary>
    public void ApplyMovementModeSettings(float moveSpeed, bool allowHorizontalMovement, bool allowDepthMovement)
    {
        EnsureInitialized();
        movement.SetMoveSpeed(moveSpeed);
        movement.SetMovementAxesAllowed(allowHorizontalMovement, allowDepthMovement);
    }
}
