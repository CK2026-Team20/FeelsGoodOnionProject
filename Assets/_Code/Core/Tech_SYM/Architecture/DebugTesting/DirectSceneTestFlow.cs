#if UNITY_EDITOR
using System;
using Cooked.Contracts;
namespace Cooked.DebugTesting
{
    /// <summary>Only direct-test lifetime transitions; never fabricates a Stage or checkpoint flow.</summary>
    public sealed class DirectSceneTestFlow : IGameFlowService
    {
        private readonly Action returnToTitle;
        private bool disposed;
        public FlowSnapshot Snapshot { get; private set; } = new FlowSnapshot(FlowState.Playing, false, 0, null);
        public event Action<FlowSnapshot> Changed;
        public DirectSceneTestFlow(Action returnToTitle) => this.returnToTitle = returnToTitle ?? throw new ArgumentNullException(nameof(returnToTitle));
        public bool Request(FlowRequest request)
        {
            if (disposed || Snapshot.IsBusy || Snapshot.State != FlowState.Playing || request.Command != FlowCommand.ReturnToTitle) return false;
            Publish(FlowState.ReturningToTitle, true, null);
            try { returnToTitle(); }
            catch (Exception error) { Fail(error.Message); throw; }
            return true;
        }
        internal void Fail(string error) => Publish(FlowState.Faulted, false, error);
        private void Publish(FlowState state, bool busy, string error)
        {
            Snapshot = new FlowSnapshot(state, busy, Snapshot.Revision + 1, error);
            Changed?.Invoke(Snapshot);
        }
        // A dispatched single-scene load cannot be rolled back safely. Teardown owns cancellation.
        public void CancelCurrent() { }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { Publish(FlowState.Disposed, false, null); }
            finally { Changed = null; }
        }
    }
}
#endif
