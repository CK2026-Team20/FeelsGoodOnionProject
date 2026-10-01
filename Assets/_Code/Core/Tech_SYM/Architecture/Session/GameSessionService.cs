using System;
using System.Collections.Generic;
using Cooked.Contracts;

namespace Cooked.Session
{
    public sealed class GameSessionService : IGameSessionService
    {
        private readonly HashSet<AbilityId> unlocked = new HashSet<AbilityId>();
        private CheckpointSnapshot checkpoint;
        private bool disposed;
        public bool IsActive { get; private set; }
        public CheckpointSnapshot Checkpoint { get { RequireActive(); return checkpoint; } }
        public event Action Changed;

        public void Begin(CheckpointSnapshot initialCheckpoint)
        {
            ThrowIfDisposed();
            if (IsActive) throw new InvalidOperationException("Session already active.");
            checkpoint = Validate(initialCheckpoint);
            unlocked.Clear();
            IsActive = true;
            Changed?.Invoke();
        }
        public void CaptureCheckpoint(string stageId, string checkpointId, int fragments)
        {
            RequireActive();
            var next = new CheckpointSnapshot(stageId, checkpointId, fragments);
            if (checkpoint.StageId == next.StageId && checkpoint.CheckpointId == next.CheckpointId) return;
            checkpoint = next;
            Changed?.Invoke();
        }
        public bool IsUnlocked(AbilityId ability) => IsActive && !disposed && unlocked.Contains(ability);
        public void Unlock(AbilityId ability)
        {
            RequireActive();
            if (!Enum.IsDefined(typeof(AbilityId), ability)) throw new ArgumentOutOfRangeException(nameof(ability));
            bool changed = unlocked.Add(ability);
            // The first form tutorial must also make its shell recoverable.
            if (ability == AbilityId.FormChange) changed |= unlocked.Add(AbilityId.RecoverShell);
            if (changed) Changed?.Invoke();
        }
        public void End()
        {
            ThrowIfDisposed();
            if (!IsActive) return;
            IsActive = false;
            checkpoint = default;
            unlocked.Clear();
            Changed?.Invoke();
        }
        public void Dispose()
        {
            if (disposed) return;
            try { End(); }
            finally { disposed = true; Changed = null; }
        }
        private static CheckpointSnapshot Validate(CheckpointSnapshot value) => new CheckpointSnapshot(value.StageId, value.CheckpointId, value.Fragments);
        private void RequireActive() { ThrowIfDisposed(); if (!IsActive) throw new InvalidOperationException("Session is inactive."); }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(GameSessionService)); }
    }
}

