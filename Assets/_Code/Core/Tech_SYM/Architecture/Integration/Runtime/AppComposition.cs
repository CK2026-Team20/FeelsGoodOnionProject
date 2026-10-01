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
        [SerializeField] private Bootstrapper bootstrap;
        [SerializeField] private ArchitectureInstallation installation;
        [SerializeField] private GlobalUiRoot ui;
        [SerializeField] private AudioRuntimeHost audio;
        private ServiceLocator services;
        private SceneCatalog catalog;
        private OptionsService options;
        private bool subscribed;
        public ServiceLocator Services => services;
        public void Configure(Bootstrapper bootstrapper, ArchitectureInstallation config, GlobalUiRoot globalUi, AudioRuntimeHost sound)
        { bootstrap = bootstrapper; installation = config; ui = globalUi; audio = sound; }
        private void Awake()
        {
            try
            {
                if (bootstrap == null || installation == null || ui == null || audio == null)
                    throw new InvalidOperationException("Bootstrap composition dependencies are incomplete.");
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
                services.Register<IGameRuntimeService>(new GameRuntimeService(() =>
                    SceneComponentLookup.RequireSingle<CoreComposition>(catalog.CorePath).CreateSession(services, catalog, audio.Binding)));
                SceneManager.sceneLoaded += SceneLoaded; subscribed = true;
                bootstrap.Initialize(services, catalog, new BootRoute(false, null));
                var flow = services.Get<IGameFlowService>();
                options = new OptionsService(flow, control, settings);
                services.Register<OptionsService>(options);
                ui.Bind(options, settings, fade);
                audio.BindFlow(flow, control, new Dictionary<string,string>
                {
                    ["03_1_Stage"] = AudioCueIds.Stage1, ["03_2_Stage"] = AudioCueIds.Stage2, ["03_3_Stage"] = AudioCueIds.Stage3
                });
                services.Register<AudioFlowBinding>(audio.Binding);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Cleanup();
                throw;
            }
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (catalog == null || scene.path != catalog.TitlePath) return;
            if (options == null) throw new InvalidOperationException("Title loaded before options composition completed.");
            SceneComponentLookup.RequireSingle<TitleUiRoot>(catalog.TitlePath).Bind(services.Get<IGameFlowService>(), options);
        }
        private void Cleanup()
        {
            if (subscribed) SceneManager.sceneLoaded -= SceneLoaded;
            subscribed = false;
            if (ui != null) ui.Unbind();
            try { services?.Dispose(); } finally { services = null; }
        }
        private void OnDestroy() { try { Cleanup(); } catch (Exception error) { Debug.LogException(error, this); } }
    }
}