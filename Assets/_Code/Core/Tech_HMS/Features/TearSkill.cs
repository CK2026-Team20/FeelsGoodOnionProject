using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>PC를 중심으로 일정 거리 내 IStunnable 대상에게 스턴을 적용하는 스킬</summary>
public class TearSkill : PlayerSkill
{
    private readonly TearSkillDefinition definition;
    private readonly PlayerModel model;
    private readonly Transform owner;
    private readonly PlayerFormController formController;
    private Transform effectRoot;
    private Func<Vector3, Collider, IStunnable, bool> targetValidator;

    /// <summary>외부 조립이 루트 중심과 후보/직접 피격 정책을 선택합니다. 기본 구성은 기존 범위 스턴을 유지합니다.</summary>
    /// <param name="root">3D 구의 중심이며 자기 Collider를 제외할 플레이어 루트. 해제 시 null입니다.</param>
    /// <param name="validator">중심·후보 Collider·대상을 받아 직접 피격이 유효할 때만 true를 반환합니다. 해제 시 null입니다.</param>
    /// <remarks>후보 타입·센서 제외·벽 차단 Raycast는 주입자가 책임집니다. 실행 중 교체는 금지하며 해제 전 Cancel을 호출합니다. 빌린 객체는 해제하지 않습니다.</remarks>
    public void SetTargetValidation(Transform root, Func<Vector3, Collider, IStunnable, bool> validator)
    {
        if (IsInUse) throw new InvalidOperationException("진행 중인 눈물 스킬의 검증 정책은 교체할 수 없습니다.");
        if (validator != null && root == null) throw new ArgumentNullException(nameof(root));
        if (validator == null && root != null) throw new ArgumentException("루트와 검증 함수를 함께 연결하거나 함께 해제해야 합니다.", nameof(validator));
        effectRoot = root;
        targetValidator = validator;
    }

    private float remainingPreparationTime;
    
    public int RequiredFragments => definition.RequiredFragments;

    /// <summary>현재 형태에 따른 효과 반경입니다. 실제 발동 순간에 확정합니다.</summary>
    public float CurrentEffectRadius => definition.GetEffectRadius(
        formController != null ? formController.CurrentForm : PlayerForm.Normal);

    /// <param name="definition">눈물 스킬 설정</param>
    /// <param name="model">사용자의 플레이어 모델</param>
    /// <param name="owner">플레이어의 최상위 Transform입니다.
    /// 범위의 중심과, 자기 자신을 제외하는 기준으로 사용합니다.</param>
    /// <param name="formController">현재 형태의 소유자입니다. 생략하면 기본 형태의 반경을 사용합니다.</param>
    public TearSkill(TearSkillDefinition definition, PlayerModel model, Transform owner,
        PlayerFormController formController = null) : base(definition)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (owner == null) throw new ArgumentNullException(nameof(owner));

        this.definition = definition;
        this.model = model;
        this.owner = owner;
        this.formController = formController;
    }

    /// <summary>새로운 사용 요청을 받을 수 있는지 검사</summary>
    public override PlayerSkillBlockReason GetBlockReason()
    {
        if (IsInUse) return PlayerSkillBlockReason.AlreadyInUse;
        return CheckActivationConditions();
    }

    /// <summary>사용 조건을 검사하고 준비를 시작합니다.<br/>
    /// 준비 시간이 0이면 즉시 발동합니다.</summary>
    public override bool TryUse(out PlayerSkillBlockReason blockReason)
    {
        blockReason = GetBlockReason();

        if (blockReason != PlayerSkillBlockReason.None) return false;

        remainingPreparationTime = definition.PreparationTime;
        Phase = PlayerSkillPhase.Preparing;

        if (remainingPreparationTime <= 0f) return TryActivate(out blockReason);

        return true;
    }

    /// <summary>준비 중 사용 조건을 확인하고 준비 시간을 갱신합니다.</summary>
    public override void Tick(float deltaTime)
    {
        if (Phase != PlayerSkillPhase.Preparing) return;

        if (CheckActivationConditions() != PlayerSkillBlockReason.None)
        {
            Cancel();
            return;
        }

        remainingPreparationTime = Math.Max(0f, remainingPreparationTime - deltaTime);

        if (remainingPreparationTime <= 0f) TryActivate(out _);
    }

    /// <summary>준비 또는 실행 상태를 종료합니다.<br/>
    /// 이미 소비한 조각과 대상에게 적용한 스턴은 되돌리지 않습니다.</summary>
    public override void Cancel()
    {
        remainingPreparationTime = 0f;
        Phase = PlayerSkillPhase.Idle;
    }

    /// <summary>준비 중에도 유지되어야 하는 발동 조건을 검사합니다.<br/>
    /// 자신의 실행 단계와 슬롯의 쿨다운은 검사하지 않습니다.</summary>
    private PlayerSkillBlockReason CheckActivationConditions()
    {
        if (owner == null || !owner.gameObject.activeInHierarchy) return PlayerSkillBlockReason.Unavailable;
        if (targetValidator != null && (effectRoot == null || !effectRoot.gameObject.activeInHierarchy)) return PlayerSkillBlockReason.Unavailable;
        if (model.CurrentSkillFragment < definition.RequiredFragments) return PlayerSkillBlockReason.InsufficientFragments;

        return PlayerSkillBlockReason.None;
    }

    /// <summary>발동 조건을 다시 확인한 뒤 조각을 소비하고 효과를 적용합니다.</summary>
    private bool TryActivate(out PlayerSkillBlockReason blockReason)
    {
        blockReason = CheckActivationConditions();

        if (blockReason != PlayerSkillBlockReason.None)
        {
            Cancel();
            return false;
        }

        // 실제 발동 순간의 위치를 사용
        Vector3 center = targetValidator != null ? effectRoot.position : owner.position;
        float radius = CurrentEffectRadius;
        Phase = PlayerSkillPhase.Executing;

        try
        {
            int requiredFragments = definition.RequiredFragments;
            if (requiredFragments > 0 && !model.TryConsumeSkillFragments(requiredFragments))
            {
                blockReason = PlayerSkillBlockReason.InsufficientFragments;
                return false;
            }

            // 발동 확정 시점
            #if UNITY_EDITOR
            Debug.Log($"[눈물 스킬 발동] 남은 조각: {model.CurrentSkillFragment} / " + $"적용할 쿨다운: {Cooldown}초", owner);
            #endif
            NotifyActivated();
            ApplyStun(center, radius);
            return true;
        }
        finally
        {
            #if UNITY_EDITOR
            if (Phase == PlayerSkillPhase.Preparing)
            {
                Debug.Log("[눈물 스킬] 준비 취소", owner);
            }
            #endif
            Cancel();
        }
    }

    /// <summary>범위 내 대상을 찾고 동일 대상에게 한 번씩 스턴을 시도합니다.</summary>
    private void ApplyStun(Vector3 center, float radius)
    {
        if (radius <= 0f) return;
        // 추후 레이어를 나누게 되면 Layer부분 수정
        Collider[] colliders = Physics.OverlapSphere(center, radius, Physics.AllLayers, QueryTriggerInteraction.Collide);
        HashSet<IStunnable> processedTargets = new HashSet<IStunnable>();
        
        int stunMilliseconds = Mathf.RoundToInt(definition.StunDuration * 1000f);

        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider == null) continue;
            // PC 자신과 자식 Collider를 제외
            if (targetCollider.transform.IsChildOf(owner)) continue;
            if (effectRoot != null && targetCollider.transform.IsChildOf(effectRoot)) continue;

            // Collider가 자식에 있고 처리 컴포넌트가 부모에 있는 경우
            IStunnable target = targetCollider.GetComponentInParent<IStunnable>();

            if (target == null) continue;
            // 찾아낸 처리 컴포넌트도 자기 자신에 속하는지 확인
            if (target is Component targetComponent && targetComponent.transform.IsChildOf(owner)) continue;
            if (target is Component rootTarget && effectRoot != null && rootTarget.transform.IsChildOf(effectRoot)) continue;
            // 센서가 먼저 열거되어도 같은 적의 유효한 몸체 후보를 버리지 않도록 검증 후 중복을 제거합니다.
            if (targetValidator != null && !targetValidator(center, targetCollider, target)) continue;
            if (!processedTargets.Add(target)) continue;

            target.TryStun(stunMilliseconds);
        }
    }
}
