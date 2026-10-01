using System;
using System.Collections;
using System.Runtime.ExceptionServices;
using Cooked.Contracts;

namespace Cooked.Integration
{
    /// <summary>Owned shutdown lifetime. Foundation's runner unwinds this iterator on nested failure,
    /// preserving the child exception together with errors raised by this finally block.</summary>
    public static class SessionShutdownOperation
    {
        public static IEnumerator Run(Func<IEnumerator> despawn, Action dispose, Action release, OperationResult result)
        {
            if (despawn == null || dispose == null || release == null || result == null)
                throw new ArgumentNullException("Shutdown requires lifecycle callbacks and a result.");
            try
            {
                yield return despawn();
            }
            finally
            {
                Exception disposeError = null;
                try { dispose(); }
                catch (Exception error) { disposeError = error; }
                try { release(); }
                catch (Exception releaseError)
                {
                    if (disposeError != null) throw new AggregateException("Session dispose and release failed.", disposeError, releaseError);
                    throw;
                }
                if (disposeError != null) ExceptionDispatchInfo.Capture(disposeError).Throw();
            }
            result.Succeed();
        }
    }
}
