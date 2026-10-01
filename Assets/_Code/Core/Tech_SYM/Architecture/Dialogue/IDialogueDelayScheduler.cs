using System;
namespace Cooked.Dialogue
{
    // Dialogue-local external timing boundary, not a global timer service. Schedule never invokes synchronously.
    public interface IDialogueDelayScheduler
    {
        int FrameId { get; }
        IDialogueDelayHandle Schedule(float seconds, Action completed);
    }
    public interface IDialogueDelayHandle : IDisposable
    {
        float Remaining { get; }
        void SetPaused(bool paused);
    }
}
