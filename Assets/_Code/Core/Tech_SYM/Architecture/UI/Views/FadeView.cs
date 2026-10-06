using UnityEngine;
namespace Cooked.UI
{
    public sealed class FadeView : BoundView<FadeViewModel>
    {
        [Tooltip("씬 전환 시 투명도를 조절할 전체 화면 CanvasGroup입니다.")]
        [SerializeField] private CanvasGroup cover;
        public void Configure(CanvasGroup value) => cover = value;
        protected override void Refresh()
        { if (cover == null) return; cover.alpha = Model?.Alpha ?? 1; cover.blocksRaycasts = Model?.BlocksInput ?? true; cover.interactable = cover.blocksRaycasts; }
    }
}
