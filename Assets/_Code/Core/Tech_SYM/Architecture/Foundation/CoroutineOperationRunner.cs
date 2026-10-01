using System;
using System.Collections;
using System.Collections.Generic;
using Cooked.Contracts;
namespace Cooked.Foundation
{
    /// <summary>Flattens nested iterators so exceptions and finally blocks cannot disappear in Unity's scheduler.</summary>
    public static class CoroutineOperationRunner
    {
        public static IEnumerator Run(IEnumerator routine, OperationResult result, Action<Exception> report = null)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            var stack = new Stack<IEnumerator>();
            Exception failure = null;
            if (routine != null) stack.Push(routine);
            else failure = new ArgumentNullException(nameof(routine));
            try
            {
                while (stack.Count > 0 && failure == null)
                {
                    var current = stack.Peek();
                    bool next = false;
                    object yielded = null;
                    try { next = current.MoveNext(); if (next) yielded = current.Current; }
                    catch (Exception e) { failure = e; }
                    if (failure != null) break;
                    if (!next)
                    {
                        stack.Pop();
                        try { (current as IDisposable)?.Dispose(); } catch (Exception e) { failure = e; }
                    }
                    else if (yielded is IEnumerator nested) stack.Push(nested);
                    else yield return yielded;
                }
            }
            finally
            {
                while (stack.Count > 0)
                {
                    try { (stack.Pop() as IDisposable)?.Dispose(); }
                    catch (Exception e) { failure = failure == null ? e : new AggregateException(failure, e); }
                }
                // A provider reporting Success before its finally has run is only provisional.
                // The supervisor must never expose success when iterator disposal itself failed.
                if (failure != null)
                {
                    result.FailAfterCoroutineException(failure);
                    report?.Invoke(failure);
                }
                else if (result.Status == OperationStatus.Pending)
                    result.Fail("Coroutine ended without a terminal result.");
            }
        }
    }
}
