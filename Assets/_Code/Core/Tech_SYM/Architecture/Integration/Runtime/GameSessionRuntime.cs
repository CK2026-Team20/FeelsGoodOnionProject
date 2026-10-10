using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cooked.Contracts;
using Cooked.Dialogue;
using Cooked.Foundation;
using Cooked.Level;
using Cooked.Session;
using Cooked.UI;
using UnityEngine;

namespace Cooked.Integration
{
    /// <summary>One Core's composition lifetime. Retry preserves this object; title return disposes it.</summary>
    public sealed class GameSessionRuntime : IDisposable
    {
        private readonly ServiceLocator scope;
        private readonly SessionActorHost host;
        private readonly DialogueView dialogueView;
        private readonly DialogueViewModel dialogueModel;
        private readonly IntegrationEntrySettings entries;
        private readonly IGameFlowService flow;
        private readonly OptionsService options;
        private bool disposed, visibilityInitialized;
        private bool actorBindingsReady;
        private Func<bool> actorViewReady;
        public void BindActorViewReadiness(Func<bool> ready)
        { ThrowIfDisposed(); actorViewReady = ready ?? throw new ArgumentNullException(nameof(ready)); }
        private CheckpointMarker lastContactMarker;
        private bool capturingCheckpoint;
        private Func<string, CancellationToken, OperationResult, IEnumerator> prepareStage;
        public string ActiveStageId { get; private set; }
        public IStageService ActiveStage => Stages.Require(ActiveStageId);
        public void BindStageLifecycle(Func<string, CancellationToken, OperationResult, IEnumerator> prepare)
        { ThrowIfDisposed(); prepareStage = prepare ?? throw new ArgumentNullException(nameof(prepare)); }
        public IEnumerator PrepareInitialStages(CancellationToken token, OperationResult result)
        {
            ThrowIfDisposed();
            Stages.Refresh();
            foreach (var stage in Stages.Stages)
            {
                var step = new OperationResult();
                yield return PrepareStage(stage.StageId, token, step);
                if (step.Status != OperationStatus.Succeeded) { CopyFailure(step, result); yield break; }
            }
            result.Succeed();
        }
        private IEnumerator PrepareStage(string stageId, CancellationToken token, OperationResult result)
        {
            ThrowIfDisposed(); token.ThrowIfCancellationRequested();
            if (prepareStage == null) { result.Fail("T9 Stage preparation callback is not connected."); yield break; }
            yield return prepareStage(stageId, token, result);
            token.ThrowIfCancellationRequested();
            if (result.Status == OperationStatus.Pending) result.Fail("Stage preparation did not report completion: " + stageId);
        }
        public IEnumerator PrepareCurrentStageForRetry(CancellationToken token, OperationResult result)
        {
            ThrowIfDisposed();
            if (!Session.IsActive || Player.HasActor) { result.Fail("Retry preparation requires an active session without an Actor."); yield break; }
            var step = new OperationResult();
            yield return PrepareStage(ActiveStageId, token, step);
            if (step.Status != OperationStatus.Succeeded) { CopyFailure(step, result); yield break; }
            // Actor replacement does not clear physical Marker identity or the completed dialogue ledger.
            result.Succeed();
        }
        private static void CopyFailure(OperationResult source, OperationResult target)
        { if (source.Status == OperationStatus.Cancelled) target.Cancel(); else target.Fail(source.Error ?? "Stage preparation failed."); }
        public GameSessionService Session { get; }
        public PlayerBridgeService Player { get; }
        public DialogueService Dialogue { get; }
        public ICinematicService Cinematics { get; }
        public LoadedStageRegistry Stages { get; }
        public SessionActorHost ActorHost => host;
        public bool GameplayVisible { get; private set; }
        public event Action ActorReady;
        public event Action ActorRemoving;
        public event Action CheckpointCaptured;
        public event Action<bool> GameplayVisibilityChanged;

        public GameSessionRuntime(ServiceLocator app, SceneCatalog catalog, SessionActorHost host,
            DialogueView dialogueView, ICinematicService cinematics,
            IntegrationEntrySettings entries)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            this.host = host != null ? host : throw new ArgumentNullException(nameof(host));
            this.dialogueView = dialogueView != null ? dialogueView : throw new ArgumentNullException(nameof(dialogueView));
            this.entries = entries != null ? entries : throw new ArgumentNullException(nameof(entries));
            this.entries.ValidateCatalog(catalog);
            Cinematics = cinematics ?? throw new ArgumentNullException(nameof(cinematics));
            Stages = new LoadedStageRegistry(catalog);
            flow = app.Get<IGameFlowService>();
            options = app.Get<OptionsService>();
            var control = app.Get<IGameplayControlService>();
            scope = new ServiceLocator(app);
            try
            {
                Session = new GameSessionService();
                scope.Register<IGameSessionService>(Session);
                Player = new PlayerBridgeService(control, Session, RequestDeathRetry,
                    host.HorizontalKnockbackSpeed, host.UpwardKnockbackSpeed);
                scope.Register<IPlayerBridgeService>(Player);
                Player.Dying += OnActorDying;
                Dialogue = new DialogueService(app.Get<IDialogueTableService>(), control, new DOTweenDialogueDelayScheduler(), new DialogueSettings(.3f, 3f));
                scope.Register<IDialogueService>(Dialogue);
                scope.Register<ICinematicService>(Cinematics);
                dialogueModel = new DialogueViewModel(Dialogue);
                dialogueView.Bind(dialogueModel);
                host.Initialize(Player, control);
                Player.SetEnemyHurtColliderResolver(enemy => enemy.HurtCollider);

            }
            catch (Exception initializationError)
            {
                // Preserve construction failure even if partially acquired resources fail to release.
                try { Dispose(); }
                catch (Exception cleanupError)
                { throw new AggregateException("Core construction and rollback both failed.", initializationError, cleanupError); }
                throw;
            }
        }
        public void Begin(string entryStageId)
        {
            ThrowIfDisposed();
            var entry = entries.Require(entryStageId);
            Stages.Refresh(entryStageId);
            var stage = Stages.Require(entryStageId);
            var initialPose = stage.InitialPose;
            AbilityMask mask = AbilityMask.None;
            foreach (var ability in entry.Abilities)
                mask |= ability == AbilityId.FormChange ? AbilityMask.FormChange : AbilityMask.Tear;
            var initial = new CheckpointSnapshot(initialPose.position, entry.Fragments, mask);
            ActiveStageId = entryStageId;
            lastContactMarker = null;
            Session.Begin(initial);
            Dialogue.ResetRound();
        }
        public void SpawnActorAtCheckpoint()
        {
            ThrowIfDisposed();
            if (!Session.IsActive) throw new InvalidOperationException("Session is inactive.");
            var saved = Session.RestoreCheckpoint();
            Stages.Require(ActiveStageId);
            // Rotation is fresh authoring, not a third checkpoint value. Retry never re-queries a marker.
            var pose = new Pose(saved.Position, Session.HasCheckpoint ? Quaternion.identity : ActiveStage.InitialPose.rotation);
            try
            {
                host.Spawn(pose, saved.Fragments);
            }
            catch (Exception spawnError)
            {
                var errors = new List<Exception> { spawnError };
                try { NotifyActorRemoving(); } catch (Exception error) { errors.Add(error); }
                try { host.ReleaseNow(); } catch (Exception error) { errors.Add(error); }
                finally { Stages.Clear(); }
                if (errors.Count > 1) throw new AggregateException("Actor spawn and rollback failed.", errors);
                throw;
            }
        }
        // Spawn is synchronous, but HMS initializes its authoritative movement mode in Start.
        // The runtime coroutine calls this after Start and keeps gameplay hidden until Brain is ready.
        public void CompleteActorBindings()
        {
            ThrowIfDisposed();
            if (actorBindingsReady) throw new InvalidOperationException("Actor bindings were already completed.");
            if (!host.ActorModesReady)
                throw new InvalidOperationException("Actor HMS mode has not initialized.");
            ActorReady?.Invoke();
            actorBindingsReady = true;
        }
        public bool ActorViewReady => actorBindingsReady && host.ActorModesReady && actorViewReady != null && actorViewReady();
        public IEnumerator DespawnActor()
        {
            if (disposed) yield break;
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, CancelWorldOperations);
            // Every observer releases its references even if another presentation observer fails.
            SessionCleanup.Attempt(errors, NotifyActorRemoving);
            yield return host.Despawn();
            SessionCleanup.ThrowIfAny(errors, "World bindings failed to release.");
        }
        private void NotifyActorRemoving()
        {
            actorBindingsReady = false;
            var handlers = ActorRemoving;
            if (handlers == null) return;
            var errors = new List<Exception>();
            foreach (Action handler in handlers.GetInvocationList())
                try { handler(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0) throw new AggregateException("Actor binding release failed.", errors);
        }
        public bool TryStartSafeDialogue(string stageId, string code)
        {
            ThrowIfDisposed();
            if (!CanAcceptWorldRequest) return false;
            if (stageId != ActiveStageId) return false;
            var owner = Stages.Require(stageId);
            var source = Player.Actor;
            long generation = Player.ActorGeneration;
            return Dialogue.TryStart(code, () => PrepareDialogueStart(owner, source, generation));
        }
        /// <summary>현재 판의 활성 Stage 최종 접촉만 완료/Skipped Code 재생을 요청한다.</summary>
        public bool TryStartFinalDialogue(StageService owner, StageTrigger trigger, PlayerFacade source,
            long generation, string expectedCode)
        {
            if (!IsCurrentDialogueSource(owner, source, generation) || trigger == null) return false;
            var selection = trigger.Dialogue;
            if (!IsFinalContact()) return false;
            var table = scope.Get<IDialogueTableService>();
            string code = table.RequireSelection(selection);
            if (!string.Equals(code, expectedCode, StringComparison.Ordinal)) return false;
            return Dialogue.TryStartFinalDialogue(code, () => PrepareDialogueStart(owner, source, generation,
                () => IsFinalContact() && string.Equals(table.RequireSelection(selection), code, StringComparison.Ordinal)));

            bool IsFinalContact() => owner.CanDispatch(source) && trigger.isActiveAndEnabled &&
                trigger.Kind == StageTriggerKind.Escape && trigger.Owner == owner &&
                trigger.gameObject.scene == owner.gameObject.scene && ReferenceEquals(trigger.Dialogue, selection);
        }
        private bool IsCurrentDialogueSource(IStageService owner, PlayerFacade source, long generation)
            => CanAcceptWorldRequest && owner != null && source != null &&
                ReferenceEquals(ActiveStage, owner) && Player.IsCurrentActor(source, generation);
        private bool PrepareDialogueStart(IStageService owner, PlayerFacade source, long generation,
            Func<bool> finalContact = null)
        {
            if (!IsCurrentDialogueSource(owner, source, generation) || (finalContact != null && !finalContact())) return false;
            // DialogueService already owns its input lease: closing the Memo cannot briefly restore input.
            host.Interaction?.ForceCloseOverlays();
            return IsCurrentDialogueSource(owner, source, generation) && (finalContact == null || finalContact());
        }
        public void CancelDialogue() { if (!disposed) Dialogue?.Cancel(); }
        public void SetGameplayVisible(bool value)
        {
            ThrowIfDisposed();
            if (visibilityInitialized && GameplayVisible == value) return;
            GameplayVisible = value; visibilityInitialized = true;
            GameplayVisibilityChanged?.Invoke(value);
            // UI owns mount activation; rebind only after it has become active again.
            // DialogueView.OnDisable cancels and unbinds when opening/ending hides the mount.
            if (value) dialogueView.Bind(dialogueModel);
        }
        private bool CanAcceptWorldRequest => !disposed && Session.IsActive && Player.HasActor &&
            !Player.Snapshot.IsDead && flow.Snapshot.State == FlowState.Playing && !flow.Snapshot.IsBusy;
        private void OnActorDying()
        {
            var errors = new List<Exception>();
            // Options owns its three pause leases. Release them through that owner before
            // common death waits for presentation completion; unrelated pause owners remain.
            SessionCleanup.Attempt(errors, options.Close);
            SessionCleanup.Attempt(errors, CancelDialogue);
            SessionCleanup.Attempt(errors, () => host.Interaction?.ForceCloseOverlays());
            SessionCleanup.Attempt(errors, () => host.Interaction?.CancelOperations());
            SessionCleanup.ThrowIfAny(errors, "Death presentation cancellation failed.");
        }
        private void CancelWorldOperations()
        {
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, CancelDialogue);
            SessionCleanup.Attempt(errors, () => host.Interaction?.ForceCloseOverlays());
            SessionCleanup.Attempt(errors, () => host.Interaction?.CancelOperations());
            SessionCleanup.ThrowIfAny(errors, "World operation cancellation failed.");
        }
        private bool RequestDeathRetry(PlayerFacade source, long generation)
        {
            if (disposed || !Session.IsActive || !Player.IsCurrentActor(source, generation) || !source.IsDead ||
                flow.Snapshot.State != FlowState.Playing || flow.Snapshot.IsBusy) return false;
            return flow.Request(new FlowRequest(FlowCommand.RetryCheckpoint));
        }
        public bool TryCaptureCheckpoint(string stageId, CheckpointMarker marker, PlayerFacade source)
        {
            if (marker == null || !CanCapture(stageId, source) || !ActiveStage.ContainsCheckpoint(marker) ||
                ReferenceEquals(lastContactMarker, marker)) return false;
            return Capture(source, marker);
        }
        public bool TryCaptureBeforeChase(StageService owner, PlayerFacade source)
        {
            if (owner == null || !CanCapture(owner.StageId, source) || !ReferenceEquals(ActiveStage, owner)) return false;
            return Capture(source, null);
        }
        private bool CanCapture(string stageId, PlayerFacade source) => !capturingCheckpoint && CanAcceptWorldRequest &&
            stageId == ActiveStageId && source != null && Player.IsCurrentActor(source, Player.ActorGeneration) &&
            host.ActorRoot != null && (source.transform == host.ActorRoot.transform || source.transform.IsChildOf(host.ActorRoot.transform));
        private bool Capture(PlayerFacade source, CheckpointMarker marker)
        {
            capturingCheckpoint = true;
            try
            {
                Session.CaptureCheckpoint(source.transform.position, Player.Snapshot.Fragments);
                if (marker != null) lastContactMarker = marker;
                var observers = CheckpointCaptured;
                if (observers != null)
                    foreach (Action observer in observers.GetInvocationList())
                        try { observer(); }
                        catch (Exception error) { Debug.LogException(new InvalidOperationException("완료된 체크포인트 저장의 관찰자 통지에 실패했습니다. 저장은 유지됩니다.", error), host); }
                return true;
            }
            catch (ArgumentException error)
            {
                Debug.LogException(new InvalidOperationException("현재 Actor의 체크포인트 저장을 거절했습니다. 이전 저장을 유지합니다.", error), host);
                return false;
            }
            finally { capturingCheckpoint = false; }
        }

        public void Dispose()
        {
            if (disposed) return;
            var errors = new List<Exception>();
            // Cancellation runs while the owned dialogue and interaction still accept forced release.
            SessionCleanup.Attempt(errors, CancelWorldOperations);
            disposed = true;
            SessionCleanup.Attempt(errors, NotifyActorRemoving);
            SessionCleanup.Attempt(errors, () => { if (Player != null) Player.Dying -= OnActorDying; });
            SessionCleanup.Attempt(errors, () => { if (dialogueView != null) dialogueView.Unbind(); });
            SessionCleanup.Attempt(errors, () => dialogueModel?.Dispose());
            SessionCleanup.Attempt(errors, () => { if (host != null) host.ReleaseSession(); });
            SessionCleanup.Attempt(errors, () => { if (Session != null && Session.IsActive) Session.End(); });
            SessionCleanup.Attempt(errors, () => scope?.Dispose());
            Stages.Clear();
            ActiveStageId = null; lastContactMarker = null;
            prepareStage = null; actorViewReady = null;
            ActorReady = null; ActorRemoving = null; CheckpointCaptured = null; GameplayVisibilityChanged = null;
            SessionCleanup.ThrowIfAny(errors, "Core session cleanup failed.");
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(GameSessionRuntime)); }
    }
}
