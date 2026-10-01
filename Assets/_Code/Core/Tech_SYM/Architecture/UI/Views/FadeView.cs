using UnityEngine;
namespace Cooked.UI
{
    public sealed class FadeView : BoundView<FadeViewModel>
    {
        [SerializeField] private CanvasGroup cover;
        public void Configure(CanvasGroup value) => cover = value;
        protected override void Refresh()
        { if (cover == null) return; cover.alpha = Model?.Alpha ?? 1; cover.blocksRaycasts = Model?.BlocksInput ?? true; cover.interactable = cover.blocksRaycasts; }
    }
}
