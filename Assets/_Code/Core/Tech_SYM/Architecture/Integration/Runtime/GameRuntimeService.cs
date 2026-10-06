using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Integration
{
    /// <summary>App lifetime bridge. The factory resolves the newly loaded Core; no Core reference survives shutdown.</summary>
    public sealed class GameRuntimeService : IGameRuntimeService, IDisposable
    {
        private readonly Func<GameSessionRuntime> createLoadedCore;
        private GameSessionRuntime session;
        private bool disposed;
        public GameRuntimeService(Func<GameSessionRuntime> createLoadedCore)
        { this.createLoadedCore = createLoadedCore ?? throw new ArgumentNullException(nameof(createLoadedCore)); }
        public ICinematicService Cinematics => RequireSession().Cinematics;
        public GameSessionRuntime CurrentSession => session;
        public event Action<bool> GameplayOutputChanged;
        public void SetGameplayVisible(bool visible) => RequireSession().SetGameplayVisible(visible);
        public void CancelDialogue() { if (!disposed) session?.CancelDialogue(); }
        public IEnumerator InitializeSession(string entryStageId, CancellationToken token, OperationResult result)
        {
            ThrowIfDisposed(); token.ThrowIfCancellationRequested();
            if (session != null) throw new InvalidOperationException("Previous session must be shut down first.");
            // Assign before Begin: failed partial initialization remains reachable for Foundation recovery.
            session = createLoadedCore() ?? throw new InvalidOperationException("Loaded Core did not provide a session composition.");
            session.GameplayVisibilityChanged += OnGameplayVisibilityChanged;
            session.ActorRemoving += OnActorRemoving;
            session.Begin(entryStageId);
            PublishGameplayOutput();
            result.Succeed();
            yield break;
        }
        public IEnumerator SpawnActorAtCheckpoint(CancellationToken token, OperationResult result)
        {
            token.ThrowIfCancellationRequested();
            var current = RequireSession();
            current.SpawnActorAtCheckpoint();
            float deadline = Time.realtimeSinceStartup + 10;
            while (current.ActorHost.CameraRig != null && !current.ActorHost.CameraRig.IsReady)
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
            current.ActorHost.CameraRig.SetOutputVisible(current.GameplayVisible);
            // Retry retains GameplayVisible=true, so no visibility-change event is required to fire.
            PublishGameplayOutput();
            result.Succeed();
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
                DetachSession(old);
                session = null;
                try { GameplayOutputChanged?.Invoke(false); }
                catch (Exception error) { errors.Add(error); }
                finally { GameplayOutputChanged = null; }
            }
            if (errors.Count != 0) throw new AggregateException("Game runtime disposal failed.", errors);
        }
        private void OnGameplayVisibilityChanged(bool visible)
        {
            // A requested view cannot become actual output before HMS and Brain initialization finish.
            if (visible && session != null && !session.ActorViewReady)
                session.ActorHost.CameraRig?.SetOutputVisible(false);
            PublishGameplayOutput();
        }
        private void OnActorRemoving()
        {
            // Hide the old output before fallback resumes, including the nested despawn iterator gap.
            session?.ActorHost.CameraRig?.SetOutputVisible(false);
            GameplayOutputChanged?.Invoke(false);
        }
        private void PublishGameplayOutput()
            => GameplayOutputChanged?.Invoke(session != null && session.GameplayVisible && session.ActorViewReady);
        private void DetachSession(GameSessionRuntime old)
        {
            if (old == null) return;
            old.GameplayVisibilityChanged -= OnGameplayVisibilityChanged;
            old.ActorRemoving -= OnActorRemoving;
        }
        private void ReleaseSession(GameSessionRuntime old)
        {
            try { DetachSession(old); }
            finally
            {
                if (ReferenceEquals(session, old))
                {
                    session = null;
                    // App subscribers survive Title and the next game; only the old session is detached.
                    GameplayOutputChanged?.Invoke(false);
                }
            }
        }
        private GameSessionRuntime RequireSession()
        {
            ThrowIfDisposed();
            return session ?? throw new InvalidOperationException("Game session has not been initialized.");
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(GameRuntimeService)); }
    }
}

