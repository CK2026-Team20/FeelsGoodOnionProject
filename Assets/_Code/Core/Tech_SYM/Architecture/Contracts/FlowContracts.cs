using System;
namespace Cooked.Contracts
{
    public enum FlowState { Booting, Title, LoadingGame, Opening, Playing, Retrying, Ending, ReturningToTitle, Recovering, Faulted, Disposed }
    public enum FlowCommand { NewGame, RetryCheckpoint, ReturnToTitle, Escape, EditorStart }
    public readonly struct FlowRequest
    {
        public FlowRequest(FlowCommand command, string stageId = null) { Command = command; StageId = stageId; }
        public FlowCommand Command { get; }
        public string StageId { get; }
    }
    public readonly struct BootRoute
    {
        public BootRoute(bool directGame, string stageId) { DirectGame = directGame; StageId = stageId; }
        public bool DirectGame { get; }
        public string StageId { get; }
    }
    public readonly struct FlowSnapshot
    {
        public FlowSnapshot(FlowState state, bool isBusy, long revision, string error) { State = state; IsBusy = isBusy; Revision = revision; Error = error; }
        public FlowState State { get; }
        public bool IsBusy { get; }
        public long Revision { get; }
        public string Error { get; }
    }
    public interface IGameFlowService : IDisposable
    {
        FlowSnapshot Snapshot { get; }
        event Action<FlowSnapshot> Changed;
        bool Request(FlowRequest request);
        void CancelCurrent();
    }
}
