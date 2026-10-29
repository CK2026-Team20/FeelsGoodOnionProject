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
        private CancellationTokenSource current;
        private CancellationTokenSource cinematicCancellation;
        private bool cinematicReturnRequested;
        private OwnedTransition execution;
        private IDisposable inputLease, worldLease;
        private bool disposed, booted, sessionInitialized;
        private long revision;
        private string selectedStagePath;
        public FlowSnapshot Snapshot { get; private set; } = new FlowSnapshot(FlowState.Booting, false, 0, null);
        public event Action<FlowSnapshot> Changed;
        public string ActiveStageId => runtime.ActiveStageId;

        public GameFlowService(SceneCatalog catalog, ISceneService scenes, IFadeService fade,
            IGameplayControlService control, IGameRuntimeService runtime,
            Action<IEnumerator> schedule, Action<string> log = null)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            this.fade = fade ?? throw new ArgumentNullException(nameof(fade));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            this.schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            this.log = log ?? (_ => { });
        }
        public bool Boot(BootRoute route)
        {
            if (disposed || booted || Snapshot.IsBusy) return false;
            booted = true;
            return Start(FlowState.Booting, token => BootSequence(route, token));
        }
        public bool Request(FlowRequest request)
        {
            if (disposed || !booted) return false;
            // Only a cinematic's title request may interrupt a busy transition. The same
            // transition owns cancellation, teardown and leases; no second coroutine races it.
            if (request.Command == FlowCommand.ReturnToTitle &&
                (Snapshot.State == FlowState.Opening || Snapshot.State == FlowState.Ending))
            {
                if (cinematicReturnRequested || cinematicCancellation == null) return false;
                cinematicReturnRequested = true;
                cinematicCancellation.Cancel();
                Publish(FlowState.ReturningToTitle, true, null);
                return true;
            }
            if (Snapshot.IsBusy) return false;
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
            var cancellation = new CancellationTokenSource();
            current = cancellation;
            long startedRevision = ++revision;
            // Synchronous contract: options VM releases its presentation/world/input leases here,
            // before StartCoroutine can execute even the first nested operation.
            Publish(state, true, null);
            try
            {
                if (disposed) return false;
                if (inputLease == null)
                {
                    var acquired = control.BlockGameplay("GameFlow");
                    if (disposed) { acquired.Dispose(); return false; }
                    inputLease = acquired;
                }
                if (worldLease == null)
                {
                    var acquired = control.PauseWorld("GameFlow");
                    if (disposed) { acquired.Dispose(); return false; }
                    worldLease = acquired;
                }
                var completion = new OperationResult();
                execution = new OwnedTransition(CoroutineOperationRunner.Run(
                    Transition(sequence, cancellation.Token, cancellation, startedRevision, completion), completion,
                    error => log($"Flow execution failed: {error}")));
                schedule(execution);
                return true;
            }
            catch (Exception e)
            {
                log(e.ToString());
                // 동기 스케줄러/관찰자가 이미 종료하거나 다음 전환을 시작했다면 그 소유권을 건드리지 않습니다.
                if (disposed || revision != startedRevision) return false;
                var abandoned = execution; execution = null;
                try { abandoned?.Dispose(); } catch (Exception cleanupError) { log(cleanupError.ToString()); }
                ReleaseCancellation(cancellation, startedRevision);
                try { fade.SetCoveredImmediate(); } catch (Exception overlayError) { log(overlayError.ToString()); }
                Publish(FlowState.Faulted, false, e.ToString());
                return false;
            }
        }
        private IEnumerator Transition(Func<CancellationToken, IEnumerator> sequence, CancellationToken token,
            CancellationTokenSource cancellation, long startedRevision, OperationResult completion)
        {
            var result = new OperationResult();
            IEnumerator guarded = CompleteSequence(sequence, token, result);
            yield return CoroutineOperationRunner.Run(guarded, result, e => log($"Flow revision {revision}: {e}"));
            if (disposed) { completion.Cancel(); yield break; }
            if (cinematicReturnRequested)
            {
                cinematicReturnRequested = false;
                // A real playback/cleanup failure must still use recovery. An accepted
                // title request ends the old sequence successfully before entering here.
                if (result.Status == OperationStatus.Succeeded)
                {
                    result = new OperationResult();
                    yield return CoroutineOperationRunner.Run(CompleteSequence(ReturnToTitle, token, result),
                        result, e => log($"Cinematic title return revision {revision}: {e}"));
                    if (disposed) { completion.Cancel(); yield break; }
                }
            }
            if (result.Status != OperationStatus.Succeeded)
            {
                string reason = result.Error ?? "Transition cancelled.";
                Publish(FlowState.Recovering, true, reason);
                var recovery = new OperationResult();
                yield return CoroutineOperationRunner.Run(Recover(recovery), recovery, e => log($"Recovery revision {revision}: {e}"));
                if (disposed) { completion.Cancel(); yield break; }
                if (recovery.Status != OperationStatus.Succeeded)
                {
                    ReleaseCancellation(cancellation, startedRevision);
                    Publish(FlowState.Faulted, false, reason + "\n" + recovery.Error);
                    completion.Fail(reason + "\n" + recovery.Error);
                    yield break; // retain lock leases until a successful title recovery
                }
            }
            try { ReleaseTransitionLeases(); }
            catch (Exception error)
            {
                ReleaseCancellation(cancellation, startedRevision);
                Publish(FlowState.Faulted, false, error.ToString());
                throw;
            }
            finally { ReleaseCancellation(cancellation, startedRevision); }
            if (disposed) { completion.Cancel(); yield break; }
            Publish(Snapshot.State, false, Snapshot.Error);
            completion.Succeed();
        }
        private void ReleaseCancellation(CancellationTokenSource owned, long ownedRevision)
        {
            // Faulted 통지가 새 전환을 시작해도 이전 전환은 자기 CTS만 해제합니다.
            if (revision == ownedRevision && ReferenceEquals(current, owned)) current = null;
            owned.Dispose();
        }
        private IEnumerator CompleteSequence(Func<CancellationToken, IEnumerator> sequence, CancellationToken token, OperationResult result)
        {
            token.ThrowIfCancellationRequested();
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
            foreach (var path in scenes.LoadedGameplayScenes)
                if (path != catalog.CorePath && !IsStage(Path.GetFileNameWithoutExtension(path)))
                    yield return Step(r => scenes.Unload(path, token, r), token);
            if (!scenes.IsLoaded(catalog.CorePath)) yield return Step(r => scenes.LoadAdditive(catalog.CorePath, token, r), token);
            scenes.SetRootsActive(catalog.CorePath, true);
            scenes.SetActive(catalog.CorePath); // BEFORE dynamic objects / session initialization
            yield return LoadStages(token); // Session.Begin uses the initial checkpoint trigger's authored Pose.
            // Mark before initialization so even partially initialized sessions get teardown on error.
            sessionInitialized = true;
            yield return Step(r => runtime.InitializeSession(stageId, token, r), token);
            runtime.SetGameplayVisible(false);
            yield return Step(r => runtime.SpawnActorAtCheckpoint(token, r), token);
            if (opening)
            {
                yield return Step(r => runtime.Cinematics.Prepare(CinematicId.Opening, token, r), token);
                yield return Step(r => fade.Reveal(token, r), token);
                yield return PlayCinematic(FlowState.Opening, token);
                if (cinematicReturnRequested) yield break;
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
            scenes.SetActive(catalog.CorePath);
            yield return Step(r => runtime.PrepareCurrentStageForRetry(token, r), token);
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
            yield return PlayCinematic(FlowState.Ending, token);
            if (cinematicReturnRequested) yield break;
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
        private IEnumerator PlayCinematic(FlowState state, CancellationToken token)
        {
            // Create the cancellation owner before publishing Opening/Ending: an observer
            // can request title synchronously, even before Play's first MoveNext.
            using (var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                cinematicCancellation = cancellation;
                try
                {
                    Publish(state, true, null);
                    token.ThrowIfCancellationRequested();
                    var result = new OperationResult();
                    Exception failure = null;
                    yield return CoroutineOperationRunner.Run(
                        CreateOperation(r => runtime.Cinematics.Play(cancellation.Token, r), result),
                        result, e => failure = e);
                    token.ThrowIfCancellationRequested();
                    // CinematicService normally returns Cancelled without throwing. Also
                    // accept an exception carrying this exact, requested playback token;
                    // unrelated cancellation and cleanup exceptions still require recovery.
                    bool requestedPlaybackCancellation = cinematicReturnRequested && cancellation.IsCancellationRequested &&
                        failure is OperationCanceledException cancelled && cancelled.CancellationToken == cancellation.Token;
                    if (failure != null && !requestedPlaybackCancellation) throw failure;
                    if (result.Status == OperationStatus.Failed) throw new InvalidOperationException(result.Error);
                    // The last cut can complete just before the option command. The request
                    // still wins until this coordinator has left the cinematic flow state.
                    if (cinematicReturnRequested)
                    {
                        runtime.Cinematics.Hide();
                        yield break;
                    }
                    if (result.Status != OperationStatus.Succeeded) throw new OperationCanceledException(token);
                    CheckCinematicOutcome();
                }
                finally
                {
                    if (ReferenceEquals(cinematicCancellation, cancellation)) cinematicCancellation = null;
                }
            }
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
            yield return UnloadGameplayScenes(token);
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
            if (errors.Count == 0)
            {
                var unload = new OperationResult();
                yield return SafeOperation(r => UnloadGameplayScenes(CancellationToken.None, r), unload);
                if (unload.Status != OperationStatus.Succeeded) errors.Add(unload.Error ?? "Game scene teardown failed.");
            }
            if (errors.Count > 0) { recovery.Fail(string.Join("\n", errors)); yield break; }
            yield return ShowTitle(CancellationToken.None);
            recovery.Succeed();
        }
        private IEnumerator LoadStages(CancellationToken token)
        {
            if (selectedStagePath == null) throw new InvalidOperationException("No selected stage.");
            foreach (string path in catalog.StagePaths)
            {
                if (!scenes.IsLoaded(path)) yield return Step(r => scenes.LoadAdditive(path, token, r), token);
                // PrepareStage resets inactive owners before CoreWorldBinding enables the selected Stage.
                scenes.SetRootsActive(path, false);
            }
        }
        private IEnumerator UnloadGameplayScenes(CancellationToken token, OperationResult result = null)
        {
            // 종료 시작 시의 실제 로드 목록이다. 등록 해제된 보존 씬과 부분 로드도 포함한다.
            var loaded = scenes.LoadedGameplayScenes;
            foreach (string path in loaded)
                if (path != catalog.CorePath) yield return Step(r => scenes.Unload(path, token, r), token);
            if (scenes.IsLoaded(catalog.CorePath)) yield return Step(r => scenes.Unload(catalog.CorePath, token, r), token);
            if (scenes.LoadedGameplayScenes.Count != 0) throw new InvalidOperationException("타이틀 복귀 전에 게임 씬 언로드가 완료되어야 합니다.");
            selectedStagePath = null;
            result?.Succeed();
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
            if (disposed) return;
            Snapshot = new FlowSnapshot(state, busy, revision, error);
            log($"Flow revision {revision}: {state}, busy={busy}" + (error == null ? "" : $", error={error}"));
            var listeners = Changed;
            if (listeners == null) return;
            foreach (Action<FlowSnapshot> listener in listeners.GetInvocationList())
                try { listener(Snapshot); } catch (Exception e) { log($"Flow subscriber failed: {e}"); }
        }
        public void CancelCurrent() { if (!disposed) current?.Cancel(); }
        private static void Attempt(List<Exception> errors, Action action)
        { try { action(); } catch (Exception error) { errors.Add(error); } }
        private void ReleaseTransitionLeases()
        {
            // 통지가 재진입하거나 실패해도 이미 반납한 토큰을 다시 소유하지 않습니다.
            var world = worldLease; worldLease = null;
            var input = inputLease; inputLease = null;
            var errors = new List<Exception>();
            Attempt(errors, () => world?.Dispose());
            Attempt(errors, () => input?.Dispose());
            if (errors.Count != 0) throw new AggregateException("전환 차단 해제 중 오류가 발생했습니다.", errors);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var cancellation = current; current = null;
            var running = execution; execution = null;
            Snapshot = new FlowSnapshot(FlowState.Disposed, false, revision, null);
            Changed = null;
            var errors = new List<Exception>();
            Attempt(errors, () => cancellation?.Cancel());
            // Unity의 StopCoroutine이 중첩 IEnumerator를 Dispose한다고 가정하지 않습니다.
            Attempt(errors, () => running?.Dispose());
            Attempt(errors, () => cancellation?.Dispose());
            Attempt(errors, ReleaseTransitionLeases);
            if (errors.Count != 0) throw new AggregateException("앱 전환 종료 중 오류가 발생했습니다.", errors);
        }
        /// <summary>단일 전환의 평탄화된 코루틴을 소유합니다. 실행 중 취소 재진입은 MoveNext 종료 뒤 해제합니다.</summary>
        private sealed class OwnedTransition : IEnumerator, IDisposable
        {
            private IEnumerator routine;
            private bool advancing, stopping;
            public OwnedTransition(IEnumerator routine) { this.routine = routine; }
            public object Current => routine?.Current;
            public bool MoveNext()
            {
                if (routine == null || stopping) return false;
                advancing = true;
                try
                {
                    bool next = routine.MoveNext();
                    if (!next) stopping = true;
                    return next && !stopping;
                }
                finally
                {
                    advancing = false;
                    if (stopping) Dispose();
                }
            }
            public void Reset() => throw new NotSupportedException();
            public void Dispose()
            {
                stopping = true;
                if (advancing) return;
                var owned = routine; routine = null;
                (owned as IDisposable)?.Dispose();
            }
        }
    }
}
