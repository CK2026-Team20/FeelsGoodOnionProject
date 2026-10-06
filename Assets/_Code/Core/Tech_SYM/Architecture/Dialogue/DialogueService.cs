using System;
using System.Collections.Generic;
using Cooked.Contracts;

namespace Cooked.Dialogue
{
    /// <summary>Owns conversation rules, history, timers and its own input lease. No Unity/UI dependency.</summary>
    public sealed class DialogueService : IDialogueService
    {
        private readonly IDialogueTableService table;
        private readonly IGameplayControlService control;
        private readonly DialogueSettings settings;
        private readonly IDialogueDelayScheduler scheduler;
        private readonly List<DialogueRow> history = new List<DialogueRow>();
        private readonly IReadOnlyList<DialogueRow> historyView;
        private IReadOnlyList<DialogueRow> rows;
        private IDisposable inputLease, worldLease;
        private bool starting;
        private int rowIndex;
        private int lastProgressFrame = -1;
        private IDialogueDelayHandle cooldown;
        private IDialogueDelayHandle autoDelay;
        private long cooldownRevision;
        private long autoRevision;
        private bool disposed;
        private bool optionsPaused;

        public DialogueService(IDialogueTableService table, IGameplayControlService control, IDialogueDelayScheduler scheduler, DialogueSettings settings = null)
        {
            this.table = table ?? throw new ArgumentNullException(nameof(table));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.settings = settings ?? new DialogueSettings();
            historyView = history.AsReadOnly();
            optionsPaused = control.State.PresentationPaused;
            control.Changed += OnControlChanged;
        }
        public event Action Changed;
        public event Action<DialogueOutcome> Ended;
        public DialogueState State { get; private set; }
        public DialogueOutcome Outcome { get; private set; }
        public bool IsActive => State != DialogueState.Closed;
        public bool AutoEnabled { get; private set; }
        public bool LogOpen { get; private set; }
        public bool OptionsPaused => optionsPaused;
        public bool PresentationPaused => optionsPaused || LogOpen;
        public bool CanAdvance => IsActive && !PresentationPaused && cooldown == null;
        public long SessionId { get; private set; }
        public long RowId { get; private set; }
        public DialogueRow CurrentRow => IsActive ? rows[rowIndex] : null;
        public IReadOnlyList<DialogueRow> History => historyView;
        public float AutoRemaining => autoDelay?.Remaining ?? 0;
        public float CooldownRemaining => cooldown?.Remaining ?? 0;

        public bool TryStart(string dialogueCode)
        {
            if (disposed || starting || IsActive || optionsPaused || !table.TryGetRows(dialogueCode, out var found) || found.Count == 0) return false;
            // Acquire before publishing a visible conversation; failure cannot strand an active UI.
            starting = true;
            try
            {
                inputLease = control.BlockGameplay("Dialogue");
                worldLease = control.PauseWorld("Dialogue");
                if (disposed || optionsPaused) { ReleaseLeases(); return false; }
            rows = found;
            rowIndex = 0;
            SessionId++;
            Outcome = DialogueOutcome.None;
            history.Clear();
            AutoEnabled = false;
            LogOpen = false;
            CancelCooldown();
            lastProgressFrame = -1;
            BeginRow();
            return true;
            }
            catch (Exception startError)
            {
                State = DialogueState.Closed;
                rows = null;
                var errors = new List<Exception> { startError };
                try { CancelAutoDelay(); } catch (Exception e) { errors.Add(e); }
                try { CancelCooldown(); } catch (Exception e) { errors.Add(e); }
                try { ReleaseLeases(); } catch (Exception e) { errors.Add(e); }
                throw new AggregateException("Dialogue start failed.", errors);
            }
            finally { starting = false; }
        }

        public bool Advance(int frameId)
        {
            if (disposed || !CanAdvance || lastProgressFrame == frameId) return false;
            StartCooldown();
            lastProgressFrame = frameId;
            if (State == DialogueState.Revealing) CompleteRowPresentation();
            else NextRow();
            return true;
        }
        public bool NotifyRevealCompleted(long sessionId, long rowId, int frameId)
        {
            if (disposed || !IsActive || PresentationPaused || State != DialogueState.Revealing || SessionId != sessionId || RowId != rowId) return false;
            lastProgressFrame = frameId;
            CompleteRowPresentation();
            return true;
        }
        public void SetAuto(bool enabled)
        {
            if (disposed || !IsActive || PresentationPaused || AutoEnabled == enabled) return;
            AutoEnabled = enabled;
            CancelAutoDelay();
            if (enabled && State == DialogueState.AwaitingAdvance) StartAutoDelay();
            Notify();
        }
        public void OpenLog()
        {
            if (disposed || !IsActive || optionsPaused || LogOpen) return;
            AutoEnabled = false;
            CancelAutoDelay();
            LogOpen = true;
            ApplyPause();
            Notify();
        }
        public void CloseLog()
        {
            if (disposed || !IsActive || optionsPaused || !LogOpen) return;
            LogOpen = false;
            ApplyPause();
            Notify();
        }
        public void SkipAll()
        {
            if (!disposed && IsActive && !optionsPaused) Finish(DialogueOutcome.Skipped);
        }
        public void Cancel() { if (!disposed && IsActive) Finish(DialogueOutcome.Cancelled); }
        private void BeginRow()
        {
            RowId++;
            CancelAutoDelay();
            State = DialogueState.Revealing;
            if (CurrentRow.ContextRevealDuration == 0) CompleteRowPresentation();
            else Notify();
        }
        private void CompleteRowPresentation()
        {
            State = DialogueState.AwaitingAdvance;
            history.Add(CurrentRow);
            CancelAutoDelay();
            if (AutoEnabled) StartAutoDelay();
            Notify();
        }
        private void NextRow()
        {
            if (rowIndex + 1 == rows.Count) Finish(DialogueOutcome.Completed);
            else { rowIndex++; BeginRow(); }
        }
        private void Finish(DialogueOutcome result)
        {
            State = DialogueState.Closed;
            Outcome = result;
            AutoEnabled = false;
            LogOpen = false;
            history.Clear();
            rows = null;
            var errors = new List<Exception>();
            try { CancelAutoDelay(); } catch (Exception e) { errors.Add(e); }
            try { CancelCooldown(); } catch (Exception e) { errors.Add(e); }
            try { ReleaseLeases(); } catch (Exception e) { errors.Add(e); }
            try { Notify(); } catch (Exception e) { errors.Add(e); }
            try { Ended?.Invoke(result); } catch (Exception e) { errors.Add(e); }
            if (errors.Count > 0) throw new AggregateException("Dialogue completion cleanup failed.", errors);
        }
        private void ReleaseLeases()
        {
            var input = inputLease; var world = worldLease;
            inputLease = worldLease = null;
            var errors = new List<Exception>();
            try { input?.Dispose(); } catch (Exception e) { errors.Add(e); }
            try { world?.Dispose(); } catch (Exception e) { errors.Add(e); }
            if (errors.Count > 0) throw new AggregateException("Dialogue leases could not be released cleanly.", errors);
        }
        private void OnControlChanged(ControlState state)
        {
            if (disposed || optionsPaused == state.PresentationPaused) return;
            optionsPaused = state.PresentationPaused;
            ApplyPause();
            Notify();
        }
        private void StartCooldown()
        {
            CancelCooldown();
            long revision = cooldownRevision, session = SessionId;
            cooldown = scheduler.Schedule(settings.ClickCooldown, () =>
            {
                if (disposed || !IsActive || session != SessionId || revision != cooldownRevision) return;
                cooldown = null;
                Notify();
            });
            cooldown.SetPaused(PresentationPaused);
        }
        private void StartAutoDelay()
        {
            long revision = autoRevision, session = SessionId, row = RowId;
            autoDelay = scheduler.Schedule(settings.AutoDelay, () =>
            {
                if (disposed || !IsActive || !AutoEnabled || PresentationPaused || session != SessionId || row != RowId || revision != autoRevision) return;
                autoDelay = null;
                lastProgressFrame = scheduler.FrameId;
                NextRow();
            });
            autoDelay.SetPaused(PresentationPaused);
        }
        private void ApplyPause()
        {
            cooldown?.SetPaused(PresentationPaused);
            autoDelay?.SetPaused(PresentationPaused);
        }
        private void CancelCooldown()
        {
            cooldownRevision++;
            var previous = cooldown; cooldown = null;
            previous?.Dispose();
        }
        private void CancelAutoDelay()
        {
            autoRevision++;
            var previous = autoDelay; autoDelay = null;
            previous?.Dispose();
        }
        private void Notify() { Changed?.Invoke(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { if (IsActive) Finish(DialogueOutcome.Cancelled); else ReleaseLeases(); }
            finally
            {
                control.Changed -= OnControlChanged;
                Changed = null;
                Ended = null;
            }
        }
    }
}
