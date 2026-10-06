using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Session;
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
        private bool changing;
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
            if (changing || IsOpen || !CanOpen) return;
            changing = true;
            try
            {
                // 모든 스크린 모달은 같은 정지 계약을 따른다. 타이틀도 예외가 아니다.
                inputLease = control.BlockGameplay("Options");
                worldLease = control.PauseWorld("Options");
                presentationLease = control.PausePresentation("Options");
                // 제어 상태 통지 중 전환/폐기가 발생했으면 새 모달을 게시하지 않는다.
                if (!CanOpen) { ReleaseLeases(); return; }
                IsOpen = true;
            }
            catch (Exception original)
            {
                try { ReleaseLeases(); }
                catch (Exception cleanup) { throw new AggregateException("옵션 열기와 정리 실패.", original, cleanup); }
                throw;
            }
            finally { changing = false; }
            Changed?.Invoke();
        }
        public void Close()
        {
            if (changing || !IsOpen) return;
            changing = true;
            IsOpen = false;
            var errors = new List<Exception>();
            try
            {
                SessionCleanup.Attempt(errors, ReleaseLeases);
                try { settings.Save(); Error = null; }
                catch (Exception e)
                {
                    Error = "설정을 저장하지 못했습니다: " + e.Message;
                    SessionCleanup.Attempt(errors, () => reportError("[Cooked.UI.OptionsService] Settings persistence failed while closing options. Runtime values retained; next close retries. " + e));
                }
                SessionCleanup.Notify(errors, Changed);
            }
            finally { changing = false; }
            SessionCleanup.ThrowIfAny(errors, "옵션 닫기 중 정리/통지 실패.");
        }
        private void ReleaseLeases()
        {
            // Release presentation last: consumers see gameplay/world state restored first.
            var input = inputLease; var world = worldLease; var presentation = presentationLease;
            inputLease = null; worldLease = null; presentationLease = null;
            var errors = new List<Exception>();
            if (input != null) SessionCleanup.Attempt(errors, input.Dispose);
            if (world != null) SessionCleanup.Attempt(errors, world.Dispose);
            if (presentation != null) SessionCleanup.Attempt(errors, presentation.Dispose);
            SessionCleanup.ThrowIfAny(errors, "옵션 정지 토큰 해제 실패.");
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
            if (disposed) return;
            disposed = true;
            flow.Changed -= OnFlow;
            var errors = new List<Exception>();
            try
            {
                SessionCleanup.Attempt(errors, Close);
                SessionCleanup.Attempt(errors, ReleaseLeases);
            }
            finally { IsOpen = false; Changed = null; }
            SessionCleanup.ThrowIfAny(errors, "옵션 폐기 실패.");
        }
    }
}
