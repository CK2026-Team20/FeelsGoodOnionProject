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
        private readonly HashSet<string> completedCodes = new HashSet<string>(StringComparer.Ordinal);
        private readonly IReadOnlyList<DialogueRow> historyView;
        private IReadOnlyList<DialogueRow> rows;
        private IDisposable inputLease;
        private bool starting, resettingRound, finishing;
        private int rowIndex;
        private int lastProgressFrame = -1;
        private IDialogueDelayHandle cooldown;
        private IDialogueDelayHandle autoDelay;
        private long cooldownRevision;
        private long autoRevision;
        private bool disposed;
        private bool optionsPaused;
        private long lifecycleRevision;
        private long notificationRevision;

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
        public bool CanAdvance => !disposed && !resettingRound && !finishing && IsActive && !PresentationPaused && cooldown == null;
        public bool CanSkip => !disposed && !resettingRound && !finishing && IsActive && !PresentationPaused;
        public long SessionId { get; private set; }
        public long RowId { get; private set; }
        public DialogueRow CurrentRow => IsActive ? rows[rowIndex] : null;
        public IReadOnlyList<DialogueRow> History => historyView;
        public float AutoRemaining => autoDelay?.Remaining ?? 0;
        public float CooldownRemaining => cooldown?.Remaining ?? 0;
        public bool IsCompleted(string code) => !disposed && code != null && completedCodes.Contains(code);

        public bool TryStart(string dialogueCode)
            => TryStartCore(dialogueCode, false, null);

        /// <summary>완료 장부를 존중하며 입력 차단 획득 뒤 조립자의 메모/현재 요청 검사를 실행한다.</summary>
        public bool TryStart(string dialogueCode, Func<bool> prepare)
            => TryStartCore(dialogueCode, false, prepare);

        /// <summary>최종 접촉을 검증한 조립자 전용 경로. 장부를 지우지 않고 새 대화를 시작한다.
        /// 일반 IDialogueService 소비자에게는 이 재생 예외를 노출하지 않는다.</summary>
        public bool TryStartFinalDialogue(string dialogueCode, Func<bool> prepare = null)
            => TryStartCore(dialogueCode, true, prepare);

        private bool TryStartCore(string dialogueCode, bool allowCompletedFinal, Func<bool> prepare)
        {
            if (disposed || starting || resettingRound || finishing || IsActive || optionsPaused || dialogueCode == null ||
                (!allowCompletedFinal && completedCodes.Contains(dialogueCode)) || !table.TryGetRows(dialogueCode, out var found) || found.Count == 0) return false;
            // Acquire before publishing a visible conversation; failure cannot strand an active UI.
            starting = true;
            long revision = lifecycleRevision;
            try
            {
                inputLease = control.BlockGameplay("Dialogue");
                if (disposed || optionsPaused || revision != lifecycleRevision) { ReleaseLeases(); return false; }
                // 메모 강제 닫기의 관찰자가 Actor/Stage/옵션/수명을 바꿔도 게시 전에 거절한다.
                bool prepared = prepare == null || prepare();
                if (!prepared || disposed || optionsPaused || revision != lifecycleRevision) { ReleaseLeases(); return false; }
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
            if (CanSkip) Finish(DialogueOutcome.Skipped);
        }
        public void Cancel()
        {
            if (disposed) return;
            lifecycleRevision++;
            if (IsActive) Finish(DialogueOutcome.Cancelled);
        }
        // Temporary interface member only: T10 removes CoreWorldBinding's retry caller,
        // then the contracts owner can remove this member. Never erase a round's ledger.
        [Obsolete("완료한 대화는 능력 복원으로 지우지 않습니다. 새 판은 ResetRound를 사용하세요.")]
        public void ForgetCompletedCodes(IEnumerable<string> dialogueCodes)
        {
            if (disposed) return;
            throw new NotSupportedException("한 판의 Completed/Skipped 대화 장부는 Retry나 능력 재잠금으로 지울 수 없습니다.");
        }
        // Resident Core can begin another round without retaining a previous round's ledger.
        public void ResetRound()
        {
            if (disposed || resettingRound || finishing) return;
            resettingRound = true;
            try { Cancel(); }
            finally { completedCodes.Clear(); history.Clear(); Outcome = DialogueOutcome.None; resettingRound = false; }
        }
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
            if (finishing || !IsActive) return;
            finishing = true;
            if ((result == DialogueOutcome.Completed || result == DialogueOutcome.Skipped) && rows != null)
                completedCodes.Add(rows[0].Dialogue_Code);
            State = DialogueState.Closed;
            Outcome = result;
            AutoEnabled = false;
            LogOpen = false;
            history.Clear();
            rows = null;
            var errors = new List<Exception>();
            try
            {
                try { CancelAutoDelay(); } catch (Exception e) { errors.Add(e); }
                try { CancelCooldown(); } catch (Exception e) { errors.Add(e); }
                try { ReleaseLeases(); } catch (Exception e) { errors.Add(e); }
                try { Notify(); } catch (Exception e) { errors.Add(e); }
                var listeners = Ended;
                if (!disposed && listeners != null)
                    foreach (Action<DialogueOutcome> listener in listeners.GetInvocationList())
                    {
                        if (disposed) break;
                        if (Ended == null || Array.IndexOf(Ended.GetInvocationList(), listener) < 0) continue;
                        try { listener(result); } catch (Exception e) { errors.Add(e); }
                    }
            }
            finally { finishing = false; }
            if (errors.Count > 0) throw new AggregateException("Dialogue completion cleanup failed.", errors);
        }
        private void ReleaseLeases()
        {
            var input = inputLease;
            inputLease = null;
            var errors = new List<Exception>();
            try { input?.Dispose(); } catch (Exception e) { errors.Add(e); }
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
        private void Notify()
        {
            if (disposed) return;
            long version = ++notificationRevision;
            var errors = new List<Exception>();
            var listeners = Changed;
            if (listeners != null)
                foreach (Action listener in listeners.GetInvocationList())
                {
                    if (disposed || version != notificationRevision) break;
                    if (Changed == null || Array.IndexOf(Changed.GetInvocationList(), listener) < 0) continue;
                    try { listener(); } catch (Exception e) { errors.Add(e); }
                }
            if (errors.Count > 0) throw new AggregateException("Dialogue state observer failures.", errors);
        }
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
                completedCodes.Clear();
            }
        }
    }
}
