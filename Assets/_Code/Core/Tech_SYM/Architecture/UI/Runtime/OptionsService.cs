using System;
using Cooked.Contracts;
namespace Cooked.UI
{
    public sealed class OptionsService : IDisposable
    {
        private readonly IGameFlowService flow;
        private readonly IGameplayControlService control;
        private readonly SettingsService settings;
        private readonly Action<string> reportError;
        private IDisposable inputLease, worldLease, presentationLease;
        private bool disposed;
        public bool IsOpen { get; private set; }
        public bool CanOpen => !disposed && CanOpenAt(flow.Snapshot);
        public bool CanNavigate => !disposed && flow.Snapshot.State == FlowState.Playing && !flow.Snapshot.IsBusy;
        public string Error { get; private set; }
        public event Action Changed;
        public static bool CanOpenAt(FlowSnapshot value) =>
            value.State == FlowState.Opening || value.State == FlowState.Ending ||
            (!value.IsBusy && (value.State == FlowState.Title || value.State == FlowState.Playing));
        public OptionsService(IGameFlowService flow, IGameplayControlService control, SettingsService settings, Action<string> reportError = null)
        {
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.reportError = reportError ?? (message => UnityEngine.Debug.LogError(message));
            flow.Changed += OnFlow;
        }
        public void Toggle() { if (IsOpen) Close(); else Open(); }
        public void Open()
        {
            if (IsOpen || !CanOpen) return;
            try
            {
                // Title has no gameplay to freeze. Music and UI remain audible in every options screen.
                if (flow.Snapshot.State != FlowState.Title)
                {
                    inputLease = control.BlockGameplay("Options"); worldLease = control.PauseWorld("Options");
                    presentationLease = control.PausePresentation("Options");
                }
                IsOpen = true;
            }
            catch { ReleaseLeases(); throw; }
            Changed?.Invoke();
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; ReleaseLeases();
            try { settings.Save(); Error = null; }
            catch (Exception e)
            {
                Error = "설정을 저장하지 못했습니다: " + e.Message;
                reportError("[Cooked.UI.OptionsService] Settings persistence failed while closing options. Runtime values retained; next close retries. " + e);
            }
            Changed?.Invoke();
        }
        private void ReleaseLeases()
        {
            // Release presentation last: consumers see gameplay/world state restored first.
            inputLease?.Dispose(); inputLease = null; worldLease?.Dispose(); worldLease = null;
            presentationLease?.Dispose(); presentationLease = null;
        }
        public void Navigate(FlowCommand command)
        {
            if (!CanNavigate || (command != FlowCommand.RetryCheckpoint && command != FlowCommand.ReturnToTitle)) return;
            Close();
            if (!flow.Request(new FlowRequest(command))) { Error = "현재 전환 요청을 처리할 수 없습니다."; Changed?.Invoke(); }
        }
        private void OnFlow(FlowSnapshot snapshot)
        {
            if (!CanOpenAt(snapshot)) Close(); // Synchronous: do not leave pause leases through loading.
            Changed?.Invoke();
        }
        public void Dispose()
        {
            if (disposed) return; flow.Changed -= OnFlow; Close(); ReleaseLeases(); disposed = true; Changed = null;
        }
    }
}
