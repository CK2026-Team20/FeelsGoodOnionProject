using UnityEngine;
using Cooked.Authoring;
namespace Cooked.UI
{
    public sealed class FadeView : BoundView<FadeViewModel>
    {
        [Tooltip("씬 전환 시 투명도를 조절할 전체 화면 CanvasGroup입니다.")]
        [SerializeField, InspectorLabel("페이드 화면")] private CanvasGroup cover;
        protected override void Refresh()
        { if (cover == null) return; cover.alpha = Model?.Alpha ?? 1; cover.blocksRaycasts = Model?.BlocksInput ?? true; cover.interactable = cover.blocksRaycasts; }
    }
}
