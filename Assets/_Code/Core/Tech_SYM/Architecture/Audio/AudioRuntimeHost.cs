using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Audio
{
    /// <summary>Place exactly once in Bootstrapper. Explicit initialization prevents play-on-Awake races.</summary>
    public sealed class AudioRuntimeHost : MonoBehaviour
    {
        [Tooltip("소리 ID와 실제 AudioClip을 연결한 목록 자산입니다. 게임에서 요청하는 ID가 이 목록에 있어야 재생됩니다.")]
        [SerializeField] private AudioCueCatalog catalog = null;
        [Tooltip("소리를 재생할 AudioSource 목록입니다. 서로 다른 AudioSource를 정확히 10개 연결해야 합니다. 음악·효과음 재생에 사용하며 null이나 중복 참조는 허용하지 않습니다.")]
        [SerializeField] private AudioSource[] sources = Array.Empty<AudioSource>();
        private AudioService service;
        private AudioFlowBinding binding;
        private UnityAudioPlaybackDriver driver;
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
