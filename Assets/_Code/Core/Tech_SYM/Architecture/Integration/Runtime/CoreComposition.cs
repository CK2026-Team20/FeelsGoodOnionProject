using System;
using Cooked.Audio;
using Cooked.Chase;
using Cooked.Cinematics;
using Cooked.Contracts;
using Cooked.Dialogue;
using Cooked.Foundation;
using Cooked.Level;
using Cooked.Session;
using UnityEngine;
namespace Cooked.Integration
{
    public sealed class CoreComposition : MonoBehaviour
    {
        [SerializeField] private SessionActorHost actor;
        [SerializeField] private DialogueDriver dialogueDriver;
        [SerializeField] private DialogueView dialogueView;
        [SerializeField] private CinematicHost cinematic;
        [SerializeField] private IntegrationEntrySettings entries;
        [SerializeField] private CoreUiBinding ui;
        [SerializeField] private LevelCameraService cameraService;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private ChaseRuntimeDriver chase;
        private GameSessionRuntime runtime;
        private CoreWorldBinding world;
        public void Configure(SessionActorHost actorHost, DialogueDriver driver, DialogueView dialogue,
            CinematicHost cinematicHost, IntegrationEntrySettings entrySettings, CoreUiBinding uiBinding,
            LevelCameraService levelCamera, Camera camera, ChaseRuntimeDriver chaseDriver)
        { actor=actorHost; dialogueDriver=driver; dialogueView=dialogue; cinematic=cinematicHost; entries=entrySettings;
          ui=uiBinding; cameraService=levelCamera; gameplayCamera=camera; chase=chaseDriver; }
        public GameSessionRuntime CreateSession(ServiceLocator app, SceneCatalog catalog, AudioFlowBinding audioFlow)
        {
            if (runtime != null) throw new InvalidOperationException("Core already owns a session.");
            try
            {
                var cinematics = cinematic.Initialize(app.Get<IGameplayControlService>());
                runtime = new GameSessionRuntime(app, catalog, actor, dialogueDriver, dialogueView, cinematics, entries);
                ui.Bind(runtime);
                world = new CoreWorldBinding(runtime, cameraService, chase, ui, app.Get<IGameFlowService>(),
                    app.Get<IGameplayControlService>(), app.Get<IGameEventBus>(), app.Get<IAudioService>(), audioFlow);
                return runtime;
            }
            catch (Exception error)
            {
                try { Cleanup(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }
        private void LateUpdate()
        {
            if (runtime != null && runtime.Player.HasActor)
                runtime.Player.SetInputBasis(gameplayCamera.transform.right, gameplayCamera.transform.forward);
        }
        private void Cleanup()
        {
            var errors = new System.Collections.Generic.List<Exception>();
            void Attempt(Action action) { try { action(); } catch (Exception e) { errors.Add(e); } }
            Attempt(() => world?.Dispose()); world=null;
            Attempt(() => { if (ui != null) ui.Unbind(); });
            Attempt(() => runtime?.Dispose()); runtime=null;
            Attempt(() => { if (cinematic != null) cinematic.Shutdown(); });
            if (errors.Count > 0) throw new AggregateException("Core composition cleanup failed.", errors);
        }
        private void OnDestroy() { try { Cleanup(); } catch (Exception e) { Debug.LogException(e, this); } }
    }
}