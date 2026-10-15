using System;
using System.Threading;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Session
{
    public sealed class GameSessionService : IGameSessionService
    {
        private CheckpointSnapshot initialConfiguration;
        private AbilityMask unlockedAbilities;
        private bool disposed;
        public bool IsActive { get; private set; }
        public long RoundId { get; private set; }
        public CheckpointSnapshot InitialConfiguration { get { RequireActive(); return initialConfiguration; } }
        public AbilityMask UnlockedAbilities => unlockedAbilities;
        public event Action Changed;

        public void Begin(CheckpointSnapshot initialConfiguration)
        {
            ThrowIfDisposed();
            if (IsActive) throw new InvalidOperationException("이미 활성화된 세션입니다.");
            var initial = Validate(initialConfiguration);
            long nextRound = checked(RoundId + 1);
            this.initialConfiguration = initial;
            unlockedAbilities = initial.UnlockedAbilities;
            RoundId = nextRound;
            IsActive = true;
            NotifyChanged(nameof(Begin));
        }
        /// <summary>StageService가 선택한 복귀 payload의 능력만 현재 세션에 복원한다.</summary>
        public void RestoreAbilities(CheckpointSnapshot snapshot)
        {
            RequireActive();
            var restored = Validate(snapshot);
            if (unlockedAbilities == restored.UnlockedAbilities) return;
            unlockedAbilities = restored.UnlockedAbilities;
            NotifyChanged(nameof(RestoreAbilities));
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

