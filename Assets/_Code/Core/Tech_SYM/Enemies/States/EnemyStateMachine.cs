using System;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    public enum EnemyState { IDLE = 0, PATROL = 1, STUNNED = 2 }

    /// <summary>Idle/순찰 방향과 ADD 기절 시간의 유일한 상태 소유자입니다.</summary>
    public sealed class EnemyStateMachine
    {
        private readonly EnemyActor actor;
        private float idleRemaining;
        private EnemyState resumeState;
        private double stunMilliseconds;
        public EnemyState Current { get; private set; } = EnemyState.IDLE;
        public bool TowardEnd { get; private set; } = true;
        public bool IsStunned => Current == EnemyState.STUNNED;
        public double StunRemainingSeconds => stunMilliseconds / 1000.0;

        public EnemyStateMachine(EnemyActor actor)
        {
            this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
            idleRemaining = actor.Definition.InitialIdle;
        }
        public int TryStun(int milliseconds)
        {
            if (milliseconds <= 0) return 0;
            if (!IsStunned) resumeState = Current;
            stunMilliseconds += milliseconds;
            Current = EnemyState.STUNNED;
            return milliseconds;
        }
        public void Tick(float delta)
        {
            if (!float.IsFinite(delta) || delta <= 0f) return;
            if (IsStunned)
            {
                stunMilliseconds = Math.Max(0, stunMilliseconds - delta * 1000.0);
                if (stunMilliseconds <= .001)
                { stunMilliseconds = 0; Current = resumeState; }
                return;
            }
            if (Current == EnemyState.IDLE)
            {
                if (actor.Definition.Kind == EnemyKind.Stationary) return;
                idleRemaining -= delta;
                if (idleRemaining <= 0f) Current = EnemyState.PATROL;
                return;
            }
            var motor = actor.Motor;
            Vector3 target = TowardEnd ? motor.End : motor.Origin;
            bool blocked = motor.MoveBase(target, actor.Definition.Speed * delta);
            if (Vector3.Distance(motor.BasePosition, target) < .005f || blocked)
                TowardEnd = !TowardEnd;
        }
    }
}
