using System;

/// <summary>
/// 껍질 회수와 공간 조건을 확인하고 기본 형태로 복귀하는 스킬입니다.
/// </summary>
public sealed class RestoreFormSkill : PlayerSkill
{
    private readonly RestoreFormSkillDefinition definition;
    private readonly PlayerFormController formController;

    private float remainingPreparationTime;

    public RestoreFormSkill(
        RestoreFormSkillDefinition definition,
        PlayerFormController formController)
        : base(definition)
    {
        if (formController == null)
        {
            throw new ArgumentNullException(nameof(formController));
        }

        this.definition = definition;
        this.formController = formController;
    }

    /// <summary>
    /// 새로운 사용 요청을 받을 수 있는지 검사합니다.
    /// </summary>
    public override PlayerSkillBlockReason GetBlockReason()
    {
        if (IsInUse)
        {
            return PlayerSkillBlockReason.AlreadyInUse;
        }

        return CheckActivationConditions();
    }

    /// <summary>
    /// 사용 조건을 검사하고 복귀 준비를 시작합니다.<br/>
    /// 준비 시간이 0이면 즉시 발동을 시도합니다.
    /// </summary>
    public override bool TryUse(out PlayerSkillBlockReason blockReason)
    {
        blockReason = GetBlockReason();

        if (blockReason != PlayerSkillBlockReason.None)
        {
            return false;
        }

        remainingPreparationTime = definition.PreparationTime;
        Phase = PlayerSkillPhase.Preparing;

        if (remainingPreparationTime <= 0f)
        {
            return TryActivate(out blockReason);
        }

        return true;
    }

    /// <summary>
    /// 준비 중 복귀 조건을 확인하고 준비 시간을 갱신합니다.<br/>
    /// </summary>
    public override void Tick(float deltaTime)
    {
        if (Phase != PlayerSkillPhase.Preparing)
        {
            return;
        }

        if (CheckActivationConditions() != PlayerSkillBlockReason.None)
        {
            Cancel();
            return;
        }

        remainingPreparationTime =
            Math.Max(0f, remainingPreparationTime - deltaTime);

        if (remainingPreparationTime <= 0f)
        {
            TryActivate(out _);
        }
    }

    /// <summary>
    /// 준비 또는 실행 상태를 종료합니다.<br/>
    /// 이미 완료된 형태 전환은 되돌리지 않습니다.
    /// </summary>
    public override void Cancel()
    {
        remainingPreparationTime = 0f;
        Phase = PlayerSkillPhase.Idle;
    }

    /// <summary>
    /// 껍질 회수 여부와 복귀 공간 등 발동 조건을 검사합니다.<br/>
    /// 자신의 실행 단계와 슬롯 쿨다운은 검사하지 않습니다.
    /// </summary>
    private PlayerSkillBlockReason CheckActivationConditions()
    {
        if (formController == null)
        {
            return PlayerSkillBlockReason.Unavailable;
        }

        return formController.GetRestoreBlockReason();
    }

    /// <summary>
    /// 조건을 재검사하고 실제 복귀에 성공한 경우 발동을 알립니다.
    /// </summary>
    private bool TryActivate(out PlayerSkillBlockReason blockReason)
    {
        blockReason = CheckActivationConditions();

        if (blockReason != PlayerSkillBlockReason.None)
        {
            Cancel();
            return false;
        }

        Phase = PlayerSkillPhase.Executing;

        try
        {
            if (!formController.RestoreForm())
            {
                blockReason = CheckActivationConditions();

                if (blockReason == PlayerSkillBlockReason.None)
                {
                    blockReason = PlayerSkillBlockReason.Unavailable;
                }

                return false;
            }

            NotifyActivated();

            blockReason = PlayerSkillBlockReason.None;
            return true;
        }
        finally
        {
            Cancel();
        }
    }
}