using TMPro;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class HudView : BoundView<HudViewModel>
    {
        [Tooltip("게임 HUD 전체의 표시 여부를 제어할 CanvasGroup입니다.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("체력을 숫자로 표시할 기존 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text health;
        [Tooltip("조각 수를 표시할 기존 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text fragments;
        [Tooltip("해금 능력 안내를 표시할 기존 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text abilities;
        [Tooltip("체력 순서대로 채울 하트 아이콘 목록입니다. 목록 길이만큼 표시하며 실제 최대 체력을 변경하지 않습니다.")]
        [SerializeField] private HudIconGraphic[] hearts;
        [Tooltip("Q 상태 전환 해금 여부를 표시할 카드 그룹입니다.")]
        [SerializeField] private CanvasGroup formCard;
        [Tooltip("E 눈물 해금 여부를 표시할 카드 그룹입니다.")]
        [SerializeField] private CanvasGroup tearCard;
        [Tooltip("현재 눈물 조각 수와 최대 5개를 표시할 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text fragmentCount;
        public void Configure(CanvasGroup g, TMP_Text h, TMP_Text f, TMP_Text a) { group = g; health = h; fragments = f; abilities = a; }
        public void ConfigureIcons(CanvasGroup g, HudIconGraphic[] heartIcons, TMP_Text count, CanvasGroup form, CanvasGroup tear)
        { group = g; hearts = heartIcons; fragmentCount = count; formCard = form; tearCard = tear; }
        protected override void Refresh()
        {
            if (group == null) return; group.alpha = Model != null && Model.Visible ? 1 : 0; group.blocksRaycasts = false;
            if (Model == null) return;
            if (health != null) health.text = Model.Health;
            if (fragments != null) fragments.text = Model.Fragments;
            if (abilities != null) abilities.text = Model.Abilities;
            if (fragmentCount != null) fragmentCount.text = Model.FragmentCount;
            if (hearts != null) for (int i = 0; i < hearts.Length && i < 3; i++)
                if (hearts[i] != null) hearts[i].FillAmount = Model.HeartFill(i);
            if (formCard != null) formCard.alpha = Model.FormUnlocked ? 1 : .3f;
            if (tearCard != null) tearCard.alpha = Model.TearUnlocked ? 1 : .3f;
        }
    }
}
