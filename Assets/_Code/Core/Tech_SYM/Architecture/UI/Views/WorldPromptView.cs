using System;
using TMPro;
using UnityEngine;
using Cooked.Authoring;
namespace Cooked.UI
{
    public sealed class WorldPromptView : BoundView<WorldPromptViewModel>
    {
        [Tooltip("월드 상호작용 안내의 표시 여부를 제어할 CanvasGroup입니다.")]
        [SerializeField, InspectorLabel("안내 화면")] private CanvasGroup group;
        [Tooltip("월드 대상 위치로 이동시킬 안내 RectTransform입니다.")]
        [SerializeField, InspectorLabel("안내 위치")] private RectTransform anchor;
        [Tooltip("상호작용 F 안내를 표시할 TMP 텍스트입니다.")]
        [SerializeField, InspectorLabel("F 안내")] private TMP_Text label;
        protected override void ValidateReferences()
        {
            if (group == null || anchor == null || label == null)
                throw new InvalidOperationException("WorldPromptView의 안내 화면·위치·F 안내를 연결하세요.");
        }
        protected override void Refresh()
        {
            if (group == null) return; group.alpha = Model != null && Model.Visible ? 1 : 0; group.blocksRaycasts = false;
            if (Model == null) return; anchor.position = Model.Position; anchor.rotation = Model.Facing; label.text = Model.Label;
        }
    }
}
