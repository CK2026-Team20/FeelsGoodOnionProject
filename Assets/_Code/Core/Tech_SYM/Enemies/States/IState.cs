namespace FeelsGoodOnion.TechSYM.Enemies
{
    // T9 removes this unused historical contract after native/source transfer.
    [System.Obsolete("T9: unused legacy state contract; FSM owns its state directly.")]
    public interface IState
    {
        EnemyState Kind { get; }
        void Enter();
        void Tick(float delta);
        void Exit();
    }
}
