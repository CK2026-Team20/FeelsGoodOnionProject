using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cooked.Contracts;
using Cooked.Session;
using UnityEngine;

namespace Cooked.Integration
{
    /// <summary>App lifetime bridge. The factory resolves the newly loaded Core; no Core reference survives shutdown.</summary>
    public sealed class GameRuntimeService : IGameRuntimeService, IDisposable
    {
        private readonly Func<GameSessionRuntime> createLoadedCore;
        private GameSessionRuntime session;
        // A disposed session may still own incomplete Core composition cleanup. Keep it
        // separate from CurrentSession so recovery retries release without despawning it again.
        private GameSessionRuntime pendingRelease;
        private bool disposed;
        private Action<GameSessionRuntime> releaseLoadedCore;
        // T9: CoreComposition must dispose world/UI/cinematics and clear its runtime field,
        // without destroying or unloading the Core scene. No implicit no-op fallback.
        public void BindSessionRelease(Action<GameSessionRuntime> release)
        { ThrowIfDisposed(); releaseLoadedCore = release ?? throw new ArgumentNullException(nameof(release)); }
        public GameRuntimeService(Func<GameSessionRuntime> createLoadedCore)
        { this.createLoadedCore = createLoadedCore ?? throw new ArgumentNullException(nameof(createLoadedCore)); }
        public ICinematicService Cinematics => RequireSession().Cinematics;
        public GameSessionRuntime CurrentSession => session;
        public string ActiveStageId => session?.ActiveStageId;
        public void SetGameplayVisible(bool visible) => RequireSession().SetGameplayVisible(visible);
        public void CancelDialogue() { if (!disposed) session?.CancelDialogue(); }
        public IEnumerator InitializeSession(string entryStageId, CancellationToken token, OperationResult result)
        {
            ThrowIfDisposed(); token.ThrowIfCancellationRequested();
            if (session != null || pendingRelease != null) throw new InvalidOperationException("Previous session must be shut down first.");
            // Assign before Begin: failed partial initialization remains reachable for Foundation recovery.
            session = createLoadedCore() ?? throw new InvalidOperationException("Loaded Core did not provide a session composition.");
            var preparation = new OperationResult();
            yield return session.PrepareInitialStages(token, preparation);
            if (preparation.Status != OperationStatus.Succeeded)
            {
                if (preparation.Status == OperationStatus.Cancelled) result.Cancel();
                else result.Fail(preparation.Error ?? "Initial Stage preparation failed.");
                yield break;
            }
            session.Begin(entryStageId);
            result.Succeed();
            yield break;
        }
        public IEnumerator SpawnActorAtCheckpoint(CancellationToken token, OperationResult result)
        {
            token.ThrowIfCancellationRequested();
            var current = RequireSession();
            current.SpawnActorAtCheckpoint();
            float deadline = Time.realtimeSinceStartup + 10;
            while (!current.ActorHost.ActorModesReady)
            {
                token.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("HMS Actor initialization timed out.");
                yield return null;
            }
            token.ThrowIfCancellationRequested();
            current.CompleteActorBindings();
            while (!current.ActorViewReady)
            {
                token.ThrowIfCancellationRequested();
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Initial Cinemachine view did not become ready.");
                yield return null;
            }
            token.ThrowIfCancellationRequested();
            result.Succeed();
        }
        public IEnumerator PrepareCurrentStageForRetry(CancellationToken token, OperationResult result)
        {
            token.ThrowIfCancellationRequested();
            yield return RequireSession().PrepareCurrentStageForRetry(token, result);
        }
        public IEnumerator DespawnActor(CancellationToken token, OperationResult result)
        {
            token.ThrowIfCancellationRequested();
            yield return RequireSession().DespawnActor();
            token.ThrowIfCancellationRequested();
            result.Succeed();
        }
        public IEnumerator ShutdownSession(CancellationToken token, OperationResult result)
        {
            ThrowIfDisposed();
            // Cleanup is drain-to-completion even when the incoming flow was cancelled.
            if (session != null)
            {
                var old = session;
                // The outer lifetime is protected inside this iterator. Foundation unwinds it on
                // nested MoveNext/Dispose failure, so cleanup does not depend on normal resumption.
                yield return SessionShutdownOperation.Run(old.DespawnActor, old.Dispose,
                    () => ReleaseSession(old), result);
                yield break;
            }
            if (pendingRelease != null) ReleaseSession(pendingRelease);
            result.Succeed();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var old = session;
            var errors = new List<Exception>();
            try { old?.Dispose(); }
            catch (Exception error) { errors.Add(error); }
            finally
            {
                SessionCleanup.Attempt(errors, () => ReleaseSession(old ?? pendingRelease));
            }
            if (errors.Count != 0) throw new AggregateException("Game runtime disposal failed.", errors);
        }
        private void ReleaseSession(GameSessionRuntime old)
        {
            if (old == null) return;
            pendingRelease = old;
            var errors = new List<Exception>();
            try
            {
                if (releaseLoadedCore == null) errors.Add(new InvalidOperationException("T9 CoreComposition session release callback is not connected."));
                else SessionCleanup.Attempt(errors, () =>
                {
                    releaseLoadedCore(old);
                    pendingRelease = null; // Only confirmed Core cleanup permits Title/another round.
                });
            }
            finally
            {
                if (ReferenceEquals(session, old))
                {
                    session = null;
                }
            }
            SessionCleanup.ThrowIfAny(errors, "Core composition release failed.");
        }
        private GameSessionRuntime RequireSession()
        {
            ThrowIfDisposed();
            return session ?? throw new InvalidOperationException("Game session has not been initialized.");
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(GameRuntimeService)); }
    }
}
