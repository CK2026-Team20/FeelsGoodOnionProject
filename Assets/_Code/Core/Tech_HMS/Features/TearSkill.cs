using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>PC를 중심으로 일정 거리 내 IStunnable 대상에게 스턴을 적용하는 스킬</summary>
public class TearSkill : PlayerSkill
{
    private readonly TearSkillDefinition definition;
    private readonly PlayerModel model;
    private readonly Transform owner;

    private float remainingPreparationTime;

    /// <param name="definition">눈물 스킬 설정</param>
    /// <param name="model">사용자의 플레이어 모델</param>
    /// <param name="owner">플레이어의 최상위 Transform입니다.
    /// 범위의 중심과, 자기 자신을 제외하는 기준으로 사용합니다.</param>
    public TearSkill(TearSkillDefinition definition, PlayerModel model, Transform owner) : base(definition)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (owner == null) throw new ArgumentNullException(nameof(owner));

        this.definition = definition;
        this.model = model;
        this.owner = owner;
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
        Vector3 center = owner.position;
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
            ApplyStun(center);
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
    private void ApplyStun(Vector3 center)
    {
        // 추후 레이어를 나누게 되면 Layer부분 수정
        Collider[] colliders = Physics.OverlapSphere(center, definition.EffectRadius, Physics.AllLayers, QueryTriggerInteraction.Collide);
        HashSet<IStunnable> processedTargets = new HashSet<IStunnable>();
        
        int stunMilliseconds = Mathf.RoundToInt(definition.StunDuration * 1000f);

        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider == null) continue;
            // PC 자신과 자식 Collider를 제외
            if (targetCollider.transform.IsChildOf(owner)) continue;

            // Collider가 자식에 있고 처리 컴포넌트가 부모에 있는 경우
            IStunnable target = targetCollider.GetComponentInParent<IStunnable>();

            if (target == null) continue;
            // 찾아낸 처리 컴포넌트도 자기 자신에 속하는지 확인
            if (target is Component targetComponent && targetComponent.transform.IsChildOf(owner)) continue;
            if (!processedTargets.Add(target)) continue;

            target.TryStun(stunMilliseconds);
        }
    }
}