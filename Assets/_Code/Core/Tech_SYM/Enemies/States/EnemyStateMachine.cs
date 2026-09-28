using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    public sealed class EnemyStateMachine
    {
        private readonly EnemyActor actor;
        private readonly IState[] states;
        private IState current;
        private float idleRemaining;
        private bool hasResume;
        private Vector3 resumePosition;
        private bool towardEnd = true;
        public EnemyState Current => current.Kind;
        public bool IsReturning => hasResume;
        public Vector3 ResumePosition => resumePosition;
        public bool TowardEnd => towardEnd;

        public EnemyStateMachine(EnemyActor actor)
        {
            this.actor = actor;
            states = new IState[] { new Idle(this), new Patrol(this), new Chasing(this), new Stunned(this) };
            Change(EnemyState.IDLE);
        }
        public void Tick(float delta) => current.Tick(delta);
        private void Change(EnemyState state)
        {
            if (current != null && current.Kind == state) return;
            current?.Exit(); current = states[(int)state]; current.Enter();
        }
        private void RememberPatrol()
        {
            if (hasResume) return;
            resumePosition = actor.Motor.BasePosition;
            hasResume = true;
        }
        public void Stun()
        {
            RememberPatrol();
            Change(EnemyState.STUNNED);
        }
        private sealed class Idle : IState
        {
            private readonly EnemyStateMachine f;
            public Idle(EnemyStateMachine f) => this.f = f;
            public EnemyState Kind => EnemyState.IDLE;
            public void Enter() => f.idleRemaining = f.actor.Definition.InitialIdle;
            public void Exit() { }
            public void Tick(float delta)
            {
                f.idleRemaining -= delta;
                if (f.idleRemaining <= 0) f.Change(EnemyState.PATROL);
            }
        }
        private sealed class Patrol : IState
        {
            private readonly EnemyStateMachine f;
            public Patrol(EnemyStateMachine f) => this.f = f;
            public EnemyState Kind => EnemyState.PATROL;
            public void Enter() { }
            public void Exit() { }
            public void Tick(float delta)
            {
                if (f.actor.CanChase())
                {
                    f.RememberPatrol(); f.Change(EnemyState.CHASING); return;
                }
                var m = f.actor.Motor;
                Vector3 target = f.hasResume ? f.resumePosition : (f.towardEnd ? m.End : m.Origin);
                bool blocked = m.MoveBase(target, f.actor.Definition.Speed * delta);
                if (Vector3.Distance(m.BasePosition, target) < .005f)
                {
                    if (f.hasResume) f.hasResume = false;
                    else f.towardEnd = !f.towardEnd;
                }
                else if (blocked && !f.hasResume) f.towardEnd = !f.towardEnd;
            }
        }
        private sealed class Chasing : IState
        {
            private readonly EnemyStateMachine f;
            public Chasing(EnemyStateMachine f) => this.f = f;
            public EnemyState Kind => EnemyState.CHASING;
            public void Enter() { }
            public void Exit() { }
            public void Tick(float delta)
            {
                if (!f.actor.CanChase()) { f.Change(EnemyState.PATROL); return; }
                Vector3 next = Vector3.MoveTowards(f.actor.Motor.BasePosition,
                    f.actor.TargetPosition, f.actor.Definition.Speed * delta);
                next.z = f.actor.Motor.Origin.z;
                if (!f.actor.InPatrolRange(next)) { f.Change(EnemyState.PATROL); return; }
                f.actor.Motor.MoveBase(next, f.actor.Definition.Speed * delta);
            }
        }
        private sealed class Stunned : IState
        {
            private readonly EnemyStateMachine f;
            public Stunned(EnemyStateMachine f) => this.f = f;
            public EnemyState Kind => EnemyState.STUNNED;
            public void Enter() { }
            public void Exit() { }
            public void Tick(float delta)
            {
                if (!f.actor.IsStunned) f.Change(EnemyState.PATROL);
            }
        }
    }
}
