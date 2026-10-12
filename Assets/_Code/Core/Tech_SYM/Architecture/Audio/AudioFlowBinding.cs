using System;
using Cooked.Contracts;

namespace Cooked.Audio
{
    /// <summary>Integration forwards accepted stage/chase state here; it must not also select music directly.</summary>
    public sealed class AudioFlowBinding : IDisposable
    {
        private readonly AudioService audio;
        private readonly IGameFlowService flow;
        private readonly IGameplayControlService control;
        private FlowState state;
        private string stageMusic;
        private bool chase, disposed;

        public AudioFlowBinding(AudioService audio, IGameFlowService flow, IGameplayControlService control)
        {
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            ApplyControl(control.State); ApplyFlow(flow.Snapshot);
            flow.Changed += ApplyFlow; control.Changed += ApplyControl;
        }

        public void SetStage(AudioCuePurpose purpose)
        {
            RequireAlive();
            if (AudioCueIds.KindForPurpose(purpose) != AudioCueKind.Music) throw new ArgumentException("Stage requires a music purpose.", nameof(purpose));
            stageMusic = AudioCueIds.ForPurpose(purpose); SelectMusic();
        }
        public void SetChaseActive(bool active)
        {
            RequireAlive();
            if (active && state != FlowState.Playing) throw new InvalidOperationException("Chase can start only while Playing.");
            chase = active; SelectMusic();
        }
        private void ApplyControl(ControlState value)
        { if (!disposed) audio.SetPauseState(value.WorldPaused, value.PresentationPaused); }
        private void ApplyFlow(FlowSnapshot value)
        {
            if (disposed) return;
            bool changedState = state != value.State;
            state = value.State;
            if (changedState)
            {
                switch (state)
                {
                    case FlowState.LoadingGame:
                    case FlowState.ReturningToTitle:
                    case FlowState.Recovering:
                    case FlowState.Title:
                        stageMusic = null; chase = false; audio.ClearWorldSfx(); break;
                    case FlowState.Retrying:
                        stageMusic = null; chase = false; audio.ClearWorldSfx(); break;
                    case FlowState.Ending:
                        chase = false; audio.ClearWorldSfx(); break;
                    case FlowState.Faulted:
                    case FlowState.Disposed:
                        audio.StopAll(); stageMusic = null; chase = false; break;
                }
            }
            SelectMusic();
        }
        private void SelectMusic()
        {
            switch (state)
            {
                case FlowState.Title: audio.PlayMusic(AudioCueIds.Title); break;
                case FlowState.Opening: audio.PlayMusic(AudioCueIds.Opening); break;
                case FlowState.Ending: audio.PlayMusic(AudioCueIds.Ending); break;
                case FlowState.Playing:
                    if (chase) audio.PlayMusic(AudioCueIds.Chase);
                    else if (stageMusic != null) audio.PlayMusic(stageMusic);
                    break;
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; flow.Changed -= ApplyFlow; control.Changed -= ApplyControl;
        }
        private void RequireAlive()
        { if (disposed) throw new ObjectDisposedException(nameof(AudioFlowBinding)); }
    }
}
