using System;
using System.Collections.Generic;
using System.Linq;
using Cooked.Contracts;

namespace Cooked.Dialogue.Tests
{
    /// <summary>Behavioral regression suite callable from Unity Editor or the isolated console harness.</summary>
    public static class DialogueRegressionTests
    {
        public static IReadOnlyList<string> Run()
        {
            var passed = new List<string>();
            RunCase(passed, "JSON: Korean, punctuation, sorting, exact zero, immutable groups", TableValid);
            RunCase(passed, "JSON: missing/duplicate/type/range errors never expose partial data", TableInvalid);
            RunCase(passed, "Unknown/start/busy and own input lease lifetime", StartAndEnd);
            RunCase(passed, "Typing click reveals; cooldown and frame guard prevent skipped row", ClickAndCooldown);
            RunCase(passed, "Auto delay begins after full output, obeys 2/5 second bounds", AutoTiming);
            RunCase(passed, "Manual reveal restarts full auto delay; disable/re-enable resets", AutoManual);
            RunCase(passed, "Log stops auto and pauses presentation; options preserve remaining timers", PauseAndLog);
            RunCase(passed, "Skip/cancel/dispose clear history and reject stale callbacks", Lifetime);
            RunCase(passed, "Old session/row callbacks and automatic/manual same-frame race", Generations);
            RunCase(passed, "VM command once; disposal removes only owned subscriptions", ViewModelLifetime);
            RunCase(passed, "Zero-duration last row auto finishes once; immutable settings bounds", ZeroAndSettings);
            RunCase(passed, "Same dialogue replay clears history, records identical rows again", RepeatConversation);
            RunCase(passed, "Delay handles: pause remaining cooldown and stale auto callbacks", SchedulerLifetime);
            return passed.AsReadOnly();
        }
        private static void RunCase(List<string> passed, string name, Action test) { test(); passed.Add("PASS: " + name); }
        private static void Assert(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; }
            throw new InvalidOperationException("Expected " + typeof(T).Name);
        }
        private static string Row(int sequence, string duration = "2", string context = "어니: \"포포\"… <길> & 주방! 😀")
            => "{\"Dialogue_Code\":\"A\",\"Sequence\":" + sequence + ",\"Name\":\"어니\",\"Context\":" + Newtonsoft.Json.JsonConvert.ToString(context) + ",\"ContextRevealDuration\":" + duration + "}";
        private static string Json(params string[] rows) => "{\"Rows\":[" + string.Join(",", rows) + "]}";
        private static DialogueTableService Table(params string[] rows)
        {
            var table = new DialogueTableService("test fixture"); table.LoadJson(Json(rows)); return table;
        }
        private static void TableValid()
        {
            var table = Table(Row(3, "0"), Row(1));
            Assert(table.TryGetRows("A", out var rows) && rows.Count == 2 && rows[0].Sequence == 1 && rows[1].Sequence == 3, "Sorted rows");
            Assert(rows[1].ContextRevealDuration == 0 && rows[0].Context.Contains("<길> &") && rows[0].Context.Contains("😀"), "Raw punctuation preserved");
            Throws<NotSupportedException>(() => ((IList<DialogueRow>)rows).Add(rows[0]));
            Throws<InvalidOperationException>(() => table.LoadJson(Json(Row(2))));
            Assert(!table.TryGetRows("a", out _) && !table.TryGetRows(null, out _), "Ordinal code");
        }
        private static void TableInvalid()
        {
            var invalid = new[] {
                "", "null", "{}", "{\"Rows\":[]}", "{\"Rows\":[null]}", Json(Row(1), Row(1)),
                Json(Row(0)), Json(Row(1, "-1")), Json(Row(1, "NaN")), Json(Row(1, "Infinity")), Json(Row(1, "1e100")), Json(Row(1, "true")), Json(Row(1, "2", "")),
                Json(Row(1).Replace("\"Sequence\":1", "\"Sequence\":99999999999999999999999999999999999999")),
                Json(Row(1, "\"2\"")), Json(Row(1).Replace(",\"ContextRevealDuration\":2", "")),
                Json(Row(1).Replace("\"Sequence\":1", "\"Sequence\":1.5")),
                Json(Row(1).Replace("\"Sequence\":1", "\"Sequence\":2147483648")),
                Json(Row(1).Replace("\"Sequence\":1", "\"Sequence\":1,\"Sequence\":2")),
                Json(Row(1).Replace("\"Name\":\"어니\"", "\"Name\":3")),
                Json(Row(1).Replace("\"A\"", "\" A\"")), Json(Row(1).Replace("\"A\"", "3")),
                Json(Row(1).Replace("\"Name\":", "\"name\":")),
                Json(Row(1).Replace("\"Name\":\"어니\"", "\"Name\":\" \"")),
                Json(Row(1).Replace("\"Sequence\":1", "\"Extra\":true,\"Sequence\":1")),
                Json(Row(1)) + " true"
            };
            foreach (string json in invalid)
            {
                var table = new DialogueTableService("invalid fixture");
                Throws<FormatException>(() => table.LoadJson(json));
                Assert(!table.IsLoaded && !table.TryGetRows("A", out _), "No partial table for: " + json);
            }
        }
        private static void StartAndEnd()
        {
            using (var f = new Fixture())
            {
                Assert(!f.Service.TryStart("missing") && f.Control.Blocks == 0, "Unknown code no lease");
                Assert(f.Service.TryStart("A") && f.Control.Blocks == 1, "Start lease");
                Assert(!f.Service.TryStart("A") && f.Control.Blocks == 1, "Busy no lease");
                using (f.Control.BlockGameplay("other"))
                {
                    f.Service.SkipAll(); Assert(f.Control.Blocks == 1, "Only own lease released");
                }
                Assert(f.Control.Blocks == 0 && f.Service.Outcome == DialogueOutcome.Skipped, "Skip finished");
            }
        }
        private static void ClickAndCooldown()
        {
            foreach (float cooldown in new[] { 0.3f, 1f })
            using (var f = new Fixture(new DialogueSettings(cooldown)))
            {
                f.Service.TryStart("A");
                Assert(f.Service.Advance(1), "Reveal click accepted");
                Assert(f.Service.State == DialogueState.AwaitingAdvance && f.Service.History.Count == 1, "Revealed current row");
                Assert(!f.Service.Advance(1), "Same frame click rejected");
                f.Clock.Advance(cooldown - 0.01f, 2); Assert(!f.Service.Advance(2), "Cooldown not expired");
                f.Clock.Advance(0.011f, 3); Assert(f.Service.Advance(3) && f.Service.CurrentRow.Sequence == 2, "Next row once");
                Assert(!f.Service.Advance(3) && f.Service.History.Count == 1, "No double advance");
            }
        }
        private static void AutoTiming()
        {
            foreach (float delay in new[] { 2f, 5f })
            using (var f = new Fixture(new DialogueSettings(0.3f, delay)))
            {
                f.Service.TryStart("A"); f.Service.SetAuto(true);
                f.Clock.Advance(100, 1); Assert(f.Service.CurrentRow.Sequence == 1, "Cannot auto advance during typing");
                f.Service.NotifyRevealCompleted(f.Service.SessionId, f.Service.RowId, 2);
                f.Clock.Advance(delay - 0.1f, 3); Assert(f.Service.CurrentRow.Sequence == 1, "Wait full configured hold");
                f.Clock.Advance(0.11f, 4); Assert(f.Service.CurrentRow.Sequence == 2, "Auto advanced");
            }
        }
        private static void AutoManual()
        {
            using (var f = new Fixture())
            {
                f.Service.TryStart("A"); f.Service.SetAuto(true); f.Service.Advance(1);
                f.Clock.Advance(2, 2); Assert(f.Service.CurrentRow.Sequence == 1, "Manual reveal still waits");
                f.Service.SetAuto(false); f.Clock.Advance(20, 3); Assert(f.Service.CurrentRow.Sequence == 1, "Auto off");
                f.Service.SetAuto(true); f.Clock.Advance(2.9f, 4); Assert(f.Service.CurrentRow.Sequence == 1, "ON fresh hold");
                f.Clock.Advance(0.11f, 5); Assert(f.Service.CurrentRow.Sequence == 2, "ON eventual advance");
            }
        }
        private static void PauseAndLog()
        {
            using (var f = new Fixture())
            {
                f.Service.TryStart("A"); f.Service.SetAuto(true); f.Service.Advance(1); f.Clock.Advance(1, 2);
                using (f.Control.PausePresentation("options"))
                {
                    float remaining = f.Service.AutoRemaining;
                    f.Clock.Advance(100, 3); f.Service.SkipAll(); f.Service.Advance(3);
                    Assert(f.Service.IsActive && f.Service.AutoRemaining == remaining && f.Service.AutoEnabled, "Options freeze all");
                }
                f.Clock.Advance(1.9f, 4); Assert(f.Service.CurrentRow.Sequence == 1, "Resume remaining hold");
                f.Clock.Advance(0.11f, 5); Assert(f.Service.CurrentRow.Sequence == 2, "Resume auto");
                f.Service.OpenLog(); Assert(!f.Service.AutoEnabled && f.Service.PresentationPaused && f.Control.WorldPauses == 0, "Log not world pause");
                Assert(f.Service.History.Count == 1, "Only fully revealed history");
                long session = f.Service.SessionId, row = f.Service.RowId;
                Assert(!f.Service.NotifyRevealCompleted(session, row, 6), "Paused completion ignored");
                using (f.Control.PausePresentation("options")) { f.Service.CloseLog(); Assert(f.Service.LogOpen, "Options modal"); }
                Assert(f.Service.PresentationPaused, "Remaining log pause preserved");
                f.Service.CloseLog(); Assert(!f.Service.AutoEnabled && !f.Service.PresentationPaused, "Log close leaves auto off");
            }
        }
        private static void Lifetime()
        {
            var f = new Fixture(); f.Service.TryStart("A"); long session = f.Service.SessionId, row = f.Service.RowId;
            int ended = 0; f.Service.Ended += _ => ended++;
            f.Service.SkipAll(); f.Service.SkipAll(); f.Service.Cancel();
            Assert(ended == 1 && f.Service.History.Count == 0 && f.Control.Blocks == 0, "Exactly once end and no future history");
            Assert(!f.Service.NotifyRevealCompleted(session, row, 1), "Late completed ignored");
            f.Service.TryStart("A"); f.Dispose();
            Assert(f.Control.Blocks == 0 && f.Control.SubscriberCount == 0, "Dispose owns subscriptions");
            Assert(!f.Service.TryStart("A") && !f.Service.Advance(2), "Disposed rejects actions");
        }
        private static void Generations()
        {
            using (var f = new Fixture())
            {
                f.Service.TryStart("A"); long oldSession = f.Service.SessionId, oldRow = f.Service.RowId;
                f.Service.Cancel(); f.Service.TryStart("A");
                Assert(!f.Service.NotifyRevealCompleted(oldSession, oldRow, 1), "Old session");
                f.Service.Advance(1); f.Service.SetAuto(true); f.Clock.Advance(3.01f, 2);
                Assert(f.Service.CurrentRow.Sequence == 2 && !f.Service.Advance(2), "Auto/manual one progression");
                Assert(!f.Service.NotifyRevealCompleted(f.Service.SessionId, oldRow, 3), "Old row");
            }
        }
        private static void ViewModelLifetime()
        {
            using (var f = new Fixture())
            {
                var vm = new DialogueViewModel(f.Service); int notifications = 0, independent = 0;
                vm.PropertyChanged += (_, __) => notifications++; f.Service.Changed += () => independent++;
                f.Service.TryStart("A"); vm.Advance(1);
                Assert(vm.State == DialogueState.AwaitingAdvance && vm.History.Count == 1, "VM command observes service");
                vm.Dispose(); int after = notifications;
                f.Service.TryStart("A"); vm.Advance(10);
                Assert(notifications == after && f.Service.State == DialogueState.Revealing && independent > after, "Disposed VM no callbacks; other consumer retained");
            }
        }
        private static void ZeroAndSettings()
        {
            var control = new FakeControl();
            var clock = new FakeDialogueScheduler();
            using (var service = new DialogueService(Table(Row(1, "0")), control, clock))
            {
                int ended = 0; service.Ended += _ => ended++;
                service.TryStart("A"); Assert(service.State == DialogueState.AwaitingAdvance && service.History.Count == 1, "Zero immediate");
                service.SetAuto(true); clock.Advance(3, 1); clock.Advance(30, 2);
                Assert(!service.IsActive && service.Outcome == DialogueOutcome.Completed && ended == 1 && control.Blocks == 0, "Last auto once");
            }
            Throws<ArgumentOutOfRangeException>(() => new DialogueSettings(0.29f));
            Throws<ArgumentOutOfRangeException>(() => new DialogueSettings(1.01f));
            Throws<ArgumentOutOfRangeException>(() => new DialogueSettings(0.3f, 1.9f));
            Throws<ArgumentOutOfRangeException>(() => new DialogueSettings(0.3f, 5.1f));
        }
        private static void RepeatConversation()
        {
            using (var f = new Fixture())
            {
                for (int run = 0; run < 2; run++)
                {
                    f.Service.TryStart("A"); Assert(f.Service.History.Count == 0, "New conversation has no history");
                    f.Service.Advance(run + 10); f.Service.OpenLog();
                    Assert(f.Service.History.Count == 1 && f.Service.History[0].Context.Contains("포포"), "Repeat records full context");
                    f.Service.SkipAll(); Assert(f.Service.History.Count == 0, "End clears history");
                }
            }
        }
        private static void SchedulerLifetime()
        {
            using (var f = new Fixture())
            {
                f.Service.TryStart("A"); f.Service.Advance(1);
                f.Clock.Advance(0.1f, 2);
                float remaining = f.Service.CooldownRemaining;
                using (f.Control.PausePresentation("options"))
                {
                    f.Clock.Advance(10f, 3);
                    Assert(Math.Abs(f.Service.CooldownRemaining - remaining) < 0.0001f && !f.Service.CanAdvance, "Cooldown remains paused");
                }
                f.Clock.Advance(remaining - 0.01f, 4); Assert(!f.Service.CanAdvance, "Resume does not start timer over or expire early");
                f.Clock.Advance(0.02f, 5); Assert(f.Service.CanAdvance, "Remaining cooldown completes");
                f.Service.SetAuto(true); var oldAuto = f.Clock.LastCallback;
                f.Service.SetAuto(false); f.Service.SetAuto(true);
                oldAuto(); Assert(f.Service.CurrentRow.Sequence == 1, "Cancelled auto generation ignored");
                f.Service.OpenLog(); Assert(f.Clock.PendingCount == 0, "Log cancels automatic handle");
                f.Service.CloseLog(); f.Service.SetAuto(true); var disposedCallback = f.Clock.LastCallback;
                f.Service.Dispose(); disposedCallback();
                Assert(!f.Service.IsActive && f.Clock.PendingCount == 0 && f.Control.Blocks == 0, "Dispose cancels handles and late callbacks");
            }
        }
        private sealed class Fixture : IDisposable
        {
            public readonly FakeControl Control = new FakeControl();
            public readonly FakeDialogueScheduler Clock = new FakeDialogueScheduler();
            public readonly DialogueService Service;
            public Fixture(DialogueSettings settings = null) { Service = new DialogueService(Table(Row(1), Row(2), Row(3)), Control, Clock, settings); }
            public void Dispose() { Service.Dispose(); }
        }
        internal sealed class FakeControl : IGameplayControlService
        {
            public int Blocks, WorldPauses, PresentationPauses;
            private Action<ControlState> changed;
            public int SubscriberCount => changed?.GetInvocationList().Length ?? 0;
            public ControlState State => new ControlState(Blocks > 0, WorldPauses > 0, PresentationPauses > 0);
            public event Action<ControlState> Changed { add { changed += value; } remove { changed -= value; } }
            public IDisposable BlockGameplay(string owner) { Blocks++; changed?.Invoke(State); return new Lease(() => { Blocks--; changed?.Invoke(State); }); }
            public IDisposable PauseWorld(string owner) { WorldPauses++; changed?.Invoke(State); return new Lease(() => { WorldPauses--; changed?.Invoke(State); }); }
            public IDisposable PausePresentation(string owner) { PresentationPauses++; changed?.Invoke(State); return new Lease(() => { PresentationPauses--; changed?.Invoke(State); }); }
            public void Dispose() { changed = null; }
        }
        private sealed class Lease : IDisposable
        {
            private Action release;
            public Lease(Action release) { this.release = release; }
            public void Dispose() { var action = release; release = null; action?.Invoke(); }
        }
    }
}
