using System;
using UnityEngine;
namespace Cooked.Contracts
{
    public enum AbilityId { FormChange, Tear, RecoverShell }
    public readonly struct CheckpointSnapshot
    {
        public CheckpointSnapshot(string stageId, string checkpointId, int fragments)
        {
            if (string.IsNullOrWhiteSpace(stageId)) throw new ArgumentException("Stage ID required.", nameof(stageId));
            if (string.IsNullOrWhiteSpace(checkpointId)) throw new ArgumentException("Checkpoint ID required.", nameof(checkpointId));
            if (fragments < 0) throw new ArgumentOutOfRangeException(nameof(fragments));
            StageId = stageId; CheckpointId = checkpointId; Fragments = fragments;
        }
        public string StageId { get; }
        public string CheckpointId { get; }
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
        void CaptureCheckpoint(string stageId, string checkpointId, int fragments);
        bool IsUnlocked(AbilityId ability);
        void Unlock(AbilityId ability);
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
