using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Session
{
    public sealed class GameSessionService : IGameSessionService
    {
        // Session execution provenance, never checkpoint/serialized data. First grant owns the ability.
        private readonly Dictionary<AbilityId, string> unlocked = new Dictionary<AbilityId, string>();
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
        public void CaptureCheckpoint(Vector3 position, int fragments)
        {
            RequireActive();
            var next = new CheckpointSnapshot(position, fragments);
            checkpoint = next;
            Changed?.Invoke();
        }
        public bool IsUnlocked(AbilityId ability) => IsActive && !disposed && unlocked.ContainsKey(ability);
        public void Unlock(AbilityId ability, string grantingStageId)
        {
            RequireActive();
            if (!Enum.IsDefined(typeof(AbilityId), ability)) throw new ArgumentOutOfRangeException(nameof(ability));
            RequireStageId(grantingStageId);
            if (unlocked.ContainsKey(ability)) return;
            unlocked.Add(ability, grantingStageId);
            Changed?.Invoke();
        }
        public IReadOnlyList<AbilityId> ResetStageUnlocks(string stageId)
        {
            RequireActive(); RequireStageId(stageId);
            var remove = new List<AbilityId>();
            foreach (var pair in unlocked) if (pair.Value == stageId) remove.Add(pair.Key);
            foreach (var ability in remove) unlocked.Remove(ability);
            if (remove.Count != 0) Changed?.Invoke();
            return remove.AsReadOnly();
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
        private static CheckpointSnapshot Validate(CheckpointSnapshot value) => new CheckpointSnapshot(value.Position, value.Fragments);
        private static void RequireStageId(string stageId)
        { if (string.IsNullOrWhiteSpace(stageId)) throw new ArgumentException("Granting Stage ID required.", nameof(stageId)); }
        private void RequireActive() { ThrowIfDisposed(); if (!IsActive) throw new InvalidOperationException("Session is inactive."); }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(GameSessionService)); }
    }
}

