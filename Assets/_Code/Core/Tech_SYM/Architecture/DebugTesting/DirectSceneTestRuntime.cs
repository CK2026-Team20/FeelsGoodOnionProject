#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Foundation;
using Cooked.Integration;
using Cooked.Session;
using FeelsGoodOnion.TechSYM.Enemies;
using FeelsGoodOnion.TechSYM.Features;
using FeelsGoodOnion.TechSYM.Interaction;
using FeelsGoodOnion.TechSYM.Platforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Cooked.DebugTesting
{
    /// <summary>Owns only the temporary test session. Authored world objects are borrowed.</summary>
    public sealed class DirectSceneTestRuntime : MonoBehaviour
    {
        public const string DefaultPlayerPrefabPath = "Assets/_Prefabs/Characters/Players/Main/PlayerRoot.prefab";
        public DirectSceneTestSettings Settings { get; private set; }
        public PlayerBridgeService Player { get; private set; }
        public GameSessionService Session { get; private set; }
        public GameplayControlService Control { get; private set; }
        public DirectSceneTestFlow Flow { get; private set; }
        public SessionActorHost ActorHost { get; private set; }
        public Camera Output => testCamera?.Output;
        public DirectSceneTestCamera TestCamera => testCamera;
        public bool IsRespawning { get; private set; }
        public bool IsReady => !disposed && !IsRespawning && Player != null && Player.HasActor &&
            !Player.Actor.IsDead && Flow.Snapshot.State == FlowState.Playing;
        public event Action ActorReady;
        public event Action ActorRemoving;
        private readonly List<Action> unbind = new List<Action>();
        private bool disposed;
        private DirectSceneTestCamera testCamera;
        private DirectSceneTestInteraction testInteraction;
        private Pose initialPose;
        private CheckpointSnapshot initial;

        public void Initialize(DirectSceneTestSettings settings)
        {
            if (Settings != null) throw new InvalidOperationException("Test session already initialized.");
            if (settings == null || settings.StartPoint == null || settings.StartPoint.gameObject.scene != gameObject.scene)
                throw new ArgumentException("현재 씬의 시작점이 필요합니다.");
            foreach (var actor in FindObjectsByType<PlayerFacade>(FindObjectsSortMode.None))
                if (actor.isActiveAndEnabled) throw new InvalidOperationException("이미 활성 플레이어가 있습니다. 중복 생성하지 않습니다.");
            Settings = settings;
            initialPose = new Pose(settings.StartPoint.position, settings.StartPoint.rotation);
            initial = new CheckpointSnapshot(initialPose.position, settings.InitialFragments, settings.InitialAbilities);
            var prefab = settings.PlayerPrefab != null ? settings.PlayerPrefab : AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefabPath);
            if (prefab == null) throw new InvalidOperationException("플레이어 프리팹을 연결하세요.");
            Control = new GameplayControlService();
            Flow = new DirectSceneTestFlow(() => StartCoroutine(ReturnToTitle()));
            Session = new GameSessionService();
            Session.Begin(initial);
            ActorHost = gameObject.AddComponent<SessionActorHost>();
            ActorHost.ConfigureActorPrefab(prefab);
            Player = new PlayerBridgeService(Control, Session, RequestDeathRetry,
                ActorHost.HorizontalKnockbackSpeed, ActorHost.UpwardKnockbackSpeed);
            ActorHost.Initialize(Player, Control);
            PrepareOutput();
            testInteraction = new DirectSceneTestInteraction(transform, Control);
            SpawnActor();
        }
        private void PrepareOutput() => testCamera = new DirectSceneTestCamera(transform, Settings);
        private void LateUpdate()
        {
            testCamera?.Tick();
            testInteraction?.Tick();
        }
        private void SpawnActor()
        {
            ActorHost.Spawn(initialPose, initial.Fragments);
            // Host spawns into the active scene; explicitly retain this session's scene ownership.
            SceneManager.MoveGameObjectToScene(ActorHost.ActorRoot, gameObject.scene);
            testCamera.Bind(Player);
            BindWorld();
            ActorReady?.Invoke();
        }
        private void BindWorld()
        {
            foreach (var sensor in SceneComponentLookup.FindAll<EnemyContactSensor>(gameObject.scene))
                BindOptional(sensor, () => sensor.BindPlayer(Player), sensor.UnbindPlayer);
            foreach (var floor in SceneComponentLookup.FindAll<DamageFloorPlatform>(gameObject.scene))
                BindOptional(floor, () => floor.BindPlayer(Player), floor.UnbindPlayer);
            foreach (var platform in SceneComponentLookup.FindAll<PeriodicPlatform>(gameObject.scene))
                BindOptional(platform, () => platform.BindMaterialization(Player), () => platform.BindMaterialization(null));
            testInteraction.Bind(ActorHost.Interaction, Output);
            // Devices are discovered by the real PlayerInteractionService; fragments use the real PlayerFacade contact path.
        }
        private bool BindOptional(Component owner, Action bind, Action release)
        {
            try
            {
                bind();
                unbind.Add(() => { if (owner != null) release(); });
                return true;
            }
            catch (Exception error)
            {
                // A malformed optional authored object must not abort independent player testing.
                try { release(); } catch (Exception cleanup) { Debug.LogException(cleanup, owner); }
                Debug.LogException(new InvalidOperationException("테스트 객체 연결 실패: " + owner.name, error), owner);
                return false;
            }
        }
        private void UnbindWorld()
        {
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, () => testCamera?.Unbind());
            SessionCleanup.Attempt(errors, () => ActorRemoving?.Invoke());
            SessionCleanup.Attempt(errors, () => ActorHost?.Interaction?.ForceCloseOverlays());
            SessionCleanup.Attempt(errors, () => ActorHost?.Interaction?.CancelOperations());
            SessionCleanup.Attempt(errors, () => testInteraction?.Unbind());
            for (int i = unbind.Count - 1; i >= 0; i--) SessionCleanup.Attempt(errors, unbind[i]);
            unbind.Clear();
            SessionCleanup.ThrowIfAny(errors, "직접 씬 테스트 연결 해제 실패");
        }
        private bool RequestDeathRetry(PlayerFacade actor, long generation)
        {
            if (disposed || IsRespawning || !Player.IsCurrentActor(actor, generation)) return false;
            return Respawn();
        }
        public bool Respawn()
        {
            if (disposed || IsRespawning || Player == null || !Player.HasActor || Flow.Snapshot.State != FlowState.Playing) return false;
            IsRespawning = true;
            StartCoroutine(RespawnRoutine());
            return true;
        }
        private IEnumerator RespawnRoutine()
        {
            try
            {
                UnbindWorld();
                yield return ActorHost.Despawn();
                if (disposed) yield break;
                // Reuse normal retry restoration without requiring a complete StageService setup.
                foreach (var platform in SceneComponentLookup.FindAll<ShatteredPlatform>(gameObject.scene))
                    platform.ResetForRetry();
                Session.RestoreAbilities(initial);
                SpawnActor();
            }
            finally { IsRespawning = false; }
        }
        private IEnumerator ReturnToTitle()
        {
            // Defer past the UI command/death callback stack before releasing borrowed objects.
            yield return null;
            AsyncOperation loading = null;
            try
            {
                // DontSave 루트의 자동 씬 정리에 의존하지 않습니다. 새 Bootstrap이
                // 조립되기 전에 패널·정지 토큰·커서·Actor와 출력의 소유권을 반환합니다.
                ReleaseSession();
                gameObject.SetActive(false);
                DirectSceneTestEntry.IsSelected = false;
                loading = EditorSceneManager.LoadSceneAsyncInPlayMode(BootstrapEntryRoute.BootstrapPath,
                    new LoadSceneParameters(LoadSceneMode.Single));
                if (loading == null) throw new InvalidOperationException("Bootstrap 로드를 시작하지 못했습니다.");
                Destroy(gameObject);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                Destroy(gameObject);
            }
            yield break;
        }
        private void OnDestroy()
        {
            StopAllCoroutines();
            ReleaseSession();
        }
        private void ReleaseSession()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, () => GetComponent<DirectSceneTestPresentation>()?.Dispose());
            SessionCleanup.Attempt(errors, () => DirectSceneTestEntry.Released(this));
            SessionCleanup.Attempt(errors, UnbindWorld);
            SessionCleanup.Attempt(errors, () => { if (ActorHost != null) ActorHost.Shutdown(); });
            SessionCleanup.Attempt(errors, () => Player?.Dispose());
            SessionCleanup.Attempt(errors, () => Session?.Dispose());
            SessionCleanup.Attempt(errors, () => testCamera?.Dispose());
            SessionCleanup.Attempt(errors, () => testInteraction?.Dispose());
            SessionCleanup.Attempt(errors, () => Flow?.Dispose());
            SessionCleanup.Attempt(errors, () => Control?.Dispose());
            ActorReady = null; ActorRemoving = null;
            foreach (var error in errors) Debug.LogException(error, this);
        }
    }
}
#endif
