using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Session
{
    /// <summary>Owns gameplay, physics-time and presentation leases independently.</summary>
    public sealed class GameplayControlService : IGameplayControlService
    {
        private enum Restriction { Gameplay, World, Presentation }
        private readonly Dictionary<long, Restriction> leases = new Dictionary<long, Restriction>();
        private readonly Func<float> readTimeScale;
        private readonly Action<float> writeTimeScale;
        private long nextId;
        private float priorTimeScale;
        private bool disposed;
        public ControlState State { get; private set; }
        public event Action<ControlState> Changed;

        public GameplayControlService() : this(() => Time.timeScale, value => Time.timeScale = value) { }
        public GameplayControlService(Func<float> readTimeScale, Action<float> writeTimeScale)
        {
            this.readTimeScale = readTimeScale ?? throw new ArgumentNullException(nameof(readTimeScale));
            this.writeTimeScale = writeTimeScale ?? throw new ArgumentNullException(nameof(writeTimeScale));
        }
        public IDisposable BlockGameplay(string owner) => Acquire(Restriction.Gameplay, owner);
        public IDisposable PauseWorld(string owner) => Acquire(Restriction.World, owner);
        public IDisposable PausePresentation(string owner) => Acquire(Restriction.Presentation, owner);
        private IDisposable Acquire(Restriction restriction, string owner)
        {
            if (disposed) throw new ObjectDisposedException(nameof(GameplayControlService));
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Lease owner required.", nameof(owner));
            long id = ++nextId;
            leases.Add(id, restriction);
            try { Recalculate(); }
            catch (Exception original)
            {
                // No caller received a token: roll back ownership before reporting listener failures.
                leases.Remove(id);
                try { Recalculate(); }
                catch (Exception rollback) { throw new AggregateException("Control lease acquisition and rollback observer failures.", original, rollback); }
                throw;
            }
            return new Lease(this, id);
        }
        private void Release(long id)
        {
            if (disposed || !leases.Remove(id)) return;
            Recalculate();
        }
        private void Recalculate()
        {
            bool gameplay = false, world = false, presentation = false;
            foreach (Restriction restriction in leases.Values)
            {
                gameplay |= restriction == Restriction.Gameplay;
                world |= restriction == Restriction.World;
                presentation |= restriction == Restriction.Presentation;
            }
            var previous = State;
            if (!previous.WorldPaused && world) { priorTimeScale = readTimeScale(); writeTimeScale(0); }
            else if (previous.WorldPaused && !world) writeTimeScale(priorTimeScale);
            State = new ControlState(gameplay, world, presentation);
            if (previous.GameplayBlocked != gameplay || previous.WorldPaused != world || previous.PresentationPaused != presentation)
            {
                var errors = new List<Exception>();
                SessionCleanup.Notify(errors, Changed, State);
                SessionCleanup.ThrowIfAny(errors, "Control state observer failures.");
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            leases.Clear();
            try { Recalculate(); }
            finally { Changed = null; }
        }
        private sealed class Lease : IDisposable
        {
            private GameplayControlService owner;
            private readonly long id;
            public Lease(GameplayControlService owner, long id) { this.owner = owner; this.id = id; }
            public void Dispose() { var current = owner; owner = null; current?.Release(id); }
        }
    }
}

