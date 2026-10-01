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
                source.Stop(); source.playOnAwake = false; source.spatialBlend = 0;
                source.ignoreListenerPause = false; source.volume = 0;
            }
            clips = catalog.CreateClipMap();
        }
        public void Start(int channel, string cueId, bool loop)
        {
            var source = sources[channel];
            source.clip = clips[cueId]; source.loop = loop; source.Play();
        }
        public void Stop(int channel)
        { sources[channel].Stop(); sources[channel].clip = null; }
        public void SetGain(int channel, float gain) => sources[channel].volume = gain;
        public void SetPaused(int channel, bool paused)
        { if (paused) sources[channel].Pause(); else sources[channel].UnPause(); }
        public bool IsPlaying(int channel) => sources[channel].isPlaying;

        /// <summary>Source-output evidence, not proof of physical loudspeaker playback.</summary>
        public float MeasureSourceOutputRms(int channel, float[] buffer)
        {
            if (buffer == null || buffer.Length != 1024) throw new ArgumentException("1024 samples required.", nameof(buffer));
            sources[channel].GetOutputData(buffer, 0);
            double square = 0;
            foreach (float sample in buffer) square += sample * sample;
            return (float)Math.Sqrt(square / buffer.Length);
        }
    }
}
