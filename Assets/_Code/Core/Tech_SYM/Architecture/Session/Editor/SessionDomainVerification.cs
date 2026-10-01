using System;
using System.Collections.Generic;
using Cooked.Contracts;

namespace Cooked.Session.Editor
{
    /// <summary>Observable domain regression checks; no Unity runtime objects or replacement model.</summary>
    public static class SessionDomainVerification
    {
        public static string[] Run()
        {
            var results = new List<string>();
            var session = new GameSessionService();
            Check(!session.IsActive, "inactive before begin", results);
            Throws<InvalidOperationException>(() => { var unused = session.Checkpoint; }, "inactive checkpoint rejected", results);
            Throws<ArgumentException>(() => session.Begin(default), "default checkpoint rejected", results);
            session.Begin(new CheckpointSnapshot("03_1_Stage", "S1_START", 0));
            Check(session.IsActive && session.Checkpoint.Fragments == 0, "initial death checkpoint available", results);
            Throws<InvalidOperationException>(() => session.Begin(new CheckpointSnapshot("03_1_Stage", "S1_START", 0)), "duplicate begin rejected", results);
            int changes = 0; Action observed = () => changes++; session.Changed += observed;
            session.CaptureCheckpoint("03_1_Stage", "S1_SKILL", 1);
            Check(session.Checkpoint.Fragments == 1 && changes == 1, "capture checkpoint fragments", results);
            session.CaptureCheckpoint("03_1_Stage", "S1_SKILL", 0);
            Check(session.Checkpoint.Fragments == 1 && changes == 1, "same identity does not overwrite snapshot", results);
            session.Unlock(AbilityId.FormChange);
            Check(session.IsUnlocked(AbilityId.FormChange) && session.IsUnlocked(AbilityId.RecoverShell), "form tutorial unlocks shell recovery", results);
            Check(!session.IsUnlocked(AbilityId.Tear), "tear gate independent", results);
            int beforeRepeatedUnlock = changes; session.Unlock(AbilityId.FormChange);
            Check(changes == beforeRepeatedUnlock, "duplicate unlock no notification", results);
            session.Unlock(AbilityId.Tear);
            session.CaptureCheckpoint("03_2_Stage", "S2_ENTRY", 0);
            session.CaptureCheckpoint("03_1_Stage", "S1_SKILL", 0);
            Check(session.Checkpoint.Fragments == 0, "returning from another checkpoint captures anew", results);
            Check(session.IsUnlocked(AbilityId.Tear), "checkpoint update preserves latest unlocks", results);
            Throws<ArgumentOutOfRangeException>(() => session.CaptureCheckpoint("03_1_Stage", "BAD", -1), "negative fragments rejected", results);
            Check(session.Checkpoint.CheckpointId == "S1_SKILL", "invalid capture keeps prior snapshot", results);
            session.Changed -= observed; int afterUnbind = changes;
            session.End(); session.Begin(new CheckpointSnapshot("03_1_Stage", "S1_START", 0));
            Check(!session.IsUnlocked(AbilityId.FormChange) && !session.IsUnlocked(AbilityId.Tear), "second new game clears unlocks", results);
            Check(changes == afterUnbind, "removed session subscription stays silent", results);
            session.Dispose(); session.Dispose();
            Throws<ObjectDisposedException>(() => session.Unlock(AbilityId.Tear), "disposed session rejects commands", results);

            float scale = .75f;
            var control = new GameplayControlService(() => scale, value => scale = value);
            int controlChanges = 0; Action<ControlState> onControl = value => controlChanges++;
            control.Changed += onControl;
            var dialogue = control.BlockGameplay("dialogue");
            Check(control.State.GameplayBlocked && !control.State.WorldPaused && scale == .75f, "dialogue blocks input but world runs", results);
            var optionsInput = control.BlockGameplay("options");
            var optionsWorld = control.PauseWorld("options");
            var optionsPresentation = control.PausePresentation("options");
            Check(scale == 0 && control.State.WorldPaused && control.State.PresentationPaused, "options pauses both clocks", results);
            var transitionWorld = control.PauseWorld("transition");
            optionsWorld.Dispose();
            Check(scale == 0, "nested world pause retains ownership", results);
            transitionWorld.Dispose(); optionsPresentation.Dispose(); optionsInput.Dispose();
            Check(control.State.GameplayBlocked && !control.State.PresentationPaused && scale == .75f, "options close preserves dialogue lock and prior time", results);
            dialogue.Dispose(); int settledChanges = controlChanges; dialogue.Dispose();
            Check(!control.State.GameplayBlocked && controlChanges == settledChanges, "lease release idempotent", results);
            var oldPause = control.PauseWorld("shutdown"); control.Changed -= onControl; int unsubscribed = controlChanges;
            control.Dispose(); oldPause.Dispose(); control.Dispose();
            Check(scale == .75f && controlChanges == unsubscribed, "shutdown restores clock and does not notify unsubscribed owner", results);
            Throws<ObjectDisposedException>(() => control.BlockGameplay("late"), "disposed control rejects leases", results);
            var failures = new List<Exception>();
            var firstFailure = new InvalidOperationException("injected first cleanup failure");
            var secondFailure = new ArgumentException("injected later cleanup failure");
            int completed = 0;
            SessionCleanup.Attempt(failures, () => throw firstFailure);
            SessionCleanup.Attempt(failures, () => completed++);
            SessionCleanup.Attempt(failures, () => throw secondFailure);
            SessionCleanup.Attempt(failures, () => completed++);
            Check(completed == 2 && failures.Count == 2, "cleanup continues through independent failures", results);
            try { SessionCleanup.ThrowIfAny(failures, "injected teardown"); }
            catch (AggregateException aggregate)
            {
                Check(ReferenceEquals(aggregate.InnerExceptions[0], firstFailure) && ReferenceEquals(aggregate.InnerExceptions[1], secondFailure), "cleanup aggregate preserves both original exceptions", results);
            }
            failures.Clear();
            Action observers = () => throw firstFailure;
            observers += () => completed++;
            SessionCleanup.Notify(failures, observers);
            Check(completed == 3 && failures.Count == 1, "failing first observer does not skip later observer", results);
            failures.Clear();
            Action<int> typedObservers = value => throw secondFailure;
            typedObservers += value => completed += value;
            SessionCleanup.Notify(failures, typedObservers, 4);
            Check(completed == 7 && failures.Count == 1, "typed observer failure does not skip tail", results);
            Exception original = null;
            try { throw new InvalidOperationException("original spawn error"); } catch (Exception error) { original = error; }
            try { SessionCleanup.RethrowAfterCleanup(original, () => throw firstFailure); }
            catch (AggregateException aggregate)
            {
                Check(ReferenceEquals(aggregate.InnerExceptions[0], original) && original.StackTrace != null && ReferenceEquals(aggregate.InnerExceptions[1], firstFailure), "spawn preserves original plus cleanup error", results);
            }
            try { SessionCleanup.RethrowAfterCleanup(original, () => completed++); }
            catch (InvalidOperationException error) { Check(ReferenceEquals(error, original) && completed == 8, "successful cleanup rethrows exact original spawn exception", results); }
            var brokenSession = new GameSessionService();
            brokenSession.Begin(new CheckpointSnapshot("03_1_Stage", "S1_START", 0));
            brokenSession.Changed += () => throw new InvalidOperationException("injected session end listener");
            Throws<InvalidOperationException>(brokenSession.Dispose, "session disposal exposes listener failure", results);
            Throws<ObjectDisposedException>(() => brokenSession.Begin(new CheckpointSnapshot("03_1_Stage", "S1_START", 0)), "failed session disposal still finalizes disposed state", results);
            brokenSession.Dispose();
            float failedScale = 1;
            var brokenControl = new GameplayControlService(() => failedScale, value => failedScale = value);
            int observerTail = 0;
            Action<ControlState> failControl = value => throw new InvalidOperationException("injected control observer");
            brokenControl.Changed += failControl;
            brokenControl.Changed += value => observerTail++;
            Throws<AggregateException>(() => brokenControl.PauseWorld("failing-acquire"), "failed lease acquisition reports aggregated observers", results);
            Check(failedScale == 1 && !brokenControl.State.WorldPaused && observerTail == 2, "failed acquisition rolls back time and token, visits tail twice", results);
            brokenControl.Changed -= failControl;
            var lastLease = brokenControl.PauseWorld("dispose-case");
            brokenControl.Changed += failControl;
            Throws<AggregateException>(brokenControl.Dispose, "control dispose exposes listener failure", results);
            Check(failedScale == 1 && !brokenControl.State.WorldPaused, "failed dispose restores world time", results);
            brokenControl.Dispose(); lastLease.Dispose();
            Throws<ObjectDisposedException>(() => brokenControl.PauseWorld("late"), "failed control disposal still rejects new leases", results);
            return results.ToArray();
        }
        private static void Check(bool passed, string name, List<string> results)
        {
            if (!passed) throw new InvalidOperationException("FAIL: " + name);
            results.Add("PASS: " + name);
        }
        private static void Throws<T>(Action action, string name, List<string> results) where T : Exception
        {
            try { action(); }
            catch (T) { results.Add("PASS: " + name); return; }
            throw new InvalidOperationException("FAIL (expected " + typeof(T).Name + "): " + name);
        }
    }
}


