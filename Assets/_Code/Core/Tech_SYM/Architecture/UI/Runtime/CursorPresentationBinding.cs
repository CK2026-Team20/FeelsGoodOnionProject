using System;
using Cooked.Contracts;
using Cooked.Dialogue;
using UnityEngine;

namespace Cooked.UI
{
    /// <summary>앱 조립자가 소유하는 OS 커서 출력. 게임 상태는 Flow/Options/Dialogue가 소유한다.</summary>
    public sealed class CursorPresentationBinding : IDisposable
    {
        private readonly IGameFlowService flow;
        private readonly OptionsService options;
        private readonly bool previousVisible;
        private readonly CursorLockMode previousLock;
        private DialogueService dialogue;
        private bool disposed;
#if UNITY_EDITOR
        private bool debugVisible;
        public void SetDebugVisible(bool visible)
        {
            if (disposed) return;
            debugVisible = visible;
            Refresh();
        }
#endif

        public CursorPresentationBinding(IGameFlowService flow, OptionsService options)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            previousVisible = Cursor.visible;
            previousLock = Cursor.lockState;
            flow.Changed += OnFlow;
            options.Changed += Refresh;
            Refresh();
        }

        public void BindDialogue(DialogueService value)
        {
            if (disposed) throw new ObjectDisposedException(nameof(CursorPresentationBinding));
            if (ReferenceEquals(dialogue, value)) return;
            if (dialogue != null) dialogue.Changed -= Refresh;
            dialogue = value;
            if (dialogue != null) dialogue.Changed += Refresh;
            Refresh();
        }

        private void OnFlow(FlowSnapshot _) => Refresh();
        public void RestoreOnFocus() => Refresh();
        private void Refresh()
        {
            if (disposed) return;
            bool visible = flow.Snapshot.State != FlowState.Playing || options.IsOpen ||
                (dialogue != null && dialogue.IsActive);
#if UNITY_EDITOR
            visible |= debugVisible;
#endif
            // 일반 플레이와 F로 닫는 메모는 포인터를 사용하지 않는다.
            Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = visible;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            flow.Changed -= OnFlow;
            options.Changed -= Refresh;
            if (dialogue != null) dialogue.Changed -= Refresh;
            dialogue = null;
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }
    }
}
