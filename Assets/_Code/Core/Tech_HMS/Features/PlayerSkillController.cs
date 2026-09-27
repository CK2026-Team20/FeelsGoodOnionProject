using System;
using UnityEngine;

/// <summary>
/// 플레이어의 스킬 슬롯과 공통 사용 조건을 관리합니다.
/// </summary>
/// <remarks>
/// 외부에서 Initialize를 호출한 뒤 스킬을 생성하여 장착합니다.
/// 개별 스킬의 생성과 설정 에셋 선택은 이 클래스에서 담당하지 않습니다.
/// </remarks>
[DisallowMultipleComponent]
public class PlayerSkillController : MonoBehaviour
{
    private SkillSlot[] slots;

    /// <summary>
    /// PC와 연결된 스킬 사용 제한 공통 조건을 검사하는 함수.<br/>
    /// PlayerSkillController를 소유하고 사용하는 입장(PlayerFacade)에서 Initialize를 통해 반드시 주입받아야 합니다.
    /// </summary>
    private Func<PlayerSkillBlockReason> commonConditionChecker = null;

    /// <summary>초기화 완료 여부를 확인</summary>
    public bool IsInitialized => slots != null;

    /// <summary>슬롯의 개수. 초기화 전에는 0입니다.</summary>
    public int SlotCount => slots?.Length ?? 0;

    /// <summary>
    /// 슬롯의 총 개수와, 플레이어 공통 사용 조건 검사 함수를 설정합니다.
    /// </summary>
    /// <param name="slotCount">생성할 슬롯의 개수</param>
    /// <param name="commonConditionChecker">사망이나 제어 제한 등, PC와 연관된 공통 사용 조건을 검사하는 함수입니다.<br/>
    /// 스킬을 사용할 수 있다면 해당 함수에서 None을 반환해야 합니다.</param>
    /// <remarks>
    /// 한 번만 호출해야 하며,<br/>
    /// 검사 함수는 상태나 자원 등을 변경하지 않아야 합니다.<br/>
    /// 또한, 검사 함수는 PC의 공통 조건만을 검사하는 것으로 각 스킬의 조건을 검사하는 역할이 아닙니다.
    /// </remarks>
    public void Initialize(int slotCount, Func<PlayerSkillBlockReason> commonConditionChecker)
    {
        if (IsInitialized) throw new InvalidOperationException("스킬 컨트롤러가 이미 초기화되어 있습니다.\n스킬 컨트롤러는 1회만 초기화되어야 합니다.");
        if (slotCount < 1) throw new ArgumentOutOfRangeException(nameof(slotCount), "slot의 수는 1 이상이어야 합니다.");
        if (commonConditionChecker == null) throw new ArgumentNullException(nameof(commonConditionChecker), "공용 조건 검사 함수는 반드시 지정해야 합니다.");
        this.commonConditionChecker = commonConditionChecker;

        slots = new SkillSlot[slotCount];

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i] = new SkillSlot();
        }
    }

    /// <summary>
    /// 지정한 슬롯에 스킬을 장착합니다.
    /// </summary>
    /// <remarks>
    /// 동일한 PlayerSkill 인스턴스를 여러 슬롯에 장착할 수 없습니다.
    /// 기존 스킬의 해제, 초기 쿨다운 적용은 슬롯에서 처리합니다.
    /// </remarks>
    public void Equip(int slotIndex, PlayerSkill skill)
    {
        SkillSlot targetSlot = GetSlot(slotIndex);
        if (skill == null) throw new ArgumentNullException(nameof(skill), "스킬 장착 함수에 null을 넣을 수 없습니다. 제거하려는 경우 Unequip 함수를 활용해야 합니다.");

        for (int i = 0; i < slots.Length; i++)
        {
            if (i != slotIndex && ReferenceEquals(slots[i].EquippedSkill, skill)) throw new InvalidOperationException("동일한 스킬 인스턴스를 여러 슬롯에 장착할 수 없습니다.");
        }
        targetSlot.Equip(skill);
    }

    /// <summary>
    /// 지정한 슬롯의 스킬을 해제합니다.
    /// </summary>
    /// <remarks>
    /// 이미 비어있던 슬롯의 경우에 대한 별도 처리는 없습니다.
    /// </remarks>
    public void Unequip(int slotIndex)
    {
        GetSlot(slotIndex).Unequip();
    }

    /// <summary>
    /// 지정한 슬롯에 장착된 스킬을 반환합니다.
    /// </summary>
    /// <returns><c>null</c>: 해당 인덱스에 장착된 스킬이 없음<br/>
    /// <c>그 외</c>: 해당 인덱스에 장착된 PlayerSkill의 인스턴스</returns>
    public PlayerSkill GetEquippedSkill(int slotIndex)
    {
        return GetSlot(slotIndex).EquippedSkill;
    }

    /// <summary>
    /// 지정한 슬롯의 남은 쿨다운을 반환합니다.<br/>
    /// (단위: s)
    /// </summary>
    public float GetRemainingCooldown(int slotIndex)
    {
        return GetSlot(slotIndex).RemainingCooldown;
    }

    /// <summary>
    /// 공통 조건과 지정한 슬롯의 사용 조건을 검사합니다.
    /// </summary>
    /// <returns><c><see cref="PlayerSkillBlockReason.None"/></c>: 사용할 수 있음<br/>
    /// <c>그 외</c>: 스킬을 사용할 수 없는 원인</returns>
    public PlayerSkillBlockReason GetBlockReason(int slotIndex)
    {
        if (!IsInitialized || !isActiveAndEnabled) return PlayerSkillBlockReason.Unavailable;
        if (slotIndex < 0 || slotIndex >= slots.Length) return PlayerSkillBlockReason.InvalidSlot;

        // 스킬의 종류와 상관 없이 적용되는 PC 상태에 기반한 조건을 검사
        PlayerSkillBlockReason reason = commonConditionChecker();
        if (reason != PlayerSkillBlockReason.None) return reason;
        
        // 각 스킬의 세부 조건 검사
        return slots[slotIndex].GetBlockReason();
    }

    /// <summary>
    /// 공통 조건과 슬롯의 조건을 검사하고 스킬 사용을 요청합니다.
    /// </summary>
    /// <param name="slotIndex">사용할 슬롯의 인덱스</param>
    /// <param name="blockReason">사용이 수락되었다면 None, 거절되었다면 사용 불가 사유</param>
    /// <returns><c>true: 사용 요청이 수락됨</c></returns>
    public bool TryUse(int slotIndex, out PlayerSkillBlockReason blockReason)
    {
        blockReason = GetBlockReason(slotIndex);
        if (blockReason != PlayerSkillBlockReason.None) return false;

        return slots[slotIndex].TryUse(out blockReason);
    }

    /// <summary>
    /// 모든 슬롯의 스킬 실행을 취소합니다.<br/>
    /// 장착 상태와 남은 쿨다운은 유지합니다.
    /// </summary>
    public void CancelAll()
    {
        if (!IsInitialized) return;

        foreach (SkillSlot slot in slots)
        {
            slot.Cancel();
        }
    }

    private void Update()
    {
        if (!IsInitialized) return;

        float deltaTime = Time.deltaTime;

        foreach (SkillSlot slot in slots)
        {
            slot.Tick(deltaTime);
        }
    }

    private void OnDisable()
    {
        CancelAll();
    }

    private void OnDestroy()
    {
        if (!IsInitialized) return;

        foreach (SkillSlot slot in slots)
        {
            slot.Unequip();
        }
    }

    private SkillSlot GetSlot(int slotIndex)
    {
        if (!IsInitialized) throw new InvalidOperationException("스킬 컨트롤러가 초기화되지 않았습니다.");
        if (slotIndex < 0 || slotIndex >= slots.Length) throw new ArgumentOutOfRangeException(nameof(slotIndex), "스킬 슬롯 배열의 인덱스를 벗어났습니다.");

        return slots[slotIndex];
    }
}