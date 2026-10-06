using System;
using Cooked.Contracts;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class TitleViewModel : ObservableViewModel
    {
        private readonly IGameFlowService flow; private readonly OptionsService options; private readonly IApplicationService application;
        public bool CanStart => !IsDisposed && flow.Snapshot.State == FlowState.Title && !flow.Snapshot.IsBusy && !options.IsOpen;
        public TitleViewModel(IGameFlowService flow, OptionsService options, IApplicationService application = null)
        { this.flow = flow; this.options = options; this.application = application ?? new ApplicationService(); flow.Changed += OnFlow; options.Changed += OnOptions; }
        public void NewGame() { if (CanStart) flow.Request(new FlowRequest(FlowCommand.NewGame)); }
        public void Quit() { if (CanStart) application.Quit(); }
        public void OpenOptions() { if (CanStart) options.Open(); }
        private void OnFlow(FlowSnapshot _) => Notify(); private void OnOptions() => Notify();
        protected override void Release() { flow.Changed -= OnFlow; options.Changed -= OnOptions; }
    }
    public sealed class OptionsViewModel : ObservableViewModel
    {
        private readonly OptionsService options; private readonly SettingsService settings;
        public bool IsOpen => options.IsOpen; public bool CanOpen => options.CanOpen;
        public bool CanNavigate => options.CanNavigate; public string Error => options.Error;
        public float Master => settings.Master; public float Music => settings.Music; public float Sfx => settings.Sfx;
        public OptionsViewModel(OptionsService options, SettingsService settings)
        { this.options = options; this.settings = settings; options.Changed += OnChanged; settings.Changed += OnChanged; }
        private void OnChanged() => Notify();
        public void Toggle() { if (!IsDisposed) options.Toggle(); }
        public void Open() { if (!IsDisposed) options.Open(); }
        public void Close() { if (!IsDisposed) options.Close(); }
        public void Retry() { if (!IsDisposed) options.Navigate(FlowCommand.RetryCheckpoint); }
        public void ReturnToTitle() { if (!IsDisposed) options.Navigate(FlowCommand.ReturnToTitle); }
        public void SetMaster(float value) { if (!IsDisposed) settings.SetVolumes(value, Music, Sfx); }
        public void SetMusic(float value) { if (!IsDisposed) settings.SetVolumes(Master, value, Sfx); }
        public void SetSfx(float value) { if (!IsDisposed) settings.SetVolumes(Master, Music, value); }
        protected override void Release() { options.Changed -= OnChanged; settings.Changed -= OnChanged; }
    }
    public sealed class FadeViewModel : ObservableViewModel
    {
        private readonly FadeService service;
        public float Alpha => service.Alpha; public bool BlocksInput => service.BlocksInput;
        public FadeViewModel(FadeService service) { this.service = service; service.Changed += OnChanged; }
        private void OnChanged() => Notify();
        protected override void Release() => service.Changed -= OnChanged;
    }
    public sealed class HudViewModel : ObservableViewModel
    {
        private readonly IPlayerBridgeService bridge; private readonly IGameSessionService session;
        public bool Visible { get; private set; } = true;
        public string Health => bridge.HasActor ? $"체력  {bridge.Snapshot.Health} / {bridge.Snapshot.MaxHealth}" : "체력  —";
        public string Fragments => bridge.HasActor ? $"눈물 조각  {bridge.Snapshot.Fragments} / {bridge.Snapshot.MaxFragments}" : "눈물 조각  —";
        public string FragmentCount => bridge.HasActor ? $"{bridge.Snapshot.Fragments}/{bridge.Snapshot.MaxFragments}" : "—/—";
        public float HeartFill(int slot)
        {
            if (slot < 0 || slot >= 3) throw new ArgumentOutOfRangeException(nameof(slot));
            if (!bridge.HasActor) return 0;
            var snapshot = bridge.Snapshot;
            return Math.Max(0f, Math.Min(1f, snapshot.Health * 3f / snapshot.MaxHealth - slot));
        }
        public bool FormUnlocked => session.IsActive && session.IsUnlocked(AbilityId.FormChange);
        public bool TearUnlocked => session.IsActive && session.IsUnlocked(AbilityId.Tear);
        public bool RecoverUnlocked => session.IsActive && session.IsUnlocked(AbilityId.RecoverShell);
        public string Checkpoint => session.IsActive ? "체크포인트  " + session.Checkpoint.CheckpointId : "";
        public string Abilities => "이동 방향키 · 점프 Space · 상호작용 E" +
            (session.IsUnlocked(AbilityId.FormChange) ? " · 형태 전환 Q" : "") +
            (session.IsUnlocked(AbilityId.Tear) ? " · 눈물 F" : "");
        public HudViewModel(IPlayerBridgeService bridge, IGameSessionService session)
        { this.bridge = bridge; this.session = session; bridge.Changed += OnPlayer; session.Changed += OnSession; }
        public void SetVisible(bool value) { if (IsDisposed || Visible == value) return; Visible = value; Notify(); }
        private void OnPlayer(PlayerSnapshot _) => Notify(); private void OnSession() => Notify();
        protected override void Release() { bridge.Changed -= OnPlayer; session.Changed -= OnSession; }
    }
    public sealed class WorldPromptViewModel : ObservableViewModel
    {
        private bool gameplayVisible = true, targetVisible;
        public bool Visible => gameplayVisible && targetVisible;
        public Vector3 Position { get; private set; }
        public Quaternion Facing { get; private set; } = Quaternion.identity;
        public string Label { get; private set; } = "";
        public void SetPrompt(bool visible, Vector3 position, Quaternion facing, string label)
        { if (IsDisposed) return; targetVisible = visible; Position = position; Facing = facing; Label = "E  " + (label ?? "상호작용"); Notify(); }
        public void SetGameplayVisible(bool value) { if (IsDisposed) return; gameplayVisible = value; Notify(); }
        public void Clear() => SetPrompt(false, Vector3.zero, Quaternion.identity, "");
    }
}
