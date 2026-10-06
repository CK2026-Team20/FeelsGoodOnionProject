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
        [Tooltip("추격 속도와 시작 거리 조건을 제공하는 설정 자산입니다. 무리의 밸런스는 이 자산에서 조절하세요.")]
        [SerializeField] private ChaseDefinition definition = null;
        [Tooltip("추격 무리의 등장·상하 움직임·퇴장 외형을 표현할 컴포넌트입니다. 포획 판정 자체는 바꾸지 않습니다.")]
        [SerializeField] private ChaseSwarmPresentation presentation = null;
        private IPlayerBridgeService bridge;
        private string reportedFailure;
        private PlayerFacade actor;
        private ChasePath route;
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
            if (accepted) { this.actor = actor; route = new ChasePath(waypoints); }
            LastRejection = Service.LastRejection;
            return accepted;
        }

        public bool ReportFrontContact(PlayerFacade candidate) => candidate != null && candidate == actor && Service != null && Service.CaptureFromFrontTrigger();

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
            if (presentation != null) presentation.Unbind();
            Service?.Dispose();
            Service = null;
            bridge = null; actor = null; route = null;
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
