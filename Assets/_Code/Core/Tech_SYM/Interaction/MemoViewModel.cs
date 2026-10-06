using System;
using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    public enum MemoDisplayState { Opening, Open, Closing, Closed }
    /// <summary>표시 상태와 닫기 명령을 소유한다. 모델은 생성자로 주입한다.</summary>
    public sealed class MemoViewModel : IDisposable
    {
        private readonly MemoModel model;
        public event Action<MemoDisplayState> StateChanged;
        public event Action<bool> SuspensionChanged;
        private bool disposed;
        private readonly Func<int> readFrame;
        private int suppressCloseThroughFrame = -1;
        public bool IsSuspended { get; private set; }
        public Sprite Image => model.MemoImage;
        public MemoDisplayState State { get; private set; } = MemoDisplayState.Opening;
        public MemoViewModel(MemoModel model, Func<int> readFrame = null)
        {
            if (model == null || model.MemoID <= 0 || model.MemoImage == null)
                throw new ArgumentException("메모에는 양수 PK와 원화 Sprite가 필요합니다.", nameof(model));
            this.model = model;
            this.readFrame = readFrame ?? (() => Time.frameCount);
        }
        public void RequestClose()
        {
            if (disposed || IsSuspended || readFrame() <= suppressCloseThroughFrame) return;
            if (State == MemoDisplayState.Opening || State == MemoDisplayState.Open)
                SetState(MemoDisplayState.Closing);
        }
        public void CompletePresentation(MemoDisplayState presentedState)
        {
            if (disposed || IsSuspended) return;
            if (State != presentedState) return;
            if (State == MemoDisplayState.Opening) SetState(MemoDisplayState.Open);
            else if (State == MemoDisplayState.Closing) SetState(MemoDisplayState.Closed);
        }
        public void SetSuspended(bool value)
        {
            if (disposed || IsSuspended == value) return;
            IsSuspended = value;
            suppressCloseThroughFrame = readFrame();
            SuspensionChanged?.Invoke(value);
        }
        // 비활성화/반환/세션 종료는 애니메이션 완료를 기다리지 않는 취소 경로다.
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { SetState(MemoDisplayState.Closed); }
            finally { StateChanged = null; SuspensionChanged = null; }
        }
        private void SetState(MemoDisplayState value)
        {
            State = value;
            StateChanged?.Invoke(value);
        }
    }
}
