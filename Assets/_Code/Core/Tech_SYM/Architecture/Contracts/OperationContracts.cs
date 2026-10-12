using System;
namespace Cooked.Contracts
{
    public enum OperationStatus { Pending, Succeeded, Cancelled, Failed }
    public sealed class OperationResult
    {
        public OperationStatus Status { get; private set; }
        public string Error { get; private set; }
        public void Succeed() => Finish(OperationStatus.Succeeded, null);
        public void Cancel() => Finish(OperationStatus.Cancelled, null);
        public void Fail(string error) => Finish(OperationStatus.Failed, string.IsNullOrWhiteSpace(error) ? "Operation failed." : error);
        // Only the coroutine supervisor may revoke a provisional success if disposal/finally throws.
        internal void FailAfterCoroutineException(Exception error)
        {
            Status = error is OperationCanceledException ? OperationStatus.Cancelled : OperationStatus.Failed;
            Error = error.ToString();
        }
        private void Finish(OperationStatus status, string error)
        {
            if (Status != OperationStatus.Pending) throw new InvalidOperationException("Operation already completed.");
            Status = status; Error = error;
        }
    }
}
