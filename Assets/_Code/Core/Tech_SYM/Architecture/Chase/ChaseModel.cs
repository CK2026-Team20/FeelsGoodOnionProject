using System;
using UnityEngine;

namespace Cooked.Chase
{
    public enum ChaseState { Ready, Running, Caught, Stopped, Faulted, Disposed }
    public enum ChaseStopReason { None, FlowTransition, ActorUnavailable, OwnerDisabled, OwnerDisposed, Error }

    public readonly struct ChaseSnapshot
    {
        public ChaseSnapshot(ChaseState state, ChaseStopReason reason, float gap, float progress, Vector3 position)
        { State = state; StopReason = reason; Gap = gap; SwarmProgress = progress; SwarmPosition = position; }
        public ChaseState State { get; }
        public ChaseStopReason StopReason { get; }
        public float Gap { get; }
        public float SwarmProgress { get; }
        public Vector3 SwarmPosition { get; }
    }

    /// <summary>One attempt; retries create a fresh instance. No Unity lifecycle or external effects.</summary>
    public sealed class ChaseModel
    {
        private readonly ChaseSettings settings;
        private ChasePath path;
        private double swarmProgress;
        public ChaseSnapshot Snapshot { get; private set; }

        public ChaseModel(ChaseSettings settings)
        { this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); }

        public bool TryStart(ChasePath route, Vector3 actorPosition, out string rejection)
        {
            rejection = null;
            if (Snapshot.State != ChaseState.Ready) { rejection = "This chase attempt already started or stopped."; return false; }
            if (route == null || !ChasePath.IsFinite(actorPosition)) { rejection = "Valid route and actor position required."; return false; }
            float playerProgress = route.Project(actorPosition);
            if (route.DistanceFromRoute(actorPosition) > 2f || playerProgress < ChaseSettings.InitialGap ||
                playerProgress >= route.Length - settings.CaptureGap)
            { rejection = "Start needs 8m of route behind the actor and remaining escape space (route corridor +/-2m)."; return false; }
            path = route;
            // Exactly once per attempt. Never re-centre the swarm around the actor in Tick.
            swarmProgress = playerProgress - ChaseSettings.InitialGap;
            SetSnapshot(ChaseState.Running, ChaseStopReason.None, playerProgress);
            return true;
        }

        public void Tick(float deltaTime, Vector3 actorPosition)
        {
            if (Snapshot.State != ChaseState.Running) return;
            if (!ChaseSettings.IsFinite(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            float playerProgress = path.Project(actorPosition);
            // Analytical integration of constant speed: consumes the entire dt, no discarded hitch time.
            swarmProgress = Math.Min(path.Length, swarmProgress + (double)settings.Speed * deltaTime);
            // Beyond the route end is not a substitute for Level's rescue/escape acceptance.
            playerProgress = Math.Min(path.Length, playerProgress);
            SetSnapshot(ChaseState.Running, ChaseStopReason.None, playerProgress);
        }
        public bool Capture()
        {
            if (Snapshot.State != ChaseState.Running) return false;
            Snapshot = new ChaseSnapshot(ChaseState.Caught, ChaseStopReason.None, Snapshot.Gap, Snapshot.SwarmProgress, Snapshot.SwarmPosition);
            return true;
        }

        public void Stop(ChaseStopReason reason)
        {
            if (Snapshot.State != ChaseState.Running && Snapshot.State != ChaseState.Ready) return;
            Snapshot = new ChaseSnapshot(ChaseState.Stopped, reason, Snapshot.Gap, Snapshot.SwarmProgress, Snapshot.SwarmPosition);
        }

        public void Fail()
        {
            if (Snapshot.State == ChaseState.Disposed) return;
            Snapshot = new ChaseSnapshot(ChaseState.Faulted, ChaseStopReason.Error, Snapshot.Gap, Snapshot.SwarmProgress, Snapshot.SwarmPosition);
        }

        public void Dispose()
        {
            Snapshot = new ChaseSnapshot(ChaseState.Disposed, ChaseStopReason.OwnerDisposed,
                Snapshot.Gap, Snapshot.SwarmProgress, Snapshot.SwarmPosition);
        }

        private void SetSnapshot(ChaseState state, ChaseStopReason reason, float playerProgress)
        {
            Snapshot = new ChaseSnapshot(state, reason, (float)(playerProgress - swarmProgress),
                (float)swarmProgress, path.Evaluate((float)swarmProgress));
        }
    }
}
