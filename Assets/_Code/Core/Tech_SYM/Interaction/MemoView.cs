using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>주입된 ViewModel의 이미지와 표시 상태만 표현한다.</summary>
    public sealed class MemoView : MonoBehaviour
    {
        [Tooltip("메모 데이터의 Sprite를 표시할 본문 Image입니다. 클릭으로 메모를 닫지 않습니다.")]
        [SerializeField] private UnityEngine.UI.Image memoImage;
        [Tooltip("E 열기·닫기 때 크기 표현을 재생할 담당입니다. 옵션 중에는 멈추며 완료 후 화면 상태를 갱신합니다.")]
        [SerializeField] private UIScaleTween scaleTween;
        private MemoViewModel model;
        private int bindingVersion;
        public void Bind(MemoViewModel viewModel)
        {
            Unbind();
            if (viewModel == null) throw new System.ArgumentNullException(nameof(viewModel));
            if (memoImage == null || scaleTween == null)
                throw new System.InvalidOperationException("MemoView의 Image와 UIScaleTween 연결이 필요합니다.");
            model = viewModel;
            memoImage.sprite = model.Image;
            model.StateChanged += Present;
            model.SuspensionChanged += SetSuspended;
            Present(model.State);
            SetSuspended(model.IsSuspended);
        }
        private void Present(MemoDisplayState state)
        {
            var boundModel = model;
            if (boundModel == null) return;
            int version = bindingVersion;
            System.Action completed = () =>
            {
                if (version == bindingVersion && model == boundModel && isActiveAndEnabled)
                    boundModel.CompletePresentation(state);
            };
            if (state == MemoDisplayState.Opening)
                scaleTween.Show(completed);
            else if (state == MemoDisplayState.Closing)
                scaleTween.Hide(completed);
            scaleTween.SetPaused(boundModel.IsSuspended);
        }
        private void SetSuspended(bool suspended)
        {
            scaleTween.SetPaused(suspended);
        }
        public void Unbind()
        {
            bindingVersion++;
            if (model != null)
            {
                model.StateChanged -= Present;
                model.SuspensionChanged -= SetSuspended;
            }
            model = null;
            if (scaleTween != null) scaleTween.Stop();
        }
        private void OnDisable()
        {
            var boundModel = model;
            Unbind();
            boundModel?.Dispose();
        }
        private void OnDestroy() => OnDisable();
    }
}
