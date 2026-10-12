using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Cinematics
{
    public enum CinematicPhase { Idle, Ready, Playing, Holding, Faulted, Disposed, Preparing }

    // Sole owner of cursor, remaining time, operation results and borrowed control leases.
    // Host ticks unscaled time; IEnumerator Play only observes the same service state.
    public sealed class CinematicService : ICinematicService
    {
        private readonly CinematicCatalog catalog;
        private readonly IGameplayControlService control;
        private CinematicCut[] cuts;
        private int cursor = -1;
        private double remainingSeconds, elapsedSeconds;
        private CancellationToken prepareToken, playToken;
        private OperationResult playResult;
        private long revision;
        private IDisposable inputLease, worldLease;
        // Atomic display projection shares the playback owner's lifetime; no second state owner.
        public Texture2D Artwork { get; private set; }
        public string Caption { get; private set; } = string.Empty;
        public int CutIndex { get; private set; } = -1;
        public bool IsVisible { get; private set; }
        public bool CanSkip { get; private set; }
        public event Action Changed;
        internal long PresentationVersion { get; private set; }
        public CinematicOutcome Outcome { get; private set; }
        public CinematicPhase Phase { get; private set; }
        public long Revision => revision;
        public double Time => elapsedSeconds;
        public double RemainingSeconds => remainingSeconds;
        public string Error { get; private set; }

        public CinematicService(CinematicCatalog catalog, IGameplayControlService control)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            control.Changed += OnControlChanged;
        }

        // Pin at invocation, not at the iterator's first MoveNext: a queued old iterator
        // must never prepare/play a newer session that happens to be Ready.
        public IEnumerator Prepare(CinematicId id, CancellationToken token, OperationResult result)
        {
            RequirePending(result);
            return PrepareRoutine(id, token, result, revision);
        }

        private IEnumerator PrepareRoutine(CinematicId id, CancellationToken token, OperationResult result, long expected)
        {
            PrepareNow(id, token, result, expected);
            yield break;
        }

        private void PrepareNow(CinematicId id, CancellationToken token, OperationResult result, long expected)
        {
            if (expected != revision) { result.Cancel(); return; }
            if (Phase == CinematicPhase.Disposed) { result.Fail("CinematicService is disposed."); return; }
            if (Phase != CinematicPhase.Idle) { result.Fail("Hide the previous cinematic before Prepare."); return; }
            if (token.IsCancellationRequested) { Outcome = CinematicOutcome.Cancelled; result.Cancel(); return; }
            long run = ++revision;
            Phase = CinematicPhase.Preparing;
            prepareToken = token;
            playToken = default;
            Outcome = CinematicOutcome.None;
            Error = null;
            elapsedSeconds = 0;
            IDisposable acquiredInput = null, acquiredWorld = null;
            try
            {
                cuts = catalog.Snapshot(id);
                cursor = 0;
                remainingSeconds = cuts[0].DurationSeconds;
                acquiredInput = control.BlockGameplay("Cinematic");
                if (!ContinuePrepare(run, token, result)) return;
                acquiredWorld = control.PauseWorld("Cinematic");
                if (!ContinuePrepare(run, token, result)) return;
                inputLease = acquiredInput;
                worldLease = acquiredWorld;
                acquiredInput = acquiredWorld = null;
                Phase = CinematicPhase.Ready;
                Publish();
                if (ContinuePrepare(run, token, result)) result.Succeed();
            }
            catch (Exception error)
            {
                if (IsCurrent(run)) FailRun(error, result, "Cinematic Prepare " + id);
                else if (result.Status == OperationStatus.Pending) result.Fail("Superseded Cinematic Prepare " + id + ": " + error);
            }
            finally
            {
                var errors = new List<Exception>();
                Release(errors, acquiredInput, acquiredWorld);
                if (errors.Count > 0)
                {
                    var error = new AggregateException("Cinematic untransferred lease cleanup failed.", errors);
                    if (IsCurrent(run)) FailRun(error, result, "Cinematic Prepare " + id);
                    else if (result.Status == OperationStatus.Pending) result.Fail(error.ToString());
                }
            }
        }

        private bool ContinuePrepare(long run, CancellationToken token, OperationResult result)
        {
            if (!IsCurrent(run)) { if (result.Status == OperationStatus.Pending) result.Cancel(); return false; }
            if (!token.IsCancellationRequested) return true;
            Finish(CinematicOutcome.Cancelled);
            if (result.Status == OperationStatus.Pending) result.Cancel();
            return false;
        }

        public IEnumerator Play(CancellationToken token, OperationResult result)
        {
            RequirePending(result);
            return PlayRoutine(token, result, revision);
        }

        private IEnumerator PlayRoutine(CancellationToken token, OperationResult result, long run)
        {
            if (!IsCurrent(run)) { result.Cancel(); yield break; }
            if (Phase != CinematicPhase.Ready) { result.Fail("Cinematic must be Ready before Play."); yield break; }
            playToken = token;
            playResult = result;
            try
            {
                if (IsCancelled) { Finish(CinematicOutcome.Cancelled); yield break; }
                Phase = CinematicPhase.Playing;
                try { Publish(); } catch (Exception error) { if (IsCurrent(run)) FailRun(error, result, "Cinematic Play"); }
                while (IsCurrent(run) && Phase == CinematicPhase.Playing)
                {
                    // Cancellation remains observable even if the Host cannot tick this frame.
                    if (IsCancelled) { Finish(CinematicOutcome.Cancelled); break; }
                    yield return null;
                }
            }
            finally
            {
                try
                {
                    if (IsCurrent(run) && Phase == CinematicPhase.Playing) Finish(CinematicOutcome.Cancelled);
                }
                finally { if (result.Status == OperationStatus.Pending) result.Cancel(); }
            }
        }

        public void Tick(float unscaledDeltaSeconds)
        {
            if (Phase != CinematicPhase.Ready && Phase != CinematicPhase.Playing && Phase != CinematicPhase.Holding) return;
            if (IsCancelled)
            {
                if (Phase == CinematicPhase.Holding) Hide(); // Preserve the already terminal outcome.
                else Finish(CinematicOutcome.Cancelled);
                return;
            }
            if (Phase != CinematicPhase.Playing || control.State.PresentationPaused) return;
            long run = revision;
            if (float.IsNaN(unscaledDeltaSeconds) || float.IsInfinity(unscaledDeltaSeconds) || unscaledDeltaSeconds < 0)
            {
                FailRun(new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds), "Tick seconds must be finite and nonnegative."), playResult, "Cinematic Tick");
                return;
            }
            double delta = unscaledDeltaSeconds;
            while (IsCurrent(run) && Phase == CinematicPhase.Playing)
            {
                if (delta < remainingSeconds)
                {
                    remainingSeconds -= delta;
                    elapsedSeconds += delta;
                    return;
                }
                delta -= remainingSeconds;
                elapsedSeconds += remainingSeconds;
                remainingSeconds = 0;
                if (cursor == cuts.Length - 1) { Finish(CinematicOutcome.Completed); return; }
                cursor++;
                remainingSeconds = cuts[cursor].DurationSeconds;
                try { Publish(); }
                catch (Exception error) { if (IsCurrent(run)) FailRun(error, playResult, "Cinematic cut " + (cursor + 1)); return; }
                // An observer can open Options, cancel, or Hide and prepare another request.
                if (!IsCurrent(run)) return;
                if (IsCancelled) { Finish(CinematicOutcome.Cancelled); return; }
                if (control.State.PresentationPaused) return;
            }
        }

        public void Skip()
        {
            if (Phase != CinematicPhase.Playing) return;
            if (IsCancelled) { Finish(CinematicOutcome.Cancelled); return; }
            if (control.State.PresentationPaused) return;
            Finish(CinematicOutcome.Skipped);
        }

        public void Hide()
        {
            if (Phase == CinematicPhase.Disposed) return;
            var errors = new List<Exception>();
            Reset(CinematicPhase.Idle, errors);
            ThrowErrors(errors, "Cinematic Hide cleanup failed.");
        }

        public void Dispose()
        {
            if (Phase == CinematicPhase.Disposed) return;
            var errors = new List<Exception>();
            // Reject reentrant Prepare before disposing leases or notifying observers.
            try { control.Changed -= OnControlChanged; }
            catch (Exception error) { errors.Add(error); }
            finally { Changed = null; Reset(CinematicPhase.Disposed, errors); }
            ThrowErrors(errors, "Cinematic Dispose cleanup failed.");
        }

        private bool IsCancelled => prepareToken.IsCancellationRequested || playToken.IsCancellationRequested;
        private bool IsCurrent(long run) => revision == run && Phase != CinematicPhase.Disposed;

        private void OnControlChanged(ControlState state)
        {
            if (Phase != CinematicPhase.Ready && Phase != CinematicPhase.Playing && Phase != CinematicPhase.Holding) return;
            long run = revision;
            try { Publish(); }
            catch (Exception error)
            {
                if (IsCurrent(run)) FailRun(error, playResult, "Cinematic control presentation");
                throw;
            }
        }

        private void Publish()
        {
            bool visible = cuts != null && (Phase == CinematicPhase.Ready || Phase == CinematicPhase.Playing || Phase == CinematicPhase.Holding);
            PublishDisplay(visible ? cuts[cursor] : null, visible ? cursor : -1,
                visible, Phase == CinematicPhase.Playing && !control.State.PresentationPaused);
        }

        private void PublishDisplay(CinematicCut cut, int index, bool visible, bool canSkip)
        {
            Texture2D artwork = cut?.Artwork;
            string caption = cut?.Caption ?? string.Empty;
            if (Artwork == artwork && Caption == caption && CutIndex == index && IsVisible == visible && CanSkip == canSkip) return;
            Artwork = artwork; Caption = caption; CutIndex = index; IsVisible = visible; CanSkip = canSkip;
            long version = ++PresentationVersion;
            if (Phase == CinematicPhase.Disposed) return;
            var errors = new List<Exception>();
            var listeners = Changed;
            if (listeners != null)
                foreach (Action listener in listeners.GetInvocationList())
                {
                    if (Phase == CinematicPhase.Disposed || PresentationVersion != version) break;
                    if (Changed == null || Array.IndexOf(Changed.GetInvocationList(), listener) < 0) continue;
                    try { listener(); } catch (Exception error) { errors.Add(error); }
                }
            ThrowErrors(errors, "Cinematic presentation observer failures.");
        }

        private void Finish(CinematicOutcome outcome)
        {
            if (Phase != CinematicPhase.Ready && Phase != CinematicPhase.Playing && Phase != CinematicPhase.Preparing) return;
            Outcome = outcome;
            var result = playResult;
            if (result != null && result.Status == OperationStatus.Pending)
            {
                if (outcome == CinematicOutcome.Cancelled) result.Cancel(); else result.Succeed();
            }
            if (outcome == CinematicOutcome.Cancelled)
            {
                var errors = new List<Exception>();
                Reset(CinematicPhase.Holding, errors);
                ThrowErrors(errors, "Cinematic cancellation cleanup failed.");
                return;
            }
            Phase = CinematicPhase.Holding;
            cursor = cuts.Length - 1;
            remainingSeconds = 0;
            long run = revision;
            try { Publish(); }
            catch (Exception error)
            {
                var errors = new List<Exception> { error };
                // Result and Outcome are terminal; observer failures still hide and release.
                if (IsCurrent(run)) Reset(CinematicPhase.Holding, errors);
                ThrowErrors(errors, "Cinematic completion observer failed.");
            }
        }

        private void FailRun(Exception error, OperationResult result, string context)
        {
            Error = context + ": " + error;
            if (result != null && result.Status == OperationStatus.Pending) result.Fail(Error);
            var errors = new List<Exception> { error };
            long next = revision + 1;
            Reset(CinematicPhase.Faulted, errors);
            // Cleanup observers may already have prepared a newer request.
            if (revision == next && Phase == CinematicPhase.Faulted)
                Error = new AggregateException(context + " failed.", errors).ToString();
        }

        private void Reset(CinematicPhase target, List<Exception> errors)
        {
            // Detach all resources before calling borrowed owners or observers.
            var input = inputLease;
            var world = worldLease;
            var result = playResult;
            inputLease = worldLease = null;
            playResult = null;
            if (Phase == CinematicPhase.Preparing || Phase == CinematicPhase.Ready || Phase == CinematicPhase.Playing)
                Outcome = target == CinematicPhase.Faulted ? CinematicOutcome.None : CinematicOutcome.Cancelled;
            if (result != null && result.Status == OperationStatus.Pending) result.Cancel();
            ++revision;
            Phase = target;
            cuts = null;
            cursor = -1;
            remainingSeconds = 0;
            prepareToken = playToken = default;
            try { PublishDisplay(null, -1, false, false); }
            catch (Exception error) { errors.Add(error); }
            finally { Release(errors, input, world); }
        }

        private static void Release(List<Exception> errors, IDisposable input, IDisposable world)
        {
            try { input?.Dispose(); } catch (Exception error) { errors.Add(error); }
            finally { try { world?.Dispose(); } catch (Exception error) { errors.Add(error); } }
        }
        private static void ThrowErrors(List<Exception> errors, string message)
        { if (errors.Count > 0) throw new AggregateException(message, errors); }
        private static void RequirePending(OperationResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.Status != OperationStatus.Pending) throw new ArgumentException("Provide a fresh OperationResult.", nameof(result));
        }
    }
}
