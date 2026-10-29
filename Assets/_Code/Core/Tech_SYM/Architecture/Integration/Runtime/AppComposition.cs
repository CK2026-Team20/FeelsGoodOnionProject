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
    [DisallowMultipleComponent]
    public sealed class AppComposition : MonoBehaviour
    {
        [Tooltip("시작 시 검증할 대화 JSON TextAsset를 연결하세요.")]
        [SerializeField, InspectorLabel("대화 원본")] private TextAsset dialogueTable;
        [Tooltip("부트·타이틀·Core·스테이지 경로를 지정한 설치 자산입니다. 실제 씬 흐름을 이 자산에서 설정합니다.")]
        [SerializeField, InspectorLabel("씬 설치 설정")] private ArchitectureInstallation installation;
        [SerializeField, InspectorLabel("옵션 화면"), Tooltip("이 씬에서 음량과 닫기를 표시할 OptionsView를 연결하세요.")]
        private OptionsView optionsView;
        [SerializeField, InspectorLabel("페이드 화면"), Tooltip("이 씬에서 화면 전환을 표시할 FadeView를 연결하세요.")]
        private FadeView fadeView;
        private OptionsViewModel optionsModel;
        private FadeViewModel fadeModel;
        [Tooltip("앱의 음악·효과음을 재생할 AudioRuntimeHost입니다.")]
        [SerializeField, InspectorLabel("소리 담당")] private AudioRuntimeHost audio;
        [Tooltip("모든 화면을 출력하는 앱의 단일 Camera입니다. 같은 오브젝트에 Brain과 AudioListener를 두고 앱 종료까지 켜 둡니다.")]
        [SerializeField, InspectorLabel("출력 카메라")] private Camera presentationCamera;
        private ServiceLocator services;
        private SceneCatalog catalog;
        private OptionsService options;
        private CursorPresentationBinding cursor;
        private GameRuntimeService runtime;
        private CoreComposition loadedCore;
        private static AppComposition active;
        private bool cleaning;
        private bool ownsOutput;
        /// <summary>Editor가 제공하는 일회성 진입 경로 소비자입니다.</summary>
        public static Func<BootRoute, BootRoute> EditorRouteResolver { private get; set; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            active = null;
            // Domain Reload를 끈 경우에도 Editor가 연결한 경로 소비자는 유지합니다.
        }
        private AudioListener presentationListener;
        private bool subscribed;
        private CinemachineBrain outputBrain;
        // Scene references exist during Awake, but the scene is not yet guaranteed loaded.
        // Compose after sceneLoaded so the same-scene and output-singleton checks are valid.
        private void Start()
        {
#if UNITY_EDITOR
            if (Cooked.DebugTesting.DirectSceneTestEntry.IsSelected) return;
#endif
            try
            {
                if (active != null && active != this)
                    throw new InvalidOperationException("이미 실행 중인 앱 조립자가 있습니다.");
                active = this;
                if (dialogueTable == null || installation == null || optionsView == null || fadeView == null || audio == null || presentationCamera == null)
                    throw new InvalidOperationException("Bootstrap composition dependencies are incomplete.");
                SceneComponentLookup.RequireSameScene(this, optionsView, "옵션 화면");
                SceneComponentLookup.RequireSameScene(this, fadeView, "페이드 화면");
                SceneComponentLookup.RequireSameScene(this, audio, "소리 담당");
                SceneComponentLookup.RequireSameScene(this, presentationCamera, "출력 카메라");
                if (SceneComponentLookup.RequireSingle<Camera>(gameObject.scene) != presentationCamera ||
                    SceneComponentLookup.RequireSingle<CinemachineBrain>(gameObject.scene).gameObject != presentationCamera.gameObject ||
                    SceneComponentLookup.RequireSingle<AudioListener>(gameObject.scene).gameObject != presentationCamera.gameObject)
                    throw new InvalidOperationException("App requires one explicit output Camera/Brain/Listener in this scene.");
                var listeners = presentationCamera.GetComponents<AudioListener>();
                if (listeners.Length != 1)
                    throw new InvalidOperationException("App presentation Camera requires one AudioListener on the same object.");
                presentationListener = listeners[0];
                outputBrain = presentationCamera.GetComponent<CinemachineBrain>();
                if (outputBrain == null)
                    throw new InvalidOperationException("앱 출력 카메라에는 CinemachineBrain이 필요합니다.");
                catalog = installation.CreateCatalog();
                if (gameObject.scene.path != catalog.BootstrapPath)
                    throw new InvalidOperationException("앱 조립자는 설치 자산의 Bootstrap 씬에 있어야 합니다.");
                ownsOutput = true;
                SetPresentationOutput(true);
                services = new ServiceLocator();
                var control = new GameplayControlService();
                var fade = new FadeService();
                services.Register<IGameplayControlService>(control);
                services.Register<IFadeService>(fade);
                services.Register<ISceneService>(new SceneService(catalog));
                var table = new DialogueTableService();
                services.Register<IDialogueTableService>(table);
                table.LoadJson(dialogueTable.text);
                fade.SetCoveredImmediate();
                var sound = audio.CreateService();
                services.Register<IAudioService>(sound);
                var settings = new SettingsService(new PlayerPrefsSettingsStore(), sound);
                services.Register<SettingsService>(settings);
                runtime = new GameRuntimeService(() =>
                {
                    loadedCore = SceneComponentLookup.RequireSingle<CoreComposition>(catalog.CorePath);
                    var session = loadedCore.CreateSession(services, catalog, audio.Binding, presentationCamera, outputBrain);
                    cursor?.BindDialogue(session.Dialogue);
                    return session;
                });
                runtime.BindSessionRelease(old =>
                {
                    cursor?.BindDialogue(null);
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
                var flow = new GameFlowService(catalog, services.Get<ISceneService>(), fade,
                    control, runtime, routine => StartCoroutine(routine), message => Debug.Log(message, this));
                services.Register<IGameFlowService>(flow);
                options = new OptionsService(flow, control, settings);
                services.Register<OptionsService>(options);
                cursor = new CursorPresentationBinding(flow, options);
                optionsModel = new OptionsViewModel(options, settings);
                fadeModel = new FadeViewModel(fade);
                optionsView.Bind(optionsModel);
                fadeView.Bind(fadeModel);
                audio.BindFlow(flow, control);
                services.Register<AudioFlowBinding>(audio.Binding);
                // Starting a coroutine can consume an already loaded Core synchronously.
                // Finish every flow consumer before starting any scene transition.
                var route = new BootRoute(false, null);
                route = EditorRouteResolver != null ? EditorRouteResolver(route) : route;
                if (!flow.Boot(route))
                    throw new InvalidOperationException("앱 흐름을 시작할 수 없습니다. 중복 시작 또는 전환 실패를 확인하세요.");
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
            if (outputBrain != null) outputBrain.enabled = visible;
        }
        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (catalog == null || scene.path != catalog.TitlePath) return;
            if (options == null) throw new InvalidOperationException("Title loaded before options composition completed.");
            SceneComponentLookup.RequireSingle<TitleUiRoot>(catalog.TitlePath).Bind(services.Get<IGameFlowService>(), options, QuitApplication);
            SceneComponentLookup.RequireSingle<TitleScenePresentation>(catalog.TitlePath).Bind(presentationCamera);
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
            if (cleaning) return;
            cleaning = true;
            var errors = new List<Exception>();
            void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
            if (subscribed) SceneManager.sceneLoaded -= SceneLoaded;
            subscribed = false;
            Attempt(() => cursor?.Dispose());
            cursor = null;
            runtime = null;
            Attempt(() =>
            {
                if (services != null && !services.IsDisposed && services.TryGet<IGameFlowService>(out var flow))
                    flow.Dispose();
            });
            Attempt(StopAllCoroutines);
            if (ownsOutput) Attempt(() => SetPresentationOutput(false));
            ownsOutput = false;
            Attempt(() => { if (optionsModel != null && optionsView != null) optionsView.Unbind(); });
            Attempt(() => { if (fadeModel != null && fadeView != null) fadeView.Unbind(); });
            Attempt(() => optionsModel?.Dispose());
            Attempt(() => fadeModel?.Dispose());
            optionsModel = null; fadeModel = null;
            try { Attempt(() => services?.Dispose()); }
            finally
            {
                services = null; catalog = null; options = null; loadedCore = null;
                if (active == this) active = null;
            }
            if (errors.Count != 0) throw new AggregateException("App composition cleanup failed.", errors);
        }
        private void OnApplicationFocus(bool focused) { if (focused) cursor?.RestoreOnFocus(); }
        private void OnDestroy() { try { Cleanup(); } catch (Exception error) { Debug.LogException(error, this); } }
    }
}
