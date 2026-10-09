using System;
using System.Collections.Generic;
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
        public bool IsDisposed => disposed;
        public MemoModel Source => model;
        public Sprite Image => model.MemoImage;
        public MemoDisplayState State { get; private set; } = MemoDisplayState.Opening;
        public MemoViewModel(MemoModel model, Func<int> readFrame = null)
        {
            if (model == null || model.MemoImage == null)
                throw new ArgumentException("메모 원본 Model과 실제 Sprite가 필요합니다.", nameof(model));
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
            var errors = new List<Exception>();
            var listeners = SuspensionChanged;
            if (listeners != null)
                foreach (Action<bool> listener in listeners.GetInvocationList())
                {
                    if (disposed || IsSuspended != value) break;
                    if (SuspensionChanged == null || Array.IndexOf(SuspensionChanged.GetInvocationList(), listener) < 0) continue;
                    try { listener(value); } catch (Exception error) { errors.Add(error); }
                }
            if (errors.Count > 0) throw new AggregateException("Memo suspension observer failures.", errors);
        }
        // 비활성화/반환/세션 종료는 애니메이션 완료를 기다리지 않는 취소 경로다.
        public void CancelPresentation()
        {
            if (!disposed && State != MemoDisplayState.Closed) SetState(MemoDisplayState.Closed);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            State = MemoDisplayState.Closed;
            StateChanged = null; SuspensionChanged = null;
        }
        private void SetState(MemoDisplayState value)
        {
            State = value;
            var errors = new List<Exception>();
            var listeners = StateChanged;
            if (listeners != null)
                foreach (Action<MemoDisplayState> listener in listeners.GetInvocationList())
                {
                    if (disposed || State != value) break;
                    if (StateChanged == null || Array.IndexOf(StateChanged.GetInvocationList(), listener) < 0) continue;
                    try { listener(value); } catch (Exception error) { errors.Add(error); }
                }
            if (errors.Count > 0) throw new AggregateException("Memo state observer failures.", errors);
        }
    }
}
