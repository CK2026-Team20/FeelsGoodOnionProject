namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>붕괴 가능한 오브젝트의 인터페이스.</summary>
    public interface IBrokenable
    {
        /// <summary>붕괴 시작. 중복 요청은 무시한다.</summary>
        void Collapses();
    }
}
