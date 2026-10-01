using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Audio
{
    /// <summary>Place exactly once in Bootstrapper. Explicit initialization prevents play-on-Awake races.</summary>
    public sealed class AudioRuntimeHost : MonoBehaviour
    {
        [SerializeField] private AudioCueCatalog catalog = null;
        [SerializeField] private AudioSource[] sources = Array.Empty<AudioSource>();
        private AudioService service;
        private AudioFlowBinding binding;
        private UnityAudioPlaybackDriver driver;
        private readonly float[] outputBuffer = new float[1024];
        public AudioService Service => service ?? throw new InvalidOperationException("CreateService must run first.");
        public AudioFlowBinding Binding => binding ?? throw new InvalidOperationException("BindFlow must run first.");

        public IAudioService CreateService()
        {
            if (service != null) throw new InvalidOperationException("Audio already initialized.");
            if (catalog == null) throw new InvalidOperationException("Audio catalog not assigned.");
            var settings = catalog.CreateSettings();
            driver = new UnityAudioPlaybackDriver(sources, catalog);
            var crossfade = new DotweenMusicCrossfadeDriver(gameObject);
            try { service = new AudioService(settings, driver, crossfade, message => Debug.LogWarning(message, this)); }
            catch { crossfade.Dispose(); throw; }
            return service;
        }
        // Caller creates SettingsService and applies saved volume before this call.
        public void BindFlow(IGameFlowService flow, IGameplayControlService control,
            IReadOnlyDictionary<string, string> stageMusic)
        {
            if (binding != null) throw new InvalidOperationException("Audio flow already bound.");
            binding = new AudioFlowBinding(Service, flow, control, stageMusic);
        }
        public float MeasureOutputRms(int channel)
        {
            if (driver == null) throw new InvalidOperationException("Audio not initialized.");
            if (channel < 0 || channel >= AudioService.ChannelCount) throw new ArgumentOutOfRangeException(nameof(channel));
            return driver.MeasureSourceOutputRms(channel, outputBuffer);
        }
        private void Update() { service?.PollCompletedVoices(); }
        public void Shutdown()
        { binding?.Dispose(); binding = null; service?.Dispose(); service = null; driver = null; }
        private void OnDestroy() => Shutdown();
#if UNITY_EDITOR
        public void ConfigureForAuthoring(AudioCueCatalog value, AudioSource[] voiceSources)
        {
            if (service != null) throw new InvalidOperationException("Cannot reconfigure initialized audio.");
            catalog = value; sources = (AudioSource[])voiceSources.Clone();
        }
#endif
    }
}
