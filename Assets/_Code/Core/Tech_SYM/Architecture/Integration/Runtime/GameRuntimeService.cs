using System;
using System.Collections;
using System.Threading;
using Cooked.Contracts;

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
        public void SetGameplayVisible(bool visible) => RequireSession().SetGameplayVisible(visible);
        public void CancelDialogue() { if (!disposed) session?.CancelDialogue(); }
        public IEnumerator InitializeSession(string entryStageId, CancellationToken token, OperationResult result)
        {
            ThrowIfDisposed(); token.ThrowIfCancellationRequested();
            if (session != null) throw new InvalidOperationException("Previous session must be shut down first.");
            // Assign before Begin: failed partial initialization remains reachable for Foundation recovery.
            session = createLoadedCore() ?? throw new InvalidOperationException("Loaded Core did not provide a session composition.");
            session.Begin(entryStageId);
            result.Succeed();
            yield break;
        }
        public IEnumerator SpawnActorAtCheckpoint(CancellationToken token, OperationResult result)
        {
            token.ThrowIfCancellationRequested();
            RequireSession().SpawnActorAtCheckpoint();
            result.Succeed();
            yield break;
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
                    () => { if (ReferenceEquals(session, old)) session = null; }, result);
                yield break;
            }
            result.Succeed();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { session?.Dispose(); }
            finally { session = null; }
        }
        private GameSessionRuntime RequireSession()
        {
            ThrowIfDisposed();
            return session ?? throw new InvalidOperationException("Game session has not been initialized.");
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(GameRuntimeService)); }
    }
}

