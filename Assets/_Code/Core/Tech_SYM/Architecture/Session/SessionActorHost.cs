using System;
using System.Collections;
using System.Collections.Generic;
using Cooked.Contracts;
using FeelsGoodOnion.TechSYM.Interaction;
using UnityEngine;
using Cooked.Level;
using Cooked.Authoring;
using Cooked.Integration;
using FeelsGoodOnion.TechSYM.Enemies;
using FeelsGoodOnion.TechSYM.Platforms;

namespace Cooked.Session
{
    /// <summary>InGameCore owns this host; retry replaces only its actor, not the host or UI.</summary>
    public sealed class SessionActorHost : MonoBehaviour
    {
        [Tooltip("세션에 생성할 플레이어 루트 프리팹입니다. 병합 PlayerFacade·물리·외형 연결이 준비된 프리팹을 지정하세요.")]
        [SerializeField, InspectorLabel("플레이어 프리팹")] private GameObject actorPrefab;
        [Tooltip("여섯 통합 씬 밖에서 직접 Play할 기존 플레이어 루트입니다. 비워 두면 통합 세션이 프리팹을 생성합니다.")]
        [SerializeField, InspectorLabel("독립 플레이어")] private GameObject independentActor;
        [Tooltip("독립 씬의 한 월드 F 안내를 표시할 CoreUiBinding입니다. 기존 플레이어를 연결할 때 지정합니다.")]
        [SerializeField, InspectorLabel("독립 안내 담당")] private CoreUiBinding independentUi;
        [Tooltip("독립 씬에서 F로 열고 닫을 메모 담당 목록입니다. 각 담당의 자기 제한만 해제합니다.")]
        [SerializeField, InspectorLabel("독립 메모 담당")] private MemoUIController[] independentMemos = Array.Empty<MemoUIController>();
        [Tooltip("독립 HMS 카메라 리그의 출력 Camera입니다. 이 씬의 기존 단일 출력을 연결하며 App 출력으로 바꾸지 않습니다.")]
        [SerializeField, InspectorLabel("독립 출력 카메라")] private Camera independentCamera;
        private GameSessionService independentSession;
        private GameplayControlService independentControl;
        [SerializeField, InspectorLabel("수평 넉백 속도"), Tooltip("모든 일반 피해의 수평 넉백 속도를 조절합니다(m/s). 0이면 수평으로 밀리지 않습니다."), Min(0f)]
        private float horizontalKnockbackSpeed = 3f;
        [SerializeField, InspectorLabel("상향 넉백 속도"), Tooltip("모든 일반 피해의 위쪽 넉백 속도를 조절합니다(m/s). 0이면 위로 밀리지 않습니다."), Min(0f)]
        private float upwardKnockbackSpeed = 3f;
        public float HorizontalKnockbackSpeed => horizontalKnockbackSpeed;
        public float UpwardKnockbackSpeed => upwardKnockbackSpeed;
        private readonly List<StageTrigger> independentFalls = new List<StageTrigger>();
        private EnemyContactSensor[] independentSensors = Array.Empty<EnemyContactSensor>();
        private DamageFloorPlatform[] independentFloors = Array.Empty<DamageFloorPlatform>();
        private PeriodicPlatform[] independentPeriodic = Array.Empty<PeriodicPlatform>();
        private bool started;
        private bool ownsIndependentServices;
        public void Configure(GameObject prefab) { actorPrefab = prefab; }
        public void ConfigureIndependent(GameObject existingActor, CoreUiBinding uiBinding, MemoUIController[] memos, Camera outputCamera)
        {
            independentActor = existingActor; independentUi = uiBinding;
            independentMemos = memos ?? Array.Empty<MemoUIController>(); independentCamera = outputCamera;
        }
        private PlayerBridgeService bridge;
        private IGameplayControlService control;
        private GameObject actorRoot;
        private bool releasing, shutdown, spawning;
        public GameObject ActorRoot => actorRoot;
        public bool ActorModesReady
        {
            get
            {
                var modes = actorRoot != null ? actorRoot.GetComponentInChildren<PlayerMovementModeController>(true) : null;
                return modes != null && modes.isActiveAndEnabled && modes.IsInitialized;
            }
        }
        public PlayerInteractionService Interaction { get; private set; }
        public event Action<PlayerInteractionService> InteractionChanged;

        public void Initialize(PlayerBridgeService bridge, IGameplayControlService control)
        {
            if (shutdown) throw new ObjectDisposedException(nameof(SessionActorHost));
            if (this.bridge != null) throw new InvalidOperationException("Actor host already initialized.");
            if (actorPrefab == null) throw new InvalidOperationException("Actor host requires actor prefab.");
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));
            if (control == null) throw new ArgumentNullException(nameof(control));
            this.bridge = bridge; this.control = control;
        }
        /// <summary>Pose is ActorBody center: default capsule center 0,height 2 => floorY + 1.03.</summary>
        public void Spawn(Pose pose, int fragments)
        {
            if (releasing || shutdown || spawning) throw new InvalidOperationException("Actor host is releasing or shut down.");
            if (bridge == null) throw new InvalidOperationException("Actor host not initialized.");
            if (actorRoot != null) throw new InvalidOperationException("Despawn the current actor before spawning.");
            if (actorPrefab.activeSelf) throw new InvalidOperationException("Actor prefab must be saved inactive so dependencies exist before Awake.");
            spawning = true;
            try
            {
                actorRoot = Instantiate(actorPrefab, pose.position, pose.rotation);
                actorRoot.name = "PlayerRoot";
                var createdRoot = actorRoot;
                ValidateInputOwnership(actorRoot);
                var actor = actorRoot.GetComponentInChildren<PlayerFacade>(true);
                var reader = actorRoot.GetComponentInChildren<PlayerInputReader>(true);
                var router = actorRoot.GetComponentInChildren<GameplayInputRouter>(true);
                if (actor == null || reader == null || router == null) throw new InvalidOperationException("Actor prefab dependencies missing.");
                reader.SetTearSkillBinding("<Keyboard>/e");
                actor.transform.SetPositionAndRotation(pose.position, pose.rotation);
                if (actorRoot.GetComponentsInChildren<Camera>(true).Length != 0 ||
                    actorRoot.GetComponentsInChildren<Unity.Cinemachine.CinemachineBrain>(true).Length != 0 ||
                    actorRoot.GetComponentsInChildren<AudioListener>(true).Length != 0 ||
                    actorRoot.GetComponentsInChildren<PlayerCameraController>(true).Length != 0)
                    throw new InvalidOperationException("Integrated Actor must not contain an output camera/controller.");
                // All HMS serialized dependencies were authored before first activation.
                actorRoot.SetActive(true);
                var forms = actor.GetComponent<PlayerFormController>();
                if (forms == null || forms.CurrentForm != PlayerForm.Normal || forms.OwnedDebris != null)
                    throw new InvalidOperationException("A fresh Actor must start Normal with no shell.");
                actor.SetMaxHP(3);
                actor.SetInvincible(false);
                actor.SetHitInvincibilityDuration(2f);
                if (actor.GetComponent<PlayerAnimatorOutput>()?.KnockbackControlLockMilliseconds <= 0f)
                    Debug.LogWarning("공통 넉백 클립 연결 대기: 기존 설정의 0ms 조작 제한을 사용합니다. 실제 애니메이션 재생은 미검증입니다.", actor);
                bridge.Attach(actor);
                bridge.RestoreFragments(fragments);
                actor.Heal(actor.Model.MaxHP);
                Interaction = new PlayerInteractionService(bridge);
                router.Initialize(reader, bridge, Interaction, control);
                InteractionChanged?.Invoke(Interaction);
                if (actorRoot != createdRoot || bridge == null || !bridge.HasActor)
                    throw new InvalidOperationException("Actor was released during spawn notification.");
            }
            catch (Exception original) { SessionCleanup.RethrowAfterCleanup(original, ReleaseNow); }
            finally { spawning = false; }
        }
        public IEnumerator Despawn()
        {
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, ReleaseNow);
            // Even teardown failure drains scheduled Destroy calls before flow sees the error.
            yield return null;
            SessionCleanup.ThrowIfAny(errors, "Actor despawn completed with teardown failures.");
        }
        public void ReleaseNow()
        {
            if (releasing) return;
            releasing = true;
            var errors = new List<Exception>();
            var oldRoot = actorRoot;
            var oldInteraction = Interaction;
            var oldBridge = bridge;
            actorRoot = null;
            Interaction = null;
            try
            {
                foreach (var trigger in independentFalls)
                    if (trigger != null) SessionCleanup.Attempt(errors, trigger.UnbindIndependentFall);
                independentFalls.Clear();
                foreach (var sensor in independentSensors) if (sensor != null) SessionCleanup.Attempt(errors, sensor.UnbindPlayer);
                foreach (var floor in independentFloors) if (floor != null) SessionCleanup.Attempt(errors, floor.UnbindPlayer);
                foreach (var periodic in independentPeriodic) if (periodic != null) SessionCleanup.Attempt(errors, () => periodic.BindMaterialization(null));
                independentSensors = Array.Empty<EnemyContactSensor>();
                independentFloors = Array.Empty<DamageFloorPlatform>();
                independentPeriodic = Array.Empty<PeriodicPlatform>();
                // Detach ownership first: reentrant observers cannot dispose the same objects twice.
                if (oldRoot != null || oldInteraction != null)
                    SessionCleanup.Notify(errors, InteractionChanged, (PlayerInteractionService)null);
                if (oldInteraction != null) SessionCleanup.Attempt(errors, oldInteraction.Dispose);
                if (oldRoot != null)
                {
                    SessionCleanup.Attempt(errors, () => oldRoot.GetComponentInChildren<GameplayInputRouter>(true)?.Unbind());
                    SessionCleanup.Attempt(errors, () =>
                    {
                        var form = oldRoot.GetComponentInChildren<PlayerFormController>(true);
                        if (form != null && form.OwnedDebris != null) form.OwnedDebris.Remove();
                    });
                }
                if (oldBridge != null) SessionCleanup.Attempt(errors, oldBridge.Detach);
                if (oldRoot != null)
                {
                    if (!ownsIndependentServices)
                    {
                        SessionCleanup.Attempt(errors, () => oldRoot.SetActive(false));
                        SessionCleanup.Attempt(errors, () => Destroy(oldRoot));
                    }
                }
            }
            finally { releasing = false; }
            SessionCleanup.ThrowIfAny(errors, "Actor release completed with teardown failures.");
        }
        public static void ValidateInputOwnership(GameObject root)
        {
            if (root.GetComponentsInChildren<PlayerController>(true).Length != 0)
                throw new InvalidOperationException("Legacy input controllers must not be installed in the architecture actor.");
            if (root.GetComponentsInChildren<PlayerInputReader>(true).Length != 1 || root.GetComponentsInChildren<GameplayInputRouter>(true).Length != 1)
                throw new InvalidOperationException("Actor requires one input reader and one gameplay router.");
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && (behaviour.GetType().Name == "TestPlayerFunc" || behaviour.GetType().Name == "PlayerSkillHUD" || behaviour.GetType().Name == "TestPlayerMovementMode"))
                    throw new InvalidOperationException("Legacy debug input component found in actor.");
        }
        private void Start()
        { started = true; StartIndependent(); }
        private void OnEnable()
        { if (started && !shutdown && independentActor != null && bridge == null) StartIndependent(); }
        private void OnDisable()
        { if (ownsIndependentServices) ReleaseSession(); }
        private void StartIndependent()
        {
            if (independentActor == null) return;
            if (bridge != null || independentUi == null || independentCamera == null)
                throw new InvalidOperationException("Independent Play requires an unbound host, UI and output Camera.");
            ownsIndependentServices = true;
            try
            {
                ValidateInputOwnership(independentActor);
                var facade = independentActor.GetComponentInChildren<PlayerFacade>(true);
                var reader = independentActor.GetComponentInChildren<PlayerInputReader>(true);
                var router = independentActor.GetComponentInChildren<GameplayInputRouter>(true);
                if (facade == null || reader == null || router == null) throw new InvalidOperationException("Independent Actor dependencies missing.");
                independentControl = new GameplayControlService(); control = independentControl;
                independentSession = new GameSessionService();
                SceneComponentLookup.RequireSameScene(this, facade, "독립 플레이어");
                SceneComponentLookup.RequireSameScene(this, independentUi, "독립 안내");
                SceneComponentLookup.RequireSameScene(this, independentCamera, "독립 출력");
                independentSession.Begin(new CheckpointSnapshot(facade.transform.position, facade.Model.CurrentSkillFragment, AbilityMask.Both));
                bridge = new PlayerBridgeService(control, independentSession, requestRetry: null,
                    horizontalKnockbackSpeed: HorizontalKnockbackSpeed, upwardKnockbackSpeed: UpwardKnockbackSpeed);
                bridge.SetEnemyHurtColliderResolver(enemy => enemy.HurtCollider);
                reader.SetTearSkillBinding("<Keyboard>/e");
                actorRoot = independentActor;
                bridge.Attach(facade); bridge.Dying += OnIndependentDying;
                Interaction = new PlayerInteractionService(bridge);
                foreach (var memo in independentMemos) if (memo != null)
                {
                    SceneComponentLookup.RequireSameScene(this, memo, "독립 메모 담당");
                    memo.Initialize(control);
                }
                Interaction.BindOverlays(independentMemos);
                router.Initialize(reader, bridge, Interaction, control);
                independentUi.BindIndependent(bridge, Interaction, independentCamera);
                independentSensors = SceneComponentLookup.FindAll<EnemyContactSensor>(gameObject.scene);
                independentFloors = SceneComponentLookup.FindAll<DamageFloorPlatform>(gameObject.scene);
                independentPeriodic = SceneComponentLookup.FindAll<PeriodicPlatform>(gameObject.scene);
                foreach (var sensor in independentSensors)
                {
                    SceneComponentLookup.RequireSameScene(this, sensor.Owner, "독립 적 소유자");
                    sensor.BindPlayer(bridge);
                }
                foreach (var floor in independentFloors) floor.BindPlayer(bridge);
                foreach (var periodic in independentPeriodic) periodic.BindMaterialization(bridge);
                foreach (var trigger in SceneComponentLookup.FindAll<StageTrigger>(gameObject.scene))
                    if (trigger.IndependentHost == this) BindIndependentFall(trigger);
            }
            catch (Exception error) { SessionCleanup.RethrowAfterCleanup(error, ReleaseSession); }
        }
        // A Fall sensor may be disabled independently of this Host. Its next OnEnable reconnects the same current Actor.
        public bool BindIndependentFall(StageTrigger trigger)
        {
            if (trigger == null || trigger.IndependentHost != this || trigger.Owner != null ||
                trigger.Kind != StageTriggerKind.Fall || !ownsIndependentServices || releasing || shutdown ||
                bridge == null || !bridge.HasActor || actorRoot == null || bridge.Actor.IsDead) return false;
            SceneComponentLookup.RequireSameScene(this, trigger, "독립 데드존");
            trigger.BindIndependentFall(bridge.Actor, (source, shell) => !releasing && !shutdown && bridge != null &&
                bridge.IsCurrentActor(source, bridge.ActorGeneration) && source.isActiveAndEnabled && !source.IsDead &&
                control != null && !control.State.WorldPaused &&
                (shell != null ? bridge.ReturnOwnedDebris(shell) : bridge.ForceDeath(Vector3.zero, skipPresentation: true) > 0));
            if (!independentFalls.Contains(trigger)) independentFalls.Add(trigger);
            return true;
        }
        private void Update()
        {
            // T06 owns separation advancement via MovementUpdated. Host only polls its single death completion.
            if (!shutdown && !releasing && !spawning && actorRoot != null && bridge != null && bridge.HasActor &&
                control != null && !control.State.WorldPaused && !control.State.PresentationPaused)
                bridge.TickDeathPresentation();
        }
        private void OnIndependentDying()
        {
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, () => Interaction?.ForceCloseOverlays());
            SessionCleanup.Attempt(errors, () => Interaction?.CancelOperations());
            SessionCleanup.ThrowIfAny(errors, "Independent death cleanup failed.");
        }
        public void Shutdown()
        {
            if (shutdown) return;
            shutdown = true;
            ReleaseSession();
            InteractionChanged = null;
        }
        // Normal session release is reusable; only OnDestroy/Shutdown permanently closes the host.
        public void ReleaseSession()
        {
            var errors = new List<Exception>();
            try
            {
                SessionCleanup.Attempt(errors, ReleaseNow);
                if (ownsIndependentServices)
                {
                    SessionCleanup.Attempt(errors, () => { if (independentUi != null) independentUi.Unbind(); });
                    foreach (var memo in independentMemos) if (memo != null) SessionCleanup.Attempt(errors, memo.Unbind);
                    if (bridge != null) bridge.Dying -= OnIndependentDying;
                    SessionCleanup.Attempt(errors, () => bridge?.Dispose());
                    SessionCleanup.Attempt(errors, () => independentSession?.Dispose());
                    SessionCleanup.Attempt(errors, () => independentControl?.Dispose());
                }
            }
            finally
            {
                bridge = null; control = null; independentSession = null; independentControl = null; ownsIndependentServices = false;
            }
            SessionCleanup.ThrowIfAny(errors, "Actor host shutdown completed with teardown failures.");
        }
        private void OnDestroy()
        {
            try { Shutdown(); }
            catch (Exception error) { Debug.LogException(error, this); }
        }
    }
}

