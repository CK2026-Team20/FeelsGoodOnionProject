using System;
using Cooked.Contracts;

namespace Cooked.Audio
{
    /// <summary>앱 오디오 정책. 트리거 대신 수락된 Flow/Stage/Chase 상태만 받는다.</summary>
    public sealed class AudioFlowBinding : IDisposable
    {
        private readonly AudioService audio;
        private readonly IGameFlowService flow;
        private readonly IGameplayControlService control;
        private readonly AudioCueCatalog catalog;
        private AudioCueCatalog.StageMusic stageMusic;
        private FlowState state;
        private bool chase, disposed;

        public AudioFlowBinding(AudioService audio, IGameFlowService flow, IGameplayControlService control,
            AudioCueCatalog catalog = null)
        {
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            this.catalog = catalog;
            ApplyControl(control.State); ApplyFlow(flow.Snapshot);
            flow.Changed += ApplyFlow; control.Changed += ApplyControl;
        }
        public void SetStage(string stageId)
        {
            RequireAlive();
            if (catalog == null) throw new InvalidOperationException("앱 오디오 카탈로그가 없습니다.");
            stageMusic = catalog.RequireStage(stageId);
            SelectMusic();
        }
        public void SetChaseActive(bool active)
        {
            RequireAlive();
            if (active && state != FlowState.Playing) throw new InvalidOperationException("Chase can start only while Playing.");
            if (chase == active) return;
            chase = active; SelectMusic();
        }
        private void ApplyControl(ControlState value)
        { if (!disposed) audio.SetPauseState(value.WorldPaused, value.PresentationPaused); }
        private void ApplyFlow(FlowSnapshot value)
        {
            if (disposed) return;
            var previous = state;
            bool changed = previous != value.State;
            state = value.State;
            if (changed)
            {
                switch (state)
                {
                    case FlowState.ReturningToTitle:
                    case FlowState.Recovering:
                        stageMusic = null; chase = false; audio.ClearWorldSfx(); audio.FadeOutMusic(); break;
                    case FlowState.Title:
                        stageMusic = null; chase = false; audio.ClearWorldSfx(); break;
                    case FlowState.LoadingGame:
                        chase = false; audio.ClearWorldSfx(); audio.FadeOutMusic(); break;
                    case FlowState.Retrying:
                        // Actor/Stage reset does not own the app's ongoing music playback cursor.
                        chase = false; audio.ClearWorldSfx(); break;
                    case FlowState.Ending:
                        chase = false; audio.ClearWorldSfx(); break;
                    case FlowState.Faulted:
                    case FlowState.Disposed:
                        audio.StopAll(); stageMusic = null; chase = false; break;
                }
            }
            SelectMusic();
            // Playing은 Actor 생성/카메라 준비/표시 전환이 성공한 뒤에만 진입한다.
            if (changed && previous == FlowState.Retrying && state == FlowState.Playing)
                audio.PlaySfx(AudioCueIds.Respawn);
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
                    else if (stageMusic != null) audio.PlayMusic(stageMusic.Clip, stageMusic.Gain, stageMusic.TransitionSeconds);
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
