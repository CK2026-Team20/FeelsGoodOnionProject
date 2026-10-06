using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cooked.Contracts;
namespace Cooked.Foundation
{
    /// <summary>One app-lifetime transition coordinator. It never caches session-owned cinematics.</summary>
    public sealed class GameFlowService : IGameFlowService
    {
        private readonly SceneCatalog catalog;
        private readonly ISceneService scenes;
        private readonly IFadeService fade;
        private readonly IGameplayControlService control;
        private readonly IGameRuntimeService runtime;
        private readonly Action<IEnumerator> schedule;
        private readonly Action<string> log;
        private readonly IDisposable requestSubscription;
        private CancellationTokenSource current;
        private IDisposable inputLease, worldLease;
        private bool disposed, booted, sessionInitialized;
        private long revision;
        private string selectedStagePath;
        public FlowSnapshot Snapshot { get; private set; } = new FlowSnapshot(FlowState.Booting, false, 0, null);
        public event Action<FlowSnapshot> Changed;

        public GameFlowService(SceneCatalog catalog, ISceneService scenes, IFadeService fade,
            IGameplayControlService control, IGameRuntimeService runtime,
            IGameEventBus eventBus, Action<IEnumerator> schedule, Action<string> log = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            this.fade = fade ?? throw new ArgumentNullException(nameof(fade));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            this.log = log ?? (_ => { });
            requestSubscription = (eventBus ?? throw new ArgumentNullException(nameof(eventBus))).Subscribe<FlowRequestedEvent>(e => Request(e.Request));
        }
        public bool Boot(BootRoute route)
        {
            if (disposed || booted || Snapshot.IsBusy) return false;
            booted = true;
            return Start(FlowState.Booting, token => BootSequence(route, token));
        }
        public bool Request(FlowRequest request)
        {
            if (disposed || !booted || Snapshot.IsBusy) return false;
            switch (request.Command)
            {
                case FlowCommand.NewGame:
                    if (Snapshot.State != FlowState.Title) return false;
                    return Start(FlowState.LoadingGame, token => StartGame(null, true, token));
                case FlowCommand.EditorStart:
                    if (Snapshot.State != FlowState.Title) return false;
                    if (!IsStage(request.StageId)) return false;
                    return Start(FlowState.LoadingGame, token => StartGame(request.StageId, false, token));
                case FlowCommand.RetryCheckpoint:
                    if (Snapshot.State != FlowState.Playing) return false;
                    return Start(FlowState.Retrying, Retry);
                case FlowCommand.Escape:
                    if (Snapshot.State != FlowState.Playing) return false;
                    return Start(FlowState.ReturningToTitle, EndGame);
                case FlowCommand.ReturnToTitle:
                    if (Snapshot.State != FlowState.Playing && Snapshot.State != FlowState.Faulted) return false;
                    return Start(FlowState.ReturningToTitle, ReturnToTitle);
                default: return false;
            }
        }
        private bool Start(FlowState state, Func<CancellationToken, IEnumerator> sequence)
        {
            current = new CancellationTokenSource();
            revision++;
            // Synchronous contract: options VM releases its presentation/world/input leases here,
            // before StartCoroutine can execute even the first nested operation.
            Publish(state, true, null);
            try
            {
                inputLease = inputLease ?? control.BlockGameplay("GameFlow");
                worldLease = worldLease ?? control.PauseWorld("GameFlow");
                schedule(Transition(sequence, current.Token));
                return true;
            }
            catch (Exception e)
            {
                log(e.ToString());
                current.Dispose(); current = null;
                try { fade.SetCoveredImmediate(); } catch (Exception overlayError) { log(overlayError.ToString()); }
                Publish(FlowState.Faulted, false, e.ToString());
                return false;
            }
        }
        private IEnumerator Transition(Func<CancellationToken, IEnumerator> sequence, CancellationToken token)
        {
            var result = new OperationResult();
            IEnumerator guarded = CompleteSequence(sequence, token, result);
            yield return CoroutineOperationRunner.Run(guarded, result, e => log($"Flow revision {revision}: {e}"));
            if (disposed) yield break;
            if (result.Status != OperationStatus.Succeeded)
            {
                string reason = result.Error ?? "Transition cancelled.";
                Publish(FlowState.Recovering, true, reason);
                var recovery = new OperationResult();
                yield return CoroutineOperationRunner.Run(Recover(recovery), recovery, e => log($"Recovery revision {revision}: {e}"));
                if (disposed) yield break;
                if (recovery.Status != OperationStatus.Succeeded)
                {
                    Publish(FlowState.Faulted, false, reason + "\n" + recovery.Error);
                    current?.Dispose(); current = null;
                    yield break; // retain lock leases until a successful title recovery
                }
            }
            ReleaseTransitionLeases();
            current?.Dispose(); current = null;
            Publish(Snapshot.State, false, Snapshot.Error);
        }
        private IEnumerator CompleteSequence(Func<CancellationToken, IEnumerator> sequence, CancellationToken token, OperationResult result)
        {
            yield return sequence(token);
            token.ThrowIfCancellationRequested();
            result.Succeed();
        }
        private IEnumerator BootSequence(BootRoute route, CancellationToken token)
        {
            fade.SetCoveredImmediate();
            if (route.DirectGame)
            {
                if (!IsStage(route.StageId)) throw new InvalidOperationException($"Unknown editor entry Stage: {route.StageId}");
                yield return StartGame(route.StageId, false, token, true);
            }
            else
            {
                yield return Step(r => scenes.LoadAdditive(catalog.TitlePath, token, r), token);
                scenes.SetActive(catalog.TitlePath);
                yield return Step(r => fade.Reveal(token, r), token);
                Publish(FlowState.Title, true, null);
            }
        }
        private IEnumerator StartGame(string stageId, bool opening, CancellationToken token, bool alreadyCovered = false)
        {
            stageId = stageId ?? Path.GetFileNameWithoutExtension(catalog.StagePaths[0]);
            if (!IsStage(stageId)) throw new InvalidOperationException($"Unknown entry Stage: {stageId}");
            foreach (string path in catalog.StagePaths)
                if (Path.GetFileNameWithoutExtension(path) == stageId) selectedStagePath = path;
            Publish(FlowState.LoadingGame, true, null);
            if (!alreadyCovered) yield return Step(r => fade.Cover(token, r), token);
            yield return Step(r => scenes.Unload(catalog.TitlePath, token, r), token);
            yield return Step(r => scenes.LoadAdditive(catalog.CorePath, token, r), token);
            scenes.SetActive(catalog.CorePath); // BEFORE dynamic objects / session initialization
            // Mark before initialization so even partially initialized sessions get teardown on error.
            sessionInitialized = true;
            yield return Step(r => runtime.InitializeSession(stageId, token, r), token);
            runtime.SetGameplayVisible(false);
            yield return LoadStages(token);
            yield return Step(r => runtime.SpawnActorAtCheckpoint(token, r), token);
            if (opening)
            {
                yield return Step(r => runtime.Cinematics.Prepare(CinematicId.Opening, token, r), token);
                yield return Step(r => fade.Reveal(token, r), token);
                Publish(FlowState.Opening, true, null);
                yield return Step(r => runtime.Cinematics.Play(token, r), token);
                CheckCinematicOutcome();
                Publish(FlowState.LoadingGame, true, null);
                yield return Step(r => fade.Cover(token, r), token);
                runtime.Cinematics.Hide();
            }
            runtime.SetGameplayVisible(true);
            yield return Step(r => fade.Reveal(token, r), token);
            Publish(FlowState.Playing, true, null);
        }
        private IEnumerator Retry(CancellationToken token)
        {
            yield return Step(r => fade.Cover(token, r), token);
            runtime.CancelDialogue();
            yield return Step(r => runtime.DespawnActor(token, r), token);
            yield return UnloadStages(token);
            // Core remains loaded/active, its UI and game scope survive.
            scenes.SetActive(catalog.CorePath);
            yield return LoadStages(token);
            yield return Step(r => runtime.SpawnActorAtCheckpoint(token, r), token);
            runtime.SetGameplayVisible(true);
            yield return Step(r => fade.Reveal(token, r), token);
            Publish(FlowState.Playing, true, null);
        }
        private IEnumerator EndGame(CancellationToken token)
        {
            runtime.CancelDialogue();
            yield return Step(r => fade.Cover(token, r), token);
            runtime.SetGameplayVisible(false);
            yield return Step(r => runtime.Cinematics.Prepare(CinematicId.Ending, token, r), token);
            yield return Step(r => fade.Reveal(token, r), token);
            Publish(FlowState.Ending, true, null);
            yield return Step(r => runtime.Cinematics.Play(token, r), token);
            CheckCinematicOutcome();
            Publish(FlowState.ReturningToTitle, true, null);
            yield return Step(r => fade.Cover(token, r), token);
            runtime.Cinematics.Hide();
            yield return Teardown(token);
            yield return ShowTitle(token);
        }
        private IEnumerator ReturnToTitle(CancellationToken token)
        {
            yield return Step(r => fade.Cover(token, r), token);
            yield return Teardown(token);
            yield return ShowTitle(token);
        }
        private IEnumerator ShowTitle(CancellationToken token)
        {
            yield return Step(r => scenes.LoadAdditive(catalog.TitlePath, token, r), token);
            scenes.SetActive(catalog.TitlePath);
            yield return Step(r => fade.Reveal(token, r), token);
            Publish(FlowState.Title, true, null);
        }
        private IEnumerator Teardown(CancellationToken token)
        {
            runtime.CancelDialogue();
            if (sessionInitialized)
            {
                yield return Step(r => runtime.ShutdownSession(token, r), token);
                sessionInitialized = false;
            }
            yield return UnloadStages(token);
            yield return Step(r => scenes.Unload(catalog.CorePath, token, r), token);
        }
        private IEnumerator Recover(OperationResult recovery)
        {
            fade.SetCoveredImmediate();
            var errors = new List<string>();
            try { runtime.CancelDialogue(); } catch (Exception e) { errors.Add(e.ToString()); }
            if (sessionInitialized)
            {
                var stop = new OperationResult();
                yield return SafeOperation(r => runtime.ShutdownSession(CancellationToken.None, r), stop);
                if (stop.Status == OperationStatus.Succeeded) sessionInitialized = false;
                else errors.Add(stop.Error ?? "Session teardown cancelled.");
            }
            for (int i = catalog.StagePaths.Count - 1; i >= 0; i--)
            {
                string path = catalog.StagePaths[i];
                var unload = new OperationResult();
                yield return SafeOperation(r => scenes.Unload(path, CancellationToken.None, r), unload);
                if (unload.Status != OperationStatus.Succeeded) errors.Add(unload.Error ?? $"Unload cancelled: {path}");
            }
            var core = new OperationResult();
            yield return SafeOperation(r => scenes.Unload(catalog.CorePath, CancellationToken.None, r), core);
            if (core.Status != OperationStatus.Succeeded) errors.Add(core.Error ?? "Core cleanup cancelled.");
            if (errors.Count > 0) { recovery.Fail(string.Join("\n", errors)); yield break; }
            yield return ShowTitle(CancellationToken.None);
            recovery.Succeed();
        }
        private IEnumerator LoadStages(CancellationToken token)
        {
            if (selectedStagePath == null) throw new InvalidOperationException("No selected stage.");
            yield return Step(r => scenes.LoadAdditive(selectedStagePath, token, r), token);
        }
        private IEnumerator UnloadStages(CancellationToken token)
        {
            for (int i = catalog.StagePaths.Count - 1; i >= 0; i--)
            {
                string path = catalog.StagePaths[i];
                yield return Step(r => scenes.Unload(path, token, r), token);
            }
        }
        private static IEnumerator Step(Func<OperationResult, IEnumerator> operation, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var result = new OperationResult();
            Exception failure = null;
            yield return CoroutineOperationRunner.Run(CreateOperation(operation, result), result, e => failure = e);
            if (failure != null) throw failure;
            if (result.Status == OperationStatus.Cancelled) throw new OperationCanceledException(token);
            if (result.Status != OperationStatus.Succeeded) throw new InvalidOperationException(result.Error);
            token.ThrowIfCancellationRequested();
        }
        private static IEnumerator SafeOperation(Func<OperationResult, IEnumerator> operation, OperationResult result)
        {
            yield return CoroutineOperationRunner.Run(CreateOperation(operation, result), result);
        }
        private static IEnumerator CreateOperation(Func<OperationResult, IEnumerator> operation, OperationResult result)
        {
            yield return operation(result);
        }
        private void CheckCinematicOutcome()
        {
            CinematicOutcome outcome = runtime.Cinematics.Outcome;
            if (outcome == CinematicOutcome.Cancelled) throw new OperationCanceledException("Cinematic cancelled.");
            if (outcome != CinematicOutcome.Completed && outcome != CinematicOutcome.Skipped) throw new InvalidOperationException("Cinematic returned without completion or skip.");
        }
        private bool IsStage(string stageId)
        {
            foreach (string path in catalog.StagePaths) if (Path.GetFileNameWithoutExtension(path) == stageId) return true;
            return false;
        }
        private void Publish(FlowState state, bool busy, string error)
        {
            Snapshot = new FlowSnapshot(state, busy, revision, error);
            log($"Flow revision {revision}: {state}, busy={busy}" + (error == null ? "" : $", error={error}"));
            var listeners = Changed;
            if (listeners == null) return;
            foreach (Action<FlowSnapshot> listener in listeners.GetInvocationList())
                try { listener(Snapshot); } catch (Exception e) { log($"Flow subscriber failed: {e}"); }
        }
        public void CancelCurrent() { if (!disposed) current?.Cancel(); }
        private void ReleaseTransitionLeases()
        {
            worldLease?.Dispose(); worldLease = null;
            inputLease?.Dispose(); inputLease = null;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            current?.Cancel(); current?.Dispose(); current = null;
            requestSubscription.Dispose();
            ReleaseTransitionLeases();
            Snapshot = new FlowSnapshot(FlowState.Disposed, false, revision, null);
            Changed = null;
        }
    }
}

