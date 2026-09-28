namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>화면이 열려 있는 동안 상호작용 입력을 닫기에 사용하는 최소 계약.</summary>
    public interface IInteractionOverlay
    {
        bool IsOpen { get; }
        void RequestClose();
    }
}
