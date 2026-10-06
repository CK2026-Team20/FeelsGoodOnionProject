using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Chase
{
    /// <summary>Session-injected coordinator. Does not own actor, control, flow, bus, scenes or UI.</summary>
    public sealed class ChaseService : IDisposable
    {
        private readonly IPlayerBridgeService bridge;
        private readonly IGameplayControlService control;
        private readonly IGameFlowService flow;
        private readonly IGameEventBus eventBus;
        private readonly ChaseModel model;
        private IChaseActor actor;
        private long lastFrame = -1;
        private bool disposed;
        public ChaseSnapshot Snapshot => model.Snapshot;
        public string LastRejection { get; private set; }
        public string Failure { get; private set; }
        public event Action<ChaseSnapshot> Changed;

        public ChaseService(IPlayerBridgeService bridge, IGameplayControlService control,
            IGameFlowService flow, IGameEventBus eventBus, ChaseSettings settings)
        {
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            this.flow = flow ?? throw new ArgumentNullException(nameof(flow));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            model = new ChaseModel(settings);
            flow.Changed += OnFlowChanged;
        }

        /// <summary>True is the sole signal for closing Level's rear gate and playing its start cue.</summary>
        public bool Start(string stageId, IReadOnlyList<Vector3> waypoints, IChaseActor candidate)
        {
            LastRejection = null;
            if (disposed) return Reject("Chase service disposed.");
            if (string.IsNullOrWhiteSpace(stageId)) return Reject("A configured Stage ID is required.");
            if (Snapshot.State != ChaseState.Ready) return Reject("Chase attempt is no longer Ready.");
            if (flow.Snapshot.State != FlowState.Playing || flow.Snapshot.IsBusy || control.State.WorldPaused)
                return Reject("Gameplay is not running or the world is paused.");
            if (!bridge.HasActor || bridge.Snapshot.IsDead || candidate == null || !candidate.IsAlive)
                return Reject("A live, session-bound Actor is required.");
            ChasePath path;
            try { path = new ChasePath(waypoints); }
            catch (ArgumentException error) { return Reject(error.Message); }
            if (!model.TryStart(path, candidate.Position, out string rejection)) return Reject(rejection);
            actor = candidate;
            Changed?.Invoke(Snapshot);
            return true;
        }

        public void Tick(float deltaTime, long frameId)
        {
            if (disposed || Snapshot.State != ChaseState.Running) return;
            if (frameId < 0 || frameId < lastFrame) throw new ArgumentOutOfRangeException(nameof(frameId));
            if (frameId == lastFrame) return;
            lastFrame = frameId;
            if (flow.Snapshot.State != FlowState.Playing || flow.Snapshot.IsBusy)
            { Stop(ChaseStopReason.FlowTransition); return; }
            if (!bridge.HasActor || bridge.Snapshot.IsDead || actor == null || !actor.IsAlive)
            { Stop(ChaseStopReason.ActorUnavailable); return; }
            if (control.State.WorldPaused) return;
            try { model.Tick(deltaTime, actor.Position); }
            catch (Exception error)
            {
                Fault("Chase tick failed: " + error.Message);
                throw;
            }
            if (!disposed) Changed?.Invoke(Snapshot);
        }

        public bool CaptureFromFrontTrigger()
        {
            if (disposed || control.State.WorldPaused || flow.Snapshot.State != FlowState.Playing || flow.Snapshot.IsBusy ||
                !bridge.HasActor || bridge.Snapshot.IsDead || actor == null || !actor.IsAlive || !model.Capture()) return false;
            // Terminal state precedes dispatch; multiple colliders cannot request a second retry.
            try
            {
                eventBus.Publish(new FlowRequestedEvent(new FlowRequest(FlowCommand.RetryCheckpoint)));
                if (flow.Snapshot.State != FlowState.Retrying || !flow.Snapshot.IsBusy)
                    Fault("Front-trigger retry was not accepted by GameFlow.");
            }
            catch (Exception error) { Fault("Capture dispatch failed: " + error.Message); throw; }
            if (!disposed) Changed?.Invoke(Snapshot);
            return true;
        }

        public void Stop(ChaseStopReason reason)
        {
            if (disposed) return;
            ChaseState previous = Snapshot.State;
            model.Stop(reason);
            if (Snapshot.State != previous) Changed?.Invoke(Snapshot);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            flow.Changed -= OnFlowChanged;
            actor = null;
            model.Dispose();
            // Consumers unbind their own callbacks; nobody else's service/listeners are disposed.
            Changed = null;
        }

        private void OnFlowChanged(FlowSnapshot state)
        {
            // Services are composed during loading/opening: an unstarted attempt must remain Ready.
            if (Snapshot.State == ChaseState.Running && (state.State != FlowState.Playing || state.IsBusy))
                Stop(ChaseStopReason.FlowTransition);
        }
        private bool Reject(string reason) { LastRejection = reason; return false; }
        private void Fault(string reason) { Failure = reason; model.Fail(); }
    }
}
