using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Audio
{
    /// <summary>
    /// 논리 등록, 추적 위치, 가상 시계와 출력 슬롯 수명 소유자.
    /// 요청의 ID별 제한은 AudioService가 판정하며, 이 객체는 승인된 요청의 가청 수명만 관리한다.
    /// </summary>
    public sealed class SfxPlaybackRuntime : IDisposable
    {
        private sealed class Voice
        {
            public AudioCue Cue;
            public int Handle, OutputToken;
            public bool Spatial, HasFollow, Paused;
            public Transform Follow;
            public Vector3 Position, LocalOffset;
            public double Elapsed, LastClock;
            public float ClipLength;
        }
        public const float MaximumAudibleDistance = 20f;
        private readonly ISfxOutput output;
        private readonly GameplayAudioSettings settings;
        private readonly Func<double> realtime;
        private readonly List<Voice> voices = new List<Voice>();
        private Transform listener;
        private int nextHandle;
        private float volume = 1;
        private bool worldPaused, presentationPaused, disposed;
        public int ActiveSourceCount => output.ActiveSourceCount;
        public int PooledSourceCount => output.PooledSourceCount;
        public int RegisteredCount => voices.Count;
        public int VirtualLoopCount { get { int n = 0; foreach (var v in voices) if (v.Cue.PlaybackPolicy == SfxPlaybackPolicy.VirtualLoop) n++; return n; } }

        public SfxPlaybackRuntime(ISfxOutput output, GameplayAudioSettings settings, Func<double> realtime)
        {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); settings.Validate();
            this.realtime = realtime ?? throw new ArgumentNullException(nameof(realtime));
        }
        public int Start(AudioCue cue, bool spatial, Vector3 position, Transform follow = null)
        {
            RequireAlive();
            if (cue == null || cue.Kind == AudioCueKind.Music) throw new ArgumentException("효과음 설정이 필요합니다.", nameof(cue));
            if (cue.PlaybackPolicy == SfxPlaybackPolicy.VirtualLoop && !spatial) throw new ArgumentException("VirtualLoop에는 위치 출력이 필요합니다.");
            ValidatePosition(position);
            double now = ReadClock();
            var voice = new Voice
            {
                Cue = cue, Handle = checked(++nextHandle), Spatial = spatial, Position = position,
                Follow = follow, HasFollow = !ReferenceEquals(follow, null), LastClock = now,
                LocalOffset = follow != null ? follow.InverseTransformPoint(position) : Vector3.zero,
                ClipLength = output.ClipLength(cue.Id)
            };
            if (!float.IsFinite(voice.ClipLength) || voice.ClipLength <= 0)
                throw new InvalidOperationException("효과음 출력이 유효한 Clip 길이를 제공하지 않았습니다: " + cue.Id);
            voice.Paused = ShouldPause(voice);
            voices.Add(voice);
            try { RefreshVoice(voice, now); return voices.Contains(voice) ? voice.Handle : 0; }
            catch { Release(voice); throw; }
        }
        public int CountActive(string cueId)
        {
            int n = 0;
            foreach (var voice in voices)
                if (voice.Cue.Id == cueId && voice.OutputToken != 0 && output.IsActive(voice.OutputToken)) n++;
            return n;
        }
        public int CountRegistered(string cueId)
        { int n = 0; foreach (var voice in voices) if (voice.Cue.Id == cueId) n++; return n; }
        public bool IsWorldVoiceRegistered(int handle) => handle != 0 && voices.Exists(v => v.Handle == handle && v.Cue.Kind == AudioCueKind.World);
        public double VirtualElapsedSeconds(int handle)
        { var voice = voices.Find(v => v.Handle == handle); return voice?.Elapsed ?? 0; }
        public void StopWorldVoice(int handle)
        {
            var voice = voices.Find(v => v.Handle == handle && v.Cue.Kind == AudioCueKind.World);
            if (voice != null) Release(voice);
        }
        public void SetListener(Transform value) { RequireAlive(); listener = value; Tick(); }
        public void SetVolume(float value) { RequireAlive(); AudioCue.ValidateUnit(value, nameof(value)); volume = value; Tick(); }
        public void SetPauseState(bool world, bool presentation)
        {
            RequireAlive();
            double now = ReadClock();
            Tick(now); // 이전 정지 상태로 현재 시각까지 계산한다.
            worldPaused = world; presentationPaused = presentation;
            Tick(now);
        }
        public void Tick() { if (!disposed && voices.Count > 0) Tick(ReadClock()); }
        private void Tick(double now)
        {
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                var voice = voices[i];
                if (voice.Cue.PlaybackPolicy != SfxPlaybackPolicy.VirtualLoop && voice.OutputToken != 0 && !output.IsActive(voice.OutputToken)) Release(voice);
                else RefreshVoice(voice, now);
            }
        }
        private void RefreshVoice(Voice voice, double now)
        {
            if (!voice.Paused) voice.Elapsed += Math.Max(0, now - voice.LastClock);
            voice.LastClock = now;
            if (voice.HasFollow)
            {
                if (voice.Follow == null || !voice.Follow.gameObject.activeInHierarchy || !voice.Follow.gameObject.scene.isLoaded)
                { Release(voice); return; }
                voice.Position = voice.Follow.TransformPoint(voice.LocalOffset);
            }
            voice.Paused = ShouldPause(voice);
            float gain = volume * voice.Cue.Gain;
            if (voice.Spatial)
            {
                bool hasListener = listener != null && listener.gameObject.activeInHierarchy && listener.gameObject.scene.isLoaded;
                float distance = hasListener ? Vector3.Distance(listener.position, voice.Position) : float.PositiveInfinity;
                gain *= distance >= MaximumAudibleDistance ? 0 : settings.Attenuation(distance);
            }
            bool virtualLoop = voice.Cue.PlaybackPolicy == SfxPlaybackPolicy.VirtualLoop;
            if (gain <= 0)
            {
                if (virtualLoop) ReturnSlot(voice);
                else Release(voice); // 단발음은 무음 진입 시 완료 처리해 복귀 후 늦게 재생하지 않는다.
                return;
            }
            if (virtualLoop && voice.OutputToken != 0 && !output.IsActive(voice.OutputToken)) ReturnSlot(voice);
            if (voice.OutputToken == 0)
            {
                if (virtualLoop && voice.Paused) return;
                float seek = virtualLoop ? (float)(voice.Elapsed % voice.ClipLength) : 0;
                if (seek >= voice.ClipLength) seek = 0;
                voice.OutputToken = output.Start(voice.Cue, voice.Spatial, voice.Position, gain, seek, voice.Paused);
                if (voice.OutputToken == 0) throw new InvalidOperationException("효과음 출력이 유효한 슬롯을 반환하지 않았습니다.");
            }
            else
            {
                output.SetGain(voice.OutputToken, gain);
                if (voice.Spatial) output.SetPosition(voice.OutputToken, voice.Position);
                output.SetPaused(voice.OutputToken, voice.Paused);
            }
        }
        private bool ShouldPause(Voice voice) => voice.Cue.Kind == AudioCueKind.UI ? false :
            voice.Cue.Kind == AudioCueKind.Transition ? presentationPaused : worldPaused || (voice.Spatial && presentationPaused);
        private void ReturnSlot(Voice voice)
        { if (voice.OutputToken != 0) output.Stop(voice.OutputToken); voice.OutputToken = 0; }
        private void Release(Voice voice)
        { ReturnSlot(voice); voice.Follow = null; voices.Remove(voice); }
        public void ClearWorld() => Clear(true);
        public void Clear() => Clear(false);
        private void Clear(bool worldOnly)
        {
            var errors = new List<Exception>();
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                var voice = voices[i];
                if (worldOnly && voice.Cue.Kind != AudioCueKind.World) continue;
                try { Release(voice); }
                // Stop 실패 시 등록과 OutputToken을 유지해야 다음 정리에서 같은 출력을 반환할 수 있다.
                catch (Exception error) { errors.Add(error); }
            }
            if (errors.Count > 0) throw new AggregateException("효과음 출력 반환 실패.", errors);
        }
        private double ReadClock()
        {
            double now = realtime();
            if (double.IsNaN(now) || double.IsInfinity(now)) throw new InvalidOperationException("효과음 시계는 유한값이어야 합니다.");
            return now;
        }
        private static void ValidatePosition(Vector3 position)
        {
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z)) throw new ArgumentOutOfRangeException(nameof(position));
        }
        private void RequireAlive() { if (disposed) throw new ObjectDisposedException(nameof(SfxPlaybackRuntime)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { Clear(); }
            finally { listener = null; output.Dispose(); }
        }
    }
}
