using System;
using System.Collections;
using System.Collections.Generic;
using Cooked.Contracts;
using FeelsGoodOnion.TechSYM.Interaction;
using UnityEngine;
using Cooked.Level;

namespace Cooked.Session
{
    /// <summary>InGameCore owns this host; retry replaces only its actor, not the host or UI.</summary>
    public sealed class SessionActorHost : MonoBehaviour
    {
        [Tooltip("세션에 생성할 플레이어 루트 프리팹입니다. 병합 PlayerFacade·물리·외형 연결이 준비된 프리팹을 지정하세요.")]
        [SerializeField] private GameObject actorPrefab;
        [Tooltip("플레이어 껍질·일회성 객체를 세션 종료 때 정리할 소유 범위입니다.")]
        [SerializeField] private SessionTransientScope transientScope;
        private PlayerBridgeService bridge;
        private IGameplayControlService control;
        private SessionTransientService transients;
        private GameObject actorRoot;
        private bool releasing, shutdown, spawning;
        public GameObject ActorRoot => actorRoot;
        public ActorCameraRig CameraRig => actorRoot != null ? actorRoot.GetComponentInChildren<ActorCameraRig>(true) : null;
        public PlayerInteractionService Interaction { get; private set; }
        public event Action<PlayerInteractionService> InteractionChanged;

        public void Initialize(PlayerBridgeService bridge, IGameplayControlService control, SessionTransientService transients)
        {
            if (shutdown) throw new ObjectDisposedException(nameof(SessionActorHost));
            if (this.bridge != null) throw new InvalidOperationException("Actor host already initialized.");
            if (actorPrefab == null || transientScope == null) throw new InvalidOperationException("Actor host requires actor prefab and transient scope.");
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (transients == null) throw new ArgumentNullException(nameof(transients));
            transientScope.Bind(transients);
            this.bridge = bridge; this.control = control; this.transients = transients;
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
                var router = actorRoot.GetComponent<GameplayInputRouter>();
                if (actor == null || reader == null || router == null) throw new InvalidOperationException("Actor prefab dependencies missing.");
                var camera = CameraRig;
                if (camera == null) throw new InvalidOperationException("Actor prefab requires the authored HMS Cinemachine rig.");
                camera.ValidateConfiguration();
                // All HMS serialized dependencies were authored before first activation.
                actorRoot.SetActive(true);
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
            var oldTransients = transients;
            actorRoot = null;
            Interaction = null;
            try
            {
                // Detach ownership first: reentrant observers cannot dispose the same objects twice.
                if (oldRoot != null || oldInteraction != null)
                    SessionCleanup.Notify(errors, InteractionChanged, (PlayerInteractionService)null);
                if (oldInteraction != null) SessionCleanup.Attempt(errors, oldInteraction.Dispose);
                if (oldRoot != null)
                {
                    SessionCleanup.Attempt(errors, () => oldRoot.GetComponent<GameplayInputRouter>()?.Unbind());
                    SessionCleanup.Attempt(errors, () =>
                    {
                        var form = oldRoot.GetComponentInChildren<PlayerFormController>(true);
                        if (form != null && form.OwnedDebris != null) form.OwnedDebris.Remove();
                    });
                }
                if (oldBridge != null) SessionCleanup.Attempt(errors, oldBridge.Detach);
                if (oldRoot != null)
                {
                    SessionCleanup.Attempt(errors, () => oldRoot.SetActive(false));
                    SessionCleanup.Attempt(errors, () => Destroy(oldRoot));
                }
                if (oldTransients != null) SessionCleanup.Attempt(errors, oldTransients.Clear);
            }
            finally { releasing = false; }
            SessionCleanup.ThrowIfAny(errors, "Actor release completed with teardown failures.");
        }
        public static void ValidateInputOwnership(GameObject root)
        {
            if (root.GetComponentsInChildren<PlayerController>(true).Length != 0 || root.GetComponentsInChildren<CheckInteract>(true).Length != 0)
                throw new InvalidOperationException("Legacy input controllers must not be installed in the architecture actor.");
            if (root.GetComponentsInChildren<PlayerInputReader>(true).Length != 1 || root.GetComponentsInChildren<GameplayInputRouter>(true).Length != 1)
                throw new InvalidOperationException("Actor requires one input reader and one gameplay router.");
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && (behaviour.GetType().Name == "TestPlayerFunc" || behaviour.GetType().Name == "PlayerSkillHUD" || behaviour.GetType().Name == "TestPlayerMovementMode"))
                    throw new InvalidOperationException("Legacy debug input component found in actor.");
        }
        public void Shutdown()
        {
            if (shutdown) return;
            shutdown = true;
            var errors = new List<Exception>();
            try
            {
                SessionCleanup.Attempt(errors, ReleaseNow);
                SessionCleanup.Attempt(errors, () => { if (transientScope != null) transientScope.Unbind(transients); });
            }
            finally
            {
                InteractionChanged = null;
                bridge = null; control = null; transients = null;
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


