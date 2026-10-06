using System;
using System.Collections.Generic;
using Cooked.Audio;
using Cooked.Contracts;
using Cooked.Dialogue;
using Cooked.Foundation;
using Cooked.Session;
using Cooked.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Cooked.Integration
{
    [DefaultExecutionOrder(-29000)]
    public sealed class AppComposition : MonoBehaviour
    {
        [Tooltip("앱 시작과 공통 서비스 준비를 담당하는 Bootstrapper입니다.")]
        [SerializeField] private Bootstrapper bootstrap;
        [Tooltip("부트·타이틀·Core·스테이지 경로를 지정한 설치 자산입니다. 실제 씬 흐름을 이 자산에서 설정합니다.")]
        [SerializeField] private ArchitectureInstallation installation;
        [Tooltip("앱 수명 동안 유지할 옵션·화면 전환 UI 루트입니다.")]
        [SerializeField] private GlobalUiRoot ui;
        [Tooltip("앱의 음악·효과음을 재생할 AudioRuntimeHost입니다.")]
        [SerializeField] private AudioRuntimeHost audio;
        [Tooltip("부트·타이틀·연출 화면을 출력할 앱의 Camera입니다. 이 루트 아래 BootstrapCamera를 연결하고 같은 오브젝트에 AudioListener 하나를 두세요. 준비된 플레이어 화면이 나오면 이 Camera와 AudioListener를 함께 끕니다.")]
        [SerializeField] private Camera presentationCamera;
        private ServiceLocator services;
        private SceneCatalog catalog;
        private OptionsService options;
        private GameRuntimeService runtime;
        private AudioListener presentationListener;
        private bool subscribed;
        public ServiceLocator Services => services;
        public void Configure(Bootstrapper bootstrapper, ArchitectureInstallation config, GlobalUiRoot globalUi, AudioRuntimeHost sound, Camera fallbackCamera)
        { bootstrap = bootstrapper; installation = config; ui = globalUi; audio = sound; presentationCamera = fallbackCamera; }
        private void Awake()
        {
            try
            {
                if (bootstrap == null || installation == null || ui == null || audio == null || presentationCamera == null)
                    throw new InvalidOperationException("Bootstrap composition dependencies are incomplete.");
                if (presentationCamera.transform == transform || !presentationCamera.transform.IsChildOf(transform))
                    throw new InvalidOperationException("App presentation Camera must be a child of this composition root.");
                var listeners = presentationCamera.GetComponents<AudioListener>();
                if (listeners.Length != 1)
                    throw new InvalidOperationException("App presentation Camera requires one AudioListener on the same object.");
                presentationListener = listeners[0];
                SetPresentationOutput(true);
                catalog = installation.CreateCatalog();
                services = new ServiceLocator();
                var control = new GameplayControlService();
                var fade = new FadeService();
                services.Register<IGameplayControlService>(control);
                services.Register<IGameEventBus>(new GameEventBus());
                services.Register<IFadeService>(fade);
                services.Register<ISceneService>(new SceneService(catalog));
                services.Register<IDialogueTableService>(new DialogueTableService());
                var sound = audio.CreateService();
                services.Register<IAudioService>(sound);
                var settings = new SettingsService(new PlayerPrefsSettingsStore(), sound);
                services.Register<SettingsService>(settings);
                runtime = new GameRuntimeService(() =>
                    SceneComponentLookup.RequireSingle<CoreComposition>(catalog.CorePath).CreateSession(services, catalog, audio.Binding));
                services.Register<IGameRuntimeService>(runtime);
                runtime.GameplayOutputChanged += OnGameplayOutputChanged;
                SceneManager.sceneLoaded += SceneLoaded; subscribed = true;
                bootstrap.Initialize(services, catalog, new BootRoute(false, null));
                var flow = services.Get<IGameFlowService>();
                options = new OptionsService(flow, control, settings);
                services.Register<OptionsService>(options);
                ui.Bind(options, settings, fade);
                audio.BindFlow(flow, control, new Dictionary<string,string>
                {
                    ["1_Stage"] = AudioCueIds.Stage1, ["03_1_Stage"] = AudioCueIds.Stage1, ["03_2_Stage"] = AudioCueIds.Stage2, ["03_3_Stage"] = AudioCueIds.Stage3
                });
                services.Register<AudioFlowBinding>(audio.Binding);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                try { Cleanup(); }
                catch (Exception cleanupError)
                { throw new AggregateException("Bootstrap initialization and rollback both failed.", error, cleanupError); }
                throw;
            }
        }
        private void OnGameplayOutputChanged(bool gameplayOutput) => SetPresentationOutput(!gameplayOutput);
        private void SetPresentationOutput(bool visible)
        {
            // Only this root's fallback output is owned here; ActorCameraRig owns the player output.
            if (presentationCamera == null || !presentationCamera.transform.IsChildOf(transform)) return;
            presentationCamera.enabled = visible;
            if (presentationListener != null) presentationListener.enabled = visible;
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (catalog == null || scene.path != catalog.TitlePath) return;
            if (options == null) throw new InvalidOperationException("Title loaded before options composition completed.");
            SceneComponentLookup.RequireSingle<TitleUiRoot>(catalog.TitlePath).Bind(services.Get<IGameFlowService>(), options);
        }
        private void Cleanup()
        {
            var errors = new List<Exception>();
            void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
            if (subscribed) SceneManager.sceneLoaded -= SceneLoaded;
            subscribed = false;
            if (runtime != null) runtime.GameplayOutputChanged -= OnGameplayOutputChanged;
            runtime = null;
            Attempt(() => SetPresentationOutput(false));
            Attempt(() => { if (ui != null) ui.Unbind(); });
            try { Attempt(() => services?.Dispose()); }
            finally { services = null; catalog = null; options = null; }
            if (errors.Count != 0) throw new AggregateException("App composition cleanup failed.", errors);
        }
        private void OnDestroy() { try { Cleanup(); } catch (Exception error) { Debug.LogException(error, this); } }
    }
}
