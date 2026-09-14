namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>플랫폼 검증용 최소 상태 계약. 실제 캐릭터 상태 머신은 구현하지 않는다.</summary>
    public interface IState
    {
        void Enter();
        void Update();
        void Exit();
    }
}
