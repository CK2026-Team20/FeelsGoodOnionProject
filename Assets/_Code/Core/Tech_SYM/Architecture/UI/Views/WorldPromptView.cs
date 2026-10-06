using TMPro;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class WorldPromptView : BoundView<WorldPromptViewModel>
    {
        [Tooltip("월드 상호작용 안내의 표시 여부를 제어할 CanvasGroup입니다.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("월드 대상 위치로 이동시킬 안내 RectTransform입니다.")]
        [SerializeField] private RectTransform anchor;
        [Tooltip("상호작용 E 안내를 표시할 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text label;
        public void Configure(CanvasGroup g, RectTransform a, TMP_Text text) { group = g; anchor = a; label = text; }
        protected override void Refresh()
        {
            if (group == null) return; group.alpha = Model != null && Model.Visible ? 1 : 0; group.blocksRaycasts = false;
            if (Model == null) return; anchor.position = Model.Position; anchor.rotation = Model.Facing; label.text = Model.Label;
        }
    }
}
