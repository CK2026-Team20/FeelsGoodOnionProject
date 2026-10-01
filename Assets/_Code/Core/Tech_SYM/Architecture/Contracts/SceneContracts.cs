using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
namespace Cooked.Contracts
{
    public sealed class SceneCatalog
    {
        public string BootstrapPath { get; }
        public string TitlePath { get; }
        public string CorePath { get; }
        public IReadOnlyList<string> StagePaths { get; }
        public SceneCatalog(string bootstrapPath, string titlePath, string corePath, IEnumerable<string> stagePaths)
        {
            string[] stages = stagePaths?.ToArray() ?? throw new ArgumentNullException(nameof(stagePaths));
            string[] all = new[] { bootstrapPath, titlePath, corePath }.Concat(stages).ToArray();
            if (stages.Length == 0 || all.Any(p => string.IsNullOrWhiteSpace(p) || !p.StartsWith("Assets/", StringComparison.Ordinal) || !p.EndsWith(".unity", StringComparison.Ordinal)) || all.Distinct(StringComparer.Ordinal).Count() != all.Length)
                throw new ArgumentException("Scene paths must be unique Assets/*.unity paths with at least one Stage.");
            BootstrapPath = bootstrapPath; TitlePath = titlePath; CorePath = corePath;
            StagePaths = Array.AsReadOnly(stages);
        }
    }
    public interface ISceneService : IDisposable
    {
        bool IsLoaded(string scenePath);
        IEnumerator LoadAdditive(string scenePath, CancellationToken token, OperationResult result);
        IEnumerator Unload(string scenePath, CancellationToken token, OperationResult result);
        void SetActive(string scenePath);
    }
    public interface IGameRuntimeService
    {
        // Valid only after InitializeSession and before ShutdownSession. Never cached by app flow.
        ICinematicService Cinematics { get; }
        void SetGameplayVisible(bool visible);
        // Safe with no active session; resolves only the current session DialogueService.
        void CancelDialogue();
        IEnumerator InitializeSession(string entryStageId, CancellationToken token, OperationResult result);
        IEnumerator SpawnActorAtCheckpoint(CancellationToken token, OperationResult result);
        IEnumerator DespawnActor(CancellationToken token, OperationResult result);
        IEnumerator ShutdownSession(CancellationToken token, OperationResult result);
    }
}
