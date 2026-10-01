using System;
using System.Collections.Generic;
using Cooked.Contracts;

namespace Cooked.Audio
{
    /// <summary>Integration forwards accepted stage/chase state here; it must not also select music directly.</summary>
    public sealed class AudioFlowBinding : IDisposable
    {
        private readonly AudioService audio;
        private readonly IGameFlowService flow;
        private readonly IGameplayControlService control;
        private readonly Dictionary<string, string> stageMusic;
        private FlowState state;
        private string stageId;
        private bool chase, disposed;

        public AudioFlowBinding(AudioService audio, IGameFlowService flow, IGameplayControlService control,
            IReadOnlyDictionary<string, string> stageMusic)
        {
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            if (stageMusic == null || stageMusic.Count == 0) throw new ArgumentException("Stage music map required.", nameof(stageMusic));
            this.stageMusic = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in stageMusic)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                    throw new ArgumentException("Empty stage music mapping.", nameof(stageMusic));
                this.stageMusic.Add(pair.Key, pair.Value);
            }
            ApplyControl(control.State); ApplyFlow(flow.Snapshot);
            flow.Changed += ApplyFlow; control.Changed += ApplyControl;
        }

        public void SetStage(string id)
        {
            RequireAlive();
            if (id == null || !stageMusic.ContainsKey(id)) throw new ArgumentException("Unmapped stage: " + id, nameof(id));
            stageId = id; SelectMusic();
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
                        stageId = null; chase = false; audio.ClearWorldSfx(); break;
                    case FlowState.Retrying:
                        stageId = null; chase = false; audio.ClearWorldSfx(); break;
                    case FlowState.Ending:
                        chase = false; audio.ClearWorldSfx(); break;
                    case FlowState.Faulted:
                    case FlowState.Disposed:
                        audio.StopAll(); stageId = null; chase = false; break;
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
                    else if (stageId != null) audio.PlayMusic(stageMusic[stageId]);
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
