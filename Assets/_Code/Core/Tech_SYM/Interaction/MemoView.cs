using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>주입된 ViewModel의 이미지와 표시 상태만 표현한다.</summary>
    public sealed class MemoView : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image memoImage;
        [SerializeField] private UIScaleTween scaleTween;
        private MemoViewModel model;
        public void Bind(MemoViewModel viewModel)
        {
            Unbind();
            model = viewModel;
            memoImage.sprite = model.Image;
            model.StateChanged += Present;
            Present(model.State);
        }
        private void Present(MemoDisplayState state)
        {
            var boundModel = model;
            if (state == MemoDisplayState.Opening)
                scaleTween.Show(() => boundModel.CompletePresentation(state));
            else if (state == MemoDisplayState.Closing)
                scaleTween.Hide(() => boundModel.CompletePresentation(state));
        }
        public void Unbind()
        {
            if (model != null) model.StateChanged -= Present;
            model = null;
            if (scaleTween != null) scaleTween.Stop();
        }
        private void OnDisable() => Unbind();
    }
}
