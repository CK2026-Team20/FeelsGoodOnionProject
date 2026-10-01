using System;
namespace Cooked.Contracts
{
    public readonly struct ControlState
    {
        public ControlState(bool gameplayBlocked, bool worldPaused, bool presentationPaused) { GameplayBlocked = gameplayBlocked; WorldPaused = worldPaused; PresentationPaused = presentationPaused; }
        public bool GameplayBlocked { get; }
        public bool WorldPaused { get; }
        public bool PresentationPaused { get; }
    }
    public interface IGameplayControlService : IDisposable
    {
        ControlState State { get; }
        event Action<ControlState> Changed;
        IDisposable BlockGameplay(string owner);
        IDisposable PauseWorld(string owner);
        IDisposable PausePresentation(string owner);
    }
}
