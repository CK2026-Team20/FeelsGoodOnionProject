using System;
using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    public enum MemoDisplayState { Opening, Open, Closing, Closed }
    /// <summary>표시 상태와 닫기 명령을 소유한다. 모델은 생성자로 주입한다.</summary>
    public sealed class MemoViewModel
    {
        private readonly MemoModel model;
        public event Action<MemoDisplayState> StateChanged;
        public Sprite Image => model.MemoImage;
        public MemoDisplayState State { get; private set; } = MemoDisplayState.Opening;
        public MemoViewModel(MemoModel model)
        {
            if (model == null || model.MemoID <= 0 || model.MemoImage == null)
                throw new ArgumentException("메모에는 양수 PK와 원화 Sprite가 필요합니다.", nameof(model));
            this.model = model;
        }
        public void RequestClose()
        {
            if (State == MemoDisplayState.Opening || State == MemoDisplayState.Open)
                SetState(MemoDisplayState.Closing);
        }
        public void CompletePresentation(MemoDisplayState presentedState)
        {
            if (State != presentedState) return;
            if (State == MemoDisplayState.Opening) SetState(MemoDisplayState.Open);
            else if (State == MemoDisplayState.Closing) SetState(MemoDisplayState.Closed);
        }
        private void SetState(MemoDisplayState value)
        {
            State = value;
            StateChanged?.Invoke(value);
        }
    }
}
