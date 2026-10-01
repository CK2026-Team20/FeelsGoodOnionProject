using System;
using System.Collections;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Dialogue;
using Cooked.Foundation;
using Cooked.Session;
using UnityEngine;

namespace Cooked.Integration
{
    /// <summary>One Core's composition lifetime. Retry preserves this object; title return disposes it.</summary>
    public sealed class GameSessionRuntime : IDisposable
    {
        private readonly ServiceLocator scope;
        private readonly SessionActorHost host;
        private readonly DialogueDriver dialogueDriver;
        private readonly DialogueView dialogueView;
        private readonly DialogueViewModel dialogueModel;
        private readonly IntegrationEntrySettings entries;
        private readonly IGameFlowService flow;
        private readonly List<IDisposable> eventSubscriptions = new List<IDisposable>();
        private bool disposed, visibilityInitialized;
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
            DialogueDriver dialogueDriver, DialogueView dialogueView, ICinematicService cinematics,
            IntegrationEntrySettings entries)
        {
            if (app == null) throw new ArgumentNullException(nameof(app));
            this.host = host != null ? host : throw new ArgumentNullException(nameof(host));
            this.dialogueDriver = dialogueDriver != null ? dialogueDriver : throw new ArgumentNullException(nameof(dialogueDriver));
            this.dialogueView = dialogueView != null ? dialogueView : throw new ArgumentNullException(nameof(dialogueView));
            this.entries = entries != null ? entries : throw new ArgumentNullException(nameof(entries));
            Cinematics = cinematics ?? throw new ArgumentNullException(nameof(cinematics));
            Stages = new LoadedStageRegistry(catalog);
            flow = app.Get<IGameFlowService>();
            var control = app.Get<IGameplayControlService>();
            var bus = app.Get<IGameEventBus>();
            scope = new ServiceLocator(app);
            try
            {
                Session = new GameSessionService();
                scope.Register<IGameSessionService>(Session);
                Player = new PlayerBridgeService(control, Session, bus);
                scope.Register<IPlayerBridgeService>(Player);
                var transients = new SessionTransientService();
                scope.Register<SessionTransientService>(transients);
                Dialogue = new DialogueService(app.Get<IDialogueTableService>(), control, new DOTweenDialogueDelayScheduler(), new DialogueSettings(.3f, 3f));
                scope.Register<IDialogueService>(Dialogue);
                scope.Register<ICinematicService>(Cinematics);
                dialogueModel = new DialogueViewModel(Dialogue);
                dialogueDriver.Initialize(Dialogue);
                dialogueView.Bind(dialogueModel);
                host.Initialize(Player, control, transients);
                eventSubscriptions.Add(bus.Subscribe<CheckpointReachedEvent>(CaptureCheckpoint));
                eventSubscriptions.Add(bus.Subscribe<AbilityUnlockedEvent>(Unlock));
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
            Session.Begin(entry.Checkpoint);
            foreach (var ability in entry.Abilities) Session.Unlock(ability);
        }
        public void SpawnActorAtCheckpoint()
        {
            ThrowIfDisposed();
            if (!Session.IsActive) throw new InvalidOperationException("Session is inactive.");
            Stages.Refresh();
            var saved = Session.Checkpoint;
            var stage = Stages.Require(saved.StageId);
            if (!stage.TryGetCheckpointPose(saved.CheckpointId, out Pose pose))
                throw new InvalidOperationException("Missing checkpoint " + saved.StageId + "/" + saved.CheckpointId);
            try
            {
                host.Spawn(pose, saved.Fragments);
                Player.SetDepthMovementAllowed(saved.StageId == "03_2_Stage");
                ActorReady?.Invoke();
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
        public IEnumerator DespawnActor()
        {
            if (disposed) yield break;
            CancelDialogue();
            // Subscribers drop borrowed world/Actor references before the host destroys the Actor.
            Exception bindingFailure = null;
            try { NotifyActorRemoving(); }
            catch (Exception error) { bindingFailure = error; }
            yield return host.Despawn();
            Stages.Clear();
            if (bindingFailure != null) throw new InvalidOperationException("World bindings failed to release.", bindingFailure);
        }
        private void NotifyActorRemoving()
        {
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
            if (!CanAcceptWorldRequest || stageId != "03_2_Stage") return false;
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
        private void CaptureCheckpoint(CheckpointReachedEvent message)
        {
            if (!CanAcceptWorldRequest) return;
            var stage = Stages.Require(message.StageId);
            if (!stage.TryGetCheckpointPose(message.CheckpointId, out _))
                throw new InvalidOperationException("Checkpoint event references an unknown checkpoint.");
            Session.CaptureCheckpoint(message.StageId, message.CheckpointId, Player.Snapshot.Fragments);
        }
        private void Unlock(AbilityUnlockedEvent message)
        {
            if (CanAcceptWorldRequest) Session.Unlock(message.Ability);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error); } }
            foreach (var subscription in eventSubscriptions) Attempt(subscription.Dispose);
            eventSubscriptions.Clear();
            Attempt(NotifyActorRemoving);
            Attempt(() => { if (dialogueView != null) dialogueView.Unbind(); });
            Attempt(() => { if (dialogueDriver != null) dialogueDriver.Detach(); });
            Attempt(() => dialogueModel?.Dispose());
            Attempt(() => { if (host != null) host.ReleaseNow(); });
            Attempt(() => { if (Session != null && Session.IsActive) Session.End(); });
            Attempt(() => scope?.Dispose());
            Stages.Clear();
            ActorReady = null; ActorRemoving = null; GameplayVisibilityChanged = null;
            if (errors.Count != 0) throw new AggregateException("Core session cleanup failed.", errors);
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(GameSessionRuntime)); }
    }
}



