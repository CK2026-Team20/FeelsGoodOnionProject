using System;
using System.Threading;
using UnityEngine;
namespace Cooked.Contracts
{
    public enum AbilityId { FormChange = 0, Tear = 1 }
    [Flags]
    public enum AbilityMask { None = 0, FormChange = 1, Tear = 2, Both = 3 }

    public readonly struct CheckpointSnapshot
    {
        public CheckpointSnapshot(Vector3 position, int fragments, AbilityMask unlockedAbilities)
        {
            if (!float.IsFinite(position.x) || !float.IsFinite(position.y) || !float.IsFinite(position.z))
                throw new ArgumentOutOfRangeException(nameof(position), "체크포인트 월드 위치는 유한해야 합니다.");
            if (fragments < 0 || fragments > 5) throw new ArgumentOutOfRangeException(nameof(fragments));
            if ((uint)unlockedAbilities > (uint)AbilityMask.Both)
                throw new ArgumentOutOfRangeException(nameof(unlockedAbilities), "해금 마스크는 0, 1, 2, 3만 허용합니다.");
            Position = position; Fragments = fragments; UnlockedAbilities = unlockedAbilities;
        }
        public Vector3 Position { get; }
        public int Fragments { get; }
        public AbilityMask UnlockedAbilities { get; }
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
        /// <summary>스테이지 초기 구성은 완료된 체크포인트 저장과 구별합니다.</summary>
        CheckpointSnapshot InitialConfiguration { get; }
        bool HasCheckpoint { get; }
        /// <summary>완료된 저장이 없으면 InvalidOperationException을 발생시킵니다.</summary>
        CheckpointSnapshot Checkpoint { get; }
        AbilityMask UnlockedAbilities { get; }
        /// <summary>반영된 상태를 각 관찰자에게 통지합니다. 관찰자 오류는 문맥을 담아 Unity 로그에 기록하며 다른 통지와 완료 결과를 막지 않습니다.</summary>
        event Action Changed;
        void Begin(CheckpointSnapshot initialConfiguration);
        /// <summary>완료된 저장만 반환합니다. 저장 전·End·Dispose 후에는 false입니다.</summary>
        bool TryGetCheckpoint(out CheckpointSnapshot snapshot);
        /// <summary>위치·조각·현재 해금을 검증하고 원자 저장합니다. 취소·검증 실패는 기존 저장을 유지합니다.</summary>
        CheckpointSnapshot CaptureCheckpoint(Vector3 position, int fragments, CancellationToken cancellationToken = default);
        /// <summary>저장 마스크(저장 전에는 초기 마스크)를 복원하고 Actor에 적용할 위치·조각을 반환합니다.</summary>
        CheckpointSnapshot RestoreCheckpoint();
        bool IsUnlocked(AbilityId ability);
        void Unlock(AbilityId ability);
        void End();
    }
    public interface IPlayerBridgeService : IDisposable
    {
        bool HasActor { get; }
        global::PlayerFacade Actor { get; }
        long ActorGeneration { get; }
        bool IsCurrentActor(global::PlayerFacade expected, long generation);
        bool OwnsDebris(global::PlayerDebris shell);
        /// <summary>피해원의 월드 위치를 전달합니다. 모든 일반 피해원의 방향과 넉백 세기는 Bridge가 계산합니다.</summary>
        int Damage(Vector3 sourcePosition);
        int ForceDeath(Vector3 knockbackVelocity, bool skipPresentation = false);
        /// <summary>실제 Volume/Trigger가 전달한 Collider에서 현재 Player와 같은 OwnedDebris를 구별합니다.</summary>
        bool HandleDeadzoneContact(Collider other);
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
        global::Cooked.Level.CheckpointMarker InitialCheckpoint { get; }
        Pose InitialPose { get; }
        bool ContainsCheckpoint(global::Cooked.Level.CheckpointMarker marker);
    }
}
