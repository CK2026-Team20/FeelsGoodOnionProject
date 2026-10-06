using System;
using System.Collections.Generic;
using Cooked.Contracts;

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
        private readonly IMusicCrossfadeDriver crossfade;
        private readonly Action<string> warning;
        private readonly HashSet<string> warned = new HashSet<string>(StringComparer.Ordinal);
        private readonly Voice[] voices = new Voice[ChannelCount];
        private readonly float fadeDuration;
        private float master = 1, music = 0.7f, sfx = 1;
        private bool worldPaused, presentationPaused, disposed;
        private int targetChannel = -1;
        private long nextOrder;
        private long transitionRevision;
        public string CurrentMusicId { get; private set; }
        public int ReplacedWorldVoices { get; private set; }
        public int RejectedRequests { get; private set; }

        public AudioService(IEnumerable<AudioCue> catalog, IAudioPlaybackDriver driver,
            IMusicCrossfadeDriver crossfade, Action<string> warning, float crossfadeSeconds = 0.3f)
        {
            cues = AudioCue.Index(catalog);
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
            if (CurrentMusicId == cueId) return;
            CancelCrossfade();
            int next = -1;
            for (int i = 0; i < 2; i++) if (voices[i].Cue?.Id == cueId) next = i;
            if (next < 0)
            {
                next = voices[0].Cue == null ? 0 : voices[1].Cue == null ? 1 :
                    (voices[0].Envelope <= voices[1].Envelope ? 0 : 1);
                StopVoice(next);
                StartVoice(next, cue, 0);
            }
            targetChannel = next;
            CurrentMusicId = cueId;
            if (fadeDuration == 0) FinishFade();
            else
            {
                long ticket = transitionRevision;
                crossfade.Start(voices[0].Envelope, next == 0 ? 1 : 0,
                    voices[1].Envelope, next == 1 ? 1 : 0, fadeDuration, false,
                    (a, b) =>
                    {
                        if (disposed || ticket != transitionRevision) return;
                        voices[0].Envelope = a; voices[1].Envelope = b; ApplyGains();
                    }, () =>
                    {
                        if (!disposed && ticket == transitionRevision) FinishFade();
                    });
            }
        }

        public void StopMusic()
        {
            RequireAlive();
            CancelCrossfade();
            StopVoice(0); StopVoice(1);
            targetChannel = -1; CurrentMusicId = null;
        }

        public void PlaySfx(string cueId)
        {
            RequireAlive();
            if (!TryCue(cueId, false, out var cue)) return;
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
        }

        /// <summary>Flow cleanup deliberately excludes accepted death/escape one-shots.</summary>
        public void ClearWorldSfx()
        {
            RequireAlive();
            for (int i = FirstWorldChannel; i < ChannelCount; i++) StopVoice(i);
        }

        public void StopAll()
        {
            RequireAlive(); StopMusic();
            for (int i = 2; i < ChannelCount; i++) StopVoice(i);
        }

        public void SetPauseState(bool world, bool presentation)
        {
            RequireAlive(); worldPaused = world; presentationPaused = presentation;
            // Music continues through modal pauses so volume changes can be heard.
            crossfade.SetPaused(false);
            for (int i = 0; i < ChannelCount; i++) RefreshPause(i);
        }

        /// <summary>Polls native one-shot completion only; no timer or interpolation lives here.</summary>
        public void PollCompletedVoices()
        {
            if (disposed) return;
            for (int i = 2; i < ChannelCount; i++)
                if (voices[i].Cue != null && !voices[i].Paused && !driver.IsPlaying(i)) StopVoice(i);
        }

        public void Dispose()
        {
            if (disposed) return;
            StopAll(); disposed = true; crossfade.Dispose();
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
            driver.Start(channel, cue.Id, cue.Kind == AudioCueKind.Music);
            RefreshPause(channel);
        }
        private void StopVoice(int channel)
        {
            driver.Stop(channel);
            voices[channel].Cue = null; voices[channel].Envelope = 0; voices[channel].Paused = false;
        }
        private void RefreshPause(int channel)
        {
            var voice = voices[channel];
            if (voice.Cue == null) return;
            bool pause = channel == TransitionChannel ? presentationPaused :
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
        { for (int i = 0; i < ChannelCount; i++) driver.SetGain(i, Gain(i)); }
        private void FinishFade()
        {
            for (int i = 0; i < 2; i++)
                if (i != targetChannel) StopVoice(i); else voices[i].Envelope = 1;
            ApplyGains();
        }
        private void CancelCrossfade()
        { transitionRevision++; crossfade.Cancel(); }
        private void RequireAlive()
        { if (disposed) throw new ObjectDisposedException(nameof(AudioService)); }
    }
}
