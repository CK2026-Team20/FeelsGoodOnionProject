using TMPro;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class WorldPromptView : BoundView<WorldPromptViewModel>
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform anchor;
        [SerializeField] private TMP_Text label;
        public void Configure(CanvasGroup g, RectTransform a, TMP_Text text) { group = g; anchor = a; label = text; }
        protected override void Refresh()
        {
            if (group == null) return; group.alpha = Model != null && Model.Visible ? 1 : 0; group.blocksRaycasts = false;
            if (Model == null) return; anchor.position = Model.Position; anchor.rotation = Model.Facing; label.text = Model.Label;
        }
    }
}
