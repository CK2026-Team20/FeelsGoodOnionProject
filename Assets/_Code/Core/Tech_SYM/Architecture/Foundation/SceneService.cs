using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cooked.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Cooked.Foundation
{
    public sealed class SceneService : ISceneService
    {
        private readonly SceneCatalog catalog;
        private readonly HashSet<string> paths;
        private bool busy, disposed;
        public SceneService(SceneCatalog catalog)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            paths = new HashSet<string>(catalog.StagePaths, StringComparer.Ordinal) { catalog.BootstrapPath, catalog.TitlePath, catalog.CorePath };
        }
        public bool IsLoaded(string scenePath) { Validate(scenePath); return SceneManager.GetSceneByPath(scenePath).isLoaded; }
        public IEnumerator LoadAdditive(string scenePath, CancellationToken token, OperationResult result)
        {
            Validate(scenePath);
            if (busy) { result.Fail("Another scene operation is in progress."); yield break; }
            if (token.IsCancellationRequested) { result.Cancel(); yield break; }
            if (IsLoaded(scenePath)) { result.Succeed(); yield break; }
            busy = true;
            try
            {
                AsyncOperation operation;
#if UNITY_EDITOR
                // Dedicated prototype scenes need not mutate the global EditorBuildSettings list.
                if (SceneUtility.GetBuildIndexByScenePath(scenePath) < 0)
                    operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scenePath, new LoadSceneParameters(LoadSceneMode.Additive));
                else
#endif
                    operation = SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Additive);
                if (operation == null) { result.Fail($"Load returned null: {scenePath}"); yield break; }
                // Do not interrupt Unity's non-cancellable load. A cancelled newly-loaded scene is cleaned up here.
                while (!operation.isDone) yield return null;
                if (token.IsCancellationRequested || disposed)
                {
                    var loaded = SceneManager.GetSceneByPath(scenePath);
                    if (loaded.isLoaded && scenePath != catalog.BootstrapPath)
                    {
                        var cleanup = SceneManager.UnloadSceneAsync(loaded);
                        if (cleanup == null) { result.Fail($"Cancellation cleanup returned null: {scenePath}"); yield break; }
                        while (!cleanup.isDone) yield return null;
                        if (SceneManager.GetSceneByPath(scenePath).isLoaded) { result.Fail($"Cancellation cleanup did not unload: {scenePath}"); yield break; }
                    }
                    result.Cancel(); yield break;
                }
                if (!SceneManager.GetSceneByPath(scenePath).isLoaded) result.Fail($"Scene load did not produce a loaded scene: {scenePath}");
                else result.Succeed();
            }
            finally { busy = false; }
        }
        public IEnumerator Unload(string scenePath, CancellationToken token, OperationResult result)
        {
            Validate(scenePath);
            if (scenePath == catalog.BootstrapPath) { result.Fail("Bootstrapper cannot be unloaded."); yield break; }
            if (busy) { result.Fail("Another scene operation is in progress."); yield break; }
            if (token.IsCancellationRequested) { result.Cancel(); yield break; }
            if (!IsLoaded(scenePath)) { result.Succeed(); yield break; }
            busy = true;
            try
            {
                if (SceneManager.GetActiveScene().path == scenePath)
                {
                    var bootstrap = SceneManager.GetSceneByPath(catalog.BootstrapPath);
                    if (!bootstrap.isLoaded || !SceneManager.SetActiveScene(bootstrap)) { result.Fail("Bootstrap scene is unavailable during unload."); yield break; }
                }
                var operation = SceneManager.UnloadSceneAsync(scenePath);
                if (operation == null) { result.Fail($"Unload returned null: {scenePath}"); yield break; }
                while (!operation.isDone) yield return null;
                if (SceneManager.GetSceneByPath(scenePath).isLoaded) result.Fail($"Scene still loaded after unload: {scenePath}");
                else if (token.IsCancellationRequested || disposed) result.Cancel();
                else result.Succeed();
            }
            finally { busy = false; }
        }
        public void SetActive(string scenePath)
        {
            Validate(scenePath);
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded) throw new InvalidOperationException($"Cannot activate unloaded scene: {scenePath}");
            // Unity can return false when asked to activate the already active scene.
            // Retry deliberately retains Core, so this operation must be idempotent.
            if (SceneManager.GetActiveScene() == scene) return;
            if (!SceneManager.SetActiveScene(scene)) throw new InvalidOperationException($"Cannot activate scene: {scenePath}");
        }
        public void Dispose() { disposed = true; }
        private void Validate(string path)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SceneService));
            if (path == null || !paths.Contains(path)) throw new ArgumentException($"Scene is not in the catalog: {path}");
        }
    }
}
