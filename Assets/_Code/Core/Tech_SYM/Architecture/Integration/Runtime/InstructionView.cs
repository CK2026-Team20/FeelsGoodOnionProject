using Cooked.UI;
using TMPro;
using UnityEngine;
namespace Cooked.Integration
{
    public sealed class InstructionView : BoundView<InstructionViewModel>
    {
        [SerializeField] private CanvasGroup group;
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
