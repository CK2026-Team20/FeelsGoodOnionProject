using System;
using System.Collections.Generic;
using System.ComponentModel;
using Cooked.Contracts;

namespace Cooked.Dialogue
{
    /// <summary>Binding surface. Service owns rules; this object never knows a View or a tween.</summary>
    public sealed class DialogueViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DialogueService service;
        private bool disposed;
        private readonly Action click;
        public DialogueViewModel(DialogueService service, Action click = null)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service)); this.click = click;
            service.Changed += OnChanged;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsDisposed => disposed;
        public bool IsActive => !disposed && service.IsActive;
        public bool CanAdvance => !disposed && service.CanAdvance;
        public bool CanSkip => !disposed && service.CanSkip;
        public bool AutoEnabled => service.AutoEnabled;
        public bool LogOpen => service.LogOpen;
        public bool OptionsPaused => service.OptionsPaused;
        public bool PresentationPaused => service.PresentationPaused;
        public DialogueState State => service.State;
        public string Name => service.CurrentRow?.Name ?? string.Empty;
        public string Context => service.CurrentRow?.Context ?? string.Empty;
        public float RevealDuration => service.CurrentRow?.ContextRevealDuration ?? 0;
        public long SessionId => service.SessionId;
        public long RowId => service.RowId;
        public IReadOnlyList<DialogueRow> History => service.History;
        public void Advance(int frameId, bool pointer = false) { if (CanAdvance && service.Advance(frameId) && pointer) click?.Invoke(); }
        public void ToggleAuto(bool pointer = false) { if (IsActive && !PresentationPaused && !LogOpen) { bool old = AutoEnabled; service.SetAuto(!old); if (pointer && old != AutoEnabled) click?.Invoke(); } }
        public void OpenLog(bool pointer = false) { if (IsActive && !PresentationPaused && !LogOpen) { service.OpenLog(); if (pointer && LogOpen) click?.Invoke(); } }
        public void CloseLog(bool pointer = false) { if (IsActive && !OptionsPaused && LogOpen) { service.CloseLog(); if (pointer && !LogOpen) click?.Invoke(); } }
        public void SkipAll(bool pointer = false) { if (CanSkip) { service.SkipAll(); if (pointer) click?.Invoke(); } }
        public void NotifyRevealCompleted(long sessionId, long rowId, int frameId)
        { if (!disposed) service.NotifyRevealCompleted(sessionId, rowId, frameId); }
        public void CancelPresentation() { if (!disposed) service.Cancel(); }
        private void OnChanged() { if (!disposed) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { service.Cancel(); }
            finally
            {
                // A failing completion observer must not retain this disposed binding.
                service.Changed -= OnChanged;
                PropertyChanged = null;
            }
        }
    }
}
