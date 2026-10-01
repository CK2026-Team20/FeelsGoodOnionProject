using System;
using Cooked.Contracts;
using UnityEngine;
namespace Cooked.Foundation
{
    [DefaultExecutionOrder(-30000)]
    [DisallowMultipleComponent]
    public sealed class Bootstrapper : MonoBehaviour
    {
        [SerializeField] private TextAsset dialogueTable;
        private static Bootstrapper active;
        private ServiceLocator appServices;
        private GameFlowService flow;
        private bool initialized;
        /// <summary>Editor hook injects a one-shot route reader without runtime UnityEditor dependencies.</summary>
        public static Func<BootRoute, BootRoute> EditorRouteResolver { private get; set; }
        public bool IsInitialized => initialized;
        public IGameFlowService Flow => flow;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime() { active = null; }
        public void ConfigureDialogueTable(TextAsset asset)
        {
            if (initialized) throw new InvalidOperationException("Bootstrapper already initialized.");
            dialogueTable = asset;
        }
        /// <summary>Call synchronously from the integration composition root. All app dependencies must be registered.</summary>
        public void Initialize(ServiceLocator appServices, SceneCatalog catalog, BootRoute route)
        {
            if (initialized) throw new InvalidOperationException("Bootstrapper initialization is single-use.");
            if (active != null && active != this) throw new InvalidOperationException("A Bootstrapper is already active.");
            if (appServices == null) throw new ArgumentNullException(nameof(appServices));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (gameObject.scene.path != catalog.BootstrapPath) throw new InvalidOperationException("Bootstrapper must live in its catalog scene.");
            this.appServices = appServices;
            active = this;
            initialized = true;
            try
            {
                var fade = appServices.Get<IFadeService>();
                fade.SetCoveredImmediate();
                if (dialogueTable == null) throw new InvalidOperationException("Bootstrapper requires the validated Dialogue JSON TextAsset.");
                appServices.Get<IDialogueTableService>().LoadJson(dialogueTable.text);
                flow = new GameFlowService(catalog, appServices.Get<ISceneService>(), fade,
                    appServices.Get<IGameplayControlService>(), appServices.Get<IGameRuntimeService>(),
                    appServices.Get<IGameEventBus>(),
                    routine => StartCoroutine(routine), message => Debug.Log(message, this));
                appServices.Register<IGameFlowService>(flow);
                route = EditorRouteResolver != null ? EditorRouteResolver(route) : route;
                if (!flow.Boot(route)) throw new InvalidOperationException("Bootstrap flow could not start.");
            }
            catch
            {
                flow?.Dispose();
                throw;
            }
        }
        private void OnDestroy()
        {
            if (active != this) return;
            // App quit does not launch scene recovery; the host and its coroutine tree are ending.
            flow?.Dispose();
            try { appServices?.Dispose(); } catch (Exception e) { Debug.LogException(e); }
            active = null;
        }
    }
}
