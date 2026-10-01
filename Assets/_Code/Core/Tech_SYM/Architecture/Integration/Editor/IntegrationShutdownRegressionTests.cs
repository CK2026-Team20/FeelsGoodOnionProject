using System;
using System.Collections;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Foundation;

namespace Cooked.Integration.Editor
{
    /// <summary>Runs the production shutdown iterator with the actual Foundation runner.
    /// Managed failure injection only; not a substitute for native Core/Actor teardown tests.</summary>
    public static class IntegrationShutdownRegressionTests
    {
        public static string Run()
        {
            var passed = new List<string>();
            Check("normal shutdown disposes and detaches once", () =>
            {
                int disposals = 0, releases = 0; var result = new OperationResult();
                Drain(SessionShutdownOperation.Run(() => YieldOnce(), () => disposals++, () => releases++, result), result, out var error);
                Require(result.Status == OperationStatus.Succeeded && error == null && disposals == 1 && releases == 1);
            }, passed);
            Check("nested MoveNext failure preserves error and clears owner", () =>
            {
                var expected = new InvalidOperationException("injected ActorRemoving failure");
                object owner = new object(); int disposals = 0; var result = new OperationResult();
                Drain(SessionShutdownOperation.Run(() => NestedFailure(expected), () => disposals++, () => owner = null, result), result, out var error);
                Require(result.Status == OperationStatus.Failed && Contains(error, expected) && owner == null && disposals == 1);
            }, passed);
            Check("nested and Dispose failures are both preserved", () =>
            {
                var original = new InvalidOperationException("injected child failure");
                var cleanup = new InvalidOperationException("injected Core Dispose failure");
                object owner = new object(); var result = new OperationResult();
                Drain(SessionShutdownOperation.Run(() => NestedFailure(original), () => throw cleanup, () => owner = null, result), result, out var error);
                Require(result.Status == OperationStatus.Failed && Contains(error, original) && Contains(error, cleanup) && owner == null);
            }, passed);
            Check("despawn factory failure still releases owner", () =>
            {
                var original = new InvalidOperationException("factory failed"); int disposals = 0; object owner = new object();
                var result = new OperationResult();
                Drain(SessionShutdownOperation.Run(() => throw original, () => disposals++, () => owner = null, result), result, out var error);
                Require(Contains(error, original) && owner == null && disposals == 1);
            }, passed);
            Check("child iterator Dispose failure runs outer cleanup", () =>
            {
                var original = new InvalidOperationException("child iterator disposal");
                int disposals = 0; object owner = new object(); var result = new OperationResult();
                Drain(SessionShutdownOperation.Run(() => new DisposeFailure(original), () => disposals++, () => owner = null, result), result, out var error);
                Require(Contains(error, original) && owner == null && disposals == 1);
            }, passed);
            Check("external runner disposal unwinds suspended shutdown", () =>
            {
                int disposals = 0; object owner = new object(); var result = new OperationResult();
                var runner = CoroutineOperationRunner.Run(SessionShutdownOperation.Run(() => YieldOnce(), () => disposals++, () => owner = null, result), result);
                Require(runner.MoveNext());
                ((IDisposable)runner).Dispose();
                Require(owner == null && disposals == 1 && result.Status != OperationStatus.Succeeded);
            }, passed);
            Check("cancellation keeps cancellation result and releases owner", () =>
            {
                var cancelled = new OperationCanceledException("cancelled child"); object owner = new object(); var result = new OperationResult();
                Drain(SessionShutdownOperation.Run(() => NestedFailure(cancelled), () => { }, () => owner = null, result), result, out var error);
                Require(result.Status == OperationStatus.Cancelled && Contains(error, cancelled) && owner == null);
            }, passed);
            Check("dispose and release failures do not mask each other", () =>
            {
                var cleanup = new InvalidOperationException("dispose"); var release = new InvalidOperationException("release");
                bool attempted = false; var result = new OperationResult();
                Drain(SessionShutdownOperation.Run(() => YieldOnce(), () => throw cleanup, () => { attempted = true; throw release; }, result), result, out var error);
                Require(attempted && Contains(error, cleanup) && Contains(error, release) && result.Status == OperationStatus.Failed);
            }, passed);
            Check("owner is gone before a recovery retry can reuse it", () =>
            {
                object owner = new object(); int attempts = 0; var result = new OperationResult();
                Drain(SessionShutdownOperation.Run(() => { attempts++; return NestedFailure(new Exception("bad binding")); }, () => { }, () => owner = null, result), result, out _);
                if (owner != null) attempts++;
                Require(owner == null && attempts == 1);
            }, passed);
            return string.Join(Environment.NewLine, passed) + Environment.NewLine + "PASS " + passed.Count + "/" + passed.Count + " managed shutdown checks; native scene lifecycle remains unverified.";
        }
        private static void Check(string name, Action body, List<string> results)
        { try { body(); results.Add("PASS " + name); } catch (Exception error) { throw new InvalidOperationException("FAIL " + name, error); } }
        private static void Require(bool value) { if (!value) throw new InvalidOperationException("Assertion failed."); }
        private static IEnumerator YieldOnce() { yield return null; }
        private static IEnumerator NestedFailure(Exception error) { yield return YieldOnce(); throw error; }
        private static bool Contains(Exception aggregate, Exception expected)
        {
            if (ReferenceEquals(aggregate, expected)) return true;
            if (aggregate is AggregateException multiple)
                foreach (var child in multiple.InnerExceptions) if (Contains(child, expected)) return true;
            return aggregate?.InnerException != null && Contains(aggregate.InnerException, expected);
        }
        private static void Drain(IEnumerator operation, OperationResult result, out Exception error)
        {
            Exception observed = null;
            var runner = CoroutineOperationRunner.Run(operation, result, e => observed = e);
            try { int remaining = 100; while (runner.MoveNext()) if (--remaining == 0) throw new InvalidOperationException("Unexpected endless test iterator."); }
            finally { (runner as IDisposable)?.Dispose(); }
            error = observed;
        }
        private sealed class DisposeFailure : IEnumerator, IDisposable
        {
            private readonly Exception error;
            internal DisposeFailure(Exception error) { this.error = error; }
            public object Current => null;
            public bool MoveNext() => false;
            public void Reset() => throw new NotSupportedException();
            public void Dispose() => throw error;
        }
    }
}
