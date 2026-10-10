using System;
using System.Collections.Generic;
using Cooked.Audio;
using Cooked.Authoring;
using Cooked.Contracts;
using Cooked.Dialogue;
using Cooked.Foundation;
using Cooked.Session;
using Cooked.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
namespace Cooked.Integration
{
    [DefaultExecutionOrder(-29000)]
    public sealed class AppComposition : MonoBehaviour
    {
        [Tooltip("앱 시작과 공통 서비스 준비를 담당하는 Bootstrapper입니다.")]
        [SerializeField, InspectorLabel("앱 시작 담당")] private Bootstrapper bootstrap;
        [Tooltip("부트·타이틀·Core·스테이지 경로를 지정한 설치 자산입니다. 실제 씬 흐름을 이 자산에서 설정합니다.")]
        [SerializeField, InspectorLabel("씬 설치 설정")] private ArchitectureInstallation installation;
        // T11 transfers the two existing view references; T12 removes this serialized migration field and Editor-only Configure.
        [SerializeField, HideInInspector] private GlobalUiRoot ui;
        [SerializeField, InspectorLabel("옵션 화면"), Tooltip("이 씬에서 음량과 닫기를 표시할 OptionsView를 연결하세요.")]
        private OptionsView optionsView;
        [SerializeField, InspectorLabel("페이드 화면"), Tooltip("이 씬에서 화면 전환을 표시할 FadeView를 연결하세요.")]
        private FadeView fadeView;
        private OptionsViewModel optionsModel;
        private FadeViewModel fadeModel;
        [Tooltip("앱의 음악·효과음을 재생할 AudioRuntimeHost입니다.")]
        [SerializeField, InspectorLabel("소리 담당")] private AudioRuntimeHost audio;
        [Tooltip("모든 화면을 출력하는 앱의 단일 Camera입니다. 같은 오브젝트의 Brain·AudioListener·분리 리그 HMS 카메라 담당을 연결하고 앱 종료까지 켜 둡니다.")]
        [SerializeField, InspectorLabel("출력 카메라")] private Camera presentationCamera;
        private ServiceLocator services;
        private SceneCatalog catalog;
        private OptionsService options;
        private GameRuntimeService runtime;
        private CoreComposition loadedCore;
        private bool cleaning;
        private AudioListener presentationListener;
        private bool subscribed;
        public ServiceLocator Services => services;
        public Camera OutputCamera => presentationCamera;
        public CinemachineBrain OutputBrain { get; private set; }
        public PlayerCameraController OutputController { get; private set; }
#if UNITY_EDITOR
        // Temporary caller: IntegrationSceneBuilder. Never used for runtime binding; T12 removes the Builder and this method.
        public void Configure(Bootstrapper bootstrapper, ArchitectureInstallation config, GlobalUiRoot globalUi, AudioRuntimeHost sound, Camera outputCamera)
        { bootstrap = bootstrapper; installation = config; ui = globalUi; audio = sound; presentationCamera = outputCamera; }
#endif
        private void Awake()
        {
            try
            {
                if (bootstrap == null || installation == null || optionsView == null || fadeView == null || audio == null || presentationCamera == null)
                    throw new InvalidOperationException("Bootstrap composition dependencies are incomplete.");
                foreach (var reference in new Component[] { bootstrap, optionsView, fadeView, audio, presentationCamera })
                    SceneComponentLookup.RequireSameScene(this, reference, "App 연결");
                if (SceneComponentLookup.RequireSingle<Camera>(gameObject.scene) != presentationCamera ||
                    SceneComponentLookup.RequireSingle<CinemachineBrain>(gameObject.scene).gameObject != presentationCamera.gameObject ||
                    SceneComponentLookup.RequireSingle<AudioListener>(gameObject.scene).gameObject != presentationCamera.gameObject)
                    throw new InvalidOperationException("App requires one explicit output Camera/Brain/Listener in this scene.");
                var listeners = presentationCamera.GetComponents<AudioListener>();
                if (listeners.Length != 1)
                    throw new InvalidOperationException("App presentation Camera requires one AudioListener on the same object.");
                presentationListener = listeners[0];
                OutputBrain = presentationCamera.GetComponent<CinemachineBrain>();
                OutputController = presentationCamera.GetComponent<PlayerCameraController>();
                if (OutputBrain == null || OutputController == null)
                    throw new InvalidOperationException("App output requires Brain and separated HMS controller.");
                SetPresentationOutput(true);
                catalog = installation.CreateCatalog();
                services = new ServiceLocator();
                var control = new GameplayControlService();
                var fade = new FadeService();
                services.Register<IGameplayControlService>(control);
                services.Register<IFadeService>(fade);
                services.Register<ISceneService>(new SceneService(catalog));
                services.Register<IDialogueTableService>(new DialogueTableService());
                var sound = audio.CreateService();
                services.Register<IAudioService>(sound);
                var settings = new SettingsService(new PlayerPrefsSettingsStore(), sound);
                services.Register<SettingsService>(settings);
                runtime = new GameRuntimeService(() =>
                {
                    loadedCore = SceneComponentLookup.RequireSingle<CoreComposition>(catalog.CorePath);
                    return loadedCore.CreateSession(services, catalog, audio.Binding, OutputCamera, OutputBrain, OutputController);
                });
                runtime.BindSessionRelease(old =>
                {
                    // Core owns its own destruction cleanup. App destruction may run after
                    // Unity has removed that scene, so do not search an unloaded scene here.
                    if (loadedCore == null)
                    {
                        if (cleaning || (services != null && services.IsDisposed)) return;
                        throw new InvalidOperationException("Loaded Core is unavailable for session release.");
                    }
                    loadedCore.ReleaseSession(old);
                });
                services.Register<IGameRuntimeService>(runtime);
                SceneManager.sceneLoaded += SceneLoaded; subscribed = true;
                bootstrap.Initialize(services, catalog, new BootRoute(false, null));
                var flow = services.Get<IGameFlowService>();
                options = new OptionsService(flow, control, settings);
                services.Register<OptionsService>(options);
                optionsModel = new OptionsViewModel(options, settings);
                fadeModel = new FadeViewModel(fade);
                optionsView.Bind(optionsModel);
                fadeView.Bind(fadeModel);
                audio.BindFlow(flow, control);
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
        private void SetPresentationOutput(bool visible)
        {
            // App owns the single output. Gameplay visibility belongs to UI/rig readiness.
            if (presentationCamera == null || presentationCamera.gameObject.scene != gameObject.scene) return;
            presentationCamera.enabled = visible;
            if (presentationListener != null) presentationListener.enabled = visible;
            if (OutputBrain != null) OutputBrain.enabled = visible;
            if (OutputController != null) OutputController.enabled = visible;
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (catalog == null || scene.path != catalog.TitlePath) return;
            if (options == null) throw new InvalidOperationException("Title loaded before options composition completed.");
            SceneComponentLookup.RequireSingle<TitleUiRoot>(catalog.TitlePath).Bind(services.Get<IGameFlowService>(), options, QuitApplication);
        }
        private static void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void Cleanup()
        {
            cleaning = true;
            var errors = new List<Exception>();
            void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
            if (subscribed) SceneManager.sceneLoaded -= SceneLoaded;
            subscribed = false;
            runtime = null;
            Attempt(() => SetPresentationOutput(false));
            Attempt(() => { if (optionsView != null) optionsView.Unbind(); });
            Attempt(() => { if (fadeView != null) fadeView.Unbind(); });
            Attempt(() => optionsModel?.Dispose());
            Attempt(() => fadeModel?.Dispose());
            optionsModel = null; fadeModel = null;
            try { Attempt(() => services?.Dispose()); }
            finally { services = null; catalog = null; options = null; }
            if (errors.Count != 0) throw new AggregateException("App composition cleanup failed.", errors);
        }
        private void OnDestroy() { try { Cleanup(); } catch (Exception error) { Debug.LogException(error, this); } }
    }
}
