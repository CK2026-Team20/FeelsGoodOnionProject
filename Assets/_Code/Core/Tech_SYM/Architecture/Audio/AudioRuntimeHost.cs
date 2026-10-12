using Cooked.Authoring;
using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Audio
{
    /// <summary>Place exactly once in Bootstrapper. Explicit initialization prevents play-on-Awake races.</summary>
    public sealed class AudioRuntimeHost : MonoBehaviour
    {
        [Tooltip("게임에 사용할 소리 용도와 실제 AudioClip을 연결한 AudioCueCatalog 자산을 연결하세요.")]
        [SerializeField, InspectorLabel("소리 목록")] private AudioCueCatalog catalog = null;
        [Tooltip("소리를 재생할 AudioSource 목록입니다. 서로 다른 AudioSource를 정확히 10개 연결해야 합니다. 음악·효과음 재생에 사용하며 null이나 중복 참조는 허용하지 않습니다.")]
        [SerializeField, InspectorLabel("재생 채널")] private AudioSource[] sources = Array.Empty<AudioSource>();
        private AudioService service;
        private AudioFlowBinding binding;
        private bool shuttingDown;
        public AudioService Service => service ?? throw new InvalidOperationException("CreateService must run first.");
        public AudioFlowBinding Binding => binding ?? throw new InvalidOperationException("BindFlow must run first.");

        public IAudioService CreateService()
        {
            if (shuttingDown || service != null) throw new InvalidOperationException("Audio already initialized or shutting down.");
            if (catalog == null) throw new InvalidOperationException("Audio catalog not assigned.");
            if (sources == null) throw new InvalidOperationException("재생 채널 목록을 연결하세요.");
            foreach (var source in sources) Cooked.Integration.SceneComponentLookup.RequireSameScene(this, source, "재생 채널");
            var settings = catalog.CreateSettings();
            var driver = new UnityAudioPlaybackDriver(sources, catalog);
            var crossfade = new DotweenMusicCrossfadeDriver(gameObject);
            try { service = new AudioService(settings, driver, crossfade, message => Debug.LogWarning(message, this)); }
            catch { crossfade.Dispose(); throw; }
            return service;
        }
        // Caller creates SettingsService and applies saved volume before this call.
        public void BindFlow(IGameFlowService flow, IGameplayControlService control)
        {
            if (binding != null) throw new InvalidOperationException("Audio flow already bound.");
            binding = new AudioFlowBinding(Service, flow, control);
        }
        private void Update() { service?.PollCompletedVoices(); }
        public void Shutdown()
        {
            if (shuttingDown) return;
            shuttingDown = true;
            var previousBinding = binding;
            var previousService = service;
            binding = null;
            service = null;
            var errors = new List<Exception>();
            try
            {
                // 앱 서비스 범위가 먼저 Dispose했어도 Host는 자기 구독을 해제한다.
                try { previousBinding?.Dispose(); } catch (Exception error) { errors.Add(error); }
                try { previousService?.Dispose(); } catch (Exception error) { errors.Add(error); }
            }
            finally { binding = null; service = null; shuttingDown = false; }
            if (errors.Count > 0) throw new AggregateException("오디오 Host 종료 실패.", errors);
        }
        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();
    }
}
