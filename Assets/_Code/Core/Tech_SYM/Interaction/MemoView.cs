using System;
using System.Threading;
using Cooked.Authoring;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>주입된 VM의 표시만 담당하며 자기 Tween·취소·원래 크기를 직접 소유한다.</summary>
    public sealed class MemoView : MonoBehaviour
    {
        [SerializeField, InspectorLabel("메모 이미지"), Tooltip("메모 원본의 Sprite를 표시할 Image를 연결하세요.")]
        private Image memoImage;
        [SerializeField, InspectorLabel("크기 표현 대상"), Tooltip("열고 닫을 때 크기를 바꿀 실제 RectTransform을 연결하세요.")]
        private RectTransform scaleRoot;
        [SerializeField, InspectorLabel("표현 시간"), Tooltip("메모를 열고 닫는 시간을 조절합니다(초, 0보다 큰 값)."), Min(.01f)]
        private float duration = .18f;
        private MemoViewModel model;
        private CancellationTokenSource lifetime;
        private Tween tween;
        private Vector3 restScale;
        private long presentationRevision;
        private bool hasRestScale;
        private MemoDisplayState? shownState;

        public void Bind(MemoViewModel viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            if (viewModel.IsDisposed) throw new ObjectDisposedException(nameof(viewModel));
            if (!isActiveAndEnabled) throw new InvalidOperationException("MemoView는 활성 상태에서 바인딩하세요.");
            if (memoImage == null || scaleRoot == null || !float.IsFinite(duration) || duration <= 0)
                throw new InvalidOperationException("MemoView의 Image·크기 표현 대상·양수 표현 시간이 필요합니다.");
            if (ReferenceEquals(model, viewModel)) { SetSuspended(model.IsSuspended); return; }
            Unbind();
            lifetime = new CancellationTokenSource();
            restScale = scaleRoot.localScale;
            hasRestScale = true;
            model = viewModel;
            memoImage.sprite = model.Image;
            model.StateChanged += Present;
            model.SuspensionChanged += SetSuspended;
            try { Present(model.State); SetSuspended(model.IsSuspended); }
            catch { Unbind(); throw; }
        }

        private void Present(MemoDisplayState state)
        {
            var bound = model;
            if (bound == null || bound.IsDisposed || shownState == state) return;
            StopTween();
            shownState = state;
            if (state == MemoDisplayState.Open) { scaleRoot.localScale = restScale; return; }
            if (state == MemoDisplayState.Closed) { scaleRoot.localScale = Vector3.zero; return; }
            if (state == MemoDisplayState.Opening) scaleRoot.localScale = Vector3.zero;
            long revision = presentationRevision;
            var token = lifetime.Token;
            tween = scaleRoot.DOScale(state == MemoDisplayState.Opening ? restScale : Vector3.zero, duration)
                .SetEase(state == MemoDisplayState.Opening ? Ease.OutBack : Ease.InQuad).SetUpdate(true)
                .OnComplete(() =>
                {
                    if (token.IsCancellationRequested || revision != presentationRevision || !ReferenceEquals(model, bound) ||
                        !isActiveAndEnabled || bound.IsDisposed || bound.State != state) return;
                    tween = null;
                    bound.CompletePresentation(state);
                });
            SetSuspended(bound.IsSuspended);
        }

        private void SetSuspended(bool suspended)
        {
            if (tween == null || !tween.IsActive()) return;
            if (suspended) tween.Pause(); else tween.Play();
        }
        private void StopTween()
        {
            presentationRevision++;
            var previous = tween;
            tween = null;
            previous?.Kill(false);
        }
        public void Unbind()
        {
            var previous = model;
            model = null;
            if (previous != null)
            {
                previous.StateChanged -= Present;
                previous.SuspensionChanged -= SetSuspended;
            }
            var cancellation = lifetime;
            lifetime = null;
            try { cancellation?.Cancel(); }
            finally
            {
                cancellation?.Dispose();
                StopTween();
                if (hasRestScale && scaleRoot != null) scaleRoot.localScale = restScale;
                hasRestScale = false;
                shownState = null;
                if (memoImage != null) memoImage.sprite = null;
            }
        }
        private void OnDisable()
        {
            var previous = model;
            try { Unbind(); }
            finally { previous?.CancelPresentation(); }
        }
        private void OnDestroy() => OnDisable();
    }
}
