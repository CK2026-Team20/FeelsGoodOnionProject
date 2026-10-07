using System;
using System.Collections.Generic;
using UnityEngine;
namespace Cooked.Contracts
{
    public enum AbilityId { FormChange = 0, Tear = 1 }
    public readonly struct CheckpointSnapshot
    {
        public CheckpointSnapshot(Vector3 position, int fragments)
        {
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
                throw new ArgumentOutOfRangeException(nameof(position), "Checkpoint position must be finite.");
            if (fragments < 0 || fragments > 5) throw new ArgumentOutOfRangeException(nameof(fragments));
            Position = position; Fragments = fragments;
        }
        public Vector3 Position { get; }
        public int Fragments { get; }
    }
    public readonly struct PlayerSnapshot
    {
        public PlayerSnapshot(int health, int maxHealth, int fragments, int maxFragments)
        {
            if (maxHealth < 1 || health < 0 || health > maxHealth || maxFragments < 0 || fragments < 0 || fragments > maxFragments)
                throw new ArgumentOutOfRangeException(nameof(health), "Invalid player snapshot bounds.");
            Health = health; MaxHealth = maxHealth; Fragments = fragments; MaxFragments = maxFragments;
        }
        public int Health { get; }
        public int MaxHealth { get; }
        public int Fragments { get; }
        public int MaxFragments { get; }
        public bool IsDead => Health == 0;
    }
    public interface IGameSessionService : IDisposable
    {
        bool IsActive { get; }
        CheckpointSnapshot Checkpoint { get; }
        event Action Changed;
        void Begin(CheckpointSnapshot initialCheckpoint);
        void CaptureCheckpoint(Vector3 position, int fragments);
        bool IsUnlocked(AbilityId ability);
        void Unlock(AbilityId ability, string grantingStageId);
        IReadOnlyList<AbilityId> ResetStageUnlocks(string stageId);
        void End();
    }
    public interface IPlayerBridgeService : IDisposable
    {
        bool HasActor { get; }
        PlayerSnapshot Snapshot { get; }
        Transform CameraTarget { get; }
        event Action<PlayerSnapshot> Changed;
        void Attach(global::PlayerFacade actor);
        void Detach();
        void RestoreFragments(int fragments);
        void SetDepthMovementAllowed(bool allowed);
    }
    public interface IStageService : IDisposable
    {
        string StageId { get; }
        string InitialCheckpointId { get; }
        bool TryGetCheckpointPose(string checkpointId, out Pose pose);
    }
}
