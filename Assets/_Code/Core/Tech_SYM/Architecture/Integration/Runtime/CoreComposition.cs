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
        [Tooltip("세션 플레이어를 생성·복원·제거할 SessionActorHost입니다. 씬에 직접 배치한 중복 플레이어 대신 이 담당을 사용합니다.")]
        [SerializeField] private SessionActorHost actor;
        [Tooltip("대화 문장 진행을 갱신하는 담당입니다. 해당 Core의 DialogueDriver를 연결하세요.")]
        [SerializeField] private DialogueDriver dialogueDriver;
        [Tooltip("현재 대화와 로그를 보여 줄 화면입니다. 해당 Core의 DialogueView를 연결하세요.")]
        [SerializeField] private DialogueView dialogueView;
        [Tooltip("오프닝·엔딩 재생을 담당하는 CinematicHost입니다.")]
        [SerializeField] private CinematicHost cinematic;
        [Tooltip("직접 씬 진입 때 사용할 체크포인트·조각·해금 능력 설정 자산입니다.")]
        [SerializeField] private IntegrationEntrySettings entries;
        [Tooltip("Core HUD와 월드 안내를 세션 상태에 연결하는 담당입니다.")]
        [SerializeField] private CoreUiBinding ui;
        [Tooltip("트리거 구간별 카메라와 이동축 전환을 담당하는 컴포넌트입니다.")]
        [SerializeField] private LevelCameraService cameraService;
        [Tooltip("마지막 구간의 추격 무리를 갱신하고 포획을 전달할 담당입니다.")]
        [SerializeField] private ChaseRuntimeDriver chase;
        private GameSessionRuntime runtime;
        private CoreWorldBinding world;
        public void Configure(SessionActorHost actorHost, DialogueDriver driver, DialogueView dialogue,
            CinematicHost cinematicHost, IntegrationEntrySettings entrySettings, CoreUiBinding uiBinding,
            LevelCameraService levelCamera, ChaseRuntimeDriver chaseDriver)
        { actor=actorHost; dialogueDriver=driver; dialogueView=dialogue; cinematic=cinematicHost; entries=entrySettings;
          ui=uiBinding; cameraService=levelCamera; chase=chaseDriver; }
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
