using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    public sealed class EnemyStateMachine
    {
        private readonly EnemyActor actor;
        private readonly IState[] states;
        private IState current;
        private float idleRemaining;
        private EnemyState resumeState;
        private bool towardEnd = true;
        public EnemyState Current => current.Kind;
        public bool TowardEnd => towardEnd;

        public EnemyStateMachine(EnemyActor actor)
        {
            this.actor = actor;
            states = new IState[] { new Idle(this), new Patrol(this), new Stunned(this) };
            Change(EnemyState.IDLE);
        }
        public void Tick(float delta) => current.Tick(delta);
        private void Change(EnemyState state)
        {
            if (current != null && current.Kind == state) return;
            current?.Exit(); current = states[(int)state]; current.Enter();
        }
        public void Stun()
        {
            if (Current == EnemyState.STUNNED) return;
            resumeState = Current;
            Change(EnemyState.STUNNED);
        }
        private void Resume()
        {
            // Resume the paused idle timer or patrol direction without re-entering/resetting it.
            current.Exit();
            current = states[(int)resumeState];
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
                var m = f.actor.Motor;
                Vector3 target = f.towardEnd ? m.End : m.Origin;
                bool blocked = m.MoveBase(target, f.actor.Definition.Speed * delta);
                if (Vector3.Distance(m.BasePosition, target) < .005f)
                {
                    f.towardEnd = !f.towardEnd;
                }
                else if (blocked) f.towardEnd = !f.towardEnd;
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
                if (!f.actor.IsStunned) f.Resume();
            }
        }
    }
}
