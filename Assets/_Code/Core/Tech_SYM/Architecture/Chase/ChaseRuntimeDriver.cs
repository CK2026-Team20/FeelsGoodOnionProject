using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Chase
{
    /// <summary>Explicit composition/tick entry point; Integration owns this prefab's lifetime.</summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class ChaseRuntimeDriver : MonoBehaviour
    {
        [SerializeField] private ChaseDefinition definition = null;
        [SerializeField] private ChaseSwarmPresentation presentation = null;
        private IPlayerBridgeService bridge;
        private string reportedFailure;
        public ChaseService Service { get; private set; }
        public string LastRejection { get; private set; }

        public void Initialize(IPlayerBridgeService playerBridge, IGameplayControlService control,
            IGameFlowService flow, IGameEventBus eventBus)
        {
            Shutdown();
            if (definition == null || presentation == null) throw new InvalidOperationException("Chase prefab bindings are missing.");
            bridge = playerBridge ?? throw new ArgumentNullException(nameof(playerBridge));
            Service = new ChaseService(bridge, control, flow, eventBus, definition.CreateSettings());
            reportedFailure = null;
            LastRejection = null;
            try { presentation.Bind(Service, control); }
            catch { Shutdown(); throw; }
        }

        /// <summary>
        /// Integration calls this from StageService.BindChaseStart's acceptance callback.
        /// ChaseStarted is an AFTER-success audio notification, never the start command.
        /// Level closes its rear gate only on true.
        /// Never call it every frame to reposition an existing swarm.
        /// </summary>
        public bool TryStart(string stageId, IReadOnlyList<Vector3> waypoints, PlayerFacade actor)
        {
            if (!isActiveAndEnabled || Service == null || bridge == null)
                return Reject("Chase driver has not been initialized or enabled.");
            if (actor == null || bridge.CameraTarget == null ||
                (bridge.CameraTarget != actor.transform && !bridge.CameraTarget.IsChildOf(actor.transform)))
                return Reject("Stage Actor does not match the session bridge CameraTarget owner.");
            bool accepted = Service.Start(stageId, waypoints, new PlayerActor(actor));
            LastRejection = Service.LastRejection;
            return accepted;
        }

        private void LateUpdate()
        {
            Service?.Tick(Time.deltaTime, Time.frameCount);
            if (Service != null) presentation.Tick(Time.deltaTime, Time.unscaledDeltaTime);
            if (Service != null && Service.Failure != null && Service.Failure != reportedFailure)
            {
                reportedFailure = Service.Failure;
                Debug.LogError("[Cooked.Chase] " + reportedFailure, this);
            }
        }

        public void Shutdown()
        {
            if (presentation != null) presentation.Unbind();
            Service?.Dispose();
            Service = null;
            bridge = null;
        }
        private bool Reject(string message) { LastRejection = message; return false; }
        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();

        private sealed class PlayerActor : IChaseActor
        {
            private readonly PlayerFacade actor;
            public PlayerActor(PlayerFacade actor) { this.actor = actor; }
            public bool IsAlive => actor != null && actor.isActiveAndEnabled && !actor.IsDead;
            public Vector3 Position => actor.transform.position;
        }
    }
}
