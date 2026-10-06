using Cooked.UI;
using TMPro;
using UnityEngine;
namespace Cooked.Integration
{
    public sealed class InstructionView : BoundView<InstructionViewModel>
    {
        [Tooltip("조작 설명의 표시 여부를 제어할 CanvasGroup입니다.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("가장 가까운 기믹의 조작 설명을 출력할 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text label;
        public void Configure(CanvasGroup group, TMP_Text label) { this.group = group; this.label = label; }
        protected override void Refresh()
        {
            if (group == null || label == null) return;
            group.alpha = Model != null && Model.Visible ? 1 : 0;
            group.blocksRaycasts = false; group.interactable = false;
            label.text = Model?.Text ?? string.Empty;
        }
    }
}
