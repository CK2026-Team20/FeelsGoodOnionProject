using System;
using System.Threading;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Session
{
    public sealed class GameSessionService : IGameSessionService
    {
        private CheckpointSnapshot initialConfiguration;
        private CheckpointSnapshot? checkpoint;
        private AbilityMask unlockedAbilities;
        private bool disposed;
        public bool IsActive { get; private set; }
        public CheckpointSnapshot InitialConfiguration { get { RequireActive(); return initialConfiguration; } }
        public bool HasCheckpoint => IsActive && checkpoint.HasValue;
        public CheckpointSnapshot Checkpoint
        {
            get
            {
                RequireActive();
                if (!checkpoint.HasValue) throw new InvalidOperationException("완료된 체크포인트 저장이 없습니다.");
                return checkpoint.Value;
            }
        }
        public AbilityMask UnlockedAbilities => unlockedAbilities;
        public event Action Changed;

        public void Begin(CheckpointSnapshot initialConfiguration)
        {
            ThrowIfDisposed();
            if (IsActive) throw new InvalidOperationException("이미 활성화된 세션입니다.");
            var initial = Validate(initialConfiguration);
            this.initialConfiguration = initial;
            checkpoint = null;
            unlockedAbilities = initial.UnlockedAbilities;
            IsActive = true;
            NotifyChanged(nameof(Begin));
        }
        public bool TryGetCheckpoint(out CheckpointSnapshot snapshot)
        {
            snapshot = default;
            if (!HasCheckpoint) return false;
            snapshot = checkpoint.Value;
            return true;
        }
        public CheckpointSnapshot CaptureCheckpoint(Vector3 position, int fragments, CancellationToken cancellationToken = default)
        {
            RequireActive();
            cancellationToken.ThrowIfCancellationRequested();
            var next = new CheckpointSnapshot(position, fragments, unlockedAbilities);
            cancellationToken.ThrowIfCancellationRequested();
            // 완료 경계 이전에는 원본을 건드리지 않으며 관찰자는 세 항목이 완성된 상태를 읽습니다.
            checkpoint = next;
            NotifyChanged(nameof(CaptureCheckpoint));
            return next;
        }
        public CheckpointSnapshot RestoreCheckpoint()
        {
            RequireActive();
            var restored = checkpoint ?? initialConfiguration;
            if (unlockedAbilities != restored.UnlockedAbilities)
            {
                unlockedAbilities = restored.UnlockedAbilities;
                NotifyChanged(nameof(RestoreCheckpoint));
            }
            return restored;
        }
        public bool IsUnlocked(AbilityId ability)
        {
            if (!IsActive || disposed) return false;
            switch (ability)
            {
                case AbilityId.FormChange: return (unlockedAbilities & AbilityMask.FormChange) != AbilityMask.None;
                case AbilityId.Tear: return (unlockedAbilities & AbilityMask.Tear) != AbilityMask.None;
                default: return false;
            }
        }
        public void Unlock(AbilityId ability)
        {
            RequireActive();
            AbilityMask granted;
            switch (ability)
            {
                case AbilityId.FormChange: granted = AbilityMask.FormChange; break;
                case AbilityId.Tear: granted = AbilityMask.Tear; break;
                default: throw new ArgumentOutOfRangeException(nameof(ability));
            }
            var next = unlockedAbilities | granted;
            if (next == unlockedAbilities) return;
            unlockedAbilities = next;
            NotifyChanged(nameof(Unlock));
        }
        public void End()
        {
            ThrowIfDisposed();
            ClearSession(nameof(End));
        }
        public void Dispose()
        {
            if (disposed) return;
            // 종료 통지 중 Begin/변경이 재진입해도 폐기된 세션을 되살릴 수 없습니다.
            disposed = true;
            try { ClearSession(nameof(Dispose)); }
            finally { Changed = null; }
        }
        private void ClearSession(string operation)
        {
            var wasActive = IsActive;
            IsActive = false;
            initialConfiguration = default;
            checkpoint = null;
            unlockedAbilities = AbilityMask.None;
            if (wasActive) NotifyChanged(operation);
        }
        private void NotifyChanged(string operation)
        {
            var observers = Changed;
            if (observers == null) return;
            // 기존 다중 델리게이트처럼 통지 시작 시의 목록을 사용합니다. 자기 해제는 다음 통지부터 반영됩니다.
            foreach (Action observer in observers.GetInvocationList())
            {
                try { observer(); }
                catch (Exception error)
                {
                    Debug.LogException(new InvalidOperationException(
                        $"GameSessionService.{operation} Changed 관찰자 {observer.Method.DeclaringType?.FullName}.{observer.Method.Name} 통지 실패. 반영된 상태는 유지됩니다. 원인: {error.GetType().Name}: {error.Message}", error));
                }
            }
        }
        private static CheckpointSnapshot Validate(CheckpointSnapshot value) => new CheckpointSnapshot(value.Position, value.Fragments, value.UnlockedAbilities);
        private void RequireActive() { ThrowIfDisposed(); if (!IsActive) throw new InvalidOperationException("세션이 활성화되지 않았습니다."); }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(GameSessionService)); }
    }
}

