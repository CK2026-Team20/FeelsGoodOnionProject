using TMPro;
using System;
using Cooked.Authoring;
using UnityEngine;
using UnityEngine.UI;
namespace Cooked.UI
{
    public sealed class HudView : BoundView<HudViewModel>
    {
        [Tooltip("게임 HUD 전체의 표시 여부를 제어할 CanvasGroup입니다.")]
        [SerializeField, InspectorLabel("HUD 화면")] private CanvasGroup group;
        [Tooltip("체력을 숫자로 표시할 기존 TMP 텍스트입니다.")]
        [SerializeField, InspectorLabel("체력 글자")] private TMP_Text health;
        [Tooltip("조각 수를 표시할 기존 TMP 텍스트입니다.")]
        [SerializeField, InspectorLabel("조각 글자")] private TMP_Text fragments;
        [Tooltip("해금 능력 안내를 표시할 기존 TMP 텍스트입니다.")]
        [SerializeField, InspectorLabel("능력 안내")] private TMP_Text abilities;
        // T15: retain the actual prototype until a human connects all three Images and
        // the real heart Sprite, migrates asset GUID references, then removes this field/branch.
        [SerializeField, HideInInspector] private HudIconGraphic[] hearts;
        [SerializeField, InspectorLabel("하트 이미지"), Tooltip("왼쪽부터 체력 순서대로 실제 하트 Image 세 개를 연결하세요.")]
        private Image[] heartImages = Array.Empty<Image>();
        [SerializeField, InspectorLabel("하트 그림"), Tooltip("세 하트 Image에 표시할 실제 하트 Sprite를 연결하세요.")]
        private Sprite heartSprite;
        [Tooltip("Q 상태 전환 해금 여부를 표시할 카드 그룹입니다.")]
        [SerializeField, InspectorLabel("Q 해금 표시")] private CanvasGroup formCard;
        [Tooltip("E 눈물 해금 여부를 표시할 카드 그룹입니다.")]
        [SerializeField, InspectorLabel("E 해금 표시")] private CanvasGroup tearCard;
        [Tooltip("현재 눈물 조각 수와 최대 5개를 표시할 TMP 텍스트입니다.")]
        [SerializeField, InspectorLabel("조각 개수")] private TMP_Text fragmentCount;
#if UNITY_EDITOR
        // Temporary Editor field setter for the retained Builder. T12 removes both after T11 reference migration.
        public void ConfigureIcons(CanvasGroup g, HudIconGraphic[] heartIcons, TMP_Text count, CanvasGroup form, CanvasGroup tear)
        { group = g; hearts = heartIcons; fragmentCount = count; formCard = form; tearCard = tear; }
#endif
        protected override void Refresh()
        {
            if (group == null) return; group.alpha = Model != null && Model.Visible ? 1 : 0; group.blocksRaycasts = false;
            if (Model == null) return;
            if (health != null) health.text = Model.Health;
            if (fragments != null) fragments.text = Model.Fragments;
            if (abilities != null) abilities.text = Model.Abilities;
            if (fragmentCount != null) fragmentCount.text = Model.FragmentCount;
            bool useImages = heartSprite != null && heartImages != null && heartImages.Length == 3 &&
                heartImages[0] != null && heartImages[1] != null && heartImages[2] != null &&
                heartImages[0] != heartImages[1] && heartImages[0] != heartImages[2] && heartImages[1] != heartImages[2];
            if (hearts != null) for (int i = 0; i < hearts.Length && i < 3; i++)
                if (hearts[i] != null)
                {
                    hearts[i].enabled = !useImages;
                    if (!useImages) hearts[i].FillAmount = Model.HeartFill(i);
                }
            if (useImages) for (int i = 0; i < 3; i++)
            {
                var image = heartImages[i];
                image.sprite = heartSprite;
                image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = (int)Image.OriginHorizontal.Left;
                image.fillAmount = Model.HeartFill(i); image.raycastTarget = false;
            }
            if (formCard != null) formCard.alpha = Model.FormUnlocked ? 1 : .3f;
            if (tearCard != null) tearCard.alpha = Model.TearUnlocked ? 1 : .3f;
        }
    }
}
