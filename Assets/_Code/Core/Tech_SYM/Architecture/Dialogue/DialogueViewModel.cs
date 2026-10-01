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
        public DialogueViewModel(DialogueService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            service.Changed += OnChanged;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public bool IsActive => service.IsActive;
        public bool CanAdvance => service.CanAdvance;
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
        public void Advance(int frameId) { if (!disposed) service.Advance(frameId); }
        public void SkipAll() { if (!disposed) service.SkipAll(); }
        public void ToggleAuto() { if (!disposed) service.SetAuto(!service.AutoEnabled); }
        public void OpenLog() { if (!disposed) service.OpenLog(); }
        public void CloseLog() { if (!disposed) service.CloseLog(); }
        public void NotifyRevealCompleted(long sessionId, long rowId, int frameId)
        { if (!disposed) service.NotifyRevealCompleted(sessionId, rowId, frameId); }
        public void CancelPresentation() { if (!disposed) service.Cancel(); }
        private void OnChanged() { if (!disposed) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            service.Cancel();
            service.Changed -= OnChanged;
            PropertyChanged = null;
        }
    }
}
