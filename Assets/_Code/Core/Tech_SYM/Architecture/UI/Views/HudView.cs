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
        // 실제 하트 리소스로 사람이 이관할 때 이 배열과 HudIconGraphic을 함께 교체한다.
        [SerializeField, InspectorLabel("현재 하트 표시"), Tooltip("왼쪽부터 현재 사용 중인 하트 표시 세 개를 연결합니다. 실제 리소스 이관 전까지 유지합니다.")]
        private HudIconGraphic[] hearts;
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
            for (int i = 0; i < hearts.Length; i++) hearts[i].FillAmount = Model.HeartFill(i);
            formCard.alpha = Model.FormUnlocked ? 1 : .3f;
            tearCard.alpha = Model.TearUnlocked ? 1 : .3f;
        }
    }
}
