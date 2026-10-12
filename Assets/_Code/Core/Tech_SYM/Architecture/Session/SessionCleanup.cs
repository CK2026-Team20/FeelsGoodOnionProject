using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Cooked.Session
{
    /// <summary>Only for teardown and observer boundaries: finish independent work, then expose every failure.</summary>
    public static class SessionCleanup
    {
        public static void Attempt(List<Exception> errors, Action action)
        {
            try { action(); }
            catch (Exception error) { errors.Add(error); }
        }
        public static void Notify(List<Exception> errors, Action handlers)
        {
            if (handlers == null) return;
            foreach (Action handler in handlers.GetInvocationList()) Attempt(errors, handler);
        }
        public static void Notify<T>(List<Exception> errors, Action<T> handlers, T value)
        {
            if (handlers == null) return;
            foreach (Action<T> handler in handlers.GetInvocationList()) Attempt(errors, () => handler(value));
        }
        public static void ThrowIfAny(List<Exception> errors, string operation)
        {
            if (errors.Count > 0) throw new AggregateException(operation, errors);
        }
        public static void RethrowAfterCleanup(Exception original, Action cleanup)
        {
            try { cleanup(); }
            catch (Exception cleanupError)
            {
                // The exact original exception (and its stack) remains the first inner exception.
                throw new AggregateException("Actor spawn failed; teardown also reported failures.", original, cleanupError);
            }
            ExceptionDispatchInfo.Capture(original).Throw();
        }
    }
}

