using Cooked.Authoring;
using System;
using Cooked.Contracts;
using UnityEngine;
namespace Cooked.Foundation
{
    [DefaultExecutionOrder(-30000)]
    [DisallowMultipleComponent]
    public sealed class Bootstrapper : MonoBehaviour
    {
        [Tooltip("대화 ID·화자·문장을 담은 JSON TextAsset입니다. 시작 시 검증하므로 현재 대화 테이블 자산을 연결하세요.")]
        [SerializeField, InspectorLabel("대화 원본")] private TextAsset dialogueTable;
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
        /// <summary>Registers the flow without starting it so composition can bind its remaining consumers first.</summary>
        public void Initialize(ServiceLocator appServices, SceneCatalog catalog)
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
                    routine => StartCoroutine(routine), message => Debug.Log(message, this));
                appServices.Register<IGameFlowService>(flow);
            }
            catch
            {
                flow?.Dispose();
                throw;
            }
        }
        /// <summary>Call only after options, fade and audio consumers are connected.</summary>
        public void Boot(BootRoute route)
        {
            if (!initialized || flow == null || appServices.IsDisposed)
                throw new InvalidOperationException("앱 시작 전에 공통 서비스를 조립하세요.");
            route = EditorRouteResolver != null ? EditorRouteResolver(route) : route;
            if (!flow.Boot(route)) throw new InvalidOperationException("앱 흐름을 시작할 수 없습니다. 중복 시작 또는 전환 실패를 확인하세요.");
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
