using TMPro;
using System;
using Cooked.Authoring;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class HudView : BoundView<HudViewModel>
    {
        [Tooltip("게임 HUD 전체의 표시 여부를 제어할 CanvasGroup입니다.")]
        [SerializeField, InspectorLabel("HUD 화면")] private CanvasGroup group;
        [SerializeField, InspectorLabel("현재 하트 표시"), Tooltip("왼쪽부터 체력을 표시할 하트 Image 세 개를 연결하세요.")]
        private UnityEngine.UI.Graphic[] hearts;
        [SerializeField, InspectorLabel("Q 아이콘"), Tooltip("현재 폼에서 전환할 모습을 표시할 Image를 연결하세요.")]
        private UnityEngine.UI.Image formIcon;
        [SerializeField, InspectorLabel("작아지기 그림"), Tooltip("기본 폼에서 표시할 작아지기 아이콘을 연결하세요.")]
        private Sprite formToSmall;
        [SerializeField, InspectorLabel("커지기 그림"), Tooltip("작은 폼에서 표시할 커지기 아이콘을 연결하세요.")]
        private Sprite formToHuge;
        [SerializeField, InspectorLabel("E 아이콘"), Tooltip("눈물 스킬 준비 상태를 표시할 Image를 연결하세요.")]
        private UnityEngine.UI.Image tearIcon;
        [SerializeField, InspectorLabel("눈물 활성 그림"), Tooltip("해금 후 필요한 조각이 모였을 때 표시할 아이콘을 연결하세요.")]
        private Sprite tearActive;
        [SerializeField, InspectorLabel("눈물 비활성 그림"), Tooltip("잠금 또는 조각 부족 때 표시할 아이콘을 연결하세요.")]
        private Sprite tearInactive;
        [Tooltip("Q 상태 전환 해금 여부를 표시할 카드 그룹입니다.")]
        [SerializeField, InspectorLabel("Q 해금 표시")] private CanvasGroup formCard;
        [Tooltip("E 눈물 해금 여부를 표시할 카드 그룹입니다.")]
        [SerializeField, InspectorLabel("E 해금 표시")] private CanvasGroup tearCard;
        [Tooltip("현재 눈물 조각 수와 최대 5개를 표시할 TMP 텍스트입니다.")]
        [SerializeField, InspectorLabel("조각 개수")] private TMP_Text fragmentCount;

        protected override void ConnectInputs()
        {
            string error = GetBindingError();
            if (error != null) throw new InvalidOperationException($"{name}: {error}");
        }

        private void OnValidate()
        {
            string error = GetBindingError();
            if (error != null) Debug.LogError($"{name}: {error}", this);
        }

        private string GetBindingError()
        {
            if (group == null) return "HUD 화면 CanvasGroup을 연결하세요.";
            if (formCard == null || tearCard == null || fragmentCount == null)
                return "Q/E 해금 표시와 조각 개수를 모두 연결하세요.";
            if (hearts == null || hearts.Length != 3 || hearts[0] == null || hearts[1] == null || hearts[2] == null)
                return "현재 하트 표시 세 개를 모두 연결하세요.";
            if (hearts[0] == hearts[1] || hearts[0] == hearts[2] || hearts[1] == hearts[2])
                return "현재 하트 표시에는 서로 다른 세 개를 연결하세요.";
            if ((formIcon != null || tearIcon != null) &&
                (formIcon == null || tearIcon == null || formToSmall == null || formToHuge == null || tearActive == null || tearInactive == null))
                return "Q/E Image와 폼별·활성별 Sprite를 모두 연결하세요.";
            return null;
        }

        protected override void Refresh()
        {
            if (group != null)
            {
                group.alpha = Model != null && Model.Visible ? 1 : 0;
                group.blocksRaycasts = false;
            }
            // Unbind와 아직 조립하지 않은 비활성 View의 정리는 누락 참조를 역참조하지 않는다.
            if (Model == null) return;
            string error = GetBindingError();
            if (error != null) throw new InvalidOperationException($"{name}: {error}");
            fragmentCount.text = Model.FragmentCount;
            for (int i = 0; i < hearts.Length; i++)
            {
                if (hearts[i] is UnityEngine.UI.Image image) image.fillAmount = Model.HeartFill(i);
                // 보존 씬의 임시 표시 호환. 모든 GUID 소비 이관 뒤 HudIconGraphic과 함께 제거한다.
                else if (hearts[i] is HudIconGraphic legacy) legacy.FillAmount = Model.HeartFill(i);
            }
            if (formIcon != null) formIcon.sprite = Model.IsSmall ? formToHuge : formToSmall;
            if (tearIcon != null) tearIcon.sprite = Model.TearReady ? tearActive : tearInactive;
            formCard.alpha = Model.FormUnlocked ? 1 : .3f;
            tearCard.alpha = Model.TearUnlocked ? 1 : .3f;
        }
    }
}
