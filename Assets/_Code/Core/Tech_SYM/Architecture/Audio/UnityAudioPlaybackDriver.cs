using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Audio
{
    public sealed class UnityAudioPlaybackDriver : IAudioPlaybackDriver
    {
        private readonly AudioSource[] sources;
        private readonly Dictionary<string, AudioClip> clips;
        public UnityAudioPlaybackDriver(AudioSource[] sources, AudioCueCatalog catalog)
        {
            if (sources == null || sources.Length != AudioService.ChannelCount)
                throw new ArgumentException("Exactly ten audio sources required.", nameof(sources));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.sources = (AudioSource[])sources.Clone();
            var unique = new HashSet<AudioSource>();
            foreach (var source in this.sources)
            {
                if (source == null || !unique.Add(source)) throw new ArgumentException("Missing or duplicate audio source.");
            }
            clips = catalog.CreateClipMap();
            // 모든 참조와 목록을 검증한 다음에만 빌린 출력의 기존 재생을 변경한다.
            foreach (var source in this.sources)
            {
                source.Stop(); source.playOnAwake = false; source.spatialBlend = 0;
                source.ignoreListenerPause = false; source.volume = 0;
            }
        }
        public void Start(int channel, string cueId, bool loop)
        {
            var source = sources[channel];
            source.clip = clips[cueId]; source.loop = loop; source.Play();
        }
        public void Stop(int channel, bool allowDestroyedSource = false)
        {
            var source = sources[channel];
            // 생성 시 null은 거절한다. 종료 중 먼저 파괴된 빌린 출력은 이미 정지됐다.
            if (source == null)
            {
                if (allowDestroyedSource) return;
                throw new InvalidOperationException($"오디오 채널 {channel}의 재생 출력이 사라졌습니다.");
            }
            source.Stop(); source.clip = null;
        }
        public void SetGain(int channel, float gain) => sources[channel].volume = gain;
        public void SetPaused(int channel, bool paused)
        { if (paused) sources[channel].Pause(); else sources[channel].UnPause(); }
        public bool IsPlaying(int channel) => sources[channel].isPlaying;

    }
}
