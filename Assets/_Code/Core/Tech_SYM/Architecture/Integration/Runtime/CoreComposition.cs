using Cooked.Authoring;
using System;
using System.Collections.Generic;
using Cooked.Audio;
using Cooked.Chase;
using Cooked.Cinematics;
using Cooked.Contracts;
using Cooked.Dialogue;
using Cooked.Foundation;
using Cooked.Level;
using Cooked.Session;
using Unity.Cinemachine;
using UnityEngine;
namespace Cooked.Integration
{
    public sealed class CoreComposition : MonoBehaviour
    {
        [Tooltip("통합 플레이어 프리팹을 생성·복원·제거할 Core의 SessionActorHost입니다.")]
        [SerializeField, InspectorLabel("플레이어 담당")] private SessionActorHost actor;
        [Tooltip("현재 대화와 로그를 표시할 Core의 DialogueView입니다.")]
        [SerializeField, InspectorLabel("대화 화면")] private DialogueView dialogueView;
        [Tooltip("오프닝·엔딩의 한 원화와 문구 재생을 담당할 CinematicHost입니다.")]
        [SerializeField, InspectorLabel("연출 담당")] private CinematicHost cinematic;
        [Tooltip("직접 Stage 진입의 시작 지점·조각·이전 진행 능력을 지정한 설정 자산입니다. 저장 데이터가 아닙니다.")]
        [SerializeField, InspectorLabel("초기 진입 설정")] private IntegrationEntrySettings entries;
        [Tooltip("Core HUD와 한 월드 F 안내를 세션 상태에 연결할 담당입니다.")]
        [SerializeField, InspectorLabel("게임 화면 담당")] private CoreUiBinding ui;
        [Tooltip("구간 요청과 실제 Brain 완료를 확인할 Core의 LevelCameraService입니다.")]
        [SerializeField, InspectorLabel("카메라 구간 담당")] private LevelCameraService cameraService;
        [Tooltip("추격 무리와 실제 포획을 갱신할 Core의 ChaseRuntimeDriver입니다.")]
        [SerializeField, InspectorLabel("추격 담당")] private ChaseRuntimeDriver chase;
        private GameSessionRuntime runtime;
        private CoreWorldBinding world;
        private bool releasing;
        public GameSessionRuntime CreateSession(ServiceLocator app, SceneCatalog catalog, AudioFlowBinding audioFlow,
            Camera output, CinemachineBrain brain)
        {
            if (runtime != null || world != null || releasing) throw new InvalidOperationException("Core still owns a session or unfinished release.");
            if (actor == null || dialogueView == null || cinematic == null || entries == null || ui == null ||
                cameraService == null || chase == null)
                throw new InvalidOperationException("Core composition references are incomplete.");
            foreach (var reference in new Component[] { actor, dialogueView, cinematic, ui, cameraService, chase })
                SceneComponentLookup.RequireSameScene(this, reference, "Core 연결");
            try
            {
                var cinematics = cinematic.Initialize(app.Get<IGameplayControlService>());
                runtime = new GameSessionRuntime(app, catalog, actor, dialogueView, cinematics, entries);
                ui.Bind(runtime, cinematic.transform);
                world = new CoreWorldBinding(runtime, cameraService, chase, ui, app.Get<IGameFlowService>(),
                    app.Get<IGameplayControlService>(), app.Get<IDialogueTableService>(), app.Get<IAudioService>(), audioFlow,
                    app.Get<ISceneService>(), output, brain);
                runtime.BindStageLifecycle(world.PrepareStage);
                runtime.BindActorViewReadiness(() => cameraService.IsViewReady);
                return runtime;
            }
            catch (Exception error)
            {
                try { Cleanup(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); }
                throw;
            }
        }
        // May be retried with the same disposed runtime after partial cleanup fails.
        public void ReleaseSession(GameSessionRuntime expected)
        {
            if (expected == null) throw new ArgumentNullException(nameof(expected));
            if (runtime != null && !ReferenceEquals(runtime, expected)) throw new InvalidOperationException("Cannot release another Core session.");
            Cleanup();
        }
        private void Cleanup()
        {
            if (releasing) throw new InvalidOperationException("Reentrant Core release.");
            releasing = true; var errors = new List<Exception>();
            try
            {
                SessionCleanup.Attempt(errors, () => { world?.Dispose(); world = null; });
                SessionCleanup.Attempt(errors, () => { if (ui != null) ui.Unbind(); });
                SessionCleanup.Attempt(errors, () => runtime?.Dispose());
                SessionCleanup.Attempt(errors, () => { if (actor != null) actor.ReleaseSession(); });
                SessionCleanup.Attempt(errors, () => { if (cinematic != null) cinematic.Shutdown(); });
                if (errors.Count == 0) runtime = null;
            }
            finally { releasing = false; }
            SessionCleanup.ThrowIfAny(errors, "Core composition cleanup failed.");
        }
        private void OnDestroy() { try { Cleanup(); } catch (Exception error) { Debug.LogException(error, this); } }
    }
}
