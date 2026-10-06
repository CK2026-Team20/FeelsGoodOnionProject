namespace FeelsGoodOnion.TechSYM.Enemies
{
    public enum EnemyState { IDLE, PATROL, STUNNED }
    public interface IState
    {
        EnemyState Kind { get; }
        void Enter();
        void Tick(float delta);
        void Exit();
    }
}
