using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 시연용 플레이어 스킬 HUD
/// </summary>
/// <remarks>
/// UI 표시만 담당하며 스킬 사용이나 자원 변경은 수행하지 않습니다.<br/>
/// 항상 활성인 HUD 오브젝트에 부착합니다.
/// </remarks>
[DefaultExecutionOrder(100)]
public sealed class PlayerSkillHUD : MonoBehaviour
{
    [Serializable]
    private sealed class SlotView
    {
        [Min(0)]
        public int slotIndex;

        public GameObject root;
        public Image icon;
        public Image cooldownOverlay;
        public TMP_Text cooldownText;

        public void Initialize()
        {
            icon.raycastTarget = false;
            cooldownOverlay.raycastTarget = false;
            cooldownText.raycastTarget = false;

            cooldownOverlay.type = Image.Type.Filled;
            cooldownOverlay.fillMethod = Image.FillMethod.Radial360;
            cooldownOverlay.fillOrigin = (int)Image.Origin360.Top;

            // 남은 영역을 반시계 방향으로 채워,
            // 사라지는 경계가 위쪽부터 시계 방향으로 진행
            cooldownOverlay.fillClockwise = false;

            cooldownOverlay.color = new Color(0f, 0f, 0f, 0.55f);
            Hide();
        }

        public void Refresh(PlayerSkill skill, float remainingCooldown, float totalCooldown)
        {
            if (skill == null)
            {
                Hide();
                return;
            }

            root.SetActive(true);

            if (icon.sprite != skill.Icon)
            {
                icon.sprite = skill.Icon;
            }

            bool isOnCooldown = remainingCooldown > 0f;

            cooldownOverlay.gameObject.SetActive(isOnCooldown);
            cooldownText.gameObject.SetActive(isOnCooldown);

            if (!isOnCooldown) return;
            cooldownOverlay.fillAmount = totalCooldown > 0f ? Mathf.Clamp01(remainingCooldown / totalCooldown) : 0f;
            // 소수점 첫째 자리까지 표시
            cooldownText.text = remainingCooldown.ToString("F1");
        }

        public void Hide()
        {
            root.SetActive(false);
        }
    }

    [Header("Player")]
    [SerializeField] private PlayerFacade player;

    [Header("Common Slots")]
    [SerializeField] private SlotView[] slotViews;

    [Header("Tear Skill")]
    [SerializeField] private GameObject tearPanel;
    [SerializeField] private TMP_Text fragmentCountText;

    [Header("Restore Form Skill")]
    [SerializeField] private GameObject debrisPanel;
    [SerializeField] private Image debrisDarkOverlay;

    private PlayerSkillController skillController;
    private PlayerFormController formController;

    private void Awake()
    {
        if (player == null)
        {
            Debug.LogError("HUD에 PlayerFacade를 연결해 주세요.", this);
            enabled = false;
            return;
        }

        skillController = player.GetComponent<PlayerSkillController>();
        formController = player.GetComponent<PlayerFormController>();

        if (skillController == null || formController == null)
        {
            Debug.LogError("플레이어의 스킬·형태 컨트롤러를 확인해 주세요.", this);
            enabled = false;
            return;
        }

        foreach (SlotView slotView in slotViews)
        {
            slotView.Initialize();
        }

        fragmentCountText.raycastTarget = false;
        debrisDarkOverlay.raycastTarget = false;
        debrisDarkOverlay.color = new Color(0f, 0f, 0f, 0.55f);

        HideAll();
    }

    /// <summary>
    /// 플레이어의 슬롯 교체 처리 이후 화면을 갱신합니다.
    /// </summary>
    private void LateUpdate()
    {
        if (player == null ||
            !player.gameObject.activeInHierarchy ||
            skillController == null ||
            formController == null)
        {
            HideAll();
            return;
        }

        IReadOnlyPlayerModel model = player.Model;

        if (!skillController.IsInitialized)
        {
            HideAll();
            return;
        }

        RefreshSlots();
        RefreshSpecialPanels(model);
    }

    private void RefreshSlots()
    {
        foreach (SlotView slotView in slotViews)
        {
            int slotIndex = slotView.slotIndex;

            if (slotIndex < 0 || slotIndex >= skillController.SlotCount)
            {
                slotView.Hide();
                continue;
            }

            slotView.Refresh(
                skillController.GetEquippedSkill(slotIndex),
                skillController.GetRemainingCooldown(slotIndex),
                skillController.GetTotalCooldown(slotIndex));
        }
    }

    private void RefreshSpecialPanels(IReadOnlyPlayerModel model)
    {
        TearSkill equippedTear = null;
        bool hasRestoreForm = false;

        for (int i = 0; i < skillController.SlotCount; i++)
        {
            PlayerSkill skill = skillController.GetEquippedSkill(i);

            if (skill == null) continue;

            if (skill is TearSkill tear && equippedTear == null)
            {
                equippedTear = tear;
            }

            if (skill.Id == PlayerSkillId.RestoreForm)
            {
                hasRestoreForm = true;
            }
        }

        bool hasTear = equippedTear != null;
        tearPanel.SetActive(hasTear);

        if (hasTear)
        {
            fragmentCountText.text =
                $"{model.CurrentSkillFragment}/{equippedTear.RequiredFragments}";
        }

        debrisPanel.SetActive(hasRestoreForm);

        if (hasRestoreForm)
        {
            debrisDarkOverlay.gameObject.SetActive(
                !formController.IsDebrisRecovered);
        }
    }

    private void HideAll()
    {
        if (slotViews != null)
        {
            foreach (SlotView slotView in slotViews)
            {
                if (slotView != null && slotView.root != null)
                {
                    slotView.Hide();
                }
            }
        }

        if (tearPanel != null)
        {
            tearPanel.SetActive(false);
        }

        if (debrisPanel != null)
        {
            debrisPanel.SetActive(false);
        }
    }

    private void OnDisable()
    {
        HideAll();
    }
}