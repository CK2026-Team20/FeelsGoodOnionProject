using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cooked.Contracts;
using Cooked.Dialogue;
using Cooked.Foundation;
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
        private readonly List<IDisposable> eventSubscriptions = new List<IDisposable>();
        private bool disposed, visibilityInitialized;
        private bool actorBindingsReady;
        private Func<bool> actorViewReady;
        public void BindActorViewReadiness(Func<bool> ready)
        { ThrowIfDisposed(); actorViewReady = ready ?? throw new ArgumentNullException(nameof(ready)); }
        private string lastContactCheckpointId;
        private Func<string, CancellationToken, OperationResult, IEnumerator> prepareStage;
        private Action<string, IReadOnlyList<AbilityId>> resetAbilityNotices;
        private Func<string, (string StartId, string EndId)> checkpointPair;
        // T9 supplies the authored start/end pair. These IDs are contact configuration, never saved data.
        public void BindCheckpointPair(Func<string, (string StartId, string EndId)> resolve)
        { ThrowIfDisposed(); checkpointPair = resolve ?? throw new ArgumentNullException(nameof(resolve)); }
        public string ActiveStageId { get; private set; }
        // Authoring configuration only. Never used to restore a retry position.
        public string InitialCheckpointId { get; private set; }
        public IStageService ActiveStage => Stages.Require(ActiveStageId);
        // T9 transitions release bindings/Actor first, select one loaded Stage, then prepare/bind.
        // This is execution configuration; the saved Position/Fragments are never augmented.
        public void SelectActiveStage(string stageId)
        {
            ThrowIfDisposed();
            if (!Session.IsActive || Player.HasActor) throw new InvalidOperationException("Stage selection requires an active session with Actor references released.");
            var stage = Stages.Require(stageId);
            ActiveStageId = stageId;
            InitialCheckpointId = stage.InitialCheckpointId;
            lastContactCheckpointId = null;
        }
        public void BindStageLifecycle(Func<string, CancellationToken, OperationResult, IEnumerator> prepare,
            Action<string, IReadOnlyList<AbilityId>> resetNotices)
        {
            ThrowIfDisposed();
            prepareStage = prepare ?? throw new ArgumentNullException(nameof(prepare));
            resetAbilityNotices = resetNotices ?? throw new ArgumentNullException(nameof(resetNotices));
        }
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
            if (resetAbilityNotices == null) { result.Fail("T6/T9 ability notice reset callback is not connected."); yield break; }
            var relocked = Session.ResetStageUnlocks(ActiveStageId);
            resetAbilityNotices(ActiveStageId, relocked);
            lastContactCheckpointId = null;
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
        public event Action<bool> GameplayVisibilityChanged;

        public GameSessionRuntime(ServiceLocator app, SceneCatalog catalog, SessionActorHost host,
            DialogueView dialogueView, ICinematicService cinematics,
            IntegrationEntrySettings entries)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            this.host = host != null ? host : throw new ArgumentNullException(nameof(host));
            this.dialogueView = dialogueView != null ? dialogueView : throw new ArgumentNullException(nameof(dialogueView));
            this.entries = entries != null ? entries : throw new ArgumentNullException(nameof(entries));
            Cinematics = cinematics ?? throw new ArgumentNullException(nameof(cinematics));
            Stages = new LoadedStageRegistry(catalog);
            flow = app.Get<IGameFlowService>();
            options = app.Get<OptionsService>();
            var control = app.Get<IGameplayControlService>();
            var bus = app.Get<IGameEventBus>();
            scope = new ServiceLocator(app);
            try
            {
                Session = new GameSessionService();
                scope.Register<IGameSessionService>(Session);
                Player = new PlayerBridgeService(control, Session, bus);
                scope.Register<IPlayerBridgeService>(Player);
                Player.Dying += OnActorDying;
                Dialogue = new DialogueService(app.Get<IDialogueTableService>(), control, new DOTweenDialogueDelayScheduler(), new DialogueSettings(.3f, 3f));
                scope.Register<IDialogueService>(Dialogue);
                scope.Register<ICinematicService>(Cinematics);
                dialogueModel = new DialogueViewModel(Dialogue);
                dialogueView.Bind(dialogueModel);
                host.Initialize(Player, control);
                Player.SetEnemyHurtColliderResolver(enemy => enemy.HurtCollider);
                // CheckpointReachedEvent has no Actor attribution. T9 migrates actual contacts
                // to TryCaptureCheckpoint(stageId, checkpointId, source); anonymous bus capture is rejected.
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
            if (!stage.TryGetCheckpointPose(entry.CheckpointId, out Pose initialPose))
                throw new InvalidOperationException("Missing initial entry marker: " + entryStageId + "/" + entry.CheckpointId);
            ActiveStageId = entryStageId;
            InitialCheckpointId = entry.CheckpointId;
            lastContactCheckpointId = null;
            Session.Begin(new CheckpointSnapshot(initialPose.position, entry.Fragments));
            Dialogue.ResetRound();
            foreach (var ability in entry.Abilities) Session.Unlock(ability, Stages.ResolvePresetOrigin(entryStageId, ability));
        }
        public void SpawnActorAtCheckpoint()
        {
            ThrowIfDisposed();
            if (!Session.IsActive) throw new InvalidOperationException("Session is inactive.");
            var saved = Session.Checkpoint;
            Stages.Require(ActiveStageId);
            // Rotation is fresh authoring, not a third checkpoint value. Retry never re-queries a marker.
            var pose = new Pose(saved.Position, Quaternion.identity);
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
            Stages.Require(stageId);
            return Dialogue.TryStart(code);
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
        public bool TryCaptureCheckpoint(string stageId, string checkpointId, PlayerFacade source)
        {
            if (!CanAcceptWorldRequest || stageId != ActiveStageId || source == null || source != Player.Actor ||
                host.ActorRoot == null || !source.transform.IsChildOf(host.ActorRoot.transform)) return false;
            if (checkpointPair == null) throw new InvalidOperationException("T9 start/end checkpoint contact policy is not connected.");
            var pair = checkpointPair(stageId);
            var stage = Stages.Require(stageId);
            if (string.IsNullOrWhiteSpace(pair.StartId) || string.IsNullOrWhiteSpace(pair.EndId) || pair.StartId == pair.EndId ||
                !stage.TryGetCheckpointPose(pair.StartId, out _) || !stage.TryGetCheckpointPose(pair.EndId, out _))
                throw new InvalidOperationException("Stage must author distinct registered start/end checkpoints.");
            if (checkpointId != pair.StartId && checkpointId != pair.EndId) return false;
            if (!stage.TryGetCheckpointPose(checkpointId, out _))
                throw new InvalidOperationException("Checkpoint contact references an unknown checkpoint.");
            if (lastContactCheckpointId == checkpointId) return false;
            Session.CaptureCheckpoint(source.transform.position, Player.Snapshot.Fragments);
            lastContactCheckpointId = checkpointId;
            return true;
        }

        public void Dispose()
        {
            if (disposed) return;
            var errors = new List<Exception>();
            // Cancellation runs while the owned dialogue and interaction still accept forced release.
            SessionCleanup.Attempt(errors, CancelWorldOperations);
            disposed = true;
            foreach (var subscription in eventSubscriptions) SessionCleanup.Attempt(errors, subscription.Dispose);
            eventSubscriptions.Clear();
            SessionCleanup.Attempt(errors, NotifyActorRemoving);
            SessionCleanup.Attempt(errors, () => { if (Player != null) Player.Dying -= OnActorDying; });
            SessionCleanup.Attempt(errors, () => { if (dialogueView != null) dialogueView.Unbind(); });
            SessionCleanup.Attempt(errors, () => dialogueModel?.Dispose());
            SessionCleanup.Attempt(errors, () => { if (host != null) host.ReleaseSession(); });
            SessionCleanup.Attempt(errors, () => { if (Session != null && Session.IsActive) Session.End(); });
            SessionCleanup.Attempt(errors, () => scope?.Dispose());
            Stages.Clear();
            ActiveStageId = null; InitialCheckpointId = null; lastContactCheckpointId = null;
            prepareStage = null; resetAbilityNotices = null; checkpointPair = null; actorViewReady = null;
            ActorReady = null; ActorRemoving = null; GameplayVisibilityChanged = null;
            SessionCleanup.ThrowIfAny(errors, "Core session cleanup failed.");
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(GameSessionRuntime)); }
    }
}
