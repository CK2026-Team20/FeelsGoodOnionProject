using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Audio
{
    /// <summary>App-lifetime policy. Contains no scene, UI, disk, or player-model access.</summary>
    public sealed class AudioService : IAudioService
    {
        public const int ChannelCount = 10;
        public const int UiChannel = 2, TransitionChannel = 3, FirstWorldChannel = 4;
        private sealed class Voice
        {
            public AudioCue Cue;
            public float Envelope;
            public long Order;
            public bool Paused;
        }
        private readonly Dictionary<string, AudioCue> cues;
        private readonly IAudioPlaybackDriver driver;
        private readonly WorldAudioOutput worldOutput;
        private readonly IMusicCrossfadeDriver crossfade;
        private readonly Action<string> warning;
        private readonly HashSet<string> warned = new HashSet<string>(StringComparer.Ordinal);
        private readonly Voice[] voices = new Voice[ChannelCount];
        private readonly float fadeDuration;
        private readonly Func<double> realtime;
        private readonly Dictionary<string, double> lastSfxStart = new Dictionary<string, double>(StringComparer.Ordinal);
        public int SuppressedSfxRequests { get; private set; }
        private float master = 1, music = 0.7f, sfx = 1;
        private bool worldPaused, presentationPaused, disposed;
        private long nextOrder;
        private long transitionRevision;
        public string CurrentMusicId { get; private set; }
        public int ReplacedWorldVoices { get; private set; }
        public int RejectedRequests { get; private set; }

        public AudioService(IEnumerable<AudioCue> catalog, IAudioPlaybackDriver driver,
            IMusicCrossfadeDriver crossfade, Action<string> warning, float crossfadeSeconds = 0.3f, WorldAudioOutput worldOutput = null, Func<double> realtime = null)
        {
            cues = AudioCue.Index(catalog);
            this.realtime = realtime ?? (() => Time.realtimeSinceStartupAsDouble);
            this.worldOutput = worldOutput;
            this.driver = driver ?? throw new ArgumentNullException(nameof(driver));
            this.crossfade = crossfade ?? throw new ArgumentNullException(nameof(crossfade));
            this.warning = warning ?? throw new ArgumentNullException(nameof(warning));
            if (float.IsNaN(crossfadeSeconds) || float.IsInfinity(crossfadeSeconds) || crossfadeSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(crossfadeSeconds));
            fadeDuration = crossfadeSeconds;
            for (int i = 0; i < voices.Length; i++) voices[i] = new Voice();
        }

        public void SetVolumes(float master, float music, float sfx)
        {
            RequireAlive();
            AudioCue.ValidateUnit(master, nameof(master));
            AudioCue.ValidateUnit(music, nameof(music));
            AudioCue.ValidateUnit(sfx, nameof(sfx));
            this.master = master; this.music = music; this.sfx = sfx;
            ApplyGains();
        }

        public void PlayMusic(string cueId)
        {
            RequireAlive();
            if (!TryCue(cueId, true, out var cue)) return;
            PlayMusic(cue, fadeDuration);
        }
        public void PlayMusic(AudioClip clip, float gain, float crossfadeSeconds)
        {
            RequireAlive();
            AudioCue.ValidateUnit(gain, nameof(gain));
            if (!float.IsFinite(crossfadeSeconds) || crossfadeSeconds < 0) throw new ArgumentOutOfRangeException(nameof(crossfadeSeconds));
            var id = driver.ResolveMusicId(clip);
            if (CurrentMusicId == id || driver.IsSameClip(CurrentMusicId, id)) return;
            var cue = new AudioCue(id, AudioCueKind.Music, gain);
            cues[id] = cue;
            PlayMusic(cue, crossfadeSeconds);
        }
        private void PlayMusic(AudioCue cue, float duration)
        {
            if (CurrentMusicId == cue.Id || driver.IsSameClip(CurrentMusicId, cue.Id)) return;
            CancelCrossfade();
            CurrentMusicId = cue.Id;
            long ticket = transitionRevision;
            Action startNext = () =>
            {
                if (disposed || ticket != transitionRevision) return;
                StopVoice(0); StopVoice(1);
                StartVoice(0, cue, 1);
            };
            // 다음 곡은 기존 음악의 페이드아웃이 완료된 뒤 시작한다.
            if (duration == 0 ||
                (voices[0].Cue == null && voices[1].Cue == null)) { startNext(); return; }
            crossfade.Start(voices[0].Envelope, 0, voices[1].Envelope, 0, duration, presentationPaused,
                (a, c) => { if (!disposed && ticket == transitionRevision) { voices[0].Envelope = a; voices[1].Envelope = c; ApplyGains(); } },
                startNext);
        }
        public void FadeOutMusic()
        {
            RequireAlive();
            if (CurrentMusicId == null) return;
            CancelCrossfade();
            CurrentMusicId = null;
            long ticket = transitionRevision;
            if (fadeDuration == 0) { StopMusic(); return; }
            crossfade.Start(voices[0].Envelope, 0, voices[1].Envelope, 0, fadeDuration, presentationPaused,
                (a, b) => { if (!disposed && ticket == transitionRevision) { voices[0].Envelope = a; voices[1].Envelope = b; ApplyGains(); } },
                () => { if (!disposed && ticket == transitionRevision) { StopVoice(0); StopVoice(1); } });
        }

        public void StopMusic()
        {
            RequireAlive();
            var errors = new List<Exception>();
            StopMusicTransition(errors);
            StopVoices(0, UiChannel, false, errors);
            if (errors.Count > 0) throw new AggregateException("음악 정지 실패.", errors);
        }

        public int PlaySfxAt(string cueId, Vector3 position, Transform follow = null)
        {
            RequireAlive();
            if (worldPaused || !TryCue(cueId, false, out var cue)) return 0;
            if (worldOutput == null) throw new InvalidOperationException("위치 기반 출력이 연결되지 않았습니다.");
            if (!CanStartSfx(cue, out double now)) return 0;
            int handle = worldOutput.Play(cue, position, follow);
            if (handle != 0) lastSfxStart[cue.Id] = now;
            return handle;
        }
        public void StopWorldVoice(int handle) => worldOutput?.Stop(handle);
        public void SetWorldListener(Transform listener) => worldOutput?.SetListener(listener);
        public void PlaySfx(string cueId)
        {
            RequireAlive();
            if (!TryCue(cueId, false, out var cue)) return;
            if (cue.Kind == AudioCueKind.World && worldPaused) return;
            if (!CanStartSfx(cue, out double now)) return;
            int channel;
            if (cue.Kind == AudioCueKind.UI) channel = UiChannel;
            else if (cue.Kind == AudioCueKind.Transition) channel = TransitionChannel;
            else
            {
                // Do not reserve a voice for a gameplay command rejected by pause.
                if (worldPaused) return;
                channel = FirstWorldChannel;
                bool found = false;
                for (int i = FirstWorldChannel; i < ChannelCount; i++)
                {
                    if (voices[i].Cue == null || (!voices[i].Paused && !driver.IsPlaying(i)))
                    { channel = i; found = true; break; }
                    if (voices[i].Order < voices[channel].Order) channel = i;
                }
                if (!found) ReplacedWorldVoices++;
            }
            StopVoice(channel);
            StartVoice(channel, cue, 1);
            lastSfxStart[cue.Id] = now;
        }

        private bool CanStartSfx(AudioCue cue, out double now)
        {
            now = realtime();
            if (lastSfxStart.TryGetValue(cue.Id, out double last) && now - last < cue.MinimumInterval)
            { SuppressedSfxRequests++; return false; }
            int count = worldOutput?.CountActive(cue.Id) ?? 0;
            for (int i = UiChannel; i < ChannelCount; i++)
                if (voices[i].Cue?.Id == cue.Id && (voices[i].Paused || driver.IsPlaying(i))) count++;
            if (count >= cue.MaxConcurrent) { SuppressedSfxRequests++; return false; }
            return true;
        }

        /// <summary>Flow cleanup deliberately excludes accepted death/escape one-shots.</summary>
        public void ClearWorldSfx()
        {
            RequireAlive();
            var errors = new List<Exception>();
            StopVoices(FirstWorldChannel, ChannelCount, false, errors);
            worldOutput?.Clear();
            if (errors.Count > 0) throw new AggregateException("월드 효과음 정지 실패.", errors);
        }

        public void StopAll()
        {
            RequireAlive();
            var errors = new List<Exception>();
            StopMusicTransition(errors);
            lastSfxStart.Clear();
            StopVoices(0, ChannelCount, false, errors);
            worldOutput?.Clear();
            if (errors.Count > 0) throw new AggregateException("전체 오디오 정지 실패.", errors);
        }

        public void SetPauseState(bool world, bool presentation)
        {
            RequireAlive(); worldPaused = world; presentationPaused = presentation;
            worldOutput?.SetState(master * sfx, world || presentation);
            // 옵션의 표시 정지는 음악과 진행 중인 전환도 함께 정지한다.
            crossfade.SetPaused(presentation);
            for (int i = 0; i < ChannelCount; i++) RefreshPause(i);
        }

        /// <summary>Polls native one-shot completion only; no timer or interpolation lives here.</summary>
        public void PollCompletedVoices()
        {
            if (disposed) return;
            worldOutput?.Tick();
            for (int i = 2; i < ChannelCount; i++)
                if (voices[i].Cue != null && !voices[i].Paused && !driver.IsPlaying(i)) StopVoice(i);
        }

        public void Dispose()
        {
            if (disposed) return;
            // 종료 중 재진입과 이미 예약된 완료를 외부 정리보다 먼저 차단한다.
            disposed = true;
            var errors = new List<Exception>();
            StopMusicTransition(errors);
            StopVoices(0, ChannelCount, true, errors);
            try { worldOutput?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { crossfade.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0) throw new AggregateException("오디오 종료 중 일부 자원 정리에 실패했습니다.", errors);
        }

        private void StopMusicTransition(List<Exception> errors)
        {
            transitionRevision++;

            CurrentMusicId = null;
            try { crossfade.Cancel(); } catch (Exception error) { errors.Add(error); }
        }

        // 정상 정지와 폐기가 같은 채널 정리를 사용하되, 파괴 출력 허용은 폐기에만 적용한다.
        private void StopVoices(int first, int endExclusive, bool allowDestroyedSource, List<Exception> errors)
        {
            for (int i = first; i < endExclusive; i++)
            {
                try { StopVoice(i, allowDestroyedSource); }
                catch (Exception error) { errors.Add(new InvalidOperationException($"오디오 채널 {i} 정지 실패.", error)); }
            }
        }

        private bool TryCue(string id, bool isMusic, out AudioCue cue)
        {
            cue = null;
            if (id != null && cues.TryGetValue(id, out cue) && (cue.Kind == AudioCueKind.Music) == isMusic) return true;
            RejectedRequests++;
            // Bound unknown-data diagnostics instead of retaining arbitrary unknown IDs forever.
            var key = (isMusic ? "music:" : "sfx:") + (id ?? "<null>");
            if (warned.Count < 32 && warned.Add(key)) warning("Unknown or wrong-kind audio cue: " + key);
            return false;
        }
        private void StartVoice(int channel, AudioCue cue, float envelope)
        {
            var voice = voices[channel]; voice.Cue = cue; voice.Envelope = envelope; voice.Order = ++nextOrder;
            // Set gain before Play so a new cue never emits one loud frame.
            driver.SetGain(channel, Gain(channel));
            driver.Start(channel, cue.Id, cue.Loop);
            RefreshPause(channel);
        }
        private void StopVoice(int channel, bool allowDestroyedSource = false)
        {
            voices[channel].Cue = null; voices[channel].Envelope = 0; voices[channel].Paused = false;
            driver.Stop(channel, allowDestroyedSource);
        }
        private void RefreshPause(int channel)
        {
            var voice = voices[channel];
            if (voice.Cue == null) return;
            bool pause = channel < 2 || channel == TransitionChannel ? presentationPaused :
                channel >= FirstWorldChannel && worldPaused;
            if (voice.Paused == pause) return;
            voice.Paused = pause; driver.SetPaused(channel, pause);
        }
        private float Gain(int channel)
        {
            var voice = voices[channel];
            return voice.Cue == null ? 0 : master * (channel < 2 ? music : sfx) * voice.Cue.Gain * voice.Envelope;
        }
        private void ApplyGains()
        { for (int i = 0; i < ChannelCount; i++) driver.SetGain(i, Gain(i)); worldOutput?.SetState(master * sfx, worldPaused || presentationPaused); }
        private void CancelCrossfade()
        { transitionRevision++; crossfade.Cancel(); }
        private void RequireAlive()
        { if (disposed) throw new ObjectDisposedException(nameof(AudioService)); }
    }
}
