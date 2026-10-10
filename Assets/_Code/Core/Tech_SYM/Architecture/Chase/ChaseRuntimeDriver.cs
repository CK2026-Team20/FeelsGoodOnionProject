using Cooked.Authoring;
using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Session;
using UnityEngine;

namespace Cooked.Chase
{
    /// <summary>Explicit composition/tick entry point; Integration owns this prefab's lifetime.</summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class ChaseRuntimeDriver : MonoBehaviour
    {
        [Tooltip("추격 속도와 시작 거리 조건을 제공하는 설정 자산입니다. 무리의 밸런스는 이 자산에서 조절하세요.")]
        [SerializeField, InspectorLabel("추격 설정")] private ChaseDefinition definition = null;
        [Tooltip("추격 무리의 등장·상하 움직임·퇴장 외형을 표현할 컴포넌트입니다. 포획 판정 자체는 바꾸지 않습니다.")]
        [SerializeField, InspectorLabel("무리 표현")] private ChaseSwarmPresentation presentation = null;
        private IPlayerBridgeService bridge;
        private string reportedFailure;
        private ChasePath route;
        public ChaseService Service { get; private set; }
        public string LastRejection { get; private set; }

        public void Initialize(IPlayerBridgeService playerBridge, IGameplayControlService control,
            IGameFlowService flow)
        {
            Shutdown();
            if (definition == null || presentation == null) throw new InvalidOperationException("Chase prefab bindings are missing.");
            Cooked.Integration.SceneComponentLookup.RequireSameScene(this, presentation, "무리 표현");
            bridge = playerBridge ?? throw new ArgumentNullException(nameof(playerBridge));
            if (!(bridge is PlayerBridgeService owner)) throw new ArgumentException("Chase needs the current session death owner.");
            Service = new ChaseService(bridge, control, flow, definition.CreateSettings(), velocity => owner.ForceDeath(velocity));
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
            if (actor == null || bridge.Actor != actor || !bridge.IsCurrentActor(actor, bridge.ActorGeneration))
                return Reject("Stage Actor does not match the current session Actor.");
            bool accepted = Service.Start(stageId, waypoints, new PlayerActor(actor));
            if (accepted) route = new ChasePath(waypoints);
            LastRejection = Service.LastRejection;
            return accepted;
        }


        public bool CanStart(string stageId, IReadOnlyList<Vector3> waypoints, PlayerFacade candidate)
        {
            if (!isActiveAndEnabled || Service == null || bridge == null || candidate == null ||
                !bridge.IsCurrentActor(candidate, bridge.ActorGeneration)) return false;
            return Service.CanStart(stageId, waypoints, new PlayerActor(candidate));
        }

        private void LateUpdate()
        {
            Service?.Tick(Time.deltaTime, Time.frameCount);
            if (Service != null) presentation.Tick(Time.deltaTime, Time.unscaledDeltaTime);
            if (Service != null && route != null && Service.Snapshot.State == ChaseState.Running)
            {
                float p = Service.Snapshot.SwarmProgress;
                var direction = route.Evaluate(Mathf.Min(route.Length, p + .1f)) - route.Evaluate(Mathf.Max(0,p - .1f));
                if (direction.sqrMagnitude > .0001f) transform.rotation = Quaternion.FromToRotation(Vector3.right,direction.normalized);
            }
            if (Service != null && Service.Failure != null && Service.Failure != reportedFailure)
            {
                reportedFailure = Service.Failure;
                Debug.LogError("[Cooked.Chase] " + reportedFailure, this);
            }
        }

        public void Shutdown()
        {
            var errors = new List<Exception>();
            if (presentation != null) SessionCleanup.Attempt(errors, presentation.Unbind);
            if (Service != null) SessionCleanup.Attempt(errors, Service.Dispose);
            if (errors.Count == 0) { Service = null; bridge = null; route = null; }
            SessionCleanup.ThrowIfAny(errors, "Chase release failed.");
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
